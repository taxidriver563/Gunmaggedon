using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class R230Runtime
    {
        internal const string Key="Gunmaggedon_R230",Weapon="R230_Railgun";
        internal const string NuclearKey="Gunmaggedon_R230N",NuclearWeapon="R230N_Railgun";
        static readonly List<R230Controller> Controllers=new List<R230Controller>();
        internal static bool Nuclear(Gun g){return g&&g.info&&g.info.name==NuclearWeapon&&g.attachedUnit&&g.attachedUnit.definition&&g.attachedUnit.definition.jsonKey==NuclearKey;}
        internal static bool Matches(Gun g){return Nuclear(g)||g&&g.info&&g.info.name==Weapon&&g.attachedUnit&&g.attachedUnit.definition&&g.attachedUnit.definition.jsonKey==Key;}
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(Turret),"Turret_OnInitialize"),postfix:new HarmonyMethod(typeof(R230Runtime),nameof(Initialize)));
            h.Patch(AccessTools.Method(typeof(Turret),"FixedUpdate"),prefix:new HarmonyMethod(typeof(R230Runtime),nameof(Native)));
            h.Patch(AccessTools.Method(typeof(Gun),"Fire"),prefix:new HarmonyMethod(typeof(R230Runtime),nameof(Fire)));
            h.Patch(AccessTools.Method(typeof(Gun),"SpawnBullet"),postfix:new HarmonyMethod(typeof(R230Runtime),nameof(Fired)));
            R230NuclearRuntime.Install(h);
        }
        static void Initialize(Turret __instance){var g=__instance.GetComponentInChildren<Gun>(true);if(Matches(g)&&!__instance.GetComponent<R230Controller>())Controllers.Add(__instance.gameObject.AddComponent<R230Controller>());}
        static bool Native(Turret __instance){return !__instance.GetComponent<R230Controller>();}
        internal static void DisableRecoil(Gun gun)
        {
            var field=AccessTools.Field(typeof(Gun),"recoilTransform");var pivot=field.GetValue(gun) as Transform;
            if(pivot&&pivot.name=="RecoilPivot")pivot.localPosition=Vector3.zero;
            field.SetValue(gun,null);AccessTools.Field(typeof(Gun),"recoilTravel").SetValue(gun,0f);AccessTools.Field(typeof(Gun),"recoilImpulse").SetValue(gun,0f);AccessTools.Field(typeof(Gun),"recoilEnergy").SetValue(gun,0f);
        }
        static bool Fire(Gun __instance){if(!Matches(__instance)||!__instance.attachedUnit.LocalSim)return true;var c=__instance.GetComponentInParent<R230Controller>();return c&&c.CanFire;}
        static void Fired(Gun __instance){if(Matches(__instance)&&__instance.attachedUnit.LocalSim&&__instance.attachedUnit.IsServer){var c=__instance.GetComponentInParent<R230Controller>();if(c)c.ShotFired();}}
        internal static void Unregister(R230Controller c){Controllers.Remove(c);}
        internal static void Shutdown(){foreach(var c in Controllers.ToArray())if(c){if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);}Controllers.Clear();R230NuclearRuntime.Shutdown();}
    }
    public sealed class R230Controller:MonoBehaviour
    {
        static readonly FieldInfo Muzzles=AccessTools.Field(typeof(Gun),"muzzles"),Disabled=AccessTools.Field(typeof(Turret),"disabled"),Stowed=AccessTools.Field(typeof(Turret),"stowed");
        readonly List<Unit> candidates=new List<Unit>();
        readonly R230Acquisition acquisition=new R230Acquisition();
        readonly R230Capacitor capacitor=new R230Capacitor();R230Warning warning;
        static readonly FieldInfo Loaded=AccessTools.Field(typeof(Gun),"bulletsLoaded"),Reload=AccessTools.Field(typeof(Gun),"timeUntilReload");
        bool warned;float nextWarning;
        Turret turret;Gun gun;Unit owner,target;Transform pitch,muzzle;WeaponStation station;
        float nextScan,nextPose,remoteYaw,remotePitch;bool remote,solution;Vector3 direction;GlobalPosition aimPoint;
        Unit motionTarget;Vector3 previousVelocity,targetAcceleration;float motionAt;
        internal Unit Selected {get{return target;}}
        bool Aligned {get{return Vector3.Dot(muzzle.forward,direction)>0&&Vector3.Cross(muzzle.forward,direction).sqrMagnitude<4.87e-9f;}}
        internal bool CanFire {get{return solution&&acquisition.Ready&&capacitor.Ready(Time.time)&&NativeReady&&Operational&&Eligible(target)&&Visible(target)&&Aligned&&(!R230Runtime.Nuclear(gun)||R230NuclearRuntime.ResolveTemplate());}}
        // Native Gun.Fire enables an idle gun; enabled is not a readiness gate.
        bool NativeReady {get{return gun&&!gun.Safety&&(int)Loaded.GetValue(gun)>0&&(float)Reload.GetValue(gun)<=0;}}
        bool WarningReady {get{if(!gun||gun.Safety)return false;float reload=(float)Reload.GetValue(gun);return NativeReady||reload>0&&reload<=R230Capacitor.ChargeSeconds;}}
        void Cue(bool active)
        {
            if(active){if(!warning){warning=gameObject.AddComponent<R230Warning>();warning.Initialize(muzzle);warning.SetNuclear(R230Runtime.Nuclear(gun));}warning.Show(aimPoint,(float)capacitor.Progress(Time.time));}
            else if(warning)warning.Hide();
            if(owner&&owner.LocalSim&&owner.IsServer&&(active!=warned||active&&Time.time>=nextWarning)){warned=active;nextWarning=Time.time+.1f;F88FlakNetwork.Warning(owner,aimPoint,(float)capacitor.Progress(Time.time),active);}
        }
        void CancelCharge(){capacitor.Cancel();Cue(false);}
        internal void ShotFired(){capacitor.Shot(Time.time);Cue(false);}
        internal void RemoteWarning(GlobalPosition point,float progress,bool active){if(owner&&owner.LocalSim)return;if(active){if(!warning){warning=gameObject.AddComponent<R230Warning>();warning.Initialize(muzzle);warning.SetNuclear(R230Runtime.Nuclear(gun));}warning.Show(point,progress);}else if(warning)warning.Hide();}
        bool Operational {get{return owner&&owner.LocalSim&&owner.IsServer&&!owner.Networkdisabled&&owner.NetworkHQ&&!((bool)Disabled.GetValue(turret))&&!((bool)Stowed.GetValue(turret));}}
        void Awake(){turret=GetComponent<Turret>();gun=GetComponentInChildren<Gun>(true);owner=gun?gun.attachedUnit:null;pitch=gun?gun.transform:null;var ms=gun?Muzzles.GetValue(gun) as Transform[]:null;muzzle=ms!=null&&ms.Length>0?ms[0]:null;if(!owner||!muzzle){enabled=false;return;}AccessTools.Field(typeof(Gun),"tracerSize").SetValue(gun,R230Solution.TracerSize);AccessTools.Field(typeof(Turret),"traverseRate").SetValue(turret,R230Solution.TraverseRate);R230Runtime.DisableRecoil(gun);}
        bool Eligible(Unit u){return u&&u!=owner&&!u.Networkdisabled&&u.gameObject.activeInHierarchy&&u.definition&&(u is Aircraft||u is Missile&&R230Solution.Piledriver(u.definition.jsonKey))&&u.NetworkHQ&&u.NetworkHQ!=owner.NetworkHQ;}
        bool Visible(Unit u){return u&&R230Solution.InRangeSquared((u.transform.position-muzzle.position).sqrMagnitude)&&u.LineOfSight(muzzle.position,(float)R230Solution.OpticalMagnification)&&!ShellObscurants.SmokeBlocks(muzzle.position,u.transform.position);}
        static Vector3 Velocity(Unit u){return u.rb?u.rb.velocity:Vector3.zero;}
        Vector3 Acceleration(Unit u,Vector3 velocity,float now)
        {
            if(!(u is Missile)||!R230Solution.Piledriver(u.definition.jsonKey)){motionTarget=null;return Vector3.zero;}
            if(motionTarget!=u){motionTarget=u;previousVelocity=velocity;motionAt=now;targetAcceleration=u.rb&&u.rb.useGravity?Physics.gravity:Vector3.zero;return targetAcceleration;}
            float dt=now-motionAt;
            if(dt>0&&dt<=.5f){var measured=Vector3.ClampMagnitude((velocity-previousVelocity)/dt,150f);targetAcceleration=Vector3.Lerp(targetAcceleration,measured,1-Mathf.Exp(-dt/.08f));}
            else if(dt>.5f)targetAcceleration=u.rb&&u.rb.useGravity?Physics.gravity:Vector3.zero;
            if(dt>0){previousVelocity=velocity;motionAt=now;}return targetAcceleration;
        }
        internal Unit Select(List<Unit> units)
        {
            Unit best=null;double highest=double.NegativeInfinity;
            foreach(var u in units){
                if(!Eligible(u))continue;var delta=u.transform.position-muzzle.position;float distance=delta.magnitude;if(distance>65000||delta.y/Mathf.Max(1,distance)<-.0872f||!Visible(u))continue;
                var velocity=Velocity(u);var d=u.definition;float closing=-Vector3.Dot(velocity,delta.normalized);
                int tier=R230Solution.EngagementTier(d.jsonKey,u is Aircraft,d.typeIdentity.strategic>0,u.transform.position.y-Datum.LocalSeaY,velocity.y,closing,(delta+velocity*(distance/gun.info.muzzleVelocity)).magnitude);
                double score=R230Solution.Score(tier,d.value,d.roleIdentity.antiSurface,distance,closing,u==target);
                if(score>highest||(score==highest&&best&&u.GetInstanceID()<best.GetInstanceID())){best=u;highest=score;}
            }
            return best;
        }
        void FixedUpdate()
        {
            if(!owner)return;
            if(!owner.LocalSim){if(remote){transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,remoteYaw,0),R230Solution.TraverseRate*Time.fixedDeltaTime);pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(remotePitch,0,0),30*Time.fixedDeltaTime);}return;}
            solution=false;if(!Operational){acquisition.Reset();CancelCharge();return;}
            if(Time.time>=nextScan){nextScan=Time.time+.2f;SharedWorldQueries.Units(owner.GlobalPosition(),65020,candidates);var selected=Select(candidates);if(selected!=target){acquisition.Reset();CancelCharge();}target=selected;candidates.Clear();}
            if(!Eligible(target)||!Visible(target)){target=null;motionTarget=null;acquisition.Reset();CancelCharge();return;}
            var p=target.transform.position-muzzle.position;var v=Velocity(target);OpticalVector aim;double flight;
            var acceleration=Acceleration(target,v,Time.fixedTime);
            if(!R230Solution.InterceptAccelerated(new OpticalVector(p.x,p.y,p.z),new OpticalVector(v.x,v.y,v.z),new OpticalVector(acceleration.x,acceleration.y,acceleration.z),gun.info.muzzleVelocity,9.81*gun.info.gravMult,Time.fixedDeltaTime,out aim,out flight)){acquisition.Reset();CancelCharge();return;}
            direction=new Vector3((float)aim.X,(float)aim.Y,(float)aim.Z);aimPoint=(muzzle.position+direction*(float)(flight*gun.info.muzzleVelocity)).ToGlobalPosition();
            var local=transform.parent.InverseTransformDirection(direction);float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,elevation=Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            if(elevation< -5||elevation>90){acquisition.Reset();CancelCharge();return;}
            transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,yaw,0),R230Solution.TraverseRate*Time.fixedDeltaTime);pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(-elevation,0,0),30*Time.fixedDeltaTime);solution=true;
            acquisition.Track(target.GetInstanceID(),Aligned,Time.time);
            if(Time.time>=nextPose){nextPose=Time.time+.1f;F88FlakNetwork.Pose(owner,transform.localEulerAngles.y,pitch.localEulerAngles.x);}
            if(station==null){foreach(var s in turret.GetWeaponStations())if(s!=null){station=s;break;}if(station!=null&&!station.HasTurret())station.AssignTurret(turret);}
            if(station==null){CancelCharge();return;}station.AccountAmmo();station.AssessAmmo();
            capacitor.Tick(target.GetInstanceID(),acquisition.Ready&&Aligned&&WarningReady&&station.Ammo>0,Time.time);Cue(capacitor.Charging);
            if(CanFire&&station.Ammo>0)gun.Fire(owner,target,v,station,aimPoint);
        }
        internal void RemotePose(float yaw,float pitchAngle){remote=true;remoteYaw=yaw;remotePitch=pitchAngle;}
        void OnDisable(){CancelCharge();}
        void OnDestroy(){CancelCharge();if(warning){if(Application.isPlaying)Object.Destroy(warning);else Object.DestroyImmediate(warning);}R230Runtime.Unregister(this);}
    }
}
