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
        // small guaranteed presentation clearance above the 3D pitch. Sprite feet still
        // align to the simulation root through their pivot; physics/colliders are not moved.
        public float baseHeight = 0.045f;
        public float extraHeight;
        public Transform shadow;
        public GameObject humanControlMarker;
        public Transform facingIndicator;
        public TextMesh roleLabel;
        public GameObject flightIndicator;
        public TrailRenderer voltFlightTrail;
        private Vector3 baseShadowScale = new Vector3(.36f,.01f,.25f);

        private void Start()
        {
            if (shadow != null) baseShadowScale = shadow.localScale;
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

            // Approved named-character mirroring belongs to CharacterSpriteAnimator.
            if (spriteRenderer != null && source.Character == CharacterType.Generic &&
                Mathf.Abs(source.MoveFacing.x) > 0.05f)
                spriteRenderer.flipX = source.MoveFacing.x < 0f;

            if (shadow != null)
            {
                shadow.position = new Vector3(p.x, 0.015f, p.y);
                float s = Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(extraHeight / 2.15f));
                shadow.localScale = new Vector3(baseShadowScale.x*s, baseShadowScale.y, baseShadowScale.z*s);
            }

            bool debug = GameConfig.Instance == null || GameConfig.Instance.showDebugPlayerIndicators;

            // This marker represents human control, including when the ball is loose.
            if (humanControlMarker != null)
            {
                humanControlMarker.SetActive(source.IsHuman);
                humanControlMarker.transform.position = new Vector3(p.x, 0.031f, p.y);
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

        }

        public void ApplyCharacter()
        {
            if (source == null || visualLibrary == null) return;
            visualLibrary.Apply(source.Character, source.Side, spriteRenderer, animator, animationDriver);
        }

        public void SetAlpha(float alpha)
        {
            if (spriteRenderer == null) return;
            Color c = spriteRenderer.color; c.a = Mathf.Clamp01(alpha); spriteRenderer.color = c;
        }
    }
}
