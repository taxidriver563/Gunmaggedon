using System;
namespace GunmageddonAudio
{
    public static class R230Solution
    {
        public const double Range=65000,MinimumRange=5000,SpreadDegrees=.003,TerminalAltitude=50000,OpticalMagnification=100;
        public static bool InRangeSquared(double distanceSquared){return distanceSquared>=MinimumRange*MinimumRange&&distanceSquared<=Range*Range;}
        public const float TraverseRate=32,TracerSize=6;
        public static bool Piledriver(string key){return key=="ballisticMissile1"||key=="ballisticMissile1_tacNuke";}
        public static bool Bomber(string key){return key=="FastBomber1"||key=="Darkreach";}
        public static bool Terminal(double altitude,double verticalSpeed)
        {return altitude>=0&&altitude<=TerminalAltitude&&verticalSpeed< -50;}
        public static int Tier(string key,bool aircraft,bool strategic,double altitude,double verticalSpeed)
        {if(Piledriver(key))return Terminal(altitude,verticalSpeed)?3:2;return aircraft&&(Bomber(key)||strategic)?1:0;}
        public static int EngagementTier(string key,bool aircraft,bool strategic,double altitude,double verticalSpeed,double closing,double projectedRange)
        {if(Piledriver(key)&&(verticalSpeed>50||closing< -50||projectedRange>Range+250))return -1;return Tier(key,aircraft,strategic,altitude,verticalSpeed);}
        public static double Score(int tier,double value,double antiSurface,double distance,double closing,bool held)
        {
            // Strict tiers: no amount of fighter score can beat an override.
            double threat=Math.Min(2000,Math.Max(0,value))*2+Math.Min(1,Math.Max(0,antiSurface))*250;
            threat+=Math.Max(0,1-distance/Range)*100+Math.Min(1000,Math.Max(0,closing))*.2;
            return tier*1000000+threat+(held?20:0);
        }
        public static bool Intercept(OpticalVector relative,OpticalVector velocity,double speed,double gravity,double step,out OpticalVector direction,out double flight)
        {return InterceptAccelerated(relative,velocity,new OpticalVector(),speed,gravity,step,out direction,out flight);}
        public static bool InterceptAccelerated(OpticalVector relative,OpticalVector velocity,OpticalVector acceleration,double speed,double gravity,double step,out OpticalVector direction,out double flight)
        {
            direction=new OpticalVector();flight=0;
            if(relative.Length<1||speed<=0||velocity.Length>=speed*.9||gravity<0||step<=0)return false;
            double t=relative.Length/speed;
            for(int i=0;i<24;i++){
                var aim=relative+velocity*t+acceleration*(.5*t*(t+step))+new OpticalVector(0,.5*gravity*t*(t+step),0);
                double next=aim.Length/speed;if(double.IsNaN(next)||double.IsInfinity(next)||next>16)return false;
                if(Math.Abs(next-t)<1e-8){t=next;break;}t=next;
            }
            var offset=relative+velocity*t+acceleration*(.5*t*(t+step))+new OpticalVector(0,.5*gravity*t*(t+step),0);
            if(Math.Abs(offset.Length-speed*t)>.01)return false;
            direction=offset*(1/offset.Length);flight=t;return true;
        }
    }
    public sealed class R230Acquisition
    {
        public const double Delay=.75;
        int target;double alignedSince=double.NaN;public bool Ready{get;private set;}
        public void Reset(){target=0;alignedSince=double.NaN;Ready=false;}
        public bool Track(int id,bool aligned,double now)
        {
            if(id==0){Reset();return false;}
            if(id!=target){Reset();target=id;}
            if(Ready)return true;
            if(!aligned){alignedSince=double.NaN;return false;}
            if(double.IsNaN(alignedSince))alignedSince=now;
            Ready=now-alignedSince>=Delay;return Ready;
        }
    }
}
