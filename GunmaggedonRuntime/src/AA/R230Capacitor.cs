using System;
namespace GunmageddonAudio
{
    public sealed class R230Capacitor
    {
        public const int Capacity=3;
        public const double ChargeSeconds=2,RechargeSeconds=10;
        public const double NuclearTrackingSeconds=5,NuclearChargeSeconds=3,NuclearRechargeSeconds=20;
        public int Cells{get;private set;}
        public double ChargeDuration{get;private set;}
        public double RechargeDuration{get;private set;}
        public int Remaining{get;private set;}
        public R230Capacitor():this(Capacity,ChargeSeconds,RechargeSeconds){}
        public R230Capacitor(int cells,double charge,double recharge)
        {
            if(cells<1||charge<=0||recharge<charge||double.IsNaN(charge)||double.IsNaN(recharge)||double.IsInfinity(charge)||double.IsInfinity(recharge))throw new ArgumentOutOfRangeException();
            Cells=cells;Remaining=cells;ChargeDuration=charge;RechargeDuration=recharge;
        }
        public bool Charging{get;private set;}
        public double ChargeStarted{get;private set;}
        public double RechargeUntil{get;private set;}
        int target;double lastShot=double.NegativeInfinity;
        public void Cancel(){Charging=false;target=0;}
        public bool CanPrepare(double now){return Remaining>0||now>=RechargeUntil-ChargeDuration;}
        public void Tick(int id,bool valid,double now)
        {
            if(Remaining==0&&now>=RechargeUntil)Remaining=Cells;
            if(!valid||id==0||!CanPrepare(now)){Cancel();return;}
            if(!Charging||target!=id){target=id;Charging=true;ChargeStarted=now;}
        }
        public bool Ready(double now){return Charging&&Remaining>0&&now-ChargeStarted>=ChargeDuration;}
        public double Progress(double now){return Charging?Math.Max(0,Math.Min(1,(now-ChargeStarted)/ChargeDuration)):0;}
        // Called by the authoritative native SpawnBullet postfix, never by FX.
        public bool Shot(double now)
        {
            if(now<=lastShot||Remaining<=0)return false;
            lastShot=now;Remaining--;Cancel();if(Remaining==0)RechargeUntil=now+RechargeDuration;return true;
        }
    }
}
