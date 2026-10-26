using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Only the six generated trailer wrecks use this correction. Native collision
    // impulses, mass loss, registration and mission decay remain authoritative.
    internal static class WreckageRuntime
    {
        sealed class SleepJob
        {
            internal Wreckage Owner;
            internal Rigidbody Body;
            internal Transform Mission;
            internal float CheckAt;
        }

        sealed class Retirement
        {
            internal Wreckage Owner;
            internal Transform Mission;
            internal float RemoveAt;
            internal bool Fallback;
        }

        static readonly FieldInfo DebrisBody=AccessTools.Field(typeof(Wreckage),"debrisRB");
        static readonly List<SleepJob> Jobs=new List<SleepJob>();
        static readonly List<Retirement> Retiring=new List<Retirement>();
        static bool enabled;
        const float PollSeconds=5f;

        internal static void Install(Harmony harmony)
        {
            Clear();enabled=true;
            harmony.Patch(AccessTools.Method(typeof(Wreckage),"WaitSleepPhysics"),
                prefix:new HarmonyMethod(typeof(WreckageRuntime),nameof(WaitSleep)));
            harmony.Patch(AccessTools.Method(typeof(Wreckage),"Disintegrate"),
                prefix:new HarmonyMethod(typeof(WreckageRuntime),nameof(BeginDisintegrate)),
                postfix:new HarmonyMethod(typeof(WreckageRuntime),nameof(EndDisintegrate)),
                finalizer:new HarmonyMethod(typeof(WreckageRuntime),nameof(DisintegrateFailed)));
        }

        internal static bool SupportedName(string name)
        {
            if(name==null)return false;
            if(name.EndsWith("(Clone)",StringComparison.Ordinal))name=name.Substring(0,name.Length-7);
            switch(name)
            {
                case "F88_AA_Trailer_Wreck":
                case "F120_AAA_Trailer_Wreck":
                case "F120_AAA_Trailer Special_Wreck":
                case "L25_AA_Trailer_Wreck":
                case "L40_AA_Trailer_Wreck":
                case "Searchlight_Trailer_Wreck":return true;
                default:return false;
            }
        }

        internal static bool Matches(Wreckage wreck)
        {
            if(!wreck||!SupportedName(wreck.name))return false;
            var group=wreck.GetComponent<LODGroup>();
            if(!group)return false;
            var levels=group.GetLODs();
            if(levels.Length<2)return false;
            // Name alone cannot claim another mod's unrelated native wreck.
            foreach(var renderer in levels[0].renderers)
                if(renderer)foreach(var material in renderer.sharedMaterials)
                    if(material&&material.shader&&material.shader.name=="Gunmaggedon/WreckScorch")return true;
            return false;
        }

        static bool WaitSleep(Wreckage __instance,ref UniTask __result)
        {
            if(!enabled||!Matches(__instance))return true;
            __result=UniTask.CompletedTask;
            Schedule(__instance,Time.time);
            return false;
        }

        internal static void Schedule(Wreckage owner,float now)
        {
            if(!enabled||!owner||!Datum.origin)return;
            var body=DebrisBody.GetValue(owner) as Rigidbody;
            if(!body)return;
            foreach(var job in Jobs)if(job.Owner==owner&&job.Body==body)return;
            Jobs.Add(new SleepJob{Owner=owner,Body=body,Mission=Datum.origin,CheckAt=now+PollSeconds});
        }

        internal static bool ShouldSleep(float speedSquared,float height,float seaHeight)
        {
            // The native coroutine reads position.z here. Water and altitude use
            // world/local Y, and both values shift together with Datum's origin.
            return speedSquared<1f||height<seaHeight;
        }

        static bool BeginDisintegrate(Wreckage __instance,out Retirement __state)
        {
            __state=null;
            if(!enabled||!Matches(__instance))return true;
            foreach(var previous in Retiring)if(previous.Owner==__instance)return false;
            __state=new Retirement{Owner=__instance,Mission=Datum.origin};
            Retiring.Add(__state);
            return true;
        }

        static void EndDisintegrate(Retirement __state)
        {
            if(__state==null||!__state.Owner)return;
            for(int i=Jobs.Count-1;i>=0;i--)if(Jobs[i].Owner==__state.Owner)Jobs.RemoveAt(i);
            GridSquare ignored;
            if(BattlefieldGrid.TryGetGridSquare(__state.Owner.transform.GlobalPosition(),out ignored))return;
            // Native Disintegrate returns here without removing its registration
            // or scheduling destruction. Its collision/VFX/sink work has already
            // happened; complete only the skipped cleanup with the same1s delay.
            var mission=MissionManager.i;
            if(mission&&mission.listWrecks!=null)mission.listWrecks.Remove(__state.Owner);
            __state.Fallback=true;__state.RemoveAt=Time.time+1f;
        }

        static Exception DisintegrateFailed(Exception __exception,Retirement __state)
        {
            if(__exception!=null&&__state!=null)Retiring.Remove(__state);
            return __exception;
        }

        internal static void Tick(){if(enabled)Step(Time.time);}
        internal static void Step(float now)
        {
            for(int i=Retiring.Count-1;i>=0;i--)
            {
                var retired=Retiring[i];
                if(!retired.Owner||!retired.Mission||retired.Mission!=Datum.origin)
                {Retiring.RemoveAt(i);continue;}
                if(!retired.Fallback||now<retired.RemoveAt)continue;
                Retiring.RemoveAt(i);RuntimePool<Transform>.Remove(retired.Owner.gameObject);
            }
            for(int i=Jobs.Count-1;i>=0;i--)
            {
                var job=Jobs[i];
                if(!job.Owner||!job.Body||!job.Mission||job.Mission!=Datum.origin||
                    !ReferenceEquals(DebrisBody.GetValue(job.Owner),job.Body))
                {Jobs.RemoveAt(i);continue;}
                if(now<job.CheckAt)continue;
                if(ShouldSleep(job.Body.velocity.sqrMagnitude,job.Body.position.y,Datum.LocalSeaY))
                {
                    // Detach before destruction can invoke collision/lifecycle callbacks.
                    Jobs.RemoveAt(i);RuntimePool<Transform>.Remove(job.Body);
                }
                else job.CheckAt=now+PollSeconds;
            }
        }

        internal static int PendingCount{get{return Jobs.Count;}}
        internal static int RetiringCount{get{return Retiring.Count;}}
        internal static void Clear(){Jobs.Clear();Retiring.Clear();}
        internal static void Shutdown(){enabled=false;Clear();}
    }
}
