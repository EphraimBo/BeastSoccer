using System.Collections;
using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.Ball
{
    /// <summary>
    /// One authority for goal/end-line/touchline events. Goal checks take priority over a pending
    /// restart. The existing mouth grace period is retained for fast shots and deflections.
    /// </summary>
    public sealed class MatchBoundaryResolver : MonoBehaviour
    {
        public static MatchBoundaryResolver Instance { get; private set; }
        public GoalTrigger[] goals;
        private Coroutine pendingRestart;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var manager = GameManager.Instance;
            if (manager == null) return;
            TryScore(BallControl.Instance);
            if (pendingRestart != null && manager.Phase != MatchPhase.Playing && manager.Phase != MatchPhase.Paused)
            {
                StopCoroutine(pendingRestart);
                pendingRestart = null;
            }
        }

        public bool TryScore(BallControl ball)
        {
            var manager = GameManager.Instance;
            if (ball == null || manager == null || TeamManager.Instance == null || goals == null) return false;
            bool recoveringGoalKick = DuelRules.Enabled && manager.Phase == MatchPhase.SetPiece &&
                manager.CurrentSetPieceType == SetPieceType.GoalKick;
            if (manager.Phase != MatchPhase.Playing && !recoveringGoalKick) return false;
            if (BeastSoccer.Upgrades.UpgradeSettings.Keepers)
            {
                var home = TeamManager.Instance.GoalkeeperFor(TeamSide.Home);
                var away = TeamManager.Instance.GoalkeeperFor(TeamSide.Away);
                if (home != null && home.KeeperBrain != null && home.KeeperBrain.TryCatch(ball) ||
                    away != null && away.KeeperBrain != null && away.KeeperBrain.TryCatch(ball)) return false;
            }
            var body = ball.GetComponent<Rigidbody2D>();
            Vector2 position = body != null ? body.position : (Vector2)ball.transform.position;
            foreach (var goal in goals)
            {
                if (goal == null || !goal.Contains(position)) continue;
                TeamSide scoring = TeamManager.Instance.AttackDirFor(TeamSide.Home) == goal.worldGoalSide
                    ? TeamSide.Home : TeamSide.Away;
                if (!DuelRules.Enabled && !ball.LastShotEligible)
                {
                    TeamSide defending = scoring == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                    var keeper = TeamManager.Instance.GoalkeeperFor(defending);
                    Vector2 spot = keeper != null ? (Vector2)keeper.transform.position
                        : new Vector2(goal.goalLineX - goal.worldGoalSide, 0f);
                    manager.BeginSetPiece(SetPieceType.GoalKick, defending, spot);
                    return true;
                }
                var scorer = ball.Owner != null ? ball.Owner : (ball.LastKicker != null ? ball.LastKicker : ball.LastTouch);
                ScoreManager.Instance?.ScoreGoal(scoring, scorer);
                return true;
            }
            return false;
        }

        public void RequestRestart(BoundaryTrigger boundary, BallControl ball)
        {
            if (boundary == null || ball == null || pendingRestart != null ||
                GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            pendingRestart = StartCoroutine(ResolveRestart(boundary, ball));
        }

        private IEnumerator ResolveRestart(BoundaryTrigger boundary, BallControl ball)
        {
            yield return new WaitForFixedUpdate();
            var config = GameConfig.Instance;
            if (boundary.kind == BoundaryTrigger.BoundaryKind.GoalLine && ball != null && config != null &&
                Mathf.Abs(ball.transform.position.y) <= Mathf.Max(.2f, config.shotGoalHalfWidth) + .25f)
            {
                float until = Time.time + .18f;
                while (Time.time < until && ball != null && GameManager.Instance != null &&
                       GameManager.Instance.Phase == MatchPhase.Playing)
                {
                    if (TryScore(ball)) break;
                    yield return new WaitForFixedUpdate();
                }
            }
            if (ball == null || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing ||
                config == null || TryScore(ball))
            {
                pendingRestart = null;
                yield break;
            }

            TeamSide lastSide = ball.LastTouch != null ? ball.LastTouch.Side
                : (ball.LastKicker != null ? ball.LastKicker.Side : TeamSide.Home);
            Vector2 spot = ball.transform.position;
            SetPieceType type;
            TeamSide restartSide;
            if (boundary.kind == BoundaryTrigger.BoundaryKind.Touchline)
            {
                type = SetPieceType.ThrowIn;
                restartSide = lastSide == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                float hx = config.pitchLength * .5f - .55f;
                spot.x = Mathf.Clamp(spot.x, -hx, hx);
                spot.y = (boundary.touchlineSide >= 0 ? 1f : -1f) * (config.pitchWidth * .5f - config.throwInInset);
            }
            else
            {
                int side = boundary.worldGoalSide >= 0 ? 1 : -1;
                TeamSide attacking = TeamManager.Instance.AttackDirFor(TeamSide.Home) == side ? TeamSide.Home : TeamSide.Away;
                TeamSide defending = attacking == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                bool corner = lastSide == defending;
                type = corner ? SetPieceType.Corner : SetPieceType.GoalKick;
                restartSide = corner ? attacking : defending;
                spot.x = side * (config.pitchLength * .5f - (corner ? config.cornerInset : config.goalKickDepth));
                spot.y = corner
                    ? (spot.y >= 0f ? 1f : -1f) * (config.pitchWidth * .5f - config.cornerInset)
                    : Mathf.Clamp(spot.y * .20f, -1.25f, 1.25f);
            }
            GameManager.Instance.BeginSetPiece(type, restartSide, spot);
            pendingRestart = null;
        }
    }
}
