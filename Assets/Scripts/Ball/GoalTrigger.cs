using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Ball
{
    public class GoalTrigger : MonoBehaviour
    {
        public int worldGoalSide = 1; // +1 goal at +X, -1 goal at -X
        public float goalLineX;
        public float goalHalfWidth = 2.16f;
        public float ballRadius = 0.20f;

        private bool scoredThisPlay;

        private void Update()
        {
            if (GameManager.Instance == null) return;

            // At v4's higher shot speed an end-line trigger could call GOAL KICK one physics event
            // before the goal trigger had a chance to see the ball fully inside the net. During the
            // duel, let the actual ball position recover that race for the brief goal-kick setup state.
            bool live = GameManager.Instance.Phase == MatchPhase.Playing;
            bool recoveringGoalKick = DuelRules.Enabled &&
                GameManager.Instance.Phase == MatchPhase.SetPiece &&
                GameManager.Instance.CurrentSetPieceType == SetPieceType.GoalKick;

            if (!live && !recoveringGoalKick)
            {
                scoredThisPlay = false;
                return;
            }

            if (BallControl.Instance != null)
                TryScoreBall(BallControl.Instance, recoveringGoalKick);
        }

        private void OnTriggerEnter2D(Collider2D other) { TryScore(other); }
        private void OnTriggerStay2D(Collider2D other) { TryScore(other); }

        private void TryScore(Collider2D other)
        {
            if (other == null) return;
            var ball = other.GetComponentInParent<BallControl>();
            if (ball == null) return;
            TryScoreBall(ball, false);
        }

        private void TryScoreBall(BallControl ball, bool recoveringGoalKick)
        {
            if (scoredThisPlay || ball == null || GameManager.Instance == null) return;
            if (GameManager.Instance.Phase != MatchPhase.Playing && !recoveringGoalKick) return;

            var body = ball.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)ball.transform.position;

            // For a fast arcade game the centre of the ball crossing the line is the clearest rule
            // visually and is much less vulnerable to one-frame tunnelling than waiting for the
            // entire enlarged ball to clear the line.
            bool across = worldGoalSide > 0 ? p.x >= goalLineX : p.x <= goalLineX;
            bool betweenPosts = Mathf.Abs(p.y) <= Mathf.Max(0f, goalHalfWidth - 0.04f);
            if (!across || !betweenPosts) return;

            scoredThisPlay = true;
            int homeDir = TeamManager.Instance.AttackDirFor(TeamSide.Home);
            TeamSide scoring = homeDir == worldGoalSide ? TeamSide.Home : TeamSide.Away;

            // In arcade duel mode, if the ball is physically in the net between the posts, it is a
            // goal. SHOOT is already gated by the strike-zone rule, so a second eligibility gate here
            // only creates false goal kicks and missed goals after deflections.
            if (!DuelRules.Enabled && !ball.LastShotEligible)
            {
                TeamSide defending = scoring == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                var keeper = TeamManager.Instance.GoalkeeperFor(defending);
                Vector2 restart = keeper != null ? (Vector2)keeper.transform.position : new Vector2(goalLineX - worldGoalSide, 0f);
                GameManager.Instance.BeginSetPiece(SetPieceType.GoalKick, defending, restart);
                return;
            }

            var scorer = ball.Owner != null ? ball.Owner : (ball.LastKicker != null ? ball.LastKicker : ball.LastTouch);
            ScoreManager.Instance?.ScoreGoal(scoring, scorer);
        }
    }
}
