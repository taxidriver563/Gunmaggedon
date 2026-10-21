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
  Console.WriteLine("SearchlightSolutionTests passed: delayed optical/radar acquisition, occlusion, loss, time-step guard and support boundaries.");
 }
}
