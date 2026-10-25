using System;
using GunmageddonAudio;
public static class F120FireControlTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Main()
    {
        double oldError=0,newError=0,oldLead=0,newLead=0,oldPattern=0,newPattern=0;
        for(int seed=0;seed<300;seed++){
            var old=new FlakBarrageSolution(seed);var modern=new FlakBarrageSolution(seed,true);
            for(int i=0;i<=8;i++){double time=i*1.5;old.Observe(new OpticalVector(4000+180*time,2200,0),time,7000,false);}
            for(int i=0;i<=24;i++){double time=i*.5;modern.Observe(new OpticalVector(4000+180*time,2200,0),time,7000,false);}
            var truth=new OpticalVector(6160,2200,0);oldError+=(old.Position-truth).Length;newError+=(modern.Position-truth).Length;
            var lead=truth+new OpticalVector(1080,0,0);oldLead+=(old.Position+old.Velocity*6-lead).Length;newLead+=(modern.Position+modern.Velocity*6-lead).Length;
            var a=old.Commit(12);var b=modern.Commit(12);
            for(int i=0;i<20;i++){
                var p=old.Plan(a,12,0,0,1);var q=modern.Plan(b,12,0,0,1);
                oldPattern+=(p.Point-a.Position).Length;newPattern+=(q.Point-b.Position).Length;
                Check(Math.Abs(q.FuseError)<=.040001,"Postwar time-fuze error bounded");
                Check(Math.Abs(p.FuseError)<=.180001,"F88 time-fuze bound unchanged");
                Check(Math.Abs(q.FuseError-p.FuseError*(.04/.18))<1e-12,"Independent mechanical stream remains deterministic");
            }
            Check(modern.Commitment==4&&old.Commitment==12,"Distinct commitment windows");
            Check(modern.SpreadDegrees==.20&&old.SpreadDegrees==.65,"Distinct finite launch dispersion");
            Check(!modern.CanContinue(15.01)&&old.CanContinue(15.01),"Postwar rejects stale knowledge sooner");
        }
        Check(newError<oldError*.7,"Improved optical position estimate");
        Check(newLead<oldLead*.3,"Multi-observation fit substantially tightens predictable-route extrapolation");
        Check(newPattern/6000>10&&newPattern/6000<30,"Tight but finite suppression pattern");
        var fast=new FlakBarrageSolution(7,true);fast.Observe(new OpticalVector(4000,2000,0),0,5000,false);Check(!fast.HasTrack,"No instant motion knowledge");fast.Observe(new OpticalVector(4180,2000,0),1,5000,false);Check(fast.HasTrack,"Postwar motion estimate available after measured baseline");
        OpticalVector direction;double flight;
        Check(FlakBarrageSolution.Solve(new OpticalVector(10000,3000,0),950,.035,9.81,.02,out direction,out flight)&&flight>1&&flight<24,"120mm gravity/drag solution uses real flight time");
        Check(FlakFragmentationPolicy.HeavyRadius==240&&FlakFragmentationPolicy.Radius==140,"Separate fragmentation radii");
        for(int i=0;i<2400;i++){
            double distance=i*.1,falloff=FlakFragmentationPolicy.HeavyFalloff(distance);
            Check(falloff>=0&&falloff<=1,"Bounded heavy falloff");
            double remaining=22*falloff,total=0;
            for(int part=0;part<4;part++){double hp=FlakFragmentationPolicy.HeavyHitPoints(distance,20,5,remaining);Check(hp>=0&&hp<=6*falloff+1e-9&&hp<=remaining+1e-9,"Per-part armor/budget cap");remaining-=hp;total+=hp;}
            Check(total<=22*falloff+1e-9,"Per-aircraft fragment budget");
        }
        Check(FlakFragmentationPolicy.Falloff(180)==0&&FlakFragmentationPolicy.HeavyFalloff(180)>0,"Heavy shell reaches well beyond F88 envelope");
        Check(FlakFragmentationPolicy.HeavyFalloff(240)==0&&FlakFragmentationPolicy.HeavyFalloff(double.NaN)==0,"Boundary and invalid distance produce no damage");
        Check(FlakFragmentationPolicy.HeavyHitPoints(50,200,5,22)==0,"Strong armor blocks heavy fragments");
        Console.WriteLine("F120: "+checks+" assertions passed. Optical mean position error "+(oldError/300).ToString("F2")+" -> "+(newError/300).ToString("F2")+"m; six-second lead error "+(oldLead/300).ToString("F2")+" -> "+(newLead/300).ToString("F2")+"m. Policy simulations, not combat hit rates.");
    }
}
