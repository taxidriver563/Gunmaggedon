using System;
using GunmageddonAudio;
public static class FlakFragmentationTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Main()
    {
        Check(FlakFragmentationPolicy.Falloff(0)==1&&FlakFragmentationPolicy.Falloff(140)==0&&FlakFragmentationPolicy.Falloff(141)==0,"Finite 140m envelope");
        double previous=1;
        for(int d=0;d<=140;d++){
            double f=FlakFragmentationPolicy.Falloff(d);Check(f<=previous&&f>=0,"Smooth monotone falloff");previous=f;
            foreach(double armor in new[]{0d,20,70,120,140,200})foreach(double tolerance in new[]{0d,.01,5,50}){
                double budget=FlakFragmentationPolicy.AircraftBudget*f,total=0;
                for(int hit=0;hit<FlakFragmentationPolicy.MaximumParts;hit++){
                    double hp=FlakFragmentationPolicy.HitPoints(d,armor,tolerance,budget-total);total+=hp;
                    Check(hp>=0&&hp<=4*f+1e-9,"Per-part damage remains gradual");
                    Check(hp==0||armor+hp*Math.Max(.01,tolerance)<=140+1e-9,"Native penetration cap respected");
                }
                Check(total<=budget+1e-9,"Total per-aircraft damage budget respected");
                if(armor>=140)Check(total==0,"Strong armor blocks fragments");
            }
        }
        Check(FlakFragmentationPolicy.HitPoints(double.NaN,20,5,12)==0,"Invalid distance is harmless");
        Console.WriteLine("Passed "+checks+" F88 fragment falloff, native penetration and per-aircraft damage-budget assertions.");
    }
}
