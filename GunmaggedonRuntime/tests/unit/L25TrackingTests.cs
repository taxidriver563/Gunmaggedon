using System;
using GunmageddonAudio;
public static class L25TrackingTests
{
 static int checks;static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 public static void Main()
 {
  Check(L25TrackingSolution.MaximumRange==3500,"Hard engagement limit is 3.5km");
  Check(L25TrackingSolution.SampleInterval(500)<L25TrackingSolution.SampleInterval(3500),"Near targets update more frequently");
  Check(L25TrackingSolution.Spread(500)<L25TrackingSolution.Spread(3500),"Near targets receive tighter dispersion");
  for(int seed=0;seed<20;seed++){
   var near=new L25TrackingSolution();var far=new L25TrackingSolution();near.Reset(1,0,seed);far.Reset(1,0,seed);
   near.Sample(new OpticalVector(0,0,500),0);far.Sample(new OpticalVector(0,0,3500),0);
   Check(!near.Ready(0)&&!far.Ready(0),"One observation cannot instantly establish aircraft motion");
   for(int i=1;i<=30;i++){double t=i*.02;near.Sample(new OpticalVector(t*120,0,500),t);far.Sample(new OpticalVector(t*120,0,3500),t);}
   Check(near.Ready(.6)&&!far.Ready(.6),"Close defense acquires useful motion earlier");
   var actualNear=new OpticalVector(144,0,500);var actualFar=new OpticalVector(144,0,3500);
   Check((near.Predict(.6,.6)-actualNear).Length<(far.Predict(.6,.6)-actualFar).Length,"Measured near lead converges faster than distant lead");
   Check(!near.Ready(1.5),"Stale optical tracking cannot authorize continued fire");
   near.Sample(new OpticalVector(0,0,500),2);Check(!near.Ready(2),"Reacquisition after lost track requires fresh motion samples");
  }
  Console.WriteLine("L25 tracking: "+checks+" close-range acquisition, dispersion and lost-track assertions passed.");
 }
}
