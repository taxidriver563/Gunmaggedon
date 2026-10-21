using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Enabled by L40Runtime, independently of the quarantined vehicle-job/tow hooks.
    internal static class AAOpticalRanging
    {
        internal sealed class State
        {
            internal readonly OpticalRangingSolution Solution=new OpticalRangingSolution();
            internal Gun Gun; internal Unit Target; internal Transform Muzzle;
            internal bool Visible; internal float NextSight, LastSeen, LastUsed, LastStep;
            internal int Fired;
            internal readonly List<ClipFeedback> Feedback=new List<ClipFeedback>(8);
        }
        internal sealed class ClipFeedback
        {
            internal int Epoch, Burst, Count; internal OpticalVector Sum; internal float Ready;
        }
        sealed class Shot
        {
            internal State State; internal int Epoch, Burst;
            internal OpticalVector Previous, Best; internal bool HasPrevious;
            internal float Fired; internal double Closest=double.MaxValue;
        }
        sealed class Pending
        {
            internal Pending Previous; internal Shot Shot; internal Unit Owner; internal WeaponInfo Info;
        }
        static readonly FieldInfo Owner=AccessTools.Field(typeof(AimSolver),"attachedUnit"),
            Target=AccessTools.Field(typeof(AimSolver),"currentTarget"),
            From=AccessTools.Field(typeof(AimSolver),"firingTransform"),
            Info=AccessTools.Field(typeof(AimSolver),"weaponInfo"),
            SimGun=AccessTools.Field(typeof(BulletSim),"gun"),
            Visual=AccessTools.Field(typeof(BulletSim),"visualOnly");
        static readonly Dictionary<AimSolver,State> Solvers=new Dictionary<AimSolver,State>();
        static readonly Dictionary<Gun,State> Guns=new Dictionary<Gun,State>();
        static readonly Dictionary<BulletSim.Bullet,Shot> Shots=new Dictionary<BulletSim.Bullet,Shot>();
        static readonly List<AimSolver> Expired=new List<AimSolver>();
        [ThreadStatic] static Pending pending;
        static float nextCleanup;
        static bool IsL40(WeaponInfo info,Unit owner)
        {return info&&info.name=="L40_AA_Weapon"&&owner&&owner.definition&&owner.definition.jsonKey=="Gunmaggedon_L40_AA";}
        static OpticalVector V(Vector3 p){return new OpticalVector(p.x,p.y,p.z);}
        static Vector3 V(OpticalVector p){return new Vector3((float)p.X,(float)p.Y,(float)p.Z);}
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(AimSolver),"GetAimVector"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(Aim)));
            h.Patch(AccessTools.Method(typeof(Turret),"SetObservedBullet"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(NativeObservation)));
            h.Patch(AccessTools.Method(typeof(Gun),"Fire"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(AllowFire)));
            h.Patch(AccessTools.Method(typeof(Gun),"SpawnBullet"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(AllowFire)));
            h.Patch(AccessTools.Method(typeof(BulletSim),"AddBullet"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(Add)),finalizer:new HarmonyMethod(typeof(AAOpticalRanging),nameof(Added)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"TrajectoryTrace"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(TraceStart)),postfix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(TraceEnd)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"Remove"),prefix:new HarmonyMethod(typeof(AAOpticalRanging),nameof(Remove)));
        }
        static bool NativeObservation(Turret __instance)
        {var gun=__instance.GetWeapon() as Gun;return !gun||!IsL40(gun.info,gun.attachedUnit);}
        static bool AllowFire(Gun __instance)
        {
            if(!IsL40(__instance.info,__instance.attachedUnit)||!__instance.attachedUnit.LocalSim)return true;
            State s;return Guns.TryGetValue(__instance,out s)&&s.Target&&s.Visible&&Time.time-s.LastSeen<.75f;
        }

        static bool Aim(AimSolver __instance,ref float __0,ref Vector3 __result)
        {
            var info=Info.GetValue(__instance) as WeaponInfo;
            if(!info||info.name!="L40_AA_Weapon")return true;
            var owner=Owner.GetValue(__instance) as Unit;
            if(!IsL40(info,owner)||!owner.LocalSim)return true;
            var target=Target.GetValue(__instance) as Unit;var from=From.GetValue(__instance) as Transform;
            if(!target||!from)return true;
            State state;
            if(!Solvers.TryGetValue(__instance,out state)) {
                var gun=from.GetComponentInParent<Gun>(true);if(!gun)return true;
                state=new State{Gun=gun,Muzzle=from,LastStep=Time.time};Solvers.Add(__instance,state);Guns[gun]=state;
            }
            float now=Time.time;state.LastUsed=now;
            if(state.Target!=target) {
                state.Target=target;state.Fired=0;state.Feedback.Clear();state.Visible=false;state.NextSight=now;
                state.Solution.Reset(target.GetInstanceID(),V(target.transform.position-from.position),now,state.Gun.GetInstanceID()^target.GetInstanceID());
            }
            if(now>=state.NextSight) {
                state.NextSight=now+.5f;
                state.Visible=Vector3.Distance(from.position,target.transform.position)<=6000f&&target.LineOfSight(from.position,10f);
                if(state.Visible) {state.Solution.Sample(V(target.transform.position-from.position),now);state.LastSeen=now;}
            }
            state.Solution.Step(Mathf.Clamp(now-state.LastStep,0,.2f),now);state.LastStep=now;
            for(int i=state.Feedback.Count-1;i>=0;i--) {
                var feedback=state.Feedback[i];
                if(feedback.Epoch!=state.Solution.Epoch||now>=feedback.Ready) {
                    if(state.Visible&&feedback.Epoch==state.Solution.Epoch&&feedback.Count>0)
                        state.Solution.Observe(feedback.Epoch,feedback.Burst,feedback.Sum*(1.0/feedback.Count),now);
                    state.Feedback.RemoveAt(i);
                }
            }
            if(!state.Visible) {__result=from.forward;__0=0;return false;}
            Vector3 relative=target.transform.position-from.position;
            float range=Mathf.Max(10,relative.magnitude+(float)state.Solution.RangeBias);
            float flight=FlightSeconds(range,info.muzzleVelocity,info.dragCoef);
            var estimated=state.Solution.Predict(V(relative),flight,now);
            // A simple range card, then measured miss corrections. No exact intercept solver,
            // target acceleration prediction, or redirection of bullets after launch.
            var direction=V(estimated)+Vector3.up*(.5f*9.81f*info.gravMult*flight*flight);
            __result=direction.sqrMagnitude>1?direction.normalized:from.forward;__0=range;
            Cleanup(now);return false;
        }
        internal static float FlightSeconds(float range,float speed,float drag)
        {
            speed=Mathf.Max(100,speed);drag=Mathf.Max(0,drag);
            return Mathf.Clamp(drag<.0001f?range/speed:(Mathf.Exp(Mathf.Min(3,range*drag/speed))-1)/drag,.05f,14f);
        }
        static void Add(BulletSim __instance,out Pending __state)
        {
            __state=null;var gun=SimGun.GetValue(__instance) as Gun;
            if(!gun||!IsL40(gun.info,gun.attachedUnit)||(bool)Visual.GetValue(__instance))return;
            State state;if(!Guns.TryGetValue(gun,out state)||!state.Target||!state.Visible)return;
            // At most 256 tracked shells globally. Overflow keeps native fire/damage and only
            // drops that shell's feedback. Four rounds make one crew correction clip.
            int burst=++state.Fired;burst=(burst-1)/4+1;
            if(Shots.Count>=256)return;
            __state=new Pending{Previous=pending,Owner=gun.attachedUnit,Info=gun.info,
                Shot=new Shot{State=state,Epoch=state.Solution.Epoch,Burst=burst,Fired=Time.time}};
            pending=__state;
        }
        static Exception Added(Exception __exception,Pending __state)
        {if(__state!=null)pending=__state.Previous;return __exception;}
        static void TraceStart(BulletSim.Bullet __instance,Unit owner,WeaponInfo info,bool visualOnly)
        {
            if(visualOnly||pending==null||pending.Owner!=owner||pending.Info!=info||Shots.ContainsKey(__instance))return;
            Shots.Add(__instance,pending.Shot);
        }
        static void TraceEnd(BulletSim.Bullet __instance)
        {Sample(__instance,false);}
        static void Sample(BulletSim.Bullet __instance,bool retiring)
        {
            Shot shot;if(!Shots.TryGetValue(__instance,out shot))return;
            var s=shot.State;
            if(!s.Gun||!s.Target||shot.Epoch!=s.Solution.Epoch||Time.time-shot.Fired>15) {Shots.Remove(__instance);return;}
            var relative=V(__instance.position.ToLocalPosition()-s.Target.transform.position);
            if(shot.HasPrevious) {
                var closest=OpticalRangingSolution.ClosestPass(shot.Previous,relative);
                if(closest.Length<shot.Closest){shot.Best=closest;shot.Closest=closest.Length;}
                var movement=relative-shot.Previous;
                bool passed=OpticalVector.Dot(relative,movement)>=0&&OpticalVector.Dot(shot.Previous,movement)<0;
                if(passed||retiring||__instance.impacted||!__instance.active) {
                    // A visible terminal airburst is useful feedback too; timed rounds often
                    // expire just before crossing the target. Distant ground deaths are not.
                    bool nearbyTerminal=shot.Closest<Math.Min(400,s.Solution.HasSample?
                        Vector3.Distance(s.Muzzle.position,s.Target.transform.position)*.15:0);
                    Finish(__instance,shot,passed||nearbyTerminal);return;
                }
            }
            shot.Previous=relative;shot.HasPrevious=true;
        }
        static void Finish(BulletSim.Bullet bullet,Shot shot,bool passed)
        {
            Shots.Remove(bullet);var s=shot.State;
            if(!passed||!s.Visible||shot.Epoch!=s.Solution.Epoch||Time.time-s.LastSeen>.75f||shot.Closest>600)return;
            ClipFeedback feedback=null;
            foreach(var f in s.Feedback)if(f.Epoch==shot.Epoch&&f.Burst==shot.Burst){feedback=f;break;}
            if(feedback==null) {
                if(s.Feedback.Count>=8)return;
                feedback=new ClipFeedback{Epoch=shot.Epoch,Burst=shot.Burst,Ready=Time.time+(float)OpticalRangingSolution.ReactionSeconds};s.Feedback.Add(feedback);
            }
            feedback.Sum=feedback.Sum+shot.Best;feedback.Count++;
        }
        static void Remove(BulletSim.Bullet __instance){Sample(__instance,true);Shots.Remove(__instance);}
        static void Cleanup(float now)
        {
            if(now<nextCleanup)return;nextCleanup=now+5;
            Expired.Clear();foreach(var kv in Solvers)if(!kv.Value.Gun||now-kv.Value.LastUsed>30)Expired.Add(kv.Key);
            foreach(var key in Expired){var s=Solvers[key];if(!ReferenceEquals(s.Gun,null))Guns.Remove(s.Gun);Solvers.Remove(key);}
            Expired.Clear();
        }
        internal static void Shutdown(){Solvers.Clear();Guns.Clear();Shots.Clear();Expired.Clear();pending=null;nextCleanup=0;}
    }
}
