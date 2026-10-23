using System;
using GunmageddonAudio;
public static class F88ReloadTests
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 public static void Main()
 {
  foreach(double dt in new[]{1d/20,1d/30,1d/60,1d/144}){
   for(int shot=0;shot<20;shot++){
    var cycle=new F88ReloadCycle();double now=shot*20,clear=-1,load=-1,close=-1;int ejections=0;cycle.Begin(now);
    for(int i=0;i<2000&&!cycle.Ready;i++){
     now+=dt;if(cycle.Tick(now)){clear=now;ejections++;}
     if(cycle.Phase==F88ReloadCycle.Stage.Insert&&load<0)load=now;
     if(cycle.Phase==F88ReloadCycle.Stage.Secure)close=now;
     Check(!cycle.Ready||close>=0,"Ready must follow secured breech");
    }
    Check(cycle.Ready&&ejections==1,"Exactly one casing handoff per complete cycle");
    Check(load-clear>=2-1e-6,"Loading waits at least two seconds after actual ejection");
    Check(now-shot*20>=6.2-1e-6,"No frame rate can shorten the physical reload sequence");
   }
  }
  var lag=new F88ReloadCycle();lag.Begin(0);lag.Tick(.6);lag.Tick(1);
  Check(lag.Tick(10)&&lag.Phase==F88ReloadCycle.Stage.Clearance,"Delayed extraction starts actual clearance at the handoff");
  lag.Tick(11.999);Check(lag.Phase==F88ReloadCycle.Stage.Clearance,"Cannot load early after delayed extraction");
  lag.Tick(20);Check(lag.Phase==F88ReloadCycle.Stage.Insert&&lag.Progress(20)==0,"Long frame cannot skip visible insertion");
  lag.Tick(30);Check(lag.Phase==F88ReloadCycle.Stage.Close&&!lag.Ready,"Seated shell still requires closing door");
  lag.Tick(40);Check(lag.Phase==F88ReloadCycle.Stage.Secure&&!lag.Ready,"Closing door still requires secured dwell");
  lag.Tick(40.2);Check(lag.Ready&&lag.Remaining(40.2)==0,"Ready only after all stages");
  Console.WriteLine("F88 reload: "+checks+" assertions passed, including frame-rate and delayed-frame ordering.");
 }
}
