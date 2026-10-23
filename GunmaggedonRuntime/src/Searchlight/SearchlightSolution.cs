using System;

namespace GunmageddonAudio
{
    // Continuous observation is needed even with a radar cue. Losing the beam
    // drains evidence; a nearby radar is never an immediate optical detection.
    internal sealed class SearchlightSolution
    {
        internal float Evidence, BlindSeconds;
        internal bool Acquired;
        internal void Reset() { Evidence=0; BlindSeconds=0; Acquired=false; }
        internal bool Observe(bool visible, bool radar, float seconds)
        {
            seconds=Math.Max(0,Math.Min(seconds,.25f));
            if(visible) { BlindSeconds=0; Evidence=Math.Min(1,Evidence+seconds/(radar?1.2f:2.8f)); }
            else { BlindSeconds+=seconds; Evidence=Math.Max(0,Evidence-seconds*.3f); }
            if(Evidence>=1)Acquired=true;
            if(BlindSeconds>=2.5f)Reset();
            return visible&&Acquired;
        }
        internal static float AcousticRange(float rpm)
        {
            if(float.IsNaN(rpm)||float.IsInfinity(rpm)||rpm<=.15f)return 0;
            return 3000+2000*Math.Max(0,Math.Min(1,(rpm-.2f)/.8f));
        }
        internal static float AcousticWidth(float distance,float radius)
        {return 6+8*Math.Max(0,Math.Min(1,distance/Math.Max(1,radius)));}
        internal static float AcousticBearingError(float distance,float radius,int seed,double now)
        {
            float magnitude=3+5*Math.Max(0,Math.Min(1,distance/Math.Max(1,radius)));
            return ((seed&1)==0?1:-1)*(magnitude+(float)Math.Sin(now*.17+seed*.13));
        }
        internal static bool RadarInRange(float distanceSquared, bool friendly, bool operational, bool truck)
        { return friendly&&operational&&truck&&distanceSquared<=300f*300f; }
    }
}
