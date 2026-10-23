using System;
using GunmageddonAudio;
public static class FlakBarrageTests
{
    static int checks;
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    public static void Main()
    {
        var s=new FlakBarrageSolution(1);s.Observe(new OpticalVector(4000,2000,0),0,4500,false);
        Check(!s.HasTrack,"One observation must not establish motion");s.Observe(new OpticalVector(4300,2000,0),1.5,4500,false);
        Check(s.HasTrack&&s.CanContinue(7.5)&&!s.CanContinue(7.51),"Lost-contact continuation expires after six seconds");
        var area=s.Commit(2);var saved=area.Position;s.Observe(new OpticalVector(-4000,3000,0),3,5000,true);
        Check(area.Position.X==saved.X&&area.Position.Y==saved.Y,"New observations cannot retarget an already committed area");
        double radarError=0,opticalError=0,radarPrediction=0,opticalPrediction=0;
        for(int seed=0;seed<200;seed++) {
            var radar=new FlakBarrageSolution(seed);var optical=new FlakBarrageSolution(seed);
            for(int i=0;i<=40;i++){double t=i*.35;radar.Observe(new OpticalVector(4000+t*180,2500+t*5,0),t,7000,true);}
            for(int i=0;i<=9;i++){double t=i*1.5;optical.Observe(new OpticalVector(4000+t*180,2500+t*5,0),t,7000,false);}
            radarError+=(radar.Position-new OpticalVector(6520,2570,0)).Length;
            opticalError+=(optical.Position-new OpticalVector(6430,2567.5,0)).Length;
            radarPrediction+=(radar.Position+radar.Velocity*5-new OpticalVector(7420,2595,0)).Length;
            opticalPrediction+=(optical.Position+optical.Velocity*5-new OpticalVector(7330,2592.5,0)).Length;
            var rArea=radar.Commit(14);var oArea=optical.Commit(14);
            for(int i=0;i<20;i++) {
                var r=radar.Plan(rArea,14,8,0,1);var o=optical.Plan(oArea,14,8,0,1);
                Check(r.FuseError==o.FuseError&&Math.Abs(r.FuseError)<=FlakBarrageSolution.FuseErrorSeconds,"Radar does not change mechanical fuze error stream");
            }
        }
        Check(radarError<opticalError,"Radar improves contact position knowledge");
        Check(radarPrediction<opticalPrediction,"Radar improves extrapolation knowledge");
        double totalMiss=0;for(int i=0;i<200;i++) {
            var plan=s.Plan(area,2,6,0,1);var center=area.Position+area.Velocity*(2-area.SampleAt+6);
            totalMiss+=(plan.Point-center).Length;
        }
        Check(totalMiss/200>100,"Barrage is a substantial volume, not a point intercept");
        var solo=new FlakBarrageSolution(334);var centered=new FlakBarrageArea{Position=new OpticalVector(0,2000,0),SampleAt=0};
        double meanX=0,meanZ=0;for(int i=0;i<10000;i++){var plan=solo.Plan(centered,0,0,0,1);meanX+=plan.Point.X;meanZ+=plan.Point.Z;}
        Check(Math.Abs(meanX/10000)<5&&Math.Abs(meanZ/10000)<5,"Single-gun barrage has no fixed one-sided sector bias");
        var a1=new FlakBarrageSolution(123);var a2=new FlakBarrageSolution(123);
        var one=a1.Plan(area,2,5,0,2);var two=a2.Plan(area,2,5,1,2);
        Check((one.Point-two.Point).Length>100,"Coordinated guns cover different overlapping sectors");
        foreach(double range in new[]{1000d,3000,6000,10000})foreach(double altitude in new[]{600d,1500,3000}) {
            OpticalVector dir;double time;bool valid=FlakBarrageSolution.Solve(new OpticalVector(range,altitude,0),820,.12,9.81,.02,out dir,out time);
            if(!valid){Check(range>6000||altitude>1500,"Ordinary mid-altitude target must be reachable");continue;}
            Check(Math.Abs(dir.Length-1)<1e-8&&time>0&&time<24,"Valid ballistic direction and bounded fuze");
            var p=new OpticalVector();var v=dir*820;double elapsed=0;
            while(elapsed+ .02<time){v.Y-=9.81*.02;v=v*Math.Max(0,1-v.Length*.12*.02/820);p=p+v*.02;elapsed+=.02;}
            v.Y-=9.81*.02;v=v*Math.Max(0,1-v.Length*.12*.02/820);p=p+v*(time-elapsed);
            Check((p-new OpticalVector(range,altitude,0)).Length<2,"Ballistics follow native gravity-first drag integration");
        }
        OpticalVector far;double farTime;Check(FlakBarrageSolution.Solve(new OpticalVector(12000,2000,0),820,.04,9.81,.02,out far,out farTime),"Heavy shell can reach a 12km aerial region with its configured drag");
        Console.WriteLine("Passed "+checks+" F88 barrage assertions; radar position error "+radarError/200+" vs optical "+opticalError/200+"; prediction "+radarPrediction/200+" vs "+opticalPrediction/200+".");
    }
}
