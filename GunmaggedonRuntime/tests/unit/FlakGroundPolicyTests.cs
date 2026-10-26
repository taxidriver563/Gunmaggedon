using System;
using GunmageddonAudio;
public static class FlakGroundPolicyTests
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    public static void Main()
    {
        foreach(bool heavy in new[]{false,true}){
            double max=FlakGroundPolicy.Range(heavy);
            Check(!FlakGroundPolicy.InRange(9999,heavy)&&FlakGroundPolicy.InRange(10000,heavy),"Inclusive100m ground minimum");
            Check(FlakGroundPolicy.InRange(max*max,heavy)&&!FlakGroundPolicy.InRange((max+.01)*(max+.01),heavy),"Exact ground maximum");
            Check(!FlakGroundPolicy.InRange(double.NaN,heavy),"Invalid range rejected");
        }
        Check(FlakGroundPolicy.Penetration(true)>FlakGroundPolicy.Penetration(false)&&FlakGroundPolicy.Spread(true)<FlakGroundPolicy.Spread(false),"Larger AP round and improved postwar precision");
        var track=new FlakGroundTrack();track.Observe(new OpticalVector(100,3,1000),0);Check(!track.HasTrack&&!track.Fresh(0),"No motion estimate from single identity");
        track.Observe(new OpticalVector(100,3,1000),.4);Check(track.Fresh(.4)&&track.Velocity.Length==0,"Stationary vehicles acquire normally");
        track.Reset();Check(!track.HasTrack,"Switch resets motion history");
        for(int i=0;i<20;i++)track.Observe(new OpticalVector(100+i*8,3,1000),i*.4);
        Check(Math.Abs(track.Velocity.X-20)<.001,"Measured20m/s crossing velocity");
        var next=track.Predict(7.8,2);Check(Math.Abs(next.X-296)<.01,"Prediction includes observation age and projectile flight");
        Check(track.Fresh(8.6)&&!track.Fresh(8.601)&&!track.Fresh(7),"Freshness boundary and clock rollback");
        track.Reset();track.Observe(new OpticalVector(),0);track.Observe(new OpticalVector(10000,0,0),.4);Check(track.Velocity.Length<=120.001,"Teleport cannot create unbounded lead");
        Console.WriteLine("Flak ground policy: "+checks+" assertions passed; synthetic range/motion policy, not mission hit rates.");
    }
}
