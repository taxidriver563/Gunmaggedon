using System;
using System.Collections.Generic;
using GunmageddonAudio;

public static class OpticalRangingTests
{
 static int assertions;
 static void Check(bool b,string message){assertions++;if(!b)throw new Exception(message);}
 static OpticalVector P(double x,double y,double z){return new OpticalVector(x,y,z);}
 sealed class Feedback{public int Epoch,Burst;public double At;public OpticalVector Error;}
 static double Flight(OpticalVector p){return Math.Min(6,p.Length/900);}
 static double Run(bool agile,bool feedback,bool print)
 {
  Func<double,OpticalVector> target=t=>agile?P(2500+180*Math.Sin(t*.75),750+100*Math.Sin(t*1.1),2200+240*Math.Cos(t*.65)):
      P(2500+45*t,750,2200+20*t);
  var solution=new OpticalRangingSolution();solution.Reset(1,target(0),0,42);
  var pending=new List<Feedback>();double nextShot=0,total=0;int count=0,burst=0;
  for(int i=0;i<=900;i++){
   double now=i*.05;var current=target(now);solution.Sample(current,now);solution.Step(.05,now);
   for(int j=pending.Count-1;j>=0;j--)if(now>=pending[j].At){var f=pending[j];
      if(feedback)solution.Observe(f.Epoch,f.Burst,f.Error,now);pending.RemoveAt(j);}
   if(now+.001>=nextShot){
    double flight=Flight(current);var aim=solution.Predict(current,flight,now);
    var miss=aim-target(now+flight);
    pending.Add(new Feedback{Epoch=solution.Epoch,Burst=++burst,At=now+flight+OpticalRangingSolution.ReactionSeconds,Error=miss});
    if(now>20){total+=miss.Length;count++;}
    nextShot+=3.5;
   }
  }
  double mean=total/count;if(print)Console.WriteLine((agile?"Agile":"Straight")+(feedback?" with feedback":" without feedback")+": late mean miss "+mean.ToString("F1")+" m; "+solution.Corrections+" corrections.");return mean;
 }
 public static void Main()
 {
  var s=new OpticalRangingSolution();var point=P(3000,700,1500);s.Reset(1,point,0,42);int epoch=s.Epoch;
  var original=s.Predict(point,3,0);
  Check((original-point).Length>100,"Initial systematic range/laying error");
  for(int i=1;i<=20;i++){s.Sample(point,i*.5);s.Step(.5,i*.5);}
  Check(s.Corrections==0&&s.Confidence==0,"Waiting alone never ranges the target");
  Check(!s.Sample(point,10.1),"No perfect per-frame motion samples");
  Check(s.Observe(epoch,1,P(150,40,-20),10),"Visible observed clip corrects");
  Check(s.Correction.Length<.001,"No snap on feedback arrival");
  s.Step(.5,10.5);Check(s.Correction.Length>0&&s.Correction.Length<65,"Rate-limited bounded correction");
  Check(!s.Observe(epoch,1,P(900,0,0),10.5),"One correction per clip");
  s.Sample(point,10.5);s.Sample(point+P(100,0,0),11);
  Check(s.Epoch!=epoch&&s.Confidence<.1,"Hard maneuver spoils old solution");
  Check(!s.Observe(epoch,2,P(30,0,0),11),"In-flight old-maneuver feedback rejected");
  int maneuverEpoch=s.Epoch;s.Sample(point+P(400,0,0),15);
  Check(s.Epoch!=maneuverEpoch&&s.EstimatedVelocity.Length==0,"Long sight loss drops velocity and stale corrections");
  Check(!s.Observe(s.Epoch,3,P(10,0,0),17),"No invisible/stale observation credit");
  s.Reset(2,point,18,43);Check(s.TargetId==2&&s.Corrections==0&&s.Confidence==0,"Target switch starts fresh");
  Check(!s.Sample(P(double.NaN,0,0),19),"Invalid position rejected");
  for(int i=0;i<100;i++){double t=20+i*.5;s.Sample(point,t);s.Observe(s.Epoch,i+1,P(0,0,0),t);s.Step(.5,t);}
  Check(s.Confidence<=.85,"Never perfect confidence");
  var a=s.Predict(point,0,71);var b=s.Predict(point,0,72);
  Check((a-b).Length>1,"Persistent crew error remains after ranging");
  var closest=OpticalRangingSolution.ClosestPass(P(-100,3,0),P(100,3,0));
  Check(Math.Abs(closest.Length-3)<.0001,"Swept closest pass preserves small/fast target observations");
  var straight=Run(false,true,true);var uncorr=Run(false,false,true);var agile=Run(true,true,true);
  Check(straight<uncorr*.55,"Observed fire improves on predictable flight");
  Check(agile>straight*2,"Agile flight remains substantially harder");
  Check(straight>3,"Ranging does not enable a perfect beam");
  Console.WriteLine("Passed "+assertions+" optical ranging assertions.");
 }
}
