using System;

namespace GunmageddonAudio
{
    // No Unity/game dependency: relative coordinates also make floating-origin shifts irrelevant.
    public struct OpticalVector
    {
        public double X, Y, Z;
        public OpticalVector(double x, double y, double z) { X=x; Y=y; Z=z; }
        public double Length { get { return Math.Sqrt(X*X+Y*Y+Z*Z); } }
        public static OpticalVector operator +(OpticalVector a, OpticalVector b) { return new OpticalVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static OpticalVector operator -(OpticalVector a, OpticalVector b) { return new OpticalVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static OpticalVector operator *(OpticalVector a, double b) { return new OpticalVector(a.X*b,a.Y*b,a.Z*b); }
        public static double Dot(OpticalVector a,OpticalVector b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        public static OpticalVector Limit(OpticalVector a,double maximum) { double l=a.Length; return l>maximum&&l>0 ? a*(maximum/l) : a; }
    }

    public sealed class OpticalRangingSolution
    {
        public const double SampleSeconds=.5, ReactionSeconds=.9, ForgetSeconds=3;
        public int TargetId { get; private set; }
        public int Epoch { get; private set; }
        public int Corrections { get; private set; }
        public double Confidence { get; private set; }
        public OpticalVector EstimatedVelocity { get; private set; }
        public OpticalVector Correction { get; private set; }
        public double RangeBias { get; private set; }
        OpticalVector previous, measuredPrevious, desiredCorrection;
        double lastSample, lastManeuver, seed, yawBias, pitchBias;
        int measurements, lastBurst;
        public bool HasSample { get; private set; }
        static double Clamp(double v,double a,double b) { return Math.Max(a,Math.Min(b,v)); }
        static bool Finite(double v) { return !double.IsNaN(v)&&!double.IsInfinity(v); }
        static bool Valid(OpticalVector p) { return Finite(p.X)&&Finite(p.Y)&&Finite(p.Z); }

        public void Reset(int target, OpticalVector relative, double now, int randomSeed)
        {
            TargetId=target;Epoch++;Corrections=measurements=lastBurst=0;Confidence=0;
            EstimatedVelocity=Correction=desiredCorrection=new OpticalVector();
            HasSample=false;lastSample=now;lastManeuver=now-10;
            seed=(uint)randomSeed%10007*.017;
            double sign=(randomSeed&1)==0?1:-1;
            RangeBias=Clamp(relative.Length*.14,40,700)*sign;
            yawBias=sign*(1.4+.6*Math.Abs(Math.Sin(seed)))*Math.PI/180;
            pitchBias=(.7+.5*Math.Abs(Math.Cos(seed)))*Math.PI/180;
            Sample(relative,now);
        }

        // Only actual optical samples call this; no target rigidbody velocity or acceleration feed.
        public bool Sample(OpticalVector relative,double now)
        {
            if(!Valid(relative)||!Finite(now))return false;
            if(HasSample&&now-lastSample<SampleSeconds-.00001)return false;
            double dt=now-lastSample;
            if(HasSample&&dt>ForgetSeconds) {
                Epoch++;Confidence=0;Corrections=0;measurements=lastBurst=0;
                EstimatedVelocity=Correction=desiredCorrection=new OpticalVector();
                HasSample=false;
            }
            if(HasSample&&dt>0) {
                var measured=OpticalVector.Limit((relative-previous)*(1/dt),1000);
                double acceleration=measurements>0?(measured-measuredPrevious).Length/dt:0;
                if(acceleration>12&&now-lastManeuver>1.5) {
                    Epoch++;lastBurst=0;Confidence*=.15;desiredCorrection=desiredCorrection*.3;lastManeuver=now;
                }
                EstimatedVelocity=measurements==0?measured*.55:
                    EstimatedVelocity+(measured-EstimatedVelocity)*(1-Math.Exp(-dt/2.1));
                measuredPrevious=measured;measurements++;
            }
            previous=relative;lastSample=now;HasSample=true;return true;
        }

        public void Step(double dt,double now)
        {
            if(!Finite(dt)||dt<=0||!Finite(now))return;
            Correction=Correction+(desiredCorrection-Correction)*(1-Math.Exp(-Math.Min(dt,1)/1.1));
            if(now-lastSample>SampleSeconds*2)Confidence=Math.Max(0,Confidence-dt*.12);
        }

        public OpticalVector Predict(OpticalVector currentRelative,double flightSeconds,double now)
        {
            double range=currentRelative.Length;if(range<1)return currentRelative;
            var forward=currentRelative*(1/range);
            var right=new OpticalVector(forward.Z,0,-forward.X);double r=right.Length;
            right=r>.001?right*(1/r):new OpticalVector(1,0,0);
            var up=new OpticalVector(forward.Y*right.Z,forward.Z*right.X-forward.X*right.Z,-forward.Y*right.X);
            // Correlated crew error rather than random aim jitter each frame. Even a well-ranged
            // target retains angular error; confidence never enables a perfect predictor.
            double wobble=(.26+.55*(1-Confidence))*Math.PI/180;
            double yaw=yawBias+wobble*Math.Sin(now*.73+seed);
            double pitch=pitchBias+wobble*.7*Math.Sin(now*.53+seed*1.7);
            return currentRelative+forward*RangeBias+EstimatedVelocity*Clamp(flightSeconds,0,14)+
                right*(range*yaw)+up*(range*pitch)+Correction;
        }

        public bool Observe(int epoch,int burst,OpticalVector shellMinusTarget,double now)
        {
            if(epoch!=Epoch||burst<=lastBurst||!HasSample||now-lastSample>1||!Valid(shellMinusTarget))return false;
            lastBurst=burst;
            // Only one bounded correction per observed clip; the adapter supplies delayed,
            // averaged closest-pass errors from real, unmodified projectiles.
            double range=previous.Length;
            desiredCorrection=OpticalVector.Limit(desiredCorrection-
                OpticalVector.Limit(shellMinusTarget*.32,Math.Max(12,range*.018)),Math.Max(80,range*.16));
            Corrections++;
            double miss=shellMinusTarget.Length;
            Confidence=Clamp(Confidence+(miss<Math.Max(25,range*.015)?.16:.06),0,.85);
            return true;
        }

        // Swept relative motion: fast/small targets cannot skip a feedback observation between ticks.
        public static OpticalVector ClosestPass(OpticalVector before,OpticalVector after)
        {
            var delta=after-before;double sq=OpticalVector.Dot(delta,delta);
            double t=sq>1e-12?Clamp(-OpticalVector.Dot(before,delta)/sq,0,1):0;
            return before+delta*t;
        }
    }
}
