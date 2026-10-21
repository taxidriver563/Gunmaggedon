using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Presentation and optical fire control do not need to detour native vehicle jobs.
    // Keep the unverified tow integration separate from this deployable module.
    internal static class L40Runtime
    {
        static Harmony harmony;
        static readonly List<AACrankMotion> Cranks=new List<AACrankMotion>();
        internal static void Install(ManualLogSource logger)
        {
            harmony=new Harmony("gunmageddon.l40.optical");
            try {
                AAOpticalRanging.Install(harmony);
                harmony.Patch(AccessTools.Method(typeof(Turret),"Turret_OnInitialize"),
                    postfix:new HarmonyMethod(typeof(L40Runtime),nameof(Initialize)));
                logger.LogInfo("L40 optical clip correction and aiming cranks enabled; vehicle job hooks excluded.");
            } catch(System.Exception e) {harmony.UnpatchSelf();logger.LogError("L40 initialization failed: "+e);}
        }
        static void Initialize(Turret __instance)
        {
            var gun=__instance.GetComponentInChildren<Gun>(true);
            if(!gun||!gun.info||gun.info.name!="L40_AA_Weapon"||!gun.attachedUnit||
                !gun.attachedUnit.definition||gun.attachedUnit.definition.jsonKey!="Gunmaggedon_L40_AA")return;
            var crank=gun.attachedUnit.GetComponent<AACrankMotion>();
            if(!crank){crank=gun.attachedUnit.gameObject.AddComponent<AACrankMotion>();Cranks.Add(crank);}
        }
        internal static void Unregister(AACrankMotion crank){Cranks.Remove(crank);}
        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();harmony=null;AAOpticalRanging.Shutdown();
            foreach(var crank in Cranks.ToArray())if(crank)Object.Destroy(crank);
            Cranks.Clear();
        }
    }
}
