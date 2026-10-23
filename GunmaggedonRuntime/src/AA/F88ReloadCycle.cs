using System;
namespace GunmageddonAudio
{
    // Ordered phases deliberately retain real clearance time after a delayed frame.
    internal sealed class F88ReloadCycle
    {
        internal enum Stage { Ready, Recoil, Open, Extract, Clearance, Insert, Close, Secure }
        internal const float MinimumClearance=2f,Duration=6.2f;
        static readonly double[] Durations={0,.6,.4,.8,2,1.6,.6,.2};
        internal Stage Phase {get;private set;}
        double started;
        internal bool Ready {get{return Phase==Stage.Ready;}}
        internal void Begin(double now){Phase=Stage.Recoil;started=now;}
        internal bool Tick(double now)
        {
            if(Ready||now-started+1e-6<Durations[(int)Phase])return false;
            bool cleared=Phase==Stage.Extract;
            Phase=Phase==Stage.Secure?Stage.Ready:Phase+1;started=now;
            return cleared;
        }
        internal float Progress(double now){return Ready?1:(float)Math.Max(0,Math.Min(1,(now-started)/Durations[(int)Phase]));}
        internal float Remaining(double now)
        {
            if(Ready)return 0;double remaining=Math.Max(0,Durations[(int)Phase]-(now-started));
            for(int i=(int)Phase+1;i<Durations.Length;i++)remaining+=Durations[i];return (float)remaining;
        }
    }
}
