using System;
namespace GunmageddonAudio
{
    public static class FlakGroundPolicy
    {
        public const double MinimumRange=100,Lifetime=30;
        public static double Range(bool heavy){return heavy?6000:4000;}
        public static double Penetration(bool heavy){return heavy?4200:2400;}
        public static double Spread(bool heavy){return heavy?.10:.18;}
        public static bool InRange(double squared,bool heavy){return squared>=MinimumRange*MinimumRange&&squared<=Range(heavy)*Range(heavy);}
    }
    // Direct-fire motion is estimated from visible samples, not Rigidbody truth.
    public sealed class FlakGroundTrack
    {
        public OpticalVector Position{get;private set;}
        public OpticalVector Velocity{get;private set;}
        public double LastSeen{get;private set;}
        OpticalVector previous;double previousAt;bool seen;
        public bool HasTrack{get;private set;}
        public void Reset(){seen=HasTrack=false;Position=Velocity=previous=new OpticalVector();LastSeen=previousAt=0;}
        public void Observe(OpticalVector position,double now)
        {
            if(!seen){seen=true;previous=position;previousAt=now;}
            else if(now-previousAt>=.35){
                var measured=OpticalVector.Limit((position-previous)*(1/(now-previousAt)),120);
                Velocity=HasTrack?Velocity*.5+measured*.5:measured;HasTrack=true;previous=position;previousAt=now;
            }
            Position=position;LastSeen=now;
        }
        public bool Fresh(double now){return HasTrack&&now>=LastSeen&&now-LastSeen<=1;}
        public OpticalVector Predict(double now,double flight){return Position+Velocity*(Math.Max(0,now-LastSeen)+Math.Max(0,flight));}
    }
}
