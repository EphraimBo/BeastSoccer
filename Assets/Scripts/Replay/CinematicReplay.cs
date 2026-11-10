using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using BeastSoccer.Ball;
using BeastSoccer.CameraSystem;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Player;
using BeastSoccer.Presentation;
using BeastSoccer.Upgrades;

namespace BeastSoccer.Replay
{
    /// <summary>Records rendered state; plays renderer-only copies without simulating gameplay again.</summary>
    [DefaultExecutionOrder(2200)]
    public sealed class CinematicReplay : MonoBehaviour
    {
        public static CinematicReplay Instance { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool HasGoal => sealedGoal && UpgradeSettings.Replays && buffer != null && buffer.Count >= 2;
        [Header("Recording and pacing")]
        [Range(2f, 10f)] public float bufferSeconds = 6f;
        [Range(15, 90)] public int samplesPerSecond = 60;
        [Range(1f, 6f)] public float leadSeconds = 3.5f;
        [Range(.1f, 1f)] public float finalSecondSpeed = .4f;
        public float goalAnnouncementSeconds = .55f;
        [Header("Fixed angled behind-goal camera")]
        public Camera replayView;
        public Vector3 cameraOffset = new Vector3(6f, 6f, -4.5f);
        public float lookIntoPitch = 3.5f, lookHeight = .9f;
        [Range(40f, 85f)] public float fieldOfView = 62f;
        public Color keeperSilhouette = new Color(.055f, .055f, .06f, 1f);
        [Header("Authored scene references")]
        public PlayerVisualProxy[] players;
        public BallVisualProxy ballVisual;
        public CanvasGroup gameplayHUD;
        public GameObject replayUI;
        public Transform[] goalVisualRoots;

        private sealed class Track
        {
            public Renderer source, copy;
            public PlayerVisualProxy owner;
            public bool billboard, ball;
            public Vector3 ballOffset;
        }
        private readonly List<Track> tracks = new List<Track>();
        private readonly List<Renderer> liveRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        private ReplayBuffer buffer;
        private GameObject copies;
        private GameManager manager;
        private FollowCamera follow;
        private float nextSample, goalTime;
        private bool sealedGoal, skip, wasRecording, initialized, presentationChanged;
        private int goalSide;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov, savedScale, savedHUDAlpha;
        private bool savedOrthographic, savedFollow, savedHUDInteractable, savedHUDBlocks;
        private float savedOrthoSize;

        private void Awake() => Instance = this;
        private void Start()
        {
            manager = GameManager.Instance;
            if (replayView == null) replayView = Camera.main;
            if (replayView != null) follow = replayView.GetComponent<FollowCamera>();
            if (manager != null) manager.OnPhaseChanged += Phase;
            if (replayUI != null) replayUI.SetActive(false);
        }
        private void Phase(MatchPhase phase)
        {
            if (phase != MatchPhase.GoalScored && IsPlaying) { skip = true; RestorePresentation(); }
            if (phase == MatchPhase.Kickoff || phase == MatchPhase.MatchOver)
            { buffer?.Clear(); sealedGoal = false; nextSample = 0f; }
        }
        private void LateUpdate()
        {
            if (manager == null) return;
            if (IsPlaying)
            {
                if (!UpgradeSettings.Replays) skip = true;
                // Scripts may still update visuals with time frozen. Force-render hiding survives those updates.
                foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = true;
                return;
            }
            bool recording = UpgradeSettings.Replays && manager.Phase == MatchPhase.Playing;
            if (!recording)
            {
                if (!UpgradeSettings.Replays) { buffer?.Clear(); sealedGoal = false; }
                wasRecording = false; return;
            }
            if (!initialized) InitializeTracks();
            if (buffer == null) return;
            // Never interpolate through a kickoff/reset or a period with this module disabled.
            if (!wasRecording) { buffer.Clear(); nextSample = 0f; }
            wasRecording = true;
            if (Time.time < nextSample) return;
            float step = 1f / Mathf.Clamp(samplesPerSecond, 15, 90);
            nextSample = Mathf.Max(nextSample + step, Time.time);
            Capture(false);
        }
        private void InitializeTracks()
        {
            initialized = true;
            copies = new GameObject("Replay Visual Copies (no gameplay components)");
            copies.transform.SetParent(transform, false); copies.SetActive(false);
            var seen = new HashSet<Renderer>();
            if (players != null) foreach (var visual in players)
            {
                if (visual == null || visual.source == null) continue;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true)) RememberLive(renderer);
                foreach (var sprite in visual.GetComponentsInChildren<SpriteRenderer>(true))
                    AddTrack(sprite, visual, true, false, seen);
                // Keep role labels, facing/debug markers and flight helpers out of the replay.
                // Only the authored character sprites and ground shadow are duplicated.
                if (visual.shadow != null)
                    foreach (var renderer in visual.shadow.GetComponentsInChildren<MeshRenderer>(true))
                        AddTrack(renderer, visual, false, false, seen);
                RememberObject(visual.humanControlMarker);
                RememberObject(visual.facingIndicator != null ? visual.facingIndicator.gameObject : null);
                RememberObject(visual.roleLabel != null ? visual.roleLabel.gameObject : null);
                RememberObject(visual.flightIndicator);
                if (visual.voltFlightTrail != null) RememberLive(visual.voltFlightTrail);
            }
            if (ballVisual != null && ballVisual.visualModel != null)
                foreach (var renderer in ballVisual.visualModel.GetComponentsInChildren<MeshRenderer>(true))
                    AddTrack(renderer, null, false, true, seen);
            if (tracks.Count > 0)
                buffer = new ReplayBuffer(Mathf.CeilToInt(Mathf.Clamp(bufferSeconds, 2f, 10f) * Mathf.Clamp(samplesPerSecond, 15, 90)) + 2, tracks.Count);
        }
        private void RememberLive(Renderer renderer)
        { if (renderer != null && !liveRenderers.Contains(renderer)) liveRenderers.Add(renderer); }
        private void RememberObject(GameObject target)
        { if (target != null) foreach (var r in target.GetComponentsInChildren<Renderer>(true)) RememberLive(r); }
        private void AddTrack(Renderer source, PlayerVisualProxy owner, bool billboard, bool ball, HashSet<Renderer> seen)
        {
            if (source == null || !seen.Add(source)) return;
            Renderer renderer;
            var go = new GameObject("Replay " + source.name); go.transform.SetParent(copies.transform, false);
            if (source is SpriteRenderer sprite)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.drawMode = sprite.drawMode; sr.size = sprite.size; sr.spriteSortPoint = sprite.spriteSortPoint;
                renderer = sr;
            }
            else
            {
                var originalMesh = source.GetComponent<MeshFilter>();
                if (originalMesh == null || originalMesh.sharedMesh == null) { Destroy(go); return; }
                go.AddComponent<MeshFilter>().sharedMesh = originalMesh.sharedMesh;
                renderer = go.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = source.receiveShadows;
            renderer.enabled = false;
            var track = new Track { source = source, copy = renderer, owner = owner, billboard = billboard, ball = ball };
            if (ball && ballVisual != null) track.ballOffset = source.transform.position - ballVisual.transform.position;
            tracks.Add(track); RememberLive(source);
        }
        private void Capture(bool terminal)
        {
            var frame = buffer.Write(Time.time);
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i]; var source = track.source;
                if (source == null) { frame.visuals[i] = default; continue; }
                Transform t = source.transform;
                var state = new ReplayVisualState { position = t.position, rotation = t.rotation, scale = t.lossyScale,
                    visible = source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff, color = Color.white };
                if (source is SpriteRenderer sprite)
                { state.sprite = sprite.sprite; state.color = sprite.color; state.flipX = sprite.flipX; state.flipY = sprite.flipY; }
                if (terminal && track.owner != null && track.owner.source != null)
                {
                    Vector2 physical = track.owner.source.transform.position;
                    Vector3 rendered = track.owner.transform.position;
                    state.position += new Vector3(physical.x - rendered.x, 0f, physical.y - rendered.z);
                }
                if (terminal && track.ball && ballVisual != null && ballVisual.source != null)
                {
                    // Goal detection runs before visual LateUpdate and before ForceReleaseAndStop clears arc state.
                    var ball = ballVisual.source; var body = ball.GetComponent<Rigidbody2D>();
                    Vector2 p = body != null ? body.position : (Vector2)ball.transform.position;
                    float flight = ball.Owner != null && ball.Owner.IsFlying && ball.Owner.Visual != null ? ball.Owner.Visual.extraHeight : 0f;
                    float height = GameConfig.Instance.ballVisualRadius + ball.VisualArcHeight + ball.DribbleVisualBobHeight + flight;
                    state.position = new Vector3(p.x, height, p.y) + track.ballOffset;
                }
                frame.visuals[i] = state;
            }
            buffer.TrimBefore(Time.time - Mathf.Clamp(bufferSeconds, 2f, 10f));
        }
        /// <summary>Called once by the real score authority before goal cleanup or presentation mutates visuals.</summary>
        public void SealGoal(TeamSide scoringSide)
        {
            if (!UpgradeSettings.Replays || IsPlaying || manager == null || manager.Mode != GameMode.Regular ||
                manager.Phase != MatchPhase.Playing || !initialized || buffer == null || buffer.Count < 2) return;
            Capture(true); goalTime = Time.time;
            goalSide = TeamManager.Instance != null ? TeamManager.Instance.AttackDirFor(scoringSide) : (scoringSide == TeamSide.Home ? 1 : -1);
            sealedGoal = true;
        }
        public void Skip() => skip = true;
        private void Hide(Renderer renderer)
        {
            if (renderer == null || hidden.ContainsKey(renderer)) return;
            hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true;
        }
        private void BeginPresentation()
        {
            savedPosition = replayView.transform.position; savedRotation = replayView.transform.rotation;
            savedFov = replayView.fieldOfView; savedOrthographic = replayView.orthographic; savedOrthoSize = replayView.orthographicSize;
            savedScale = Time.timeScale; savedFollow = follow != null && follow.enabled;
            if (gameplayHUD != null)
            { savedHUDAlpha = gameplayHUD.alpha; savedHUDInteractable = gameplayHUD.interactable; savedHUDBlocks = gameplayHUD.blocksRaycasts; }
            presentationChanged = true;
            if (follow != null) follow.enabled = false;
            ReplayCameraPose.Calculate(goalSide, GameConfig.Instance.pitchLength, cameraOffset, lookIntoPitch, lookHeight, out var pos, out var rot);
            replayView.transform.SetPositionAndRotation(pos, rot); replayView.orthographic = false;
            replayView.fieldOfView = fieldOfView;
            foreach (var renderer in liveRenderers) Hide(renderer);
            // Imported goals may provide a separate net mesh. Hide only that visual obstruction, keeping posts intact.
            if (goalVisualRoots != null) foreach (var root in goalVisualRoots)
                if (root != null) foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name.IndexOf("net", StringComparison.OrdinalIgnoreCase) >= 0) Hide(r);
            if (gameplayHUD != null) { gameplayHUD.alpha = 0f; gameplayHUD.interactable = false; gameplayHUD.blocksRaycasts = false; }
            if (replayUI != null) replayUI.SetActive(true);
            copies.SetActive(true); Time.timeScale = 0f;
        }
        public IEnumerator Play()
        {
            if (!HasGoal || replayView == null || GameConfig.Instance == null) yield break;
            skip = false; IsPlaying = true;
            try
            {
                BeginPresentation();
                float start = Mathf.Max(buffer.At(0).time, goalTime - Mathf.Clamp(leadSeconds, 1f, bufferSeconds));
                float cursor = start;
                while (cursor < goalTime && !skip && IsPlaying && UpgradeSettings.Replays && manager != null && manager.Phase == MatchPhase.GoalScored)
                {
                    Present(cursor);
                    yield return null;
                    float speed = cursor >= goalTime - .8f ? Mathf.Clamp(finalSecondSpeed, .1f, 1f) : 1f;
                    cursor = Mathf.Min(goalTime, cursor + Mathf.Min(.1f, Time.unscaledDeltaTime) * speed);
                }
                if (!skip && IsPlaying && UpgradeSettings.Replays)
                {
                    Present(goalTime);
                    float hold = 0f;
                    while (hold < .35f && !skip && IsPlaying && UpgradeSettings.Replays)
                    { yield return null; hold += Time.unscaledDeltaTime; }
                }
            }
            finally { RestorePresentation(); sealedGoal = false; buffer?.Clear(); wasRecording = false; }
        }
        private void Present(float time)
        {
            buffer.Sample(time, out var a, out var b, out float t);
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i]; var renderer = track.copy;
                if (renderer == null) continue;
                var from = a.visuals[i]; var to = b.visuals[i]; var discrete = t >= 1f ? to : from;
                renderer.enabled = discrete.visible;
                if (!discrete.visible) continue;
                // Discontinuous visibility and sprite changes stay discrete; only continuous transforms interpolate.
                bool continuous = from.visible && to.visible && Vector3.Distance(from.position, to.position) < 4f;
                Vector3 position = continuous ? Vector3.Lerp(from.position, to.position, t) : discrete.position;
                renderer.transform.position = position;
                renderer.transform.localScale = continuous ? Vector3.Lerp(from.scale, to.scale, t) : discrete.scale;
                renderer.transform.rotation = track.billboard ? replayView.transform.rotation : (continuous ? Quaternion.Slerp(from.rotation, to.rotation, t) : discrete.rotation);
                if (renderer is SpriteRenderer sprite)
                {
                    sprite.sprite = discrete.sprite; sprite.flipX = discrete.flipX; sprite.flipY = discrete.flipY;
                    bool defendingKeeper = track.owner != null && track.owner.source != null && track.owner.source.Role == FieldRole.Goalkeeper &&
                        TeamManager.Instance != null && TeamManager.Instance.AttackDirFor(track.owner.source.Side) == -goalSide;
                    sprite.color = defendingKeeper ? new Color(keeperSilhouette.r, keeperSilhouette.g, keeperSilhouette.b, discrete.color.a) : discrete.color;
                }
            }
        }
        private void RestorePresentation()
        {
            IsPlaying = false;
            if (!presentationChanged) return;
            presentationChanged = false;
            if (copies != null) copies.SetActive(false);
            foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            hidden.Clear();
            if (replayView != null)
            {
                replayView.transform.SetPositionAndRotation(savedPosition, savedRotation);
                replayView.fieldOfView = savedFov; replayView.orthographic = savedOrthographic; replayView.orthographicSize = savedOrthoSize;
            }
            if (follow != null) follow.enabled = savedFollow;
            if (gameplayHUD != null) { gameplayHUD.alpha = savedHUDAlpha; gameplayHUD.interactable = savedHUDInteractable; gameplayHUD.blocksRaycasts = savedHUDBlocks; }
            if (replayUI != null) replayUI.SetActive(false);
            Time.timeScale = manager != null && manager.Phase == MatchPhase.Paused ? 0f : savedScale;
        }
        private void OnApplicationPause(bool paused) { if (paused && IsPlaying) skip = true; }
        private void OnDisable() { skip = true; RestorePresentation(); sealedGoal = false; }
        private void OnDestroy()
        {
            RestorePresentation();
            if (manager != null) manager.OnPhaseChanged -= Phase;
            if (copies != null) Destroy(copies);
            if (Instance == this) Instance = null;
        }
    }
}
