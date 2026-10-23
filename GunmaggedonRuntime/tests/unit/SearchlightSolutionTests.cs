using System;
using GunmageddonAudio;
public static class SearchlightSolutionTests
{
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Main()
 {
  var optical=new SearchlightSolution();var radar=new SearchlightSolution();
  for(int i=0;i<10;i++){Check(!optical.Observe(true,false,.1f),"No instant optical acquisition");Check(!radar.Observe(true,true,.1f),"No instant radar acquisition");}
  for(int i=0;i<4;i++)radar.Observe(true,true,.1f);
  Check(radar.Acquired&&!optical.Acquired,"Radar improves acquisition time");
  for(int i=0;i<20;i++)optical.Observe(true,false,.1f);
  Check(optical.Acquired,"Steady optical observation acquires");
  Check(!optical.Observe(false,true,.1f),"Cannot report occluded aircraft even with radar");
  for(int i=0;i<30;i++)optical.Observe(false,false,.1f);
  Check(!optical.Acquired&&optical.Evidence==0,"Lost sight clears acquisition");
  Check(!optical.Observe(true,true,100),"Large time step cannot instantly acquire");
  Check(SearchlightSolution.RadarInRange(90000,true,true,true),"300m included");
  Check(!SearchlightSolution.RadarInRange(90001,true,true,true),"Beyond 300m excluded");
  Check(!SearchlightSolution.RadarInRange(1,false,true,true),"Enemy excluded");
  Check(!SearchlightSolution.RadarInRange(1,true,false,true),"Destroyed radar excluded");
  Check(!SearchlightSolution.RadarInRange(1,true,true,false),"Nontruck radar excluded");
  Check(SearchlightSolution.AcousticRange(0)==0&&SearchlightSolution.AcousticRange(.15f)==0,"Stopped/quiet engines do not provide a noise cue");
  Check(SearchlightSolution.AcousticRange(.2f)==3000&&SearchlightSolution.AcousticRange(1)==5000,"Low and high engine RPM span three to five kilometers");
  Check(SearchlightSolution.AcousticRange(float.NaN)==0,"Invalid engine state cannot become a contact");
  for(int seed=0;seed<20;seed++){float error=SearchlightSolution.AcousticBearingError(4000,5000,seed,0);Check(Math.Abs(error)>1.5f,"Sound bearing is coarser than a radar cue");Check(Math.Abs(SearchlightSolution.AcousticBearingError(4000,5000,seed,.1)-error)<.05f,"Bearing error drifts slowly instead of randomizing per frame");}
  Console.WriteLine("SearchlightSolutionTests passed: delayed optical/radar acquisition, occlusion, loss, time-step guard and support boundaries.");
 }
}
