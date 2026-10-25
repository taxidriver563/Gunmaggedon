using System;

namespace GunmageddonAudio
{
    public struct FlakBarrageArea
    {
        public OpticalVector Position,Velocity;
        public double SampleAt,Started,Uncertainty;
    }
    public struct FlakShotPlan
    {
        public OpticalVector Point;
        public double FuseError;
    }
    // Observations and gun errors use independent random streams. Radar changes
    // the observations; it cannot change shell spread or fuze precision.
    public sealed class FlakBarrageSolution
    {
        public const double GunSpreadDegrees=.65,FuseErrorSeconds=.18,CommitSeconds=12,ForgetSeconds=6;
        readonly Random observations,pattern,mechanics;
        readonly bool advanced;
        readonly double maximumTrackedSpeed;
        public const double AdvancedSpreadDegrees=.20,AdvancedFuseErrorSeconds=.04,AdvancedCommitSeconds=4;
        public double SpreadDegrees {get{return advanced?AdvancedSpreadDegrees:GunSpreadDegrees;}}
        public double FusePrecision {get{return advanced?AdvancedFuseErrorSeconds:FuseErrorSeconds;}}
        public double Commitment {get{return advanced?AdvancedCommitSeconds:CommitSeconds;}}
        OpticalVector velocityPosition;double velocityAt;int velocitySamples;
        readonly OpticalVector[] measuredPositions=new OpticalVector[16];
        readonly double[] measuredTimes=new double[16];int measuredCount,measuredNext;
        public OpticalVector Position {get;private set;}
        public OpticalVector Velocity {get;private set;}
        public double LastSeen {get;private set;}
        public int Samples {get;private set;}
        public bool HasTrack {get{return velocitySamples>0;}}
        public FlakBarrageSolution(int seed):this(seed,false){}
        public FlakBarrageSolution(int seed,bool postwar):this(seed,postwar,600){}
        public FlakBarrageSolution(int seed,bool postwar,double maximumSpeed){advanced=postwar;maximumTrackedSpeed=maximumSpeed;observations=new Random(seed);pattern=new Random(seed^14867);mechanics=new Random(seed^739391);}
        static double Signed(Random r){return r.NextDouble()*2-1;}
        public void Observe(OpticalVector position,double now,double range,bool radar)
        {
            double error=advanced?(radar?4+range*.001:8+range*.002):(radar?15+range*.004:45+range*.012);
            var measured=position+new OpticalVector(Signed(observations)*error,Signed(observations)*error*.5,Signed(observations)*error);
            if(advanced){ObserveAdvanced(measured,now);return;}
            if(Samples==0){velocityPosition=measured;velocityAt=now;}
            else if(now-velocityAt>=(advanced?.9:1.2)) {
                var velocity=OpticalVector.Limit((measured-velocityPosition)*(1/(now-velocityAt)),600);
                double response=advanced?(radar?.65:.45):(radar?.5:.25);
                Velocity=velocitySamples++==0?velocity:Velocity*(1-response)+velocity*response;
                velocityPosition=measured;velocityAt=now;
            }
            Position=measured;LastSeen=now;Samples++;
        }
        void ObserveAdvanced(OpticalVector measured,double now)
        {
            measuredPositions[measuredNext]=measured;measuredTimes[measuredNext]=now;
            measuredNext=(measuredNext+1)%measuredPositions.Length;measuredCount=Math.Min(measuredCount+1,measuredPositions.Length);
            // Fit only recent observations. Longer baselines reject optical noise
            // without deriving velocity from the aircraft's true physics state.
            var sum=new OpticalVector();double timeSum=0,oldest=now;int count=0;
            for(int i=0;i<measuredCount;i++)if(now-measuredTimes[i]<=3){double t=measuredTimes[i]-now;sum+=measuredPositions[i];timeSum+=t;oldest=Math.Min(oldest,measuredTimes[i]);count++;}
            var mean=sum*(1.0/count);double meanTime=timeSum/count,variance=0;var covariance=new OpticalVector();
            for(int i=0;i<measuredCount;i++)if(now-measuredTimes[i]<=3){double dt=measuredTimes[i]-now-meanTime;variance+=dt*dt;covariance+=(measuredPositions[i]-mean)*dt;}
            if(now-oldest>=.9&&variance>1e-6){Velocity=OpticalVector.Limit(covariance*(1/variance),maximumTrackedSpeed);velocitySamples++;Position=mean-Velocity*meanTime;}
            else Position=measured;
            LastSeen=now;Samples++;
        }
        public bool CanContinue(double now){return HasTrack&&now-LastSeen<=(advanced?3:ForgetSeconds);}
        public FlakBarrageArea Commit(double now)
        {return new FlakBarrageArea{Position=Position,Velocity=Velocity,SampleAt=LastSeen,Started=now,Uncertainty=(advanced?12:40)+Math.Max(0,now-LastSeen)*(advanced?10:30)};}
        public FlakShotPlan Plan(FlakBarrageArea area,double now,double flight,int sector,int sectors)
        {
            double age=Math.Max(0,now-area.SampleAt);
            var center=area.Position+area.Velocity*(age+flight);
            double angle=sector*2*Math.PI/Math.Max(1,sectors);
            double horizontal=(advanced?32:160)+area.Uncertainty+age*(advanced?4:12);
            // Adjacent guns cover overlapping sectors, rather than identical points.
            var sectorOffset=sectors>1?new OpticalVector(Math.Cos(angle)*horizontal*.45,0,Math.Sin(angle)*horizontal*.45):new OpticalVector();
            double theta=pattern.NextDouble()*2*Math.PI,r=Math.Sqrt(pattern.NextDouble())*horizontal*.8;
            var point=center+sectorOffset+new OpticalVector(Math.Cos(theta)*r,Signed(pattern)*(advanced?20:100),Math.Sin(theta)*r);
            return new FlakShotPlan{Point=point,FuseError=Signed(mechanics)*FusePrecision};
        }
        // Solve the native gravity-first, quadratic-drag trajectory to a FIXED
        // area point, not an intercept against a live aircraft transform.
        public static bool Solve(OpticalVector relative,double speed,double drag,double gravity,double step,out OpticalVector direction,out double flight,double maximumFlight=24)
        {
            direction=new OpticalVector();flight=0;
            double range=Math.Sqrt(relative.X*relative.X+relative.Z*relative.Z);
            if(range<50||speed<100||step<=0||drag<0||gravity<=0)return false;
            double low=Math.Max(-5,Math.Atan2(relative.Y,range)*180/Math.PI),high=85;
            double y,t;bool bracket=false;
            for(double angle=low;angle<=85;angle+=2) {
                if(!Height(angle,range,speed,drag,gravity,step,maximumFlight,out y,out t))continue;
                if(y>=relative.Y){high=angle;bracket=true;break;}low=angle;
            }
            if(!bracket)return false;
            for(int i=0;i<14;i++) {
                double mid=(low+high)*.5;
                if(!Height(mid,range,speed,drag,gravity,step,maximumFlight,out y,out t)||y<relative.Y)low=mid;else high=mid;
            }
            double radians=(low+high)*.5*Math.PI/180;
            if(!Height((low+high)*.5,range,speed,drag,gravity,step,maximumFlight,out y,out flight)||Math.Abs(y-relative.Y)>2)return false;
            direction=new OpticalVector(relative.X/range*Math.Cos(radians),Math.Sin(radians),relative.Z/range*Math.Cos(radians));return flight<maximumFlight;
        }
        static bool Height(double degrees,double range,double speed,double drag,double gravity,double step,double maximumFlight,out double height,out double time)
        {
            double a=degrees*Math.PI/180,vx=speed*Math.Cos(a),vy=speed*Math.Sin(a),x=0,y=0;height=time=0;
            for(int i=1;i*step<=maximumFlight;i++) {
                vy-=gravity*step;double scale=Math.Max(0,1-Math.Sqrt(vx*vx+vy*vy)*drag*step/speed);vx*=scale;vy*=scale;
                double next=x+vx*step;
                if(next>=range){double f=(range-x)/(next-x);height=y+vy*step*f;time=(i-1+f)*step;return true;}
                x=next;y+=vy*step;if(vx<1)return false;
            }
            return false;
        }
    }
}
