using UnityEngine;
namespace GunmageddonAudio
{
    public sealed class TravellingVoice:MonoBehaviour
    {
        internal AudioSource Near,Far;internal float Born,Limit,Retire;internal bool Playing;internal long Lease;
    }
    internal static class TravellingAudio
    {
        static readonly RuntimePool<TravellingVoice> pool=new RuntimePool<TravellingVoice>(128,Create,Reset);
        static TravellingVoice Create(){var go=new GameObject("Travelling audio emitter");go.SetActive(false);var v=go.AddComponent<TravellingVoice>();v.Near=go.AddComponent<AudioSource>();v.Far=go.AddComponent<AudioSource>();v.Near.playOnAwake=v.Far.playOnAwake=false;return v;}
        static void Reset(TravellingVoice v){v.Near.Stop();v.Far.Stop();v.Near.clip=v.Far.clip=null;v.Playing=false;v.Born=v.Limit=v.Retire=0;}
        internal static void Schedule(Vector3 position,AudioSource near,AudioSource far,float limit)
        {
            var v=Rent(position,limit);Plugin.CopyVoice(v.Near,near);Plugin.CopyVoice(v.Far,far);Commit(v);
        }
        internal static TravellingVoice Rent(Vector3 position,float limit){OptimizationRuntime.EnsureMission();var lease=pool.Rent();var v=lease.Value;Reset(v);v.Lease=lease.Id;RuntimePool<TravellingVoice>.Place(v,Datum.origin,position);v.Limit=limit;v.Born=Time.time;return v;}
        internal static void Commit(TravellingVoice v){v.gameObject.SetActive(true);Evaluate(v,Time.time);}
        static bool Evaluate(TravellingVoice v,float now)
        {
            if(v.Playing)return now>=v.Retire;
            float elapsed=now-v.Born;float distance=Vector3.Distance(v.transform.position,Plugin.ListenerPosition());
            if(elapsed*343<distance)return elapsed>Plugin.TravelSeconds(v.Limit)+1;
            float mix=Plugin.DistantMix(distance),life=0;
            if(v.Near.clip){v.Near.volume*=Mathf.Sqrt(1-mix);v.Near.Play();life=v.Near.clip.length/v.Near.pitch;}
            if(v.Far.clip){v.Far.volume*=Mathf.Sqrt(mix);v.Far.Play();life=Mathf.Max(life,v.Far.clip.length/v.Far.pitch);}
            v.Playing=true;v.Retire=now+life+1;return false;
        }
        internal static void Tick(){OptimizationMetrics.Begin("TravellingAudio");try{for(int i=pool.Count-1;i>=0;i--){var lease=pool.At(i);if(!lease.Value||Evaluate(lease.Value,Time.time))pool.Release(lease.Value,lease.Id);}}finally{OptimizationMetrics.End();}}
        internal static void Clear(){pool.Clear();}
    }
}
