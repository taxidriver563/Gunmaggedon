using System;

namespace GunmageddonAudio
{
    internal static class QueryGeometry
    {
        // Conservative broad phase only. Retain the exact ellipsoid test for candidates.
        internal static bool AxisOverlaps(float from, float to, float center, float radius)
        {
            return Math.Min(from, to) <= center + radius + .01f &&
                Math.Max(from, to) >= center - radius - .01f;
        }
    }
}
