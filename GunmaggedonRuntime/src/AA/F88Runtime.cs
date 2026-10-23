using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Mechanical presentation and the separate predicted-area flak controller.
    internal static class F88Runtime
    {
        static Harmony harmony;
        static readonly Dictionary<Gun,F88TurretMotion> Guns=new Dictionary<Gun,F88TurretMotion>();
        static readonly List<F88TurretMotion> Motions=new List<F88TurretMotion>();
        internal static void Install(ManualLogSource logger)
        {
            harmony=new Harmony("gunmaggedon.f88.presentation");
            try {
                harmony.Patch(AccessTools.Method(typeof(Turret),"Turret_OnInitialize"),
                    postfix:new HarmonyMethod(typeof(F88Runtime),nameof(Initialize)));
                harmony.Patch(AccessTools.Method(typeof(Gun),"FixedUpdate"),prefix:new HarmonyMethod(typeof(F88Runtime),nameof(BeforeGun)));
                F88FlakRuntime.Install(harmony);
                logger.LogInfo("F88 predicted-area barrages, timed fragmentation and 300m radar knowledge support enabled.");
            } catch(System.Exception e){harmony.UnpatchSelf();logger.LogError("F88 presentation initialization failed: "+e);}
        }
        static void Initialize(Turret __instance)
        {
            if(!__instance)return;
            var gun=__instance.GetComponentInChildren<Gun>(true);
            if(F88FlakRuntime.Matches(gun)&&!__instance.GetComponent<F88FlakController>())__instance.gameObject.AddComponent<F88FlakController>();
            if(__instance.name!="F88YawPivot"||!__instance.transform.Find("PitchPivot/BreechSlidePivot")||
                __instance.GetComponent<F88TurretMotion>())return;
            Motions.Add(__instance.gameObject.AddComponent<F88TurretMotion>());
        }
        internal static void Register(Gun gun,F88TurretMotion motion){Guns[gun]=motion;}
        internal static bool MechanicallyReady(Gun gun){F88TurretMotion motion;return Guns.TryGetValue(gun,out motion)&&motion&&motion.Ready;}
        internal static void Shot(Gun gun){F88TurretMotion motion;if(Guns.TryGetValue(gun,out motion)&&motion)motion.BeginReload();}
        static void BeforeGun(Gun __instance){F88TurretMotion motion;if(Guns.TryGetValue(__instance,out motion)&&motion)motion.BeforeGunUpdate();}
        internal static void Unregister(F88TurretMotion motion,Gun gun){Motions.Remove(motion);if(!ReferenceEquals(gun,null))Guns.Remove(gun);}
        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();harmony=null;F88FlakRuntime.Shutdown();
            foreach(var motion in Motions.ToArray())if(motion)Object.Destroy(motion);
            Motions.Clear();Guns.Clear();F88Casings.Shutdown();
        }
    }

    public sealed class F88TurretMotion : MonoBehaviour
    {
        static readonly FieldInfo ReloadRemaining=AccessTools.Field(typeof(Gun),"timeUntilReload"),Loaded=AccessTools.Field(typeof(Gun),"bulletsLoaded"),Queued=AccessTools.Field(typeof(Gun),"queuedBullets"),Trigger=AccessTools.Field(typeof(Gun),"ticksSinceTriggerPull"),CasingPrefab=AccessTools.Field(typeof(Gun),"ejectionPrefab");
        Transform yaw,pitch,left,right,breech,rest,caseVisual,caseSeated,caseClear,liveVisual,liveStart,liveSeat;Gun gun;float oldYaw,oldPitch;
        readonly F88ReloadCycle cycle=new F88ReloadCycle();ModelDetail detail;double lastAdvance=double.NegativeInfinity;
        internal bool Ready {get{return enabled&&cycle.Ready&&breech&&rest&&Vector3.Distance(breech.localPosition,rest.localPosition)<.001f;}}
        void Awake()
        {
            yaw=transform;pitch=yaw.Find("PitchPivot");left=yaw.Find("PitchCrankPivot");right=yaw.Find("YawCrankPivot");
            if(pitch){gun=pitch.GetComponent<Gun>();breech=pitch.Find("BreechSlidePivot");rest=pitch.Find("BreechSlideRest");
                caseVisual=pitch.Find("ReloadVisuals/ExtractingCasing");caseSeated=pitch.Find("ReloadVisuals/CasingSeated");caseClear=pitch.Find("ReloadVisuals/CasingClear");
                liveVisual=pitch.Find("ReloadVisuals/LoadingShell");liveStart=pitch.Find("ReloadVisuals/LoadingStart");liveSeat=pitch.Find("ReloadVisuals/LoadingSeated");}
            if(!gun||!left||!right||!breech||!rest||!caseVisual||!caseSeated||!caseClear||!liveVisual||!liveStart||!liveSeat){enabled=false;return;}
            detail=new ModelDetail(this);F88Runtime.Register(gun,this);ApplyPose(Time.time);ResetAngles();
        }
        void ResetAngles(){if(yaw&&pitch){oldYaw=yaw.localEulerAngles.y;oldPitch=pitch.localEulerAngles.x;}}
        void OnEnable(){ResetAngles();}
        internal void BeginReload(){cycle.Begin(Time.time);lastAdvance=double.NegativeInfinity;ApplyPose(Time.time);}
        internal void UpdateReload()
        {
            if(!gun||!caseVisual)return;
            if(lastAdvance==Time.time)return;lastAdvance=Time.time;
            if(cycle.Tick(Time.time)&&(detail==null||detail.Animate))F88Casings.Eject(CasingPrefab.GetValue(gun) as GameObject,caseClear,gun.attachedUnit);
            // Readiness must not wait for a rendered frame after the secured dwell.
            if(cycle.Ready&&breech.localPosition!=rest.localPosition)breech.localPosition=rest.localPosition;
        }
        internal void BeforeGunUpdate()
        {
            UpdateReload();if(cycle.Ready||(gun.attachedUnit&&!gun.attachedUnit.LocalSim))return;
            // Native ammunition is replenished only after insertion and the
            // final closed-door dwell. A delayed frame cannot skip the pause.
            Loaded.SetValue(gun,0);Queued.SetValue(gun,0f);Trigger.SetValue(gun,100);
            ReloadRemaining.SetValue(gun,Mathf.Max((float)ReloadRemaining.GetValue(gun),cycle.Remaining(Time.time)+.025f));
        }
        void ApplyPose(double now)
        {
            OptimizationMetrics.Count("ReloadPoseWrites");
            // The mechanical clock and ammunition gate still run at all LODs.
            // A distant door stays closed; hidden loading props need no motion.
            if(detail!=null&&!detail.Animate){breech.localPosition=rest.localPosition;if(caseVisual.gameObject.activeSelf)caseVisual.gameObject.SetActive(false);if(liveVisual.gameObject.activeSelf)liveVisual.gameObject.SetActive(false);return;}
            float progress=cycle.Progress(now),smooth=Mathf.SmoothStep(0,1,progress);var phase=cycle.Phase;
            float opening=phase==F88ReloadCycle.Stage.Open?smooth:phase==F88ReloadCycle.Stage.Close?1-smooth:
                phase>=F88ReloadCycle.Stage.Extract&&phase<=F88ReloadCycle.Stage.Insert?1:0;
            breech.localPosition=rest.localPosition+Vector3.right*(.36f*opening);
            caseVisual.localPosition=Vector3.Lerp(caseSeated.localPosition,caseClear.localPosition,smooth);
            caseVisual.gameObject.SetActive(phase==F88ReloadCycle.Stage.Extract);
            liveVisual.localPosition=Vector3.Lerp(liveStart.localPosition,liveSeat.localPosition,smooth);
            liveVisual.gameObject.SetActive(phase==F88ReloadCycle.Stage.Insert);
        }
        void LateUpdate()
        {
            if(!gun||!pitch||!left||!right)return;
            float y=yaw.localEulerAngles.y,p=pitch.localEulerAngles.x;
            if(detail==null||detail.Animate){left.Rotate(Vector3.right,-Mathf.DeltaAngle(oldPitch,p)*12f,Space.Self);
            right.Rotate(Vector3.right,Mathf.DeltaAngle(oldYaw,y)*6f,Space.Self);}
            oldYaw=y;oldPitch=p;UpdateReload();ApplyPose(Time.time);
        }
        void OnDisable()
        {
            if(breech&&rest)breech.localPosition=rest.localPosition;
            if(caseVisual)caseVisual.gameObject.SetActive(false);if(liveVisual)liveVisual.gameObject.SetActive(false);
        }
        void OnDestroy(){F88Runtime.Unregister(this,gun);}
    }
}
