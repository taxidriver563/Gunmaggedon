using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class F88FlakRuntime
    {
        internal const string Key="Gunmaggedon_F88_AA",Weapon="F88_AA_Weapon";
        internal const string HeavyKey="Gunmaggedon_F120_AA",HeavyWeapon="F120_AA_Weapon";
        internal static readonly List<F88FlakController> Controllers=new List<F88FlakController>();
        static readonly Dictionary<Gun,F88FlakController> Guns=new Dictionary<Gun,F88FlakController>();
        static readonly Dictionary<BulletSim.Bullet,Flight> Flights=new Dictionary<BulletSim.Bullet,Flight>();
        static readonly FieldInfo SimGun=AccessTools.Field(typeof(BulletSim),"gun"),SimVisual=AccessTools.Field(typeof(BulletSim),"visualOnly");
        sealed class Flight {internal bool Authority,Heavy;internal PersistentID Source;internal float Blast;}
        sealed class Pending {internal Pending Previous;internal Flight Flight;internal Unit Owner;internal WeaponInfo Info;}
        [ThreadStatic]static Pending pending;
        internal static GameObject EffectPrefab;
        internal static GameObject HeavyEffectPrefab;
        internal static bool Heavy(Gun g){return g&&g.info&&g.info.name==HeavyWeapon;}
        internal static bool Matches(Gun g){return g&&g.info&&g.attachedUnit&&g.attachedUnit.definition&&
            (g.info.name==Weapon&&g.attachedUnit.definition.jsonKey==Key||g.info.name==HeavyWeapon&&g.attachedUnit.definition.jsonKey==HeavyKey);}
        internal static void Install(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(Turret),"FixedUpdate"),prefix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(NativeTurret)));
            h.Patch(AccessTools.Method(typeof(Gun),"Fire"),prefix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(AllowFire)));
            h.Patch(AccessTools.Method(typeof(Gun),"SpawnBullet"),postfix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(Fired)));
            h.Patch(AccessTools.Method(typeof(BulletSim),"AddBullet"),prefix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(Add)),finalizer:new HarmonyMethod(typeof(F88FlakRuntime),nameof(Added)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"TrajectoryTrace"),prefix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(TraceStart)),postfix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(TraceEnd)));
            h.Patch(AccessTools.Method(typeof(BulletSim.Bullet),"Remove"),prefix:new HarmonyMethod(typeof(F88FlakRuntime),nameof(Remove)));
            F88FlakNetwork.Install();
        }
        internal static void Register(F88FlakController c,Gun gun){Controllers.Add(c);Guns[gun]=c;var effect=AccessTools.Field(typeof(Gun),"selfDestructEffect").GetValue(gun) as GameObject;if(Heavy(gun))HeavyEffectPrefab=effect;else EffectPrefab=effect;}
        internal static void Unregister(F88FlakController c,Gun gun){Controllers.Remove(c);if(!ReferenceEquals(gun,null))Guns.Remove(gun);}
        static bool NativeTurret(Turret __instance){return !__instance.GetComponent<F88FlakController>();}
        static bool AllowFire(Gun __instance)
        {if(!Matches(__instance)||!__instance.attachedUnit.LocalSim)return true;F88FlakController c;return F88Runtime.MechanicallyReady(__instance)&&Flights.Count<512&&Guns.TryGetValue(__instance,out c)&&c.ReadyToFire;}
        static void Fired(Gun __instance){F88Runtime.Shot(__instance);F88FlakController c;if(Guns.TryGetValue(__instance,out c)&&__instance.attachedUnit.LocalSim)c.ShotFired();}
        static void Add(BulletSim __instance,ref float __2,ref float __3,ref bool __4,ref Unit __8,out Pending __state)
        {
            __state=null;var gun=SimGun.GetValue(__instance) as Gun;if(!Matches(gun))return;
            __2=(float)(Heavy(gun)?FlakBarrageSolution.AdvancedSpreadDegrees:FlakBarrageSolution.GunSpreadDegrees);__8=null;__4=false;
            bool visual=(bool)SimVisual.GetValue(__instance);F88FlakController c;
            if(!visual&&gun.attachedUnit.LocalSim&&Guns.TryGetValue(gun,out c))__3=c.Fuse;
            __state=new Pending{Previous=pending,Owner=gun.attachedUnit,Info=gun.info,
                Flight=new Flight{Authority=!visual&&gun.attachedUnit.LocalSim,Source=gun.attachedUnit.persistentID,Blast=gun.info.blastDamage,Heavy=Heavy(gun)}};
            pending=__state;
        }
        static Exception Added(Exception __exception,Pending __state){if(__state!=null)pending=__state.Previous;return __exception;}
        static void TraceStart(BulletSim.Bullet __instance,WeaponInfo info,Unit owner)
        {
            if(!info||(info.name!=Weapon&&info.name!=HeavyWeapon))return;
            __instance.proximityFuse=false;__instance.target=null;__instance.explosive=false;
            if(pending!=null&&pending.Info==info&&pending.Owner==owner&&!Flights.ContainsKey(__instance))Flights[__instance]=pending.Flight;
        }
        static void TraceEnd(BulletSim.Bullet __instance)
        {
            Flight f;if(!Flights.TryGetValue(__instance,out f)||__instance.active)return;
            Flights.Remove(__instance);
            if(!f.Authority||__instance.impacted||__instance.destructTimer>0)return;
            BurstProfile(__instance.position,f.Source,f.Blast,f.Heavy);
        }
        internal static void Burst(GlobalPosition position,PersistentID source,float blast)
        {BurstProfile(position,source,blast,false);}
        internal static void BurstProfile(GlobalPosition position,PersistentID source,float blast,bool heavy)
        {
            // Native timer expiry has no damage, so emit fragmentation once here.
            DamageEffects.BlastFrag(blast,position.ToLocalPosition(),source,PersistentID.None);
            if(heavy)F88Fragments.HeavyBurst(position.ToLocalPosition(),source);else F88Fragments.Burst(position.ToLocalPosition(),source);
            F88FlakEffects.Play(heavy?HeavyEffectPrefab:EffectPrefab,position);
            F88FlakNetwork.BurstProfile(position,heavy);
        }
        static void Remove(BulletSim.Bullet __instance){Flights.Remove(__instance);}
        internal static void Shutdown()
        {
            foreach(var c in Controllers.ToArray())if(c)UnityEngine.Object.Destroy(c);
            Controllers.Clear();Guns.Clear();Flights.Clear();pending=null;EffectPrefab=HeavyEffectPrefab=null;F88FlakNetwork.Shutdown();F88FlakEffects.Shutdown();F88Fragments.Clear();
        }
    }

    public sealed class F88FlakController : MonoBehaviour
    {
        static readonly FieldInfo KnownUnit=AccessTools.Field(typeof(TrackingInfo),"unit"),Muzzles=AccessTools.Field(typeof(Gun),"muzzles"),TurretDisabled=AccessTools.Field(typeof(Turret),"disabled"),Stowed=AccessTools.Field(typeof(Turret),"stowed"),Reload=AccessTools.Field(typeof(Gun),"timeUntilReload");
        readonly List<TrackingInfo> contacts=new List<TrackingInfo>();readonly List<Radar> radars=new List<Radar>();
        Turret turret;Gun gun;Unit owner;WeaponStation station;Transform pitch,muzzle;Unit target;
        FlakBarrageSolution track;FlakBarrageArea area;
        Vector3 direction;GlobalPosition point;float nextContacts,nextRadar,nextObservation,nextPose;bool planned;int shots;
        float fuse,remoteYaw,remotePitch,nextAim;double flightEstimate;int aimSamples;bool remotePose;OpticalVector shotOffset;double fuseError;
        internal bool Advanced {get{return F88FlakRuntime.Heavy(gun);}}
        float MaximumRange {get{return Advanced?15000:12000;}}
        float Commitment {get{return (float)(Advanced?FlakBarrageSolution.AdvancedCommitSeconds:FlakBarrageSolution.CommitSeconds);}}
        internal bool ReadyToFire {get{return planned&&aimSamples>=3&&Operational&&track!=null&&track.CanContinue(Time.time);}}
        internal float Fuse {get{return fuse;}}
        bool Operational {get{return owner&&owner.LocalSim&&!owner.Networkdisabled&&owner.gameObject.activeInHierarchy&&!((bool)TurretDisabled.GetValue(turret))&&!((bool)Stowed.GetValue(turret))&&(!owner.rb||owner.rb.velocity.sqrMagnitude<4);}}
        void Awake()
        {
            turret=GetComponent<Turret>();gun=GetComponentInChildren<Gun>(true);owner=gun?gun.attachedUnit:null;pitch=gun?gun.transform:null;
            var ms=gun?Muzzles.GetValue(gun) as Transform[]:null;muzzle=ms!=null&&ms.Length>0?ms[0]:null;
            if(!owner||!muzzle){enabled=false;return;}
            F88FlakRuntime.Register(this,gun);
        }
        internal static bool RadarSupports(Unit source,Radar radar)
        {
            var unit=radar?radar.GetAttachedUnit():null;
            return unit&&source&&source.NetworkHQ&&unit.NetworkHQ==source.NetworkHQ&&!unit.Networkdisabled&&
                (unit.transform.position-source.transform.position).sqrMagnitude<=90000&&radar.IsOperational()&&radar.activated&&!radar.IsJammed();
        }
        bool RadarContact(Unit aircraft){foreach(var r in radars)if(RadarSupports(owner,r)&&r.detectedTargets.Contains(aircraft))return true;return false;}
        bool OpticalContact(Unit aircraft)
        {
            if(!aircraft||Vector3.Distance(muzzle.position,aircraft.transform.position)>(Advanced?10000:8000))return false;
            if(CoastalIllumination.IsLowLight(LevelInfo.i,muzzle.position,.15f)&&!SearchlightRuntime.IlluminatesAircraft(owner.NetworkHQ,aircraft))return false;
            return !ShellObscurants.SmokeBlocks(muzzle.position,aircraft.transform.position)&&aircraft.LineOfSight(muzzle.position,10f);
        }
        bool Eligible(Unit u){return u is Aircraft&&u&&!u.Networkdisabled&&u.gameObject.activeInHierarchy&&u.NetworkHQ&&u.NetworkHQ!=owner.NetworkHQ;}
        void FindRadar()
        {
            SharedWorldQueries.Radars(owner.GlobalPosition(),300,radars);
            for(int i=radars.Count-1;i>=0;i--)if(!RadarSupports(owner,radars[i]))radars.RemoveAt(i);
        }
        void ChooseContact(float now)
        {
            // Candidate identity comes from faction-known contacts, never a world
            // aircraft scan. Sight/radar evidence is checked separately.
            SharedWorldQueries.KnownContacts(owner.NetworkHQ,owner.transform,MaximumRange,contacts);
            Unit best=null;float distance=float.MaxValue;
            foreach(var contact in contacts) {
                var u=KnownUnit.GetValue(contact) as Unit;if(!Eligible(u))continue;
                GlobalPosition known;if(!owner.NetworkHQ.TryGetKnownPosition(u,out known))continue;
                var p=known.ToLocalPosition();float d=(p-muzzle.position).sqrMagnitude;
                if(p.y<Datum.LocalSeaY+(Advanced?250:350)||p.y>Datum.LocalSeaY+(Advanced?12000:9000)||d<(Advanced?600*600:800*800)||d>MaximumRange*MaximumRange||(!RadarContact(u)&&!OpticalContact(u)))continue;
                if(u==target){best=u;break;}if(d<distance){distance=d;best=u;}
            }
            contacts.Clear();
            if(best!=target&&(!target||track==null||!track.CanContinue(now))) {
                target=best;planned=false;shots=0;area=default(FlakBarrageArea);nextObservation=0;
                track=target?new FlakBarrageSolution(GetInstanceID()^target.GetInstanceID(),Advanced):null;
            }
        }
        void Observe(float now)
        {
            if(!Eligible(target)||track==null)return;
            bool radar=RadarContact(target);GlobalPosition known;
            if(!radar&&!OpticalContact(target))return;
            if(!owner.NetworkHQ.TryGetKnownPosition(target,out known))return;
            Vector3 observed=radar?known.ToLocalPosition():target.transform.position;
            track.Observe(V(observed.ToGlobalPosition()),now,Vector3.Distance(muzzle.position,observed),radar);
            nextObservation=now+(Advanced?(radar?.2f:.5f):(radar?.35f:1.5f));
        }
        static OpticalVector V(GlobalPosition p){return new OpticalVector(p.x,p.y,p.z);}
        static Vector3 V(OpticalVector p){return new GlobalPosition((float)p.X,(float)p.Y,(float)p.Z).ToLocalPosition();}
        void Prepare(float now)
        {
            if(area.Started==0||shots>=(Advanced?1:3)||now-area.Started>=Commitment) {
                area=track.Commit(now);shots=0;
                foreach(var other in F88FlakRuntime.Controllers)if(!Advanced&&other&&other!=this&&other.Advanced==Advanced&&other.owner&&other.owner.NetworkHQ==owner.NetworkHQ&&other.target==target&&other.area.Started>0&&now-other.area.Started<Commitment&&
                    (other.transform.position-transform.position).sqrMagnitude<=1500*1500){area=other.area;break;}
            }
            int index=0,count=1;foreach(var other in F88FlakRuntime.Controllers)if(other&&other!=this&&other.Advanced==Advanced&&other.owner&&other.owner.NetworkHQ==owner.NetworkHQ&&other.target==target&&
                (other.transform.position-transform.position).sqrMagnitude<=1500*1500){count++;if(other.GetInstanceID()<GetInstanceID())index++;}
            var sample=track.Plan(area,now,0,index,count);
            shotOffset=sample.Point-(area.Position+area.Velocity*Math.Max(0,now-area.SampleAt));fuseError=sample.FuseError;
            planned=true;nextAim=0;aimSamples=0;flightEstimate=Vector3.Distance(V(area.Position),muzzle.position)/gun.info.muzzleVelocity;
        }
        void SolveAim(float now)
        {
            double seconds=flightEstimate;
            var prediction=Advanced?track.Commit(now):area;
            OpticalVector vector=new OpticalVector();Vector3 destination=Vector3.zero;
            for(int i=0;i<1;i++) {
                destination=V(prediction.Position+prediction.Velocity*(Math.Max(0,now-prediction.SampleAt)+seconds)+shotOffset);
                if(destination.y<Datum.LocalSeaY+(Advanced?200:300)){planned=false;return;}
                var relative=destination-muzzle.position;
                if(!FlakBarrageSolution.Solve(new OpticalVector(relative.x,relative.y,relative.z),gun.info.muzzleVelocity,gun.info.dragCoef,9.81*gun.info.gravMult,Time.fixedDeltaTime,out vector,out seconds)){planned=false;return;}
            }
            flightEstimate=seconds;aimSamples++;
            direction=new Vector3((float)vector.X,(float)vector.Y,(float)vector.Z);point=destination.ToGlobalPosition();fuse=Mathf.Clamp((float)(seconds+fuseError),.5f,24f);
        }
        internal WeaponStation ReadyStation()
        {
            if(station==null) {
                var stations=turret.GetWeaponStations();
                if(stations!=null)foreach(var candidate in stations)if(candidate!=null){station=candidate;break;}
                if(station!=null&&!station.HasTurret())station.AssignTurret(turret);
            }
            if(station!=null){station.AccountAmmo();station.AssessAmmo();}return station;
        }
        void FixedUpdate()
        {
            if(!owner)return;
            if(!owner.LocalSim){if(remotePose){transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,remoteYaw,0),(Advanced?20:16)*Time.fixedDeltaTime);pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(remotePitch,0,0),(Advanced?16:12)*Time.fixedDeltaTime);}return;}
            if(!Operational||!owner.NetworkHQ){planned=false;return;}
            float now=Time.time;
            if(now>=nextRadar){nextRadar=now+1;FindRadar();}
            if(now>=nextContacts){nextContacts=now+.5f;ChooseContact(now);}
            if(now>=nextObservation){nextObservation=now+(Advanced?.2f:.35f);Observe(now);}
            if(track==null||!track.CanContinue(now)||!Eligible(target)){planned=false;return;}
            if(planned&&now-area.Started>=Commitment)planned=false;
            if(!planned) {
                if(now<nextAim||(float)Reload.GetValue(gun)>(Advanced?1.8f:1.55f))return;
                Prepare(now);
            }
            if(now>=nextAim){nextAim=now+(Advanced?.25f:.5f);SolveAim(now);}if(!planned)return;
            var v=transform.parent.InverseTransformDirection(direction);float yaw=Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg;
            float elevation=Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg;
            if(elevation< -5||elevation>85){planned=false;return;}
            transform.localRotation=Quaternion.RotateTowards(transform.localRotation,Quaternion.Euler(0,yaw,0),(Advanced?20:16)*Time.fixedDeltaTime);
            pitch.localRotation=Quaternion.RotateTowards(pitch.localRotation,Quaternion.Euler(-elevation,0,0),(Advanced?16:12)*Time.fixedDeltaTime);
            if(now>=nextPose){nextPose=now+.2f;F88FlakNetwork.Pose(owner,transform.localEulerAngles.y,pitch.localEulerAngles.x);}
            if(!ReadyToFire||Vector3.Angle(muzzle.forward,direction)>(Advanced?.2f:1f))return;
            var station=ReadyStation();if(station==null||station.Ammo<=0)return;
            gun.Fire(owner,null,Vector3.zero,station,point);
        }
        internal void ShotFired(){planned=false;shots++;}
        internal void RemotePose(float yaw,float pitchAngle){remoteYaw=yaw;remotePitch=pitchAngle;remotePose=true;}
        void OnDestroy(){F88FlakRuntime.Unregister(this,gun);}
    }
}
