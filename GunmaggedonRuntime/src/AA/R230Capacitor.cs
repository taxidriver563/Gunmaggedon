using System;
namespace GunmageddonAudio
{
    public sealed class R230Capacitor
    {
        public const int Capacity=3;
        public const double ChargeSeconds=2,RechargeSeconds=10;
        public int Remaining{get;private set;}=Capacity;
        public bool Charging{get;private set;}
        public double ChargeStarted{get;private set;}
        public double RechargeUntil{get;private set;}
        int target;double lastShot=double.NegativeInfinity;
        public void Cancel(){Charging=false;target=0;}
        public void Tick(int id,bool valid,double now)
        {
            if(Remaining==0&&now>=RechargeUntil)Remaining=Capacity;
            if(!valid||id==0||Remaining==0&&now<RechargeUntil-ChargeSeconds){Cancel();return;}
            if(!Charging||target!=id){target=id;Charging=true;ChargeStarted=now;}
        }
        public bool Ready(double now){return Charging&&Remaining>0&&now-ChargeStarted>=ChargeSeconds;}
        public double Progress(double now){return Charging?Math.Max(0,Math.Min(1,(now-ChargeStarted)/ChargeSeconds)):0;}
        // Called by the authoritative native SpawnBullet postfix, never by FX.
        public bool Shot(double now)
        {
            if(now<=lastShot||Remaining<=0)return false;
            lastShot=now;Remaining--;Cancel();if(Remaining==0)RechargeUntil=now+RechargeSeconds;return true;
        }
    }
}
