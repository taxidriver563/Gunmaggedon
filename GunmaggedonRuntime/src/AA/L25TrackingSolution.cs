using System;
namespace GunmageddonAudio
{
    public sealed class L25TrackingSolution
    {
        public const double MaximumRange=3500;
        OpticalVector previous,velocity;double last,started;int samples,target;double seed;
        static double Blend(double range){return Math.Max(0,Math.Min(1,(range-750)/2750));}
        public static double SampleInterval(double range){return .12+.38*Blend(range);}
        public static double Spread(double range){return .18+.82*Blend(range);}
        public bool Ready(double now){return samples>=2&&now-last<=.65&&now-started>=.15+.85*Blend(previous.Length);}
        public void Reset(int id,double now,int randomSeed){target=id;samples=0;velocity=new OpticalVector();last=started=now;seed=(uint)randomSeed%10007*.017;}
        public int Target {get{return target;}}
        public void Sample(OpticalVector relative,double now)
        {
            if(samples>0&&now-last<SampleInterval(relative.Length)-1e-6)return;
            if(samples>0){double dt=now-last;if(dt>.8){samples=0;velocity=new OpticalVector();started=now;}else if(dt>0){var measured=OpticalVector.Limit((relative-previous)*(1/dt),1000);double tau=.18+1.62*Blend(relative.Length);velocity=velocity+(measured-velocity)*(1-Math.Exp(-dt/tau));}}
            previous=relative;last=now;samples++;
        }
        public OpticalVector Predict(double flight,double now)
        {
            double range=Math.Max(1,previous.Length),angle=(.12+1.28*Blend(range))*Math.PI/180;
            var forward=previous*(1/range);var right=OpticalVector.Limit(new OpticalVector(forward.Z,0,-forward.X),1);double length=right.Length;right=length>.001?right*(1/length):new OpticalVector(1,0,0);
            return previous+velocity*(Math.Max(0,now-last)+flight)+right*(range*angle*Math.Sin(seed+now*.8))+new OpticalVector(0,range*angle*.6*Math.Cos(seed+now*.61),0);
        }
    }
}
