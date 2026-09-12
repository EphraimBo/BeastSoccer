using UnityEngine;
using BeastSoccer.Player;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Presentation
{
    /// <summary>
    /// Maps the hidden 2D simulation XY position onto the visible 3D XZ pitch.
    /// Gameplay never depends on final sprite art; debug indicators make control/roles/facing legible.
    /// </summary>
    public class PlayerVisualProxy : MonoBehaviour
    {
        public PlayerController source;
        public SpriteRenderer spriteRenderer;
        public Animator animator;
        public CharacterVisualLibrary visualLibrary;
        public CharacterAnimationDriver animationDriver;
        // FIX18: small guaranteed presentation clearance above the 3D pitch. Sprite feet still
        // align to the simulation root through their pivot; physics/colliders are not moved.
        public float baseHeight = 0.045f;
        public float extraHeight;
        public Transform shadow;
        public GameObject controlledIndicator;
        public GameObject possessionIndicator;
        public Transform facingIndicator;
        public TextMesh roleLabel;
        public GameObject flightIndicator;
        public GameObject ultAttackIndicator;
        public GameObject ultDefenseIndicator;
        public TrailRenderer voltFlightTrail;
        private SpriteRenderer ultGlowRenderer;
        private Vector3 baseShadowScale = new Vector3(.36f,.01f,.25f);
        private bool lastFacingRight;
        private Vector3 authoredLocalScale;
        private bool authoredScaleCaptured;

        private void Awake()
        {
            authoredLocalScale = transform.localScale;
            authoredScaleCaptured = true;
        }

        private void Start()
        {
            if (shadow != null) baseShadowScale = shadow.localScale;
            EnsureUltGlow();
        }

        private void LateUpdate()
        {
            if (source == null) return;
            Vector2 p = source.transform.position;
            float flightBob = 0f;
            if (source.IsFlying && GameConfig.Instance != null)
                flightBob = Mathf.Sin(Time.time * 7f) * GameConfig.Instance.voltFlyVisualBob;

            // Kickoff players stay physically locked to their legal spawn points, but a tiny visual
            // breathing/sway loop keeps the lineup from reading like frozen cardboard cut-outs.
            float kickoffLift = 0f;
            if (GameManager.Instance != null && GameManager.Instance.Phase == MatchPhase.Kickoff && !source.IsFlying)
            {
                float phase = source.ShirtNumber * 0.47f + (source.Side == TeamSide.Home ? 0f : 1.35f);
                float wave = Mathf.Sin(Time.time * 2.25f + phase);
                kickoffLift = wave * 0.010f;
                Vector2 idleDir = source.MoveFacing.sqrMagnitude > 0.01f ? source.MoveFacing.normalized : Vector2.right;
                p += idleDir * (Mathf.Sin(Time.time * 1.55f + phase) * 0.012f);
            }
            transform.position = new Vector3(p.x, baseHeight + extraHeight + flightBob + kickoffLift + source.ImpactVisualHeight, p.y);
            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;

            // FIX18: the first Leo art set is authored facing LEFT. Preserve the old convention
            // for placeholder/future non-directional art, but invert horizontal mirroring for the
            // directional Leo test so left is unflipped and right is flipped.
            if (spriteRenderer != null && Mathf.Abs(source.MoveFacing.x) > 0.05f)
            {
                lastFacingRight = source.MoveFacing.x > 0f;
                bool artFacesLeft = animationDriver != null && animationDriver.UsesDirectionalRunPrototype && animationDriver.SourceArtFacesLeft;
                spriteRenderer.flipX = artFacesLeft ? lastFacingRight : !lastFacingRight;
            }

            animationDriver?.UpdateDirectionalRun(source.Character, source.MoveFacing);

            if (shadow != null)
            {
                shadow.position = new Vector3(p.x, 0.015f, p.y);
                float s = Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(extraHeight / 2.15f));
                shadow.localScale = new Vector3(baseShadowScale.x*s, baseShadowScale.y, baseShadowScale.z*s);
            }

            bool debug = GameConfig.Instance == null || GameConfig.Instance.showDebugPlayerIndicators;

            // FIX27: the supplied green ground marker means HUMAN CONTROL, not possession.
            // It is always visible under whichever home outfielder the player currently controls,
            // regardless of debug-overlay settings or whether that player has the ball. AI players
            // never receive it. The old generated outline ring is suppressed to avoid two markers.
            if (controlledIndicator != null)
                controlledIndicator.SetActive(false);

            if (possessionIndicator != null)
            {
                possessionIndicator.SetActive(source.IsHuman);
                possessionIndicator.transform.position = new Vector3(p.x, 0.031f, p.y);
            }
            if (facingIndicator != null)
            {
                facingIndicator.gameObject.SetActive(debug);
                Vector2 dir = source.MoveFacing.sqrMagnitude > 0.01f ? source.MoveFacing.normalized : Vector2.right;
                facingIndicator.position = new Vector3(p.x + dir.x * .42f, .03f, p.y + dir.y * .42f);
                facingIndicator.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f);
            }
            if (roleLabel != null)
            {
                roleLabel.gameObject.SetActive(GameConfig.Instance != null && GameConfig.Instance.showDebugPlayerIndicators);
                string displayName;
                if (source.Character != CharacterType.Generic) displayName = source.Character.ToString().ToUpperInvariant();
                else if (source.Role == FieldRole.Goalkeeper) displayName = "KEEPER " + source.ShirtNumber;
                else displayName = "PLAYER " + source.ShirtNumber;
                string positionName;
                if (source.Role == FieldRole.Goalkeeper) positionName = "GK";
                else if (source.FormationRole == TacticalRole.Anchor) positionName = "CB";
                else if (source.FormationRole == TacticalRole.Presser) positionName = "MID";
                else positionName = "ATT";
                string ballDot = source.HasBall ? "●" : "○";
                roleLabel.text = displayName + "  " + ballDot + " - " + positionName;
                roleLabel.transform.position = new Vector3(p.x, 1.55f + extraHeight + flightBob + source.ImpactVisualHeight, p.y);
                if (Camera.main != null) roleLabel.transform.rotation = Camera.main.transform.rotation;
            }

            if (voltFlightTrail != null)
                voltFlightTrail.enabled = source.Character == CharacterType.Volt && source.IsFlying;

            if (flightIndicator != null)
            {
                flightIndicator.SetActive(source.IsFlying);
                flightIndicator.transform.position = new Vector3(p.x, Mathf.Max(0.12f, extraHeight * 0.5f), p.y);
                flightIndicator.transform.localScale = new Vector3(.055f, Mathf.Max(.25f, extraHeight * .5f), .055f);
            }

            bool ultActive = source.Ult != null && (source.Ult.IsActive || source.Ult.IsActivating);
            UpdateUltGlow(ultActive);
            // Old solid ground ult discs are deliberately suppressed; the sprite-edge glow is the tell.
            if (ultAttackIndicator != null) ultAttackIndicator.SetActive(false);
            if (ultDefenseIndicator != null) ultDefenseIndicator.SetActive(false);
        }

        private void EnsureUltGlow()
        {
            if (ultGlowRenderer != null || spriteRenderer == null) return;
            var go = new GameObject(name + "_ULT_GLOW");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            ultGlowRenderer = go.AddComponent<SpriteRenderer>();
            ultGlowRenderer.enabled = false;
        }

        private void UpdateUltGlow(bool active)
        {
            EnsureUltGlow();
            if (ultGlowRenderer == null || spriteRenderer == null) return;
            ultGlowRenderer.enabled = active && spriteRenderer.sprite != null;
            if (!ultGlowRenderer.enabled) return;

            ultGlowRenderer.sprite = spriteRenderer.sprite;
            ultGlowRenderer.flipX = spriteRenderer.flipX;
            ultGlowRenderer.flipY = spriteRenderer.flipY;
            ultGlowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            ultGlowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            float pulse = 1.055f + Mathf.Sin(Time.time * 7.5f) * 0.018f;
            ultGlowRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);
            Color c = source.Character == CharacterType.Leo ? new Color(1f,.30f,.06f,.28f)
                : source.Character == CharacterType.Goro ? new Color(.22f,1f,.32f,.28f)
                : source.Character == CharacterType.Volt ? new Color(.16f,.74f,1f,.30f)
                : new Color(.75f,.45f,1f,.24f);
            ultGlowRenderer.color = c;
        }

        public void ApplyCharacter()
        {
            if (source == null || visualLibrary == null) return;
            if (!authoredScaleCaptured)
            {
                authoredLocalScale = transform.localScale;
                authoredScaleCaptured = true;
            }
            float characterScale = source.Character == CharacterType.Volt ? 0.90f : 1f;
            float duelScale = DuelRules.Enabled ? DemoMatchRules.PlayerScale : 1f;
            transform.localScale = authoredLocalScale * characterScale * duelScale;
            visualLibrary.Apply(source.Character, source.Side, spriteRenderer, animator, animationDriver);
        }

        public void SetAlpha(float alpha)
        {
            if (spriteRenderer == null) return;
            Color c = spriteRenderer.color; c.a = Mathf.Clamp01(alpha); spriteRenderer.color = c;
        }
    }
}
