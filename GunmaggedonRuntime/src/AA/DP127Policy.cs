using System;
namespace GunmageddonAudio
{
    public static class DP127Policy
    {
        public const double Range=15000,MinimumRange=300;
        public static bool InRange(double squared){return squared>=MinimumRange*MinimumRange&&squared<=Range*Range;}
        public static double Score(bool aircraft,bool ship,double distance,double closing,double value,bool held)
        {
            return (aircraft&&distance<=8000?1200:0)+(ship?250:0)+Math.Min(1000,Math.Max(0,value))*.25+Math.Max(0,1-distance/Range)*500+Math.Max(0,Math.Min(800,closing))*.35+(held?100:0);
        }
    }
}
