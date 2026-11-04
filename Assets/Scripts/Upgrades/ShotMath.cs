using UnityEngine;

namespace BeastSoccer.Upgrades
{
    public static class ShotMath
    {
        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }
        // Bounded integration of the same linear-damping model used by the ball, for planning only.
        public static float TravelTime(float distance, float speed, float damping, float step = .02f)
        {
            if (distance <= 0f) return 0f;
            if (speed <= .001f) return float.PositiveInfinity;
            step = Mathf.Clamp(step, .005f, .05f);
            float travelled = 0f;
            for (int i = 1; i <= 200; i++)
            {
                speed /= 1f + Mathf.Max(0f, damping) * step;
                float delta = speed * step;
                if (travelled + delta >= distance) return (i - 1) * step + (distance - travelled) / speed;
                travelled += delta;
            }
            return float.PositiveInfinity;
        }
        public static bool GoalCrossing(Vector2 ball, Vector2 velocity, float planeX, out float y)
        {
            y = 0f;
            if (Mathf.Abs(velocity.x) < .001f) return false;
            float t = (planeX - ball.x) / velocity.x;
            if (t < 0f) return false;
            y = ball.y + velocity.y * t; return true;
        }
    }
}
