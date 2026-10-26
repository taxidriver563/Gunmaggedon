using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class DP127Runtime
    {
        internal const string Key="Gunmaggedon_DP127",Weapon="DP127_Guided",Shell="Shell_127mm_guided";
        static Gun nativePresentationGun;static float nextPresentationLookup;
        static readonly List<DP127Controller> controllers=new List<DP127Controller>();
        static readonly FieldInfo Target=AccessTools.Field(typeof(Gun),"proximityFuseTarget");
        internal static bool Matches(Gun gun){return gun&&gun.info&&gun.info.name==Weapon&&gun.attachedUnit&&gun.attachedUnit.definition&&gun.attachedUnit.definition.jsonKey==Key;}
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(Turret),"Turret_OnInitialize"),postfix:new HarmonyMethod(typeof(DP127Runtime),nameof(Initialize)));
            h.Patch(AccessTools.Method(typeof(Turret),"FixedUpdate"),prefix:new HarmonyMethod(typeof(DP127Runtime),nameof(Native)));
            h.Patch(AccessTools.Method(typeof(Gun),"Fire"),prefix:new HarmonyMethod(typeof(DP127Runtime),nameof(Fire)));
            h.Patch(AccessTools.Method(typeof(Gun),"SpawnBullet"),prefix:new HarmonyMethod(typeof(DP127Runtime),nameof(Spawn)));
            h.Patch(AccessTools.Method(typeof(OpticalSeekerShell),"Initialize"),prefix:new HarmonyMethod(typeof(DP127Runtime),nameof(SeekerBefore)),postfix:new HarmonyMethod(typeof(DP127Runtime),nameof(SeekerAfter)));
        }
        static void Initialize(Turret __instance){var gun=__instance.GetComponentInChildren<Gun>(true);if(Matches(gun)&&!__instance.GetComponent<DP127Controller>())controllers.Add(__instance.gameObject.AddComponent<DP127Controller>());}
        static bool Native(Turret __instance){return !__instance.GetComponent<DP127Controller>();}
        static bool Fire(Gun __instance)
        {
            if(!Matches(__instance)||!__instance.attachedUnit.IsServer)return true;var c=__instance.GetComponentInParent<DP127Controller>();
            if(!c||!c.CanFire)return false;
            c.QueuedTarget=c.Selected;Target.SetValue(__instance,c.QueuedTarget);return true;
        }
        static bool Spawn(Gun __instance)
        {
            if(!Matches(__instance))return true;
            if(!__instance.attachedUnit.IsServer){
                // Native remote fire needs presentation, but its fallback would
                // also create a second ballistic visual beside the synced missile.
                AccessTools.Method(typeof(Gun),"ShotSound").Invoke(__instance,null);
                var energy=AccessTools.Field(typeof(Gun),"recoilEnergy");energy.SetValue(__instance,(float)energy.GetValue(__instance)+1);
                var particles=AccessTools.Field(typeof(Gun),"muzzleParticles").GetValue(__instance) as ParticleSystem[];
                if(particles!=null)foreach(var ps in particles)if(ps)ps.Play();return false;
            }
            if(!__instance.attachedUnit.LocalSim)return false;
            var c=__instance.GetComponentInParent<DP127Controller>();if(!c||!c.Eligible(c.QueuedTarget)||__instance.attachedUnit.Networkdisabled)return false;Target.SetValue(__instance,c.QueuedTarget);c.QueuedTarget=null;return true;
        }
        static bool Own(OpticalSeekerShell seeker)
        {
            var missile=seeker?seeker.GetComponent<Missile>():null;var source=missile?missile.owner:null;
            return missile&&missile.definition&&missile.definition.jsonKey==Shell&&source&&source.definition&&source.definition.jsonKey==Key;
        }
        internal static void ConfigureSeeker(OpticalSeekerShell seeker,Unit target)
        {
            if(!Own(seeker))return;
            bool air=target is Aircraft;
            AccessTools.Field(typeof(OpticalSeekerShell),"proximityFuse").SetValue(seeker,air);
            AccessTools.Field(typeof(OpticalSeekerShell),"maxTargetSpeed").SetValue(seeker,air?1200f:120f);
            AccessTools.Field(typeof(OpticalSeekerShell),"searchRadius").SetValue(seeker,air?250f:100f);
            AccessTools.Field(typeof(OpticalSeekerShell),"triggerMissileWarning").SetValue(seeker,true);
        }
        static void SeekerBefore(OpticalSeekerShell __instance,Unit __0){ConfigureSeeker(__instance,__0);}
        static void SeekerAfter(OpticalSeekerShell __instance,Unit __0)
        {
            if(!Own(__instance)||!__0)return;
            // Native Initialize otherwise starts at a sea-plane estimate if HQ
            // lacks a contact, even when this gun's own FCS just observed it.
            AccessTools.Field(typeof(OpticalSeekerShell),"targetUnit").SetValue(__instance,__0);
            var field=AccessTools.Field(typeof(OpticalSeekerShell),"targetTransform");
            var part=__0.GetComponentInChildren<UnitPart>();if(!(field.GetValue(__instance) as Transform))field.SetValue(__instance,part?part.transform:__0.transform);
            AccessTools.Field(typeof(OpticalSeekerShell),"knownPos").SetValue(__instance,__0.GlobalPosition());
            AccessTools.Field(typeof(OpticalSeekerShell),"knownVel").SetValue(__instance,__0.rb?__0.rb.velocity:Vector3.zero);
            AccessTools.Field(typeof(OpticalSeekerShell),"hasVisual").SetValue(__instance,true);
            // OpticalSeekerShell does not itself connect MissileSeeker's flag
            // to the native swept proxy fuse. Bind it explicitly for AA only.
            if(__0 is Aircraft)__instance.GetComponent<Missile>().SetProxyFuse(__0.transform,__0.rb);
        }
        internal static ParticleSystem[] MuzzleTemplates(){
            if(!nativePresentationGun){if(Time.unscaledTime<nextPresentationLookup)return null;nextPresentationLookup=Time.unscaledTime+1;
                // The native catalogue holds prefab references even when the
                // mission contains no frigates. Never depend on a live ship.
                var catalogue=Encyclopedia.i;
                if(catalogue&&catalogue.ships!=null)foreach(var definition in catalogue.ships){if(!definition||!definition.unitPrefab)continue;foreach(var candidate in definition.unitPrefab.GetComponentsInChildren<Gun>(true))if(candidate&&candidate.info&&candidate.info.name=="Gun127mm_Guided_Naval"){nativePresentationGun=candidate;break;}if(nativePresentationGun)break;}
                if(!nativePresentationGun)foreach(var candidate in Resources.FindObjectsOfTypeAll<Gun>())if(candidate&&candidate.info&&candidate.info.name=="Gun127mm_Guided_Naval"){nativePresentationGun=candidate;break;}}
            return nativePresentationGun?AccessTools.Field(typeof(Gun),"muzzleParticles").GetValue(nativePresentationGun) as ParticleSystem[]:null;
        }
        static void ClearPresentation(){nativePresentationGun=null;nextPresentationLookup=0;}
        internal static void Unregister(DP127Controller c){controllers.Remove(c);if(controllers.Count==0)ClearPresentation();}
        internal static void Shutdown(){foreach(var c in controllers.ToArray())if(c)RuntimePool<Transform>.Remove(c);controllers.Clear();ClearPresentation();}
    }
    public sealed class DP127Controller:MonoBehaviour
    {
        static readonly FieldInfo Muzzles=AccessTools.Field(typeof(Gun),"muzzles"),Disabled=AccessTools.Field(typeof(Turret),"disabled"),Stowed=AccessTools.Field(typeof(Turret),"stowed"),Loaded=AccessTools.Field(typeof(Gun),"bulletsLoaded"),Reload=AccessTools.Field(typeof(Gun),"timeUntilReload");
        readonly List<Unit> contacts=new List<Unit>();
        Turret turret;Gun gun;Unit owner,target;Transform pitch,muzzle,sensor;WeaponStation station;
        bool solution,remote,muzzleBound;float nextScan,nextPose,nextAim,yawRemote,pitchRemote,acquiredAt;int samples;double flight;
        Vector3 direction;GlobalPosition point;
        internal Unit Selected{get{return target;}}
        internal Unit QueuedTarget;
        bool Operational{get{return owner&&owner.LocalSim&&owner.IsServer&&owner.NetworkHQ&&!owner.Networkdisabled&&!(bool)Disabled.GetValue(turret)&&!(bool)Stowed.GetValue(turret);}}
        internal bool CanFire{get{return Operational&&solution&&samples>=3&&Time.time-acquiredAt>=1.25f&&Eligible(target)&&Visible(target)&&gun&&!gun.Safety&&(int)Loaded.GetValue(gun)>0&&(float)Reload.GetValue(gun)<=0&&Vector3.Angle(muzzle.forward,direction)<.35f;}}
        void Awake()
        {
            turret=GetComponent<Turret>();gun=GetComponentInChildren<Gun>(true);owner=gun?gun.attachedUnit:null;pitch=gun?gun.transform:null;
            var ms=gun?Muzzles.GetValue(gun) as Transform[]:null;muzzle=ms!=null&&ms.Length>0?ms[0]:null;
            sensor=owner?owner.transform.Find("FireControlSensor"):null;
            if(!owner||!muzzle||!sensor){Debug.LogError("[Gunmaggedon] DP-127 missing owner, muzzle or FireControlSensor; controller disabled.");enabled=false;return;}
            BindMuzzleEffects();
        }
        void BindMuzzleEffects()
        {
            var field=AccessTools.Field(typeof(Gun),"muzzleParticles");var templates=field.GetValue(gun) as ParticleSystem[];
            if(templates==null||templates.Length==0)templates=DP127Runtime.MuzzleTemplates();if(templates==null||templates.Length==0)return;var bank=new GameObject("DP127 runtime muzzle bank");bank.SetActive(false);bank.transform.SetParent(muzzle,false);var own=new List<ParticleSystem>(templates.Length);
            for(int i=0;i<templates.Length;i++)if(templates[i]){
                if(templates[i].transform.IsChildOf(muzzle)){own.Add(templates[i]);continue;}
                var fx=UnityEngine.Object.Instantiate(templates[i].gameObject,bank.transform,false);fx.name="DP127 runtime muzzle effect "+i;fx.transform.localPosition=Vector3.zero;fx.SetActive(true);
                var ps=fx.GetComponent<ParticleSystem>();if(ps){own.Add(ps);ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.playOnAwake=false;}
            }
            field.SetValue(gun,own.ToArray());bank.SetActive(true);muzzleBound=true;
        }
        internal bool Eligible(Unit unit){return unit&&unit!=owner&&!unit.Networkdisabled&&unit.gameObject.activeInHierarchy&&unit.NetworkHQ&&unit.NetworkHQ!=owner.NetworkHQ&&(unit is Aircraft||unit is Ship||unit is GroundVehicle||unit is Building);}
        internal bool Visible(Unit unit){return unit&&DP127Policy.InRange((unit.transform.position-muzzle.position).sqrMagnitude)&&unit.LineOfSight(sensor.position,100)&&!ShellObscurants.SmokeBlocks(sensor.position,unit.transform.position);}
        internal Unit Select(List<Unit> candidates)
        {
            Unit best=null;double score=double.NegativeInfinity;
            foreach(var u in candidates)if(Eligible(u)&&Visible(u)){
                float distance=Vector3.Distance(muzzle.position,u.transform.position);
                var velocity=u.rb?u.rb.velocity:Vector3.zero;float closing=-Vector3.Dot(velocity,(u.transform.position-muzzle.position).normalized);
                double value=DP127Policy.Score(u is Aircraft,u is Ship,distance,closing,u.definition?u.definition.value:0,u==target);
                if(value>score){score=value;best=u;}
            }return best;
        }
        void Reset(){solution=false;samples=0;acquiredAt=Time.time;flight=0;nextAim=0;}
        void FixedUpdate()
        {
            if(!owner)return;
            if(!muzzleBound)BindMuzzleEffects();
            if(!owner.LocalSim||!owner.IsServer){if(remote){transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,yawRemote,0),40*Time.fixedDeltaTime);pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(pitchRemote,0,0),35*Time.fixedDeltaTime);}return;}
            if(!Operational){target=null;contacts.Clear();QueuedTarget=null;Reset();return;}
            if(Time.time>=nextScan){nextScan=Time.time+.25f;SharedWorldQueries.Units(owner.GlobalPosition(),15020,contacts);var selected=Select(contacts);contacts.Clear();if(selected!=target){target=selected;Reset();}}
            if(!Eligible(target)||!Visible(target)){target=null;Reset();return;}
            if(Time.time>=nextAim){nextAim=Time.time+.1f;solution=false;
            var velocity=target.rb?target.rb.velocity:Vector3.zero;
            if(flight<=0)flight=Vector3.Distance(muzzle.position,target.transform.position)/gun.info.muzzleVelocity;
            var destination=target.transform.position+velocity*(float)flight;
            if(!(target is Aircraft))destination+=Vector3.up*(target.definition?Mathf.Max(1,target.definition.height*.35f):1);
            var relative=destination-muzzle.position;OpticalVector aim;double seconds;
            if(!FlakBarrageSolution.Solve(new OpticalVector(relative.x,relative.y,relative.z),gun.info.muzzleVelocity,gun.info.dragCoef,9.81*gun.info.gravMult,Time.fixedDeltaTime,out aim,out seconds,60)){samples=0;return;}
            flight=seconds;samples++;direction=new Vector3((float)aim.X,(float)aim.Y,(float)aim.Z);point=destination.ToGlobalPosition();solution=true;}
            if(!solution)return;
            var local=transform.parent.InverseTransformDirection(direction);float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,elevation=Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            if(elevation< -10||elevation>85){samples=0;return;}
            transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,yaw,0),40*Time.fixedDeltaTime);pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(-elevation,0,0),35*Time.fixedDeltaTime);solution=true;
            if(Time.time>=nextPose){nextPose=Time.time+.1f;F88FlakNetwork.Pose(owner,transform.localEulerAngles.y,pitch.localEulerAngles.x);}
            if(station==null){foreach(var candidate in turret.GetWeaponStations())if(candidate!=null){station=candidate;break;}if(station!=null&&!station.HasTurret())station.AssignTurret(turret);}
            if(station==null)return;station.AccountAmmo();station.AssessAmmo();
            if(CanFire&&station.Ammo>0)gun.Fire(owner,target,Vector3.zero,station,point);
        }
        internal void RemotePose(float yaw,float elevation){remote=true;yawRemote=yaw;pitchRemote=elevation;}
        void OnDisable(){target=null;contacts.Clear();QueuedTarget=null;Reset();}
        void OnDestroy(){DP127Runtime.Unregister(this);}
    }
}
