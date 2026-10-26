using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class RemovalBuffer<T>
    {
        static readonly Stack<List<T>> free=new Stack<List<T>>();
        internal static List<T> Rent(){return free.Count==0?new List<T>():free.Pop();}
        internal static void Return(List<T> list){list.Clear();if(free.Count<8)free.Push(list);}
    }
    internal static class OptimizationRuntime
    {
        static Transform mission;static float nextMaintenance;
        internal static void EnsureMission(){var current=Datum.origin;if(!current)current=null;if(!ReferenceEquals(mission,current)){Clear();mission=current;}}
        internal static void Tick()
        {
            EnsureMission();
            TravellingAudio.Tick();ClusterVisualPools.Tick();
            if(Time.time<nextMaintenance)return;nextMaintenance=Time.time+1;
            CoastalArtillery.Maintain();CoastalIllumination.Maintain();CoastalRanging.Maintain();SpecialShells.Maintain();SharedShipBounds.Maintain();
        }
        internal static void Clear()
        {
            F88FlakRuntime.ClearFlights();C127Rounds.Clear();R230PlayerAlert.Clear();IncendiaryCanisterRuntime.Clear();R230NuclearRuntime.Shutdown();ClusterVisualPools.Clear();TrailService.Clear();TravellingAudio.Clear();SharedWorldQueries.Clear();SharedShipBounds.Clear();if(Plugin.Instance)Plugin.Instance.ResetAudioBanks();mission=null;nextMaintenance=0;
        }
    }
}
