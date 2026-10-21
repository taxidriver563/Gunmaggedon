using System;

namespace GunmageddonAudio
{
    // Independent of Unity so impact-feedback and target-change behavior can be tested directly.
    public sealed class RangingSolution
    {
        public int TargetId { get; private set; }
        public int Generation { get; private set; }
        public int ShotsFired { get; private set; }
        public int LastObservedShot { get; private set; }
        public bool Established { get; private set; }
        public float ForwardBias { get; private set; }
        public float RightBias { get; private set; }

        public void Reset(int targetId, float initialForward, float initialRight)
        {
            TargetId = targetId;
            Generation++;
            ShotsFired = 0;
            LastObservedShot = 0;
            Established = false;
            ForwardBias = initialForward;
            RightBias = initialRight;
        }

        public int Fire() { return ++ShotsFired; }

        public bool Observe(int generation, int shot, float forwardError, float rightError,
            float missDistance, float acceptableMiss, int minimumShots)
        {
            if (generation != Generation || shot <= LastObservedShot || Established) return false;
            LastObservedShot = shot;
            if (shot >= minimumShots && missDistance <= acceptableMiss)
            {
                Established = true;
                return true;
            }
            // Feedback changes the next firing solution, rather than forcing success after N shots.
            ForwardBias = Clamp(ForwardBias - Clamp(forwardError * .75f, -400f, 400f), -1200f, 1200f);
            RightBias = Clamp(RightBias - Clamp(rightError * .75f, -200f, 200f), -600f, 600f);
            return false;
        }

        private static float Clamp(float v, float lo, float hi) { return Math.Max(lo, Math.Min(hi, v)); }
    }
}
