using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Player
{
    /// <summary>
    /// FIX32 Volt BLOCK wall. The trigger is wide across Volt's wings and rotates with his facing.
    /// It is intentionally a hard defensive lane denial skill: opponents can move around the ends,
    /// but cannot simply run through the expanded wings while BLOCK is active.
    /// </summary>
    public class WingBlockZone : MonoBehaviour
    {
        public PlayerController owner;
        public UltimateAbility ability;
        private float nextBallBlockTime;
        private readonly System.Collections.Generic.Dictionary<PlayerController,float> nextPlayerBump = new System.Collections.Generic.Dictionary<PlayerController,float>();

        private void LateUpdate()
        {
            if (owner == null || ability == null || !ability.IsWingBlockActive) return;
            Vector2 facing = owner.MoveFacing.sqrMagnitude > 0.01f ? owner.MoveFacing.normalized : Vector2.right;
            // BoxCollider2D's long local Y axis is the wing span. Keep that axis perpendicular to facing.
            float facingDeg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, facingDeg);
        }

        private void OnTriggerEnter2D(Collider2D other) => Resolve(other);
        private void OnTriggerStay2D(Collider2D other) => Resolve(other);

        private void Resolve(Collider2D other)
        {
            if (owner == null || ability == null || !ability.IsWingBlockActive) return;

            var ball = other.GetComponent<BallControl>();
            if (ball != null && ball.Mode == BallControl.BallMode.Free)
            {
                if (Time.time < nextBallBlockTime) return;
                nextBallBlockTime = Time.time + 0.28f;
                var rb = ball.GetComponent<Rigidbody2D>();
                Vector2 incoming = rb != null ? rb.linearVelocity : Vector2.zero;
                Vector2 facing = owner.MoveFacing.sqrMagnitude > 0.01f ? owner.MoveFacing.normalized : new Vector2(TeamManager.Instance.AttackDirFor(owner.Side),0f);
                Vector2 rebound = incoming.sqrMagnitude > 0.05f
                    ? Vector2.Reflect(incoming, -facing).normalized * Mathf.Max(incoming.magnitude * .72f, GameConfig.Instance.passForce * .72f)
                    : facing * GameConfig.Instance.passForce;
                ball.Deflect(rebound, owner);
                TeamManager.Instance?.AddTeamUltCharge(owner.Side, GameConfig.Instance.ultChargePerAction);
                return;
            }

            var opponent = other.GetComponentInParent<PlayerController>();
            if (opponent == null || opponent == owner || opponent.Side == owner.Side || opponent.Role == FieldRole.Goalkeeper) return;

            if (nextPlayerBump.TryGetValue(opponent, out float next) && Time.time < next) return;
            nextPlayerBump[opponent] = Time.time + 0.07f;

            Vector2 ownerPos = owner.transform.position;
            Vector2 oppPos = opponent.transform.position;
            Vector2 away = oppPos - ownerPos;
            if (away.sqrMagnitude < 0.001f)
            {
                Vector2 f = owner.MoveFacing.sqrMagnitude > 0.01f ? owner.MoveFacing.normalized : Vector2.right;
                away = -f;
            }
            away.Normalize();

            // Keep the opponent outside a substantial body/wings radius, then kick their velocity away.
            // This makes BLOCK read as a wall rather than the old 90%-slow trigger that players could seep through.
            float minSeparation = 1.35f;
            var orb = opponent.GetComponent<Rigidbody2D>();
            Vector2 corrected = ownerPos + away * Mathf.Max(minSeparation, Vector2.Distance(ownerPos, oppPos));
            if (Vector2.Distance(ownerPos, oppPos) < minSeparation)
            {
                if (orb != null) orb.position = corrected;
                else opponent.transform.position = corrected;
            }
            if (orb != null) orb.linearVelocity = away * 3.2f;
            opponent.Animation?.Trigger("Hit");
        }
    }
}
