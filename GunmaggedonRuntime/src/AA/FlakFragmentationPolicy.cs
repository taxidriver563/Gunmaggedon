using System;
namespace GunmageddonAudio
{
    // Gameplay balance values, independent of the native explosive-yield radius.
    internal static class FlakFragmentationPolicy
    {
        internal const double Radius=140,AircraftBudget=12,PartBudget=4,PenetrationLimit=140;
        internal const int MaximumParts=3;
        internal const double HeavyRadius=240,HeavyAircraftBudget=22,HeavyPartBudget=6,HeavyPenetrationLimit=165;
        internal const int HeavyMaximumParts=4;
        internal static double Falloff(double distance)
        {return double.IsNaN(distance)||distance>=Radius?0:Math.Pow(Math.Max(0,1-Math.Max(0,distance)/Radius),.75);}
        internal static double HitPoints(double distance,double armor,double tolerance,double remaining)
        {
            if(double.IsNaN(armor)||double.IsNaN(tolerance)||double.IsNaN(remaining)||remaining<=0)return 0;
            double capacity=Math.Max(0,PenetrationLimit-Math.Max(0,armor))/Math.Max(.01,tolerance);
            return Math.Min(Math.Min(PartBudget*Falloff(distance),remaining),capacity);
        }
        internal static double HeavyFalloff(double distance)
        {return double.IsNaN(distance)||distance>=HeavyRadius?0:Math.Pow(Math.Max(0,1-Math.Max(0,distance)/HeavyRadius),.75);}
        internal static double HeavyHitPoints(double distance,double armor,double tolerance,double remaining)
        {
            if(double.IsNaN(armor)||double.IsNaN(tolerance)||double.IsNaN(remaining)||remaining<=0)return 0;
            double capacity=Math.Max(0,HeavyPenetrationLimit-Math.Max(0,armor))/Math.Max(.01,tolerance);
            return Math.Min(Math.Min(HeavyPartBudget*HeavyFalloff(distance),remaining),capacity);
        }
    }
}
