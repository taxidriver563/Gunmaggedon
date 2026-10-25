using System;
using GunmageddonAudio;
public static class R230SolutionTests
{
    static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    public static void Main()
    {
        Check(R230Solution.Piledriver("ballisticMissile1")&&R230Solution.Piledriver("ballisticMissile1_tacNuke"),"Both native Piledriver variants");
        Check(!R230Solution.Piledriver("some_piledriver_model"),"No substring false positives");
        Check(R230Solution.Bomber("FastBomber1")&&R230Solution.Bomber("Darkreach"),"Both named bombers");
        Check(5000*R230Solution.OpticalMagnification>=R230Solution.Range,"R230 optics cover its range against native 5km-visibility bombers and Piledrivers");
        Check(!R230Solution.InRangeSquared(4999d*4999)&&R230Solution.InRangeSquared(5000d*5000),"Inclusive 5km minimum");
        Check(R230Solution.InRangeSquared(65000d*65000)&&!R230Solution.InRangeSquared(65001d*65001),"Inclusive 65km maximum");
        Check(!R230Solution.InRangeSquared(double.NaN)&&!R230Solution.InRangeSquared(double.PositiveInfinity),"Invalid range cannot authorize engagement");
        Check(R230Solution.EngagementTier("ballisticMissile1",false,false,25000,500,-100,30000)==-1,"Climbing Piledriver is secondary");
        Check(R230Solution.EngagementTier("ballisticMissile1",false,false,25000,-500,-100,30000)==-1,"Receding descent is secondary");
        Check(R230Solution.EngagementTier("ballisticMissile1",false,false,25000,-500,0,66000)==-1,"Projected exit from envelope is secondary");
        Check(R230Solution.EngagementTier("ballisticMissile1",false,false,50000,-500,500,55000)==3,"Incoming terminal threat retains top priority");
        Check(R230Solution.Score(0,1,0,60000,100,false)>R230Solution.Score(-1,2000,1,1000,0,true),"Incoming aircraft overrides even a held high-value departing TBM");
        var acquisition=new R230Acquisition();
        Check(!acquisition.Track(1,true,10)&&!acquisition.Track(1,true,10.749)&&acquisition.Track(1,true,10.75),"Full .75 second acquisition before first shot");
        Check(acquisition.Track(1,false,11)&&acquisition.Track(1,true,12),"Established target retains acquisition between shots");
        Check(!acquisition.Track(2,true,12)&&!acquisition.Track(2,true,12.74)&&acquisition.Track(2,true,12.76),"New target reacquires");
        acquisition.Reset();Check(!acquisition.Track(3,true,20)&&!acquisition.Track(3,false,20.5)&&!acquisition.Track(3,true,21)&&!acquisition.Track(3,true,21.7)&&acquisition.Track(3,true,21.76),"Interrupted initial alignment restarts timer");
        Check(!acquisition.Track(0,true,22)&&!acquisition.Ready,"Target loss clears lock");
        Check(R230Solution.TraverseRate==32&&R230Solution.TracerSize==6,"Modest traverse and tracer balance");
        var bank=new R230Capacitor();bank.Tick(1,true,0);Check(!bank.Ready(1.999)&&bank.Ready(2),"Full two-second firing warning");
        Check(bank.Shot(2)&&bank.Remaining==2&&!bank.Charging&&!bank.Shot(2),"Actual shot consumes one cell; duplicate callback rejected");
        bank.Tick(1,true,10);bank.Cancel();Check(bank.Remaining==2&&!bank.Ready(20),"Aborted warning consumes no cell");
        bank.Tick(1,true,10);bank.Tick(2,true,11);Check(!bank.Ready(12.99)&&bank.Ready(13),"Target switch restarts warning");Check(bank.Shot(13),"Second real shot");
        bank.Tick(2,true,21);Check(bank.Shot(23)&&bank.Remaining==0&&bank.RechargeUntil==33,"Third shot starts 10-second bank recharge");
        bank.Tick(2,true,30.99);Check(!bank.Charging&&bank.Remaining==0,"No early warning/fire during recharge");bank.Tick(2,true,31);Check(bank.Charging&&bank.Remaining==0&&!bank.Ready(32.99),"Warning overlaps final two seconds without firing early");bank.Tick(2,true,33);Check(bank.Remaining==3&&bank.Ready(33),"Full warning permits shot at exact ten-second recharge boundary");
        for(int cycle=0;cycle<100;cycle++){var test=new R230Capacitor();for(int shot=0;shot<3;shot++){double now=cycle*100+shot*8;test.Tick(1,true,now);Check(test.Ready(now+2)&&test.Shot(now+2),"Repeated independent bank sequences");}Check(test.Remaining==0,"Three-shot limit maintained");}
        Check(R230Solution.Terminal(50000,-500)&&R230Solution.Terminal(40000,-200)&&!R230Solution.Terminal(50000.01,-500)&&!R230Solution.Terminal(15000,500),"Terminal priority begins at inclusive 50km descent boundary");
        for(int i=0;i<10000;i++){
            double d=i*6.5;double a=R230Solution.Score(0,2000,1,d,1000,true),b=R230Solution.Score(1,1,0,65000,0,false);
            Check(b>a,"Bomber override cannot lose to fighter score");Check(R230Solution.Score(2,0,0,65000,0,false)>b,"Piledriver override beats bomber");Check(R230Solution.Score(3,0,0,65000,0,false)>R230Solution.Score(2,2000,1,0,1000,true),"Terminal overrides other Piledrivers");
        }
        var rng=new Random(230);
        for(int i=0;i<2000;i++){
            var p=new OpticalVector(100+rng.NextDouble()*64000,500+rng.NextDouble()*25000,0);var v=new OpticalVector((rng.NextDouble()-.5)*3000,(rng.NextDouble()-.5)*3000,(rng.NextDouble()-.5)*3000);OpticalVector direction;double t;
            Check(R230Solution.Intercept(p,v,15000,9.81,.02,out direction,out t),"Finite reachable fast target intercept");
            var shell=direction*(15000*t)-new OpticalVector(0,.5*9.81*t*(t+.02),0);Check((shell-p-v*t).Length<.02,"Gravity-first intercept residual under 2cm");
            Check(t>0&&t<16&&Math.Abs(direction.Length-1)<1e-10,"Normalized finite flight");
        }
        OpticalVector ignored;double time;Check(!R230Solution.Intercept(new OpticalVector(65000,0,0),new OpticalVector(15000,0,0),15000,9.81,.02,out ignored,out time),"Reject unreachable receding targets");
        for(int i=0;i<1000;i++){
            var p=new OpticalVector(1000+rng.NextDouble()*35000,35000+rng.NextDouble()*15000,0);var v=new OpticalVector(-500,-1000-rng.NextDouble()*1800,0);var acceleration=new OpticalVector(0,-9.81-rng.NextDouble()*30,0);OpticalVector direction;double flight;
            Check(R230Solution.InterceptAccelerated(p,v,acceleration,15000,9.81,.02,out direction,out flight),"Reachable descending TBM at 35-50km");
            var shell=direction*(15000*flight)-new OpticalVector(0,.5*9.81*flight*(flight+.02),0);
            Check((shell-p-v*flight-acceleration*(.5*flight*(flight+.02))).Length<.02,"Acceleration-aware lead tracks ballistic descent");
        }
        {var p=new OpticalVector(10000,50000,0);var v=new OpticalVector(0,-1800,0);var acceleration=new OpticalVector(0,-9.81,0);OpticalVector oldAim;double oldTime;R230Solution.Intercept(p,v,15000,9.81,.02,out oldAim,out oldTime);var oldShell=oldAim*(15000*oldTime)-new OpticalVector(0,.5*9.81*oldTime*(oldTime+.02),0);Check((oldShell-p-v*oldTime-acceleration*(.5*oldTime*oldTime)).Length>30,"Regression: constant velocity misses a ballistic descent by tens of meters");}
        Console.WriteLine("R230: "+checks+" assertions passed; synthetic intercept/terminal-priority regressions, not mission hit rates.");
    }
}
