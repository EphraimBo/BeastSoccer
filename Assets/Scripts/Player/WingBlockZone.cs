using UnityEngine;
using BeastSoccer.Ball;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Player
{
    public class WingBlockZone : MonoBehaviour
    {
        public PlayerController owner;
        public UltimateAbility ability;
        private float nextBallBlockTime;

        private void OnTriggerEnter2D(Collider2D other) => Resolve(other);
        private void OnTriggerStay2D(Collider2D other) => Resolve(other);

        private void Resolve(Collider2D other)
        {
            if (owner == null || ability == null || !ability.IsWingBlockActive) return;
            var ball = other.GetComponent<BallControl>();
            if (ball != null && ball.Mode == BallControl.BallMode.Free)
            {
                if (Time.time < nextBallBlockTime) return;
                nextBallBlockTime = Time.time + 0.40f;
                var rb = ball.GetComponent<Rigidbody2D>();
                Vector2 incoming = rb != null ? rb.linearVelocity : Vector2.zero;
                int awayFromOwnGoal = TeamManager.Instance.AttackDirFor(owner.Side);
                Vector2 fallback = new Vector2(awayFromOwnGoal, 0f);
                Vector2 rebound = incoming.sqrMagnitude > 0.05f ? new Vector2(-incoming.x * .65f, incoming.y * .4f) : fallback * GameConfig.Instance.passForce;
                ball.Deflect(rebound, owner);
                TeamManager.Instance?.AddTeamUltCharge(owner.Side, GameConfig.Instance.ultChargePerAction);
                return;
            }

            var opponent = other.GetComponentInParent<PlayerController>();
            if (opponent != null && opponent != owner && opponent.Side != owner.Side)
            {
                var orb = opponent.GetComponent<Rigidbody2D>();
                if (orb != null) orb.linearVelocity *= 0.10f;
            }
        }
    }
}
