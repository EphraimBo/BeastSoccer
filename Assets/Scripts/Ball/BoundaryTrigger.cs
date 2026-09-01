using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Ball
{
    public class BoundaryTrigger : MonoBehaviour
    {
        public enum BoundaryKind { Touchline, GoalLine }
        public BoundaryKind kind = BoundaryKind.Touchline;
        // Only used for GoalLine: +1 is the +X goal line, -1 is the -X goal line.
        public int worldGoalSide;
        // Only used for Touchline: +1 is +Y/top, -1 is -Y/bottom.
        public int touchlineSide;

        private bool busy;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (busy || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            var ball = other.GetComponentInParent<BallControl>();
            if (ball == null) return;
            StartCoroutine(Resolve(ball));
        }

        private System.Collections.IEnumerator Resolve(BallControl ball)
        {
            busy = true;
            // Give GoalTrigger one physics step to win if this was actually a goal.
            yield return new WaitForFixedUpdate();
            if (ball == null || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
            {
                busy = false;
                yield break;
            }

            TeamSide lastSide = ball.LastTouch != null
                ? ball.LastTouch.Side
                : (ball.LastKicker != null ? ball.LastKicker.Side : TeamSide.Home);

            SetPieceType type;
            TeamSide restartSide;
            Vector2 spot = ball.transform.position;
            var cfg = GameConfig.Instance;

            if (kind == BoundaryKind.Touchline)
            {
                type = SetPieceType.ThrowIn;
                restartSide = lastSide == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                float hy = cfg.pitchWidth * 0.5f - cfg.throwInInset;
                float hx = cfg.pitchLength * 0.5f - 0.55f;
                spot.x = Mathf.Clamp(spot.x, -hx, hx);
                spot.y = (touchlineSide >= 0 ? 1f : -1f) * hy;
            }
            else
            {
                int side = worldGoalSide >= 0 ? 1 : -1;
                TeamSide attacking = TeamManager.Instance.AttackDirFor(TeamSide.Home) == side ? TeamSide.Home : TeamSide.Away;
                TeamSide defending = attacking == TeamSide.Home ? TeamSide.Away : TeamSide.Home;

                if (lastSide == defending)
                {
                    type = SetPieceType.Corner;
                    restartSide = attacking;
                    float hx = cfg.pitchLength * 0.5f - cfg.cornerInset;
                    float hy = cfg.pitchWidth * 0.5f - cfg.cornerInset;
                    spot.x = side * hx;
                    spot.y = (ball.transform.position.y >= 0f ? 1f : -1f) * hy;
                }
                else
                {
                    type = SetPieceType.GoalKick;
                    restartSide = defending;
                    float hx = cfg.pitchLength * 0.5f - cfg.goalKickDepth;
                    spot.x = side * hx;
                    spot.y = Mathf.Clamp(ball.transform.position.y * 0.20f, -1.25f, 1.25f);
                }
            }

            GameManager.Instance.BeginSetPiece(type, restartSide, spot);
            busy = false;
        }
    }
}
