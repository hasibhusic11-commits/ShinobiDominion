using Unity.Profiling;
using UnityEngine;

namespace ShinobiDominion.Game
{
    /// <summary>
    /// Measures what a stretch of frames cost: time per frame, the slowest frame, and how much memory the game's own
    /// code asked for each frame (which is what makes a phone stutter when it is collected). Start it, let frames
    /// pass, then Stop for the numbers. Costs nothing when not running.
    /// </summary>
    public sealed class FrameMeter
    {
        public readonly struct Reading
        {
            public readonly int Frames;
            public readonly float AverageMs;
            public readonly float WorstMs;
            /// <summary>Managed memory allocated per frame, in bytes, averaged over the stretch.</summary>
            public readonly long AllocatedPerFrame;

            public Reading(int frames, float averageMs, float worstMs, long allocatedPerFrame)
            {
                Frames = frames;
                AverageMs = averageMs;
                WorstMs = worstMs;
                AllocatedPerFrame = allocatedPerFrame;
            }

            public override string ToString() =>
                $"{Frames} frames, {AverageMs:0.00} ms average, {WorstMs:0.00} ms worst, {AllocatedPerFrame:N0} bytes allocated per frame";
        }

        private ProfilerRecorder _allocated;
        private int _startFrame;
        private float _startTime;
        private float _worst;
        private float _last;
        private long _bytes;
        private bool _running;

        public void Start()
        {
            _allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _startFrame = Time.frameCount;
            _startTime = Time.realtimeSinceStartup;
            _last = _startTime;
            _worst = 0f;
            _bytes = 0;
            _running = true;
        }

        /// <summary>Call once a frame while measuring.</summary>
        public void Tick()
        {
            if (!_running) return;
            var now = Time.realtimeSinceStartup;
            _worst = Mathf.Max(_worst, (now - _last) * 1000f);
            _last = now;
            if (_allocated.Valid) _bytes += _allocated.LastValue;
        }

        public Reading Stop()
        {
            if (!_running) return default;
            _running = false;
            var frames = Mathf.Max(1, Time.frameCount - _startFrame);
            var total = (Time.realtimeSinceStartup - _startTime) * 1000f;
            _allocated.Dispose();
            return new Reading(frames, total / frames, _worst, _bytes / frames);
        }
    }
}
