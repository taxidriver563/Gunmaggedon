using UnityEngine;

namespace GunmageddonAudio
{
    // Inflation takes two seconds. Integrate exponential drag analytically within
    // small steps so the shell-to-flare handoff preserves velocity at every frame rate.
    internal static class ParachuteMotion
    {
        internal const float OpeningSeconds = 2f;
        private static readonly float CoastSeconds = CalculateCoastSeconds();

        internal static Vector3 Advance(ref Vector3 velocity, Vector3 terminal, float age, float seconds)
        {
            Vector3 movement = Vector3.zero;
            while (seconds > .000001f)
            {
                float dt = Mathf.Min(seconds, .01f);
                float inflation = Mathf.SmoothStep(0f, 1f, (age + dt * .5f) / OpeningSeconds);
                float drag = .25f + 1.75f * inflation;
                float decay = Mathf.Exp(-drag * dt);
                Vector3 relative = velocity - terminal;
                movement += terminal * dt + relative * ((1f - decay) / drag);
                velocity = terminal + relative * decay;
                age += dt;
                seconds -= dt;
            }
            return movement;
        }

        internal static Vector3 PredictCoast(Vector3 velocity)
        {
            return velocity * CoastSeconds;
        }

        private static float CalculateCoastSeconds()
        {
            Vector3 velocity = Vector3.right;
            return Advance(ref velocity, Vector3.zero, 0f, 8f).x;
        }
    }
}
