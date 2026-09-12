using System;
using System.Collections.Generic;
using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Core;
using BeastSoccer.Data;
using BeastSoccer.Player;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// Uses the approved individual orange/blue PNG frames directly. Legacy sprite and
    /// material writers are disabled by CharacterVisualLibrary.
    /// </summary>
    [DefaultExecutionOrder(1600)]
    public sealed class DemoCharacterArtV8 : MonoBehaviour
    {
        private const float PixelsPerUnit = 200f;
        private static readonly Dictionary<string, Sprite[]> Cache = new Dictionary<string, Sprite[]>();
        private static Material spriteMaterial;

        public PlayerVisualProxy visual;
        public SpriteRenderer targetRenderer;

        private PlayerController player;
        private bool configured;
        private bool heldLastFrame;
        private float saveStartedAt = -999f;
        private float ultStartedAt = -999f;
        private bool ultLastFrame;
        private string saveAction = "Catch";
        private Vector2 previousBallPosition;
        private readonly Dictionary<string, Sprite[]> sequences = new Dictionary<string, Sprite[]>();
        private string loopAction;
        private float loopFrame;
        private bool moving;
        private string runDirection = "Run_Side";
        private string pendingRunDirection;
        private float directionChangedAt;

        public void Configure(PlayerVisualProxy proxy, SpriteRenderer renderer)
        {
            visual = proxy;
            targetRenderer = renderer;
            player = proxy != null ? proxy.source : GetComponent<PlayerController>();
            sequences.Clear();
            if (player != null)
            {
                string[] actions = player.Character == CharacterType.Goro
                    ? new[] { "Idle_Ready_ThreeQuarter", "Idle_Holding_ThreeQuarter", "Catch", "Dive_Near", "Dive_Far", "Recover", "Throw_ThreeQuarter" }
                    : new[] { "Run_Side", "Run_FrontThreeQuarter", "Run_Front", "Run_BackThreeQuarter", "Run_Back", "Shoot", "Tackle" };
                foreach (string action in actions) sequences[action] = Load(player.Character, player.Side, action);
                if (player.Character == CharacterType.Volt) sequences["Ability"] = Load(player.Character, player.Side, "Ability");
            }
            loopAction = null;
            loopFrame = 0f;
            moving = false;
            heldLastFrame = false;
            ultLastFrame = false;
            saveStartedAt = -999f;
            runDirection = "Run_Side";
            pendingRunDirection = null;
            configured = true;
            ApplyDefaultMaterial();
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (!configured)
                Configure(GetComponent<PlayerVisualProxy>(), GetComponentInChildren<SpriteRenderer>());
            if (player == null && visual != null) player = visual.source;
            if (player == null || targetRenderer == null || player.Character == CharacterType.Generic) return;

            var ball = BallControl.Instance;
            bool holding = player.HasBall && player.Role == FieldRole.Goalkeeper;
            if (holding && !heldLastFrame)
            {
                saveStartedAt = Time.time;
                float verticalReach = previousBallPosition.y - player.transform.position.y;
                saveAction = Mathf.Abs(verticalReach) > 0.42f
                    ? (verticalReach < 0f ? "Dive_Near" : "Dive_Far")
                    : "Catch";
            }
            heldLastFrame = holding;
            if (ball != null) previousBallPosition = ball.transform.position;

            bool ultNow = player.Ult != null && (player.Ult.IsActive || player.Ult.IsActivating);
            if (ultNow && !ultLastFrame) ultStartedAt = Time.time;
            ultLastFrame = ultNow;

            string action;
            float fps;
            bool loop;
            float localTime;

            if (player.Role == FieldRole.Goalkeeper && player.Character == CharacterType.Goro)
            {
                ChooseKeeperAnimation(ball, holding, out action, out fps, out loop, out localTime);
                targetRenderer.flipX = TeamManager.Instance != null && TeamManager.Instance.AttackDirFor(player.Side) < 0;
            }
            else
            {
                ChooseOutfieldAnimation(ultNow, out action, out fps, out loop, out localTime);
                if (Mathf.Abs(player.MoveFacing.x) > 0.05f)
                    targetRenderer.flipX = player.MoveFacing.x < 0f;
            }

            if (!sequences.TryGetValue(action, out Sprite[] frames)) return;
            if (frames.Length == 0 && action != "Run_Side") sequences.TryGetValue("Run_Side", out frames);
            if (frames == null) return;
            if (frames.Length == 0) return;

            int index;
            if (loop)
            {
                if (loopAction != action)
                {
                    // Turning must not repeatedly restart the stride at frame zero.
                    float phase = 0f;
                    if (loopAction != null && loopAction.StartsWith("Run_", StringComparison.Ordinal) &&
                        action.StartsWith("Run_", StringComparison.Ordinal) &&
                        sequences.TryGetValue(loopAction, out Sprite[] previous) && previous.Length > 0)
                        phase = loopFrame / previous.Length;
                    loopAction = action;
                    loopFrame = phase * frames.Length;
                }
                // Accumulate elapsed frame time. Multiplying global Time.time by a changing
                // speed made frames jump forwards/backwards during contact and acceleration.
                // Shorter supplied run sequences use the same cycle duration as four-frame runs.
                float cycleFps = action.StartsWith("Run_", StringComparison.Ordinal) ? fps * frames.Length / 4f : fps;
                loopFrame = (loopFrame + Time.deltaTime * cycleFps) % frames.Length;
                index = Mathf.FloorToInt(loopFrame);
            }
            else
            {
                loopAction = null;
                index = Mathf.Min(Mathf.FloorToInt(Mathf.Max(0f, localTime) * fps), frames.Length - 1);
            }
            targetRenderer.sprite = frames[index];
            targetRenderer.color = Color.white;
            ApplyDefaultMaterial();
        }

        private void ChooseOutfieldAnimation(bool ultNow, out string action, out float fps, out bool loop, out float localTime)
        {
            if (player.VisualKickType == KickType.Shot && Time.time < player.VisualKickUntil)
            {
                action = "Shoot";
                fps = 18f;
                loop = false;
                localTime = Time.time - player.VisualKickStartedAt;
                return;
            }
            if (player.Defense != null && player.Defense.IsTackling &&
                Time.time - player.Defense.TackleVisualStartedAt < .23f)
            {
                action = "Tackle";
                fps = 19f;
                loop = false;
                localTime = Time.time - player.Defense.TackleVisualStartedAt;
                return;
            }
            // Leo's ability folders were explicitly marked not to use. Volt's approved ability
            // frames play once at activation, then his directional movement art resumes.
            if (player.Character == CharacterType.Volt && ultNow && Time.time - ultStartedAt < 0.34f)
            {
                action = "Ability";
                fps = 14f;
                loop = false;
                localTime = Time.time - ultStartedAt;
                return;
            }

            action = StableDirectionalRun(player.MoveFacing);
            float speed01 = GameConfig.Instance != null
                ? Mathf.Clamp01(player.CurrentVelocity.magnitude / Mathf.Max(.1f, GameConfig.Instance.baseMoveSpeed))
                : Mathf.Clamp01(player.CurrentVelocity.magnitude);
            fps = player.IsSprinting ? 13.5f : Mathf.Lerp(7.5f, 11f, speed01);
            float speed = player.CurrentVelocity.magnitude;
            if (moving ? speed < .025f : speed > .08f) moving = !moving;
            loop = moving;
            localTime = loop ? Time.time : 0f;
        }

        private void ChooseKeeperAnimation(BallControl ball, bool holding, out string action, out float fps, out bool loop, out float localTime)
        {
            if (player.VisualKickType == KickType.Lob && Time.time < player.VisualKickUntil)
            {
                action = "Throw_ThreeQuarter";
                fps = 24f;
                loop = false;
                localTime = Time.time - player.VisualKickStartedAt;
                return;
            }

            float saveTime = Time.time - saveStartedAt;
            if (holding && saveTime < .20f)
            {
                action = saveAction;
                fps = 20f;
                loop = false;
                localTime = saveTime;
                return;
            }
            if (holding && saveTime < .38f && saveAction.StartsWith("Dive", StringComparison.Ordinal))
            {
                action = "Recover";
                fps = 20f;
                loop = false;
                localTime = saveTime - .20f;
                return;
            }
            if (holding)
            {
                action = "Idle_Holding_ThreeQuarter";
                fps = 7f;
                loop = true;
                localTime = Time.time;
                return;
            }

            action = "Idle_Ready_ThreeQuarter";
            fps = player.CurrentVelocity.magnitude > .12f ? 8.5f : 5.5f;
            loop = true;
            localTime = Time.time;
        }

        private static string DirectionalRun(Vector2 facing)
        {
            float ax = Mathf.Abs(facing.x);
            float ay = Mathf.Abs(facing.y);
            float angle = Mathf.Atan2(ay, Mathf.Max(.0001f, ax)) * Mathf.Rad2Deg;
            if (angle < 14f) return "Run_Side";
            if (angle < 76f) return facing.y < 0f ? "Run_FrontThreeQuarter" : "Run_BackThreeQuarter";
            return facing.y < 0f ? "Run_Front" : "Run_Back";
        }

        private string StableDirectionalRun(Vector2 facing)
        {
            string wanted = DirectionalRun(facing);
            if (wanted == runDirection) pendingRunDirection = null;
            else if (pendingRunDirection != wanted)
            {
                pendingRunDirection = wanted;
                directionChangedAt = Time.time;
            }
            else if (Time.time - directionChangedAt >= .025f)
            {
                runDirection = wanted;
                pendingRunDirection = null;
            }
            return runDirection;
        }

        public static Sprite First(CharacterType character, TeamSide side, string action)
        {
            Sprite[] frames = Load(character, side, action);
            return frames.Length > 0 ? frames[0] : null;
        }

        public static Sprite[] Load(CharacterType character, TeamSide side, string action)
        {
            string characterName = character.ToString();
            string kit = side == TeamSide.Home ? "Home_Orange" : "Away_Blue";
            string key = characterName + "/" + kit + "/" + action;
            if (Cache.TryGetValue(key, out Sprite[] cached)) return cached;

            Texture2D[] textures = Resources.LoadAll<Texture2D>("BeastArtV8/" + key);
            Array.Sort(textures, (a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));
            Sprite[] sprites = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .08f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = texture.name;
            }
            Cache[key] = sprites;
            return sprites;
        }

        private static int FrameNumber(string name)
        {
            int end = name.Length - 1;
            while (end >= 0 && !char.IsDigit(name[end])) end--;
            if (end < 0) return 0;
            int start = end;
            while (start > 0 && char.IsDigit(name[start - 1])) start--;
            return int.TryParse(name.Substring(start, end - start + 1), out int number) ? number : 0;
        }

        private void ApplyDefaultMaterial()
        {
            if (targetRenderer == null) return;
            if (spriteMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) spriteMaterial = new Material(shader) { name = "BeastArtV8_SpriteMaterial" };
                else spriteMaterial = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
            }
            if (spriteMaterial != null) targetRenderer.sharedMaterial = spriteMaterial;
        }
    }
}
