using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
namespace GunmageddonAudio
{
    internal static class L25Runtime
    {
        static Harmony harmony;
        static readonly Dictionary<Gun,L25TurretMotion> Guns=new Dictionary<Gun,L25TurretMotion>();
        internal static void Install(ManualLogSource logger)
        {
            harmony=new Harmony("gunmaggedon.l25.presentation");
            try{
                harmony.Patch(AccessTools.Method(typeof(Turret),"Turret_OnInitialize"),postfix:new HarmonyMethod(typeof(L25Runtime),nameof(Initialize)));
                harmony.Patch(AccessTools.Method(typeof(Gun),"Fire"),prefix:new HarmonyMethod(typeof(L25Runtime),nameof(AllowFire)));
                harmony.Patch(AccessTools.Method(typeof(Gun),"FixedUpdate"),prefix:new HarmonyMethod(typeof(L25Runtime),nameof(BeforeGun)));
                harmony.Patch(AccessTools.Method(typeof(Turret),"FixedUpdate"),prefix:new HarmonyMethod(typeof(L25Runtime),nameof(SelectStation)));
                L25Tracking.Install(harmony);
                harmony.Patch(AccessTools.Method(typeof(Gun),"SpawnBullet"),prefix:new HarmonyMethod(typeof(L25Runtime),nameof(Select)),postfix:new HarmonyMethod(typeof(L25Runtime),nameof(Fired)));
                logger.LogInfo("L25 sequential left/middle/right barrels and independent recoil enabled.");
            }catch(System.Exception e){harmony.UnpatchSelf();logger.LogError("L25 initialization failed: "+e);}
        }
        static void Initialize(Turret __instance)
        {
            if(!__instance||__instance.name!="L25YawPivot"||!__instance.transform.Find("PitchPivot/BarrelLeft/Muzzle")||__instance.GetComponent<L25TurretMotion>())return;
            __instance.gameObject.AddComponent<L25TurretMotion>();
        }
        internal static void Register(Gun gun,L25TurretMotion motion){Guns[gun]=motion;}
        internal static void Unregister(Gun gun){if(!ReferenceEquals(gun,null))Guns.Remove(gun);}
        static void SelectStation(Turret __instance){if(__instance.name!="L25YawPivot")return;var motion=__instance.GetComponent<L25TurretMotion>();if(motion)motion.SelectStation(__instance);}
        static bool AllowFire(Gun __instance){L25TurretMotion motion;return !Guns.TryGetValue(__instance,out motion)||motion.CanTrigger(__instance);}
        static void BeforeGun(Gun __instance){L25TurretMotion motion;if(Guns.TryGetValue(__instance,out motion))motion.BeforeGun(__instance);}
        static void Select(Gun __instance,out L25TurretMotion __state)
        {if(Guns.TryGetValue(__instance,out __state)&&__state&&__state.enabled)__state.PrepareShot(__instance);else __state=null;}
        static void Fired(Gun __instance,L25TurretMotion __state,bool __runOriginal)
        {if(__runOriginal&&__state)__state.ShotFired(__instance);}
        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();harmony=null;
            foreach(var motion in new List<L25TurretMotion>(Guns.Values.Distinct()))if(motion){if(Application.isPlaying)Object.Destroy(motion);else Object.DestroyImmediate(motion);}
            Guns.Clear();
        }
    }
    public sealed class L25TurretMotion:MonoBehaviour
    {
        static readonly FieldInfo CurrentStation=AccessTools.Field(typeof(Turret),"currentWeaponStation");
        static readonly FieldInfo TracerSeed=AccessTools.Field(typeof(Gun),"tracerSeed");
        static readonly FieldInfo TracerRatio=AccessTools.Field(typeof(Gun),"tracerRatio");
        static readonly FieldInfo Muzzles=AccessTools.Field(typeof(Gun),"muzzles");
        static readonly FieldInfo Spread=AccessTools.Field(typeof(Gun),"bulletSpread");
        static readonly FieldInfo Loaded=AccessTools.Field(typeof(Gun),"bulletsLoaded"),Reload=AccessTools.Field(typeof(Gun),"timeUntilReload"),Ammo=AccessTools.Field(typeof(Gun),"ammo"),Queued=AccessTools.Field(typeof(Gun),"queuedBullets"),Trigger=AccessTools.Field(typeof(Gun),"ticksSinceTriggerPull"),Last=AccessTools.Field(typeof(Gun),"lastFired"),Interval=AccessTools.Field(typeof(Gun),"fireInterval");
        readonly Transform[] barrels=new Transform[3];readonly Vector3[] rest=new Vector3[3];readonly float[] recoil=new float[3];readonly Gun[] guns=new Gun[3];
        Transform pitch,left,right;float oldYaw,oldPitch,nextShot;int next,tracerPhase;ModelDetail detail;
        internal bool Visible;internal float LastSeen,NextSight;internal Unit Target;internal readonly L25TrackingSolution Track=new L25TrackingSolution();
        void Awake()
        {
            pitch=transform.Find("PitchPivot");left=transform.Find("PitchCrankPivot");right=transform.Find("YawCrankPivot");
            if(!pitch||!left||!right){enabled=false;return;}
            var names=new[]{"Left","Middle","Right"};
            for(int i=0;i<3;i++){
                barrels[i]=pitch.Find("Barrel"+names[i]);var feed=pitch.Find("Feed"+names[i]);guns[i]=feed?feed.GetComponent<Gun>():null;
                if(!barrels[i]||!guns[i]){enabled=false;return;}rest[i]=barrels[i].localPosition;
            }
            for(int i=0;i<3;i++)L25Runtime.Register(guns[i],this);detail=new ModelDetail(this);ResetAngles();
        }
        void ResetAngles(){if(pitch){oldYaw=transform.localEulerAngles.y;oldPitch=pitch.localEulerAngles.x;}}
        void OnEnable(){ResetAngles();}
        bool Ready(int i)
        {return guns[i]&&!guns[i].Safety&&(int)Ammo.GetValue(guns[i])>0&&(int)Loaded.GetValue(guns[i])>0&&(float)Reload.GetValue(guns[i])<=0&&Time.timeSinceLevelLoad-(float)Last.GetValue(guns[i])+1e-5f>=(float)Interval.GetValue(guns[i]);}
        internal void SelectStation(Turret turret)
        {
            var stations=turret.GetWeaponStations();if(!enabled||stations==null||stations.Length!=3)return;
            int selected=next;for(int n=0;n<3;n++){int i=(next+n)%3;if(Ready(i)){selected=i;break;}}
            var station=stations[selected];station.AccountAmmo();station.AssessAmmo();CurrentStation.SetValue(turret,station);
        }
        internal bool CanTrigger(Gun gun)
        {
            if(!enabled||Time.time+1e-5f<nextShot)return false;
            if(gun.attachedUnit&&gun.attachedUnit.LocalSim&&gun.attachedUnit.definition&&gun.attachedUnit.definition.jsonKey=="Gunmaggedon_L25_AA"&&
                (!Visible||!Target||RangeTo(gun)>3500||Time.time-LastSeen>.65f||!Track.Ready(Time.time)))return false;
            for(int n=0;n<3;n++){int i=(next+n)%3;if(Ready(i))return guns[i]==gun;}return false;
        }
        internal void BeforeGun(Gun gun)
        {if(!CanTrigger(gun)){Queued.SetValue(gun,0f);Trigger.SetValue(gun,100);}}
        float RangeTo(Gun gun){var points=Muzzles.GetValue(gun) as Transform[];return Vector3.Distance(Target.transform.position,points!=null&&points.Length>0?points[0].position:gun.transform.position);}
        internal void PrepareShot(Gun gun)
        {
            // Native SpawnBullet increments first, then checks seed > ratio.
            // Honor the authored shot interval while sharing one phase across
            // feeds. Zero/one means every shot; three keeps the historical 1:3.
            int ratio=(int)TracerRatio.GetValue(gun),interval=Mathf.Max(1,ratio);
            TracerSeed.SetValue(gun,(tracerPhase%interval)==interval-1?Mathf.Max(0,ratio):0);
            if(Target)Spread.SetValue(gun,(float)L25TrackingSolution.Spread(RangeTo(gun)));
        }
        internal void ShotFired(Gun gun)
        {int i=System.Array.IndexOf(guns,gun);if(i<0)return;if(detail==null||detail.Animate){recoil[i]=1;barrels[i].localPosition=rest[i]-Vector3.forward*.12f;}next=(i+1)%3;tracerPhase=tracerPhase==int.MaxValue?0:tracerPhase+1;nextShot=Time.time+.1f;}
        public int RoundsInClip(int barrel){return barrel>=0&&barrel<3&&guns[barrel]?(int)Loaded.GetValue(guns[barrel]):0;}
        public float ReloadSeconds(int barrel){return barrel>=0&&barrel<3&&guns[barrel]?Mathf.Max(0,(float)Reload.GetValue(guns[barrel])):0;}
        internal void StepRecoil(float dt){for(int i=0;i<3;i++){recoil[i]=Mathf.MoveTowards(recoil[i],0,Mathf.Max(0,dt)*7);barrels[i].localPosition=rest[i]-Vector3.forward*(.12f*recoil[i]);}}
        void LateUpdate()
        {
            if(!pitch)return;float y=transform.localEulerAngles.y,p=pitch.localEulerAngles.x;
            if(detail==null||detail.Animate){left.Rotate(Vector3.right,-Mathf.DeltaAngle(oldPitch,p)*12f,Space.Self);right.Rotate(Vector3.right,Mathf.DeltaAngle(oldYaw,y)*6f,Space.Self);StepRecoil(Time.deltaTime);}
            else for(int i=0;i<3;i++)if(recoil[i]>0){recoil[i]=0;barrels[i].localPosition=rest[i];}
            oldYaw=y;oldPitch=p;
        }
        void OnDisable(){for(int i=0;i<3;i++){recoil[i]=0;if(barrels[i])barrels[i].localPosition=rest[i];}}
        void OnDestroy(){foreach(var gun in guns)L25Runtime.Unregister(gun);}
    }
}
