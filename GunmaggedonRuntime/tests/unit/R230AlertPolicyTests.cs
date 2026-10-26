using System;
using GunmageddonAudio;
public static class R230AlertPolicyTests
{
 static int checks;
 static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 public static void Main(){
  Check(R230AlertPolicy.State(false,1)==RailgunAlertState.Tracking,"Tracking isn't an imminent alarm");
  Check(R230AlertPolicy.State(true,.749)==RailgunAlertState.Charging&&R230AlertPolicy.State(true,.75)==RailgunAlertState.Imminent,"Final half-second threshold");
  Check(R230Capacitor.ChargeSeconds*(1-.75)==R230AlertPolicy.ImminentSeconds,"State window follows real capacitor");
  Check(R230AlertPolicy.State(true,R230AlertPolicy.WarningProgress(2.499/3,3))==RailgunAlertState.Charging&&R230AlertPolicy.State(true,R230AlertPolicy.WarningProgress(2.5/3,3))==RailgunAlertState.Imminent,"Nuclear alert reserves continuous tone for final half-second");
  double previous=1;for(int i=0;i<=750;i++){double n=R230AlertPolicy.PulseInterval(RailgunAlertState.Charging,i/1000d);Check(n<=previous&&n>=.239&&n<=.651,"Charging pulses accelerate with gaps");previous=n;}
  Check(R230AlertPolicy.PulseInterval(RailgunAlertState.Tracking,1)==2.4,"Tracking stays slow");
  Check(R230AlertPolicy.Newer(0,uint.MaxValue)&&!R230AlertPolicy.Newer(uint.MaxValue,0),"Sequence wrap ordering");
  Check(!R230AlertPolicy.Fresh(double.NaN)&&!R230AlertPolicy.Fresh(double.PositiveInfinity)&&!R230AlertPolicy.Fresh(.801)&&!R230AlertPolicy.Fresh(-1.01)&&R230AlertPolicy.Fresh(.8),"Packet freshness");
  var ledger=new R230AlertLedger();double progress;int count;
  Check(ledger.Receive(1,10,RailgunAlertState.Tracking,0,1,0),"First tracking lease");
  Check(ledger.Select(10,0,out progress,out count)==RailgunAlertState.Tracking&&count==1,"Correct target receives tracking");
  Check(ledger.Select(11,0,out progress,out count)==RailgunAlertState.Off,"Other aircraft not alerted");
  Check(ledger.Receive(2,10,RailgunAlertState.Imminent,.9,1,.1),"Second gun");
  Check(ledger.Select(10,.2,out progress,out count)==RailgunAlertState.Imminent&&count==2&&progress==.9,"One aggregate uses highest urgency");
  Check(ledger.Receive(2,0,RailgunAlertState.Off,0,2,.3)&&!ledger.Receive(2,10,RailgunAlertState.Imminent,.9,1,.4),"Cancel rejects delayed prior threat");
  Check(ledger.Select(10,.4,out progress,out count)==RailgunAlertState.Tracking&&count==1,"Other tracking survives one cancellation");
  Check(ledger.Receive(1,11,RailgunAlertState.Charging,.2,2,.5),"Target switch");
  Check(ledger.Select(10,.5,out progress,out count)==RailgunAlertState.Off,"Old target cleared");
  Check(ledger.Select(11,1.31,out progress,out count)==RailgunAlertState.Off,"Missing clear packet expires safely");
  Check(!ledger.Receive(0,1,RailgunAlertState.Tracking,0,1,0)&&!ledger.Receive(1,0,RailgunAlertState.Charging,0,3,0)&&!ledger.Receive(1,1,(RailgunAlertState)4,0,3,0)&&!ledger.Receive(1,1,RailgunAlertState.Tracking,double.NaN,3,0),"Invalid input ignored");
  ledger.Prune(5);Check(ledger.Receive(1,10,RailgunAlertState.Tracking,0,1,5),"Expired source history retires");
  ledger.Clear();Check(ledger.Select(10,5,out progress,out count)==RailgunAlertState.Off,"Mission cleanup");
  var capacitor=new R230Capacitor();capacitor.Tick(10,true,0);Check(R230AlertPolicy.State(capacitor.Charging,capacitor.Progress(.4))==RailgunAlertState.Charging,"Actual charge clock");
  Check(R230AlertPolicy.State(capacitor.Charging,capacitor.Progress(1.5))==RailgunAlertState.Imminent,"Actual clock final interval");
  capacitor.Cancel();Check(R230AlertPolicy.State(capacitor.Charging,0)==RailgunAlertState.Tracking,"Charge interruption returns to tracking");
  Console.WriteLine("Railgun alert policy: "+checks+" checks passed.");
 }
}
