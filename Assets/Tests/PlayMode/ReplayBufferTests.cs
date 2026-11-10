using System;
using NUnit.Framework;
using UnityEngine;
using BeastSoccer.Replay;

namespace BeastSoccer.Tests
{
    public sealed class ReplayBufferTests
    {
        [Test] public void WrapKeepsNewestFramesInChronologicalOrder()
        {
            var buffer = new ReplayBuffer(3, 1);
            for (int i = 0; i < 8; i++) buffer.Write(i).visuals[0].position = Vector3.right * i;
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.At(0).time, Is.EqualTo(5));
            Assert.That(buffer.At(2).visuals[0].position.x, Is.EqualTo(7));
            buffer.Sample(5.5f, out var a, out var b, out float blend);
            Assert.That(a.time, Is.EqualTo(5)); Assert.That(b.time, Is.EqualTo(6));
            Assert.That(blend, Is.EqualTo(.5f));
        }
        [Test] public void TerminalStampReplacesSameInstantAndNeverInterpolatesBeyondClip()
        {
            var buffer = new ReplayBuffer(4, 1);
            buffer.Write(1).visuals[0].position = Vector3.zero;
            buffer.Write(2).visuals[0].position = Vector3.one;
            buffer.Write(2).visuals[0].position = new Vector3(12.1f, .4f, .2f);
            Assert.That(buffer.Count, Is.EqualTo(2));
            buffer.Sample(20, out var a, out var b, out float blend);
            Assert.That(a.visuals[0].position.x, Is.EqualTo(12.1f)); Assert.That(a, Is.SameAs(b)); Assert.That(blend, Is.Zero);
            buffer.Sample(-20, out a, out b, out blend);
            Assert.That(a.time, Is.EqualTo(1)); Assert.That(blend, Is.Zero);
        }
        [Test] public void DurationTrimAndResetPreventInterpolationAcrossRestart()
        {
            var buffer = new ReplayBuffer(20, 1);
            for (int n = 0; n < 10; n++) buffer.Write(n);
            buffer.TrimBefore(6.5f);
            Assert.That(buffer.At(0).time, Is.EqualTo(6));
            buffer.Sample(6.5f, out var a, out var b, out float blend);
            Assert.That(a.time, Is.EqualTo(6)); Assert.That(b.time, Is.EqualTo(7)); Assert.That(blend, Is.EqualTo(.5f));
            buffer.Clear(); buffer.Write(0);
            Assert.That(buffer.Count, Is.EqualTo(1)); Assert.That(buffer.At(0).time, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Write(-1));
            buffer.Clear(); Assert.Throws<InvalidOperationException>(() => buffer.Sample(0, out _, out _, out _));
        }
        [TestCase(1)] [TestCase(-1)]
        public void FixedCameraIsBehindAndOffsetWithGoalMouthVisible(int side)
        {
            var go = new GameObject("ReplayCameraTest");
            try
            {
                var camera = go.AddComponent<Camera>(); camera.fieldOfView = 62; camera.aspect = 16f / 9f;
                ReplayCameraPose.Calculate(side, 24, new Vector3(6, 6, -4.5f), 3.5f, .9f, out var position, out var rotation);
                camera.transform.SetPositionAndRotation(position, rotation);
                Assert.That(position.x * side, Is.GreaterThan(12)); Assert.That(Mathf.Abs(position.z), Is.GreaterThan(3));
                foreach (float z in new[] { -2.16f, 2.16f }) foreach (float y in new[] { 0f, 2.5f })
                {
                    var p = camera.WorldToViewportPoint(new Vector3(12 * side, y, z));
                    Assert.That(p.z, Is.GreaterThan(camera.nearClipPlane));
                    Assert.That(p.x, Is.InRange(.02f, .98f)); Assert.That(p.y, Is.InRange(.02f, .98f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
