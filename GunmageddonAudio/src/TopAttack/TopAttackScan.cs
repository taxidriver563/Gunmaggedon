using UnityEngine;

namespace GunmageddonAudio
{
    internal static class TopAttackScan
    {
        // Continuous relative-motion test. The beam and the ship may both cross
        // the entire hull in one tick; neither endpoint needs to be on the ship.
        internal static bool SweptFootprint(Vector3 previousBeam, Vector3 beam, Vector3 previousHull,
            Vector3 hull, Vector3 extents, float beamRadius, out float fraction)
        {
            Vector3 start = previousBeam - previousHull, delta = beam - hull - start;
            float enter = 0f, exit = 1f;
            bool hit = Slab(start.x, delta.x, extents.x + beamRadius, ref enter, ref exit) &&
                Slab(start.z, delta.z, extents.z + beamRadius, ref enter, ref exit);
            fraction = enter;
            return hit;
        }

        private static bool Slab(float start, float delta, float radius, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < .00001f) return Mathf.Abs(start) <= radius;
            float a = (-radius - start) / delta, b = (radius - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Mathf.Max(enter, a); exit = Mathf.Min(exit, b);
            return enter <= exit;
        }

        internal static Vector3 Intercept(Vector3 from, Vector3 target, Vector3 targetVelocity, float speed)
        {
            Vector3 relative = target - from;
            float a = targetVelocity.sqrMagnitude - speed * speed;
            float b = 2f * Vector3.Dot(relative, targetVelocity), c = relative.sqrMagnitude;
            float time = relative.magnitude / speed;
            float discriminant = b * b - 4f * a * c;
            if (Mathf.Abs(a) > .001f && discriminant >= 0f)
            {
                float first = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                float second = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
                if (first > 0f) time = first; else if (second > 0f) time = second;
            }
            return target + targetVelocity * time + Vector3.up * (.5f * 9.81f * time * time);
        }
    }
}
