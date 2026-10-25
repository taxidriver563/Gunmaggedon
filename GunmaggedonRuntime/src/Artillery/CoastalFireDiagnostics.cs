#if GUNMAGGEDON_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Temporary, bounded mission evidence. Never calls a selector/token allocator.
    internal static class CoastalFireDiagnostics
    {
        sealed class State { internal int Reports, Triggers, Spawns; }
        static readonly Dictionary<Gun, State> states = new Dictionary<Gun, State>();
        static Transform mission;
        static float next;
        static readonly FieldInfo Pitch = AccessTools.Field(typeof(Turret), "elevationTransform");
        static readonly FieldInfo Station = AccessTools.Field(typeof(Turret), "currentWeaponStation");
        static readonly FieldInfo Muzzles = AccessTools.Field(typeof(Gun), "muzzles");
        static readonly MethodInfo Clearance = AccessTools.Method(typeof(CoastalArtillery), "CheckClearance");
        static readonly FieldInfo Mask = AccessTools.Field(typeof(PhysicsLayers), "ExclusionZonesMask");
        static bool Matches(Gun gun) { return gun && gun.attachedUnit is Building && gun.info &&
            (gun.info.name.StartsWith("203mmEmplacement", StringComparison.Ordinal) ||
            (gun.info.weaponName ?? "").IndexOf("Coastal", StringComparison.OrdinalIgnoreCase) >= 0); }
        static State Get(Gun gun) { State s; if (!states.TryGetValue(gun, out s)) states.Add(gun, s = new State()); return s; }
        static object Field(object value, string name) { return AccessTools.Field(value.GetType(), name).GetValue(value); }
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Gun), "Fire"), postfix: new HarmonyMethod(typeof(CoastalFireDiagnostics), nameof(Trigger)));
            harmony.Patch(AccessTools.Method(typeof(Gun), "SpawnBullet"), postfix: new HarmonyMethod(typeof(CoastalFireDiagnostics), nameof(Spawn)));
        }
        static void Trigger(Gun __instance) { if (Matches(__instance)) Get(__instance).Triggers++; }
        static void Spawn(Gun __instance, bool __runOriginal) { if (__runOriginal && Matches(__instance)) Get(__instance).Spawns++; }
        internal static void Clear() { states.Clear(); mission = null; next = 0f; }
        internal static void Tick()
        {
            if (mission != Datum.origin) { Clear(); mission = Datum.origin; }
            if (!mission || !LevelInfo.i || Time.time < next || !Plugin.Instance) return;
            next = Time.time + 8f;
            // Only diagnostic builds scan, once per eight seconds, up to twelve
            // reports per gun. This also catches disabled native turrets/guns.
            foreach (var gun in UnityEngine.Object.FindObjectsOfType<Gun>())
            {
                if (!Matches(gun)) continue;
                var state = Get(gun); if (state.Reports >= 12) continue;
                try { Plugin.Instance.LoggerForCoastalDiagnostics(Snapshot(gun, state)); state.Reports++; }
                catch (Exception ex) { Plugin.Instance.LoggerForCoastalDiagnostics("snapshot failed: " + ex.GetType().Name + ": " + ex.Message); state.Reports = 12; }
            }
            var dead = RemovalBuffer<Gun>.Rent();
            try { foreach (var pair in states) if (!pair.Key) dead.Add(pair.Key); foreach (var gun in dead) states.Remove(gun); }
            finally { RemovalBuffer<Gun>.Return(dead); }
        }
        static string Snapshot(Gun gun, State state)
        {
            var turret = gun.GetComponentInParent<Turret>();
            if (!turret)
                foreach (var candidate in gun.attachedUnit.GetComponentsInChildren<Turret>(true))
                    if (candidate.GetWeapon() == gun) { turret = candidate; break; }
            if (!turret) return "gun=" + gun.name + " info=" + gun.info.name + " no parent turret";
            var target = turret.GetTarget(); var station = Station.GetValue(turret) as WeaponStation;
            var pitch = Pitch.GetValue(turret) as Transform; var muzzles = Muzzles.GetValue(gun) as Transform[];
            string scan = "missing";
            foreach (var detector in gun.attachedUnit.GetComponentsInChildren<TargetDetector>(true))
                if (detector.GetAttachedUnit() == gun.attachedUnit && detector.GetScanPoint())
                { scan = detector.GetScanPoint().position.ToString(); break; }
            string obstruction = "no pitch";
            if (pitch)
            {
                object[] args = { pitch.position, pitch.position + pitch.forward * 200, default(RaycastHit), ~((LayerMask)Mask.GetValue(null)).value, turret };
                bool blocked = (bool)Clearance.Invoke(null, args); var hit = (RaycastHit)args[2];
                obstruction = blocked ? hit.collider.name + "/" + hit.distance.ToString("F1") + "m/owner=" + (hit.collider.GetComponentInParent<Unit>() ? hit.collider.GetComponentInParent<Unit>().name : "none") : "clear";
            }
            string native = "";
            foreach (var field in new[] { "disabled", "stowed", "onTarget", "timeOnTarget", "lockTime", "targetRange", "maxRange", "traverseError", "elevationError" })
                native += " " + field + "=" + Field(turret, field);
            return "gun=" + gun.attachedUnit.name + " id=" + gun.GetInstanceID() + " info=" + gun.info.name +
                " supported=" + CoastalArtillery.IsSupported(gun) + " target=" + (target ? target.name : "none") +
                " gunTarget=" + (gun.GetTarget() ? gun.GetTarget().name : "none") + " local=" + gun.attachedUnit.LocalSim +
                " server=" + gun.attachedUnit.IsServer + " turretEnabled=" + turret.enabled + " gunEnabled=" + gun.enabled +
                " team=" + (gun.attachedUnit.NetworkHQ ? gun.attachedUnit.NetworkHQ.name : "none") +
                " targetTeam=" + (target && target.NetworkHQ ? target.NetworkHQ.name : "none") +
                " clock=" + (LevelInfo.i ? Field(LevelInfo.i, "timeOfDay").ToString() : "none") +
                " safety=" + gun.Safety + " loaded=" + Field(gun, "bulletsLoaded") + " reload=" + Field(gun, "timeUntilReload") +
                " ammo=" + gun.ammo + " stationAmmo=" + (station == null ? -1 : station.Ammo) + " stationReady=" + (station != null && station.Ready()) +
                " triggers=" + state.Triggers + " spawns=" + state.Spawns + " clearance=" + obstruction +
                " muzzle=" + (muzzles != null && muzzles.Length > 0 && muzzles[0] ? muzzles[0].position.ToString() : "missing") +
                " dark=" + CoastalIllumination.NeedsLight(target) + " illuminated=" + (target && CoastalIllumination.IsIlluminated(target)) +
                " scan=" + scan + " targetDisabled=" + (gun.GetTarget() && gun.GetTarget().Networkdisabled) + " cachedPlan=" + CoastalArtillery.DiagnosticPlan(gun) + " ranging=" + CoastalRanging.DiagnosticState(gun) + native;
        }
    }
}
#endif
