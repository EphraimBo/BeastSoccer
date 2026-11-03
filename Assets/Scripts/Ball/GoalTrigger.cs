using UnityEngine;

namespace BeastSoccer.Ball
{
    /// <summary>Authored goal geometry used by MatchBoundaryResolver.</summary>
    public sealed class GoalTrigger : MonoBehaviour
    {
        public int worldGoalSide = 1;
        public float goalLineX;
        public float goalHalfWidth = 2.16f;

        public bool Contains(Vector2 position) =>
            (worldGoalSide > 0 ? position.x >= goalLineX : position.x <= goalLineX) &&
            Mathf.Abs(position.y) <= Mathf.Max(0f, goalHalfWidth - .04f);
    }
}
