using NUnit.Framework;
using UnityEngine;
using BeastSoccer.Upgrades;

namespace BeastSoccer.Tests
{
    public sealed class ShotMathTests
    {
        [Test] public void SweptCatchUsesSegmentAndDoesNotExtendBeyondEndpoints()
        {
            Assert.That(ShotMath.SegmentDistance(Vector2.zero, new Vector2(-4, 0), new Vector2(4, 0)), Is.Zero);
            Assert.That(ShotMath.SegmentDistance(new Vector2(0, 2), Vector2.zero, Vector2.right), Is.EqualTo(2).Within(.001));
            Assert.That(ShotMath.SegmentDistance(new Vector2(3, 0), Vector2.zero, Vector2.right), Is.EqualTo(2).Within(.001));
            Assert.That(ShotMath.SegmentDistance(Vector2.one, Vector2.zero, Vector2.zero), Is.EqualTo(Mathf.Sqrt(2)).Within(.001));
        }
        [Test] public void PredictedGoalCrossingRejectsShotsMovingAway()
        {
            Assert.That(ShotMath.GoalCrossing(Vector2.zero, new Vector2(4, 2), 10, out float y), Is.True);
            Assert.That(y, Is.EqualTo(5).Within(.001));
            Assert.That(ShotMath.GoalCrossing(Vector2.zero, Vector2.left, 10, out _), Is.False);
            Assert.That(ShotMath.GoalCrossing(Vector2.zero, Vector2.up, 10, out _), Is.False);
        }
        [Test] public void TravelPredictionAccountsForDampingAndUnreachableTargets()
        {
            float free = ShotMath.TravelTime(10, 20, 0);
            Assert.That(free, Is.EqualTo(.5f).Within(.001));
            Assert.That(ShotMath.TravelTime(10, 20, 1), Is.GreaterThan(free));
            Assert.That(ShotMath.TravelTime(100, 1, 2), Is.EqualTo(float.PositiveInfinity));
            Assert.That(ShotMath.TravelTime(0, 0, 1), Is.Zero);
        }
    }
}
