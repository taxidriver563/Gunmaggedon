using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace GunmageddonAudio
{
    // Native railgun flight, followed by the game's own 1.5kt shockwave.
    internal static class R230NuclearRuntime
    {
        internal const float YieldKilotons=1.5f,ProxyRadius=100f;
        static readonly Dictionary<BulletSim.Bullet,Flight> Flights=new Dictionary<BulletSim.Bullet,Flight>();
        static readonly List<GameObject> Effects=new List<GameObject>();
        static readonly System.Reflection.FieldInfo SimGun=AccessTools.Field(typeof(BulletSim),"gun"),Visual=AccessTools.Field(typeof(BulletSim),"visualOnly");
        static GameObject template;static float nextTemplate;
        sealed class Flight {internal bool Legacy;internal Unit Target;internal GlobalPosition Point;internal PersistentID Source;internal uint NetId;internal GlobalPosition Launch;internal bool Spent;}
        sealed class Pending {internal Pending Previous;internal Flight Flight;internal Unit Owner;internal WeaponInfo Info;}
        struct Step {internal Flight Flight;internal GlobalPosition Start;internal Vector3 Target,Velocity;}
        [ThreadStatic]static Pending pending;
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(BulletSim),"AddBullet"),prefix:new HarmonyMethod(typeof(R230NuclearRuntime),nameof(Add)),finalizer:new HarmonyMethod(typeof(R230NuclearRuntime),nameof(Added)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"TrajectoryTrace"),prefix:new HarmonyMethod(typeof(R230NuclearRuntime),nameof(Before)),postfix:new HarmonyMethod(typeof(R230NuclearRuntime),nameof(After)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"Remove"),prefix:new HarmonyMethod(typeof(R230NuclearRuntime),nameof(Remove)));
        }
        static void Add(BulletSim __instance,out Pending __state)
        {
            __state=null;var gun=SimGun.GetValue(__instance) as Gun;
            if(!R230Runtime.Nuclear(gun)||(bool)Visual.GetValue(__instance)||!gun.attachedUnit.LocalSim||!gun.attachedUnit.IsServer)return;
            OptimizationRuntime.EnsureMission();
            var controller=gun.GetComponentInParent<R230Controller>(true);GlobalPosition point=default(GlobalPosition);bool legacy=R230Runtime.Legacy(gun);
            if(!controller||!legacy&&!controller.TryCommittedPoint(out point))return;
            var id=gun.attachedUnit.GetComponent<Mirage.NetworkIdentity>();
            __state=new Pending{Previous=pending,Owner=gun.attachedUnit,Info=gun.info,Flight=new Flight{Legacy=legacy,Target=legacy?controller.Selected:null,Point=point,Source=gun.attachedUnit.persistentID,NetId=id?id.NetId:0,Launch=gun.transform.position.ToGlobalPosition()}};pending=__state;
        }
        static Exception Added(Exception __exception,Pending __state){if(__state!=null)pending=__state.Previous;return __exception;}
        static void Before(BulletSim.Bullet __instance,WeaponInfo info,Unit owner,out Step __state)
        {
            __state=default(Step);if(!R230Runtime.NuclearShot(info,owner))return;
            // All nuclear damage belongs to the native shockwave, never BlastFrag.
            __instance.explosive=false;__instance.proximityFuse=false;
            R230PressureRings.Begin(__instance,info,owner);
            __state.Start=__instance.position;
            Flight flight;if(!Flights.TryGetValue(__instance,out flight)&&pending!=null&&pending.Owner==owner&&pending.Info==info){flight=pending.Flight;Flights[__instance]=flight;}
            if(flight==null)return;
            __state=new Step{Flight=flight,Start=__instance.position,Target=flight.Target?flight.Target.transform.position:Vector3.zero,Velocity=flight.Target&&flight.Target.rb?flight.Target.rb.velocity:Vector3.zero};
        }
        static void After(BulletSim.Bullet __instance,float deltaTime,Step __state)
        {
            var flight=__state.Flight;if(flight==null||flight.Spent){R230PressureRings.Advance(__instance,__state.Start,deltaTime);return;}
            var start=__state.Start.ToLocalPosition();var end=__instance.position.ToLocalPosition();
            bool armed=(__instance.position-flight.Launch).sqrMagnitude>=1000f*1000f;
            bool burst=armed&&__instance.impacted;float fraction=1;
            if(armed&&!burst&&__instance.destructTimer>0&&(!flight.Legacy||flight.Target&&!flight.Target.Networkdisabled))
            {
                var relative=start-(flight.Legacy?__state.Target:flight.Point.ToLocalPosition());var change=end-start-(flight.Legacy?__state.Velocity*Mathf.Max(0,deltaTime):Vector3.zero);
                fraction=change.sqrMagnitude>0?Mathf.Clamp01(-Vector3.Dot(relative,change)/change.sqrMagnitude):0;
                burst=(relative+change*fraction).sqrMagnitude<=ProxyRadius*ProxyRadius;
            }
            if(!burst){R230PressureRings.Advance(__instance,__state.Start,deltaTime);if(!__instance.active)Flights.Remove(__instance);return;}
            flight.Spent=true;Flights.Remove(__instance);var point=Vector3.Lerp(start,end,fraction).ToGlobalPosition();
            // Native impact raycasts already bound end. A proxy cannot pass through
            // intervening terrain; it can only burst along the travelled segment.
            __instance.position=point;__instance.active=false;
            R230PressureRings.Advance(__instance,__state.Start,deltaTime*fraction);
            if(Play(point,flight.Source))F88FlakNetwork.NuclearBurst(point,flight.NetId);
        }
        static void Remove(BulletSim.Bullet __instance){Flights.Remove(__instance);R230PressureRings.Remove(__instance);}
        internal static bool ResolveTemplate()
        {
            if(template)return true;if(Time.unscaledTime<nextTemplate)return false;nextTemplate=Time.unscaledTime+1;
            // Resolve the canonical stock prefab even if that weapon has not
            // appeared in this mission; never substitute another nuclear effect.
            UnitDefinition definition;
            if(Encyclopedia.Lookup!=null&&Encyclopedia.Lookup.TryGetValue("nuclearBomb1",out definition)&&definition&&definition.unitPrefab&&BindTemplate(definition.unitPrefab.GetComponent<Missile>()))return true;
            foreach(var missile in Resources.FindObjectsOfTypeAll<Missile>())
                if(BindTemplate(missile))return true;
            return false;
        }
        static bool BindTemplate(Missile missile)
        {
            if(!missile||!missile.definition||missile.definition.jsonKey!="nuclearBomb1"||!Mathf.Approximately((float)AccessTools.Field(typeof(Missile),"blastYield").GetValue(missile),YieldKilotons*1000000f))return false;
            var warhead=AccessTools.Field(typeof(Missile),"warhead").GetValue(missile);if(warhead==null)return false;
            var effect=AccessTools.Field(warhead.GetType(),"airEffect").GetValue(warhead) as GameObject;
            if(!effect||!effect.GetComponentInChildren<Shockwave>(true)||!effect.GetComponentInChildren<MushroomCloud>(true))return false;
            template=effect;return true;
        }
        internal static bool Play(GlobalPosition point,PersistentID source)
        {
            if(!Datum.origin||!ResolveTemplate()){Debug.LogWarning("[Gunmaggedon] R230N native nuclear airburst template unavailable; burst skipped.");return false;}
            var go=UnityEngine.Object.Instantiate(template,point.ToLocalPosition(),Quaternion.identity,Datum.origin);
            go.name="R230N 1.5kt airburst";
            foreach(var shock in go.GetComponentsInChildren<Shockwave>(true))shock.SetOwner(source,YieldKilotons);
            foreach(var cloud in go.GetComponentsInChildren<MushroomCloud>(true))AccessTools.Field(typeof(MushroomCloud),"yield").SetValue(cloud,YieldKilotons*1000000f);
            Effects.RemoveAll(e=>!e);Effects.Add(go);return true;
        }
        internal static void Shutdown(){R230PressureRings.Clear();Flights.Clear();pending=null;foreach(var effect in Effects)if(effect){if(Application.isPlaying)UnityEngine.Object.Destroy(effect);else UnityEngine.Object.DestroyImmediate(effect);}Effects.Clear();template=null;nextTemplate=0;}
    }
}
