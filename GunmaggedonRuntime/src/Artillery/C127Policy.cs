using System;
namespace GunmageddonAudio
{
    public sealed class C127ReloadCycle
    {
        public const double Duration=5.17;
        double fired=double.NegativeInfinity;bool handed;
        public bool Ready(double now){return now-fired>=Duration;}
        public double Remaining(double now){return Math.Max(0,Duration-(now-fired));}
        public double Phase(double now){return double.IsNegativeInfinity(fired)?1:Math.Min(1,.05+.95*Math.Max(0,now-fired)/Duration);}
        public void Begin(double now){fired=now;handed=false;}
        public bool TakeCase(double now){if(handed||double.IsNegativeInfinity(fired)||Phase(now)<.475)return false;handed=true;return true;}
        public void Reset(){fired=double.NegativeInfinity;handed=false;}
        public static double Ease(double t,double a,double b){double x=Math.Max(0,Math.Min(1,(t-a)/(b-a)));return x*x*(3-2*x);}
        public double Recoil(double now){double t=Phase(now);return -.24*Ease(t,.05,.09)*(1-Ease(t,.09,.23));}
    }
    public static class C127Policy
    {
        public const float SurfaceRange=12000,AirRange=6500,MinimumRange=200;
        public static bool TargetKind(bool twin,bool aircraft,bool surface){return surface||twin&&aircraft;}
        public static bool Range(bool air,double squared){double maximum=air?AirRange:SurfaceRange;return squared>=MinimumRange*MinimumRange&&squared<=maximum*maximum;}
        public static double Score(bool air,double distance,bool held){return (air?0:10000)+Math.Max(0,1-distance/SurfaceRange)*1000+(held?150:0);}
    }
    // Only sampled visible positions, never the target's actual rigidbody velocity.
    public sealed class C127SurfaceTrack
    {
        OpticalVector previous,velocity;double last=double.NegativeInfinity,started,bias,correction;int samples;
        public int Epoch{get;private set;}
        public void Reset(){samples=0;last=double.NegativeInfinity;velocity=new OpticalVector();correction=0;Epoch++;}
        public bool Observe(OpticalVector point,double now,double range,int seed){if(samples>0&&now-last<.49)return false;if(samples==0){started=now;bias=Math.Min(180,Math.Max(20,range*.025))*((seed&1)==0?1:-1);}else{double dt=now-last;var measured=OpticalVector.Limit((point-previous)*(1/Math.Max(.001,dt)),120);velocity=samples==1?measured*.6:velocity+(measured-velocity)*(1-Math.Exp(-dt/1.5));}previous=point;last=now;samples++;return true;}
        public bool Ready(double now){return samples>=3&&now-started>=1.2&&now-last<=1.1;}
        public OpticalVector Predict(double now,double flight,OpticalVector from){var delta=previous-from;var forward=delta*(1/Math.Max(1,delta.Length));return previous+velocity*(Math.Max(0,now-last)+flight)+forward*(bias+correction);}
        public void Correct(int epoch,double alongMiss){if(epoch!=Epoch||double.IsNaN(alongMiss)||double.IsInfinity(alongMiss))return;correction=Math.Max(-350,Math.Min(350,correction-Math.Max(-120,Math.Min(120,alongMiss*.4))));}
    }
}
