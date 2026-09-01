using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Ball
{
    public class GoalTrigger : MonoBehaviour
    {
        public int worldGoalSide = 1; // +1 goal at +X, -1 goal at -X
        public float goalLineX;
        public float goalHalfWidth = 2.08f;
        public float ballRadius = 0.20f;

        private bool scoredThisPlay;

        private void Update()
        {
            // A goal immediately changes the phase, so this arms the sensor again for the next play.
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing)
                scoredThisPlay = false;
        }

        private void OnTriggerEnter2D(Collider2D other) { TryScore(other); }
        private void OnTriggerStay2D(Collider2D other) { TryScore(other); }

        private void TryScore(Collider2D other)
        {
            if (scoredThisPlay || GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            var ball = other.GetComponentInParent<BallControl>();
            if (ball == null) return;

            var body = ball.GetComponent<Rigidbody2D>();
            Vector2 p = body != null ? body.position : (Vector2)ball.transform.position;
            // The entire ball must cross the goal line. This is intentionally stricter than
            // merely touching a trigger volume at the mouth of the goal.
            bool fullyAcross = worldGoalSide > 0
                ? p.x - ballRadius >= goalLineX
                : p.x + ballRadius <= goalLineX;

            // Keep the whole ball between the posts as well. Physical posts handle ricochets;
            // this check prevents a grazing ball outside the post from counting.
            bool betweenPosts = Mathf.Abs(p.y) <= Mathf.Max(0f, goalHalfWidth - ballRadius);
            if (!fullyAcross || !betweenPosts) return;

            scoredThisPlay = true;
            int homeDir = TeamManager.Instance.AttackDirFor(TeamSide.Home);
            TeamSide scoring = homeDir == worldGoalSide ? TeamSide.Home : TeamSide.Away;
            var scorer = ball.Owner != null ? ball.Owner : (ball.LastKicker != null ? ball.LastKicker : ball.LastTouch);
            ScoreManager.Instance.ScoreGoal(scoring, scorer);
        }
    }
}
