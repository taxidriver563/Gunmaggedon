using System;
using System.Collections.Generic;
using UnityEngine;
namespace GunmageddonAudio
{
    internal sealed class RuntimePool<T> where T:Component
    {
        internal struct Lease {internal T Value;internal long Id;}
        sealed class Entry {internal T Value;internal long Id;internal int Index=-1;}
        readonly Func<T> create;readonly int retained;readonly Action<T> reset;
        readonly Stack<Entry> idle=new Stack<Entry>();readonly List<Entry> active=new List<Entry>();
        readonly Dictionary<T,Entry> entries=new Dictionary<T,Entry>();long generation;
        internal RuntimePool(int retained,Func<T> create,Action<T> reset){this.retained=retained;this.create=create;this.reset=reset;}
        internal int Count {get{return active.Count;}}
        internal int IdleCount {get{return idle.Count;}}
        internal Lease At(int index){var e=active[index];return new Lease{Value=e.Value,Id=e.Id};}
        internal Lease Rent()
        {
            Entry e=null;while(idle.Count>0){var candidate=idle.Pop();if(candidate.Value){e=candidate;break;}entries.Remove(candidate.Value);}
            if(e==null){e=new Entry{Value=create()};entries.Add(e.Value,e);OptimizationMetrics.Count("PoolCreates");}
            e.Id=++generation;e.Index=active.Count;active.Add(e);return new Lease{Value=e.Value,Id=e.Id};
        }
        internal bool Release(T value,long id)
        {
            Entry e;if(ReferenceEquals(value,null)||!entries.TryGetValue(value,out e)||e.Index<0||e.Id!=id)return false;
            int last=active.Count-1;active[e.Index]=active[last];active[e.Index].Index=e.Index;active.RemoveAt(last);e.Index=-1;
            try{if(value){reset(value);value.gameObject.SetActive(false);}}catch{entries.Remove(value);if(value)Remove(value.gameObject);throw;}
            if(value&&idle.Count<retained)idle.Push(e);else{entries.Remove(value);if(value)Remove(value.gameObject);}return true;
        }
        internal void Clear()
        {
            foreach(var e in entries.Values)if(e.Value){e.Value.gameObject.SetActive(false);Remove(e.Value.gameObject);}
            entries.Clear();active.Clear();idle.Clear();generation++;
        }
        internal static void Remove(UnityEngine.Object value){if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        internal static void Place(T value,Transform parent,Vector3 position){value.transform.SetParent(parent,false);value.transform.localRotation=Quaternion.identity;value.transform.localScale=Vector3.one;value.transform.position=position;}
    }
    internal static class OptimizationMetrics
    {
#if GUNMAGGEDON_DIAGNOSTICS
        internal static readonly Dictionary<string,long> Counters=new Dictionary<string,long>();
        static readonly Func<long> allocated=AllocationReader();
        static long[] starts=new long[16];static string[] scopes=new string[16];static int depth;
        static Func<long> AllocationReader(){var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);if(method==null)return null;var read=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);long before=read();GC.KeepAlive(new byte[4096]);return read()>before?read:null;}
#endif
        [System.Diagnostics.Conditional("GUNMAGGEDON_DIAGNOSTICS")]
        internal static void Count(string name){
#if GUNMAGGEDON_DIAGNOSTICS
            long n;Counters.TryGetValue(name,out n);Counters[name]=n+1;
#endif
        }
        [System.Diagnostics.Conditional("GUNMAGGEDON_DIAGNOSTICS")]
        internal static void Begin(string name){UnityEngine.Profiling.Profiler.BeginSample("Gunmaggedon."+name);
#if GUNMAGGEDON_DIAGNOSTICS
            if(depth==starts.Length){Array.Resize(ref starts,depth*2);Array.Resize(ref scopes,depth*2);}scopes[depth]=name;starts[depth]=allocated==null?0:allocated();depth++;
#endif
        }
        [System.Diagnostics.Conditional("GUNMAGGEDON_DIAGNOSTICS")]
        internal static void End(){
#if GUNMAGGEDON_DIAGNOSTICS
            if(depth>0){depth--;if(allocated!=null){long bytes=allocated()-starts[depth],old;string key=scopes[depth]+".AllocatedBytes";Counters.TryGetValue(key,out old);Counters[key]=old+bytes;}scopes[depth]=null;}
#endif
            UnityEngine.Profiling.Profiler.EndSample();}
    }
}
