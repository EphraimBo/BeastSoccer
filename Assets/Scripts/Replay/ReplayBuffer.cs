using System;
using UnityEngine;

namespace BeastSoccer.Replay
{
    // Sprite references point to already loaded art. The buffer stores no image/video bytes.
    public struct ReplayVisualState
    {
        public Vector3 position, scale;
        public Quaternion rotation;
        public Sprite sprite;
        public Color color;
        public bool visible, flipX, flipY;
    }

    public sealed class ReplayFrame
    {
        public float time;
        public readonly ReplayVisualState[] visuals;
        public ReplayFrame(int tracks) => visuals = new ReplayVisualState[tracks];
    }

    /// <summary>Preallocated chronological ring, with bounded binary-search sampling.</summary>
    public sealed class ReplayBuffer
    {
        private readonly ReplayFrame[] frames;
        private int head;
        public int Count { get; private set; }
        public int Capacity => frames.Length;
        public ReplayBuffer(int capacity, int tracks)
        {
            if (capacity < 2 || tracks < 1) throw new ArgumentOutOfRangeException();
            frames = new ReplayFrame[capacity];
            for (int i = 0; i < frames.Length; i++) frames[i] = new ReplayFrame(tracks);
        }
        public void Clear() { Count = head = 0; }
        public void TrimBefore(float time)
        {
            // Keep the nearest preceding sample so interpolation at the cutoff remains possible.
            while (Count > 1 && At(1).time < time) Count--;
        }
        public ReplayFrame At(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return frames[(head - Count + index + frames.Length) % frames.Length];
        }
        public ReplayFrame Write(float time)
        {
            if (Count > 0)
            {
                float last = At(Count - 1).time;
                if (time < last) throw new ArgumentOutOfRangeException(nameof(time), "Recording time must be monotonic.");
                if (time == last) return At(Count - 1); // Goal stamp replaces this instant, without a zero-length segment.
            }
            var frame = frames[head]; frame.time = time;
            head = (head + 1) % frames.Length; Count = Math.Min(Count + 1, frames.Length);
            return frame;
        }
        public void Sample(float time, out ReplayFrame a, out ReplayFrame b, out float blend)
        {
            if (Count == 0) throw new InvalidOperationException("No replay frames.");
            int lo = 0, hi = Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (At(mid).time <= time) lo = mid; else hi = mid - 1;
            }
            a = At(lo); b = At(Math.Min(lo + 1, Count - 1));
            blend = b.time > a.time ? Mathf.Clamp01((time - a.time) / (b.time - a.time)) : 0f;
        }
    }

    public static class ReplayCameraPose
    {
        public static void Calculate(int goalSide, float pitchLength, Vector3 offset, float lookIntoPitch,
            float lookHeight, out Vector3 position, out Quaternion rotation)
        {
            int side = goalSide >= 0 ? 1 : -1;
            float goalX = side * pitchLength * .5f;
            position = new Vector3(goalX + side * Mathf.Max(1f, offset.x), Mathf.Max(1f, offset.y), offset.z);
            Vector3 target = new Vector3(goalX - side * Mathf.Max(0f, lookIntoPitch), lookHeight, 0f);
            rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }
    }
}
