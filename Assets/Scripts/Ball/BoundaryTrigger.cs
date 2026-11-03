using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.Ball
{
    /// <summary>Forwards boundary contacts to the match's single rules resolver.</summary>
    public sealed class BoundaryTrigger : MonoBehaviour
    {
        public enum BoundaryKind { Touchline, GoalLine }
        public BoundaryKind kind = BoundaryKind.Touchline;
        public int worldGoalSide;
        public int touchlineSide;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (GameManager.Instance == null || GameManager.Instance.Phase != MatchPhase.Playing) return;
            var ball = other.GetComponentInParent<BallControl>();
            if (ball != null) MatchBoundaryResolver.Instance?.RequestRestart(this, ball);
        }
    }
}
