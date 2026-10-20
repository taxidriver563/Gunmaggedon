using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class NativeTopAttackBomb
    {
        internal const string Key = "Gunmageddon_TopAttackBomb";
        private static Gun pendingGun;
        private static float pendingPhase;

        internal static bool Is(Missile missile) { return missile && missile.definition && missile.definition.jsonKey == Key; }

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Missile), "OnStartNetwork"), postfix: new HarmonyMethod(typeof(NativeTopAttackBomb), nameof(Started)));
            harmony.Patch(AccessTools.Method(typeof(Missile), "FixedUpdate"), prefix: new HarmonyMethod(typeof(NativeTopAttackBomb), nameof(OwnMovement)));
            harmony.Patch(AccessTools.Method(typeof(Missile), "ServerFixedUpdate"), prefix: new HarmonyMethod(typeof(NativeTopAttackBomb), nameof(OwnMovement)));
            harmony.Patch(AccessTools.Method(typeof(OpticalSeekerBomb), "Initialize"), prefix: new HarmonyMethod(typeof(NativeTopAttackBomb), nameof(OwnSeeker)));
            harmony.Patch(AccessTools.Method(typeof(Missile), "Detonate"), prefix: new HarmonyMethod(typeof(NativeTopAttackBomb), nameof(Intercepted)));
        }

        private static bool OwnMovement(Missile __instance) { return !Is(__instance); }
        private static bool OwnSeeker(OpticalSeekerBomb __instance) { return !Is(__instance.GetComponent<Missile>()); }
        private static bool Intercepted(Missile __instance)
        {
            if (!Is(__instance)) return true;
            Expire(__instance); return false; // No inherited 500 kg bomb blast.
        }

        private static void Started(Missile __instance)
        {
            if (!Is(__instance)) return;
            Begin(__instance, pendingGun, pendingPhase);
        }

        internal static void Begin(Missile missile, Gun gun, float phase)
        {
            if (missile.GetComponent<TopAttackSubmunition>()) return;
            var parachute = CoastalIllumination.GetNativeParachute();
            if (!parachute) { Expire(missile); return; }
            SpecialShells.EnsureMaterials();
            TopAttackEffects.Configure(missile.definition.unitPrefab.transform.Find("SlugEffectsReference"));
            var source = missile.owner ? missile.owner : missile;
            var velocity = missile.NetworkstartingVelocity;
            missile.rb.useGravity = false; missile.rb.isKinematic = true;
            missile.SetTangible(true); missile.GetComponent<Collider>().enabled = true;
            TopAttackMunitions.Track(missile.gameObject);
            missile.gameObject.AddComponent<TopAttackSubmunition>().Initialize(gun, source,
                missile.transform.position.ToGlobalPosition(), velocity, phase, parachute);
        }

        internal static GameObject Spawn(MissileDefinition definition, Gun gun, Unit owner, Unit target, GlobalPosition position, Vector3 velocity, float phase)
        {
            if (!Spawner.i) return null;
            pendingGun = gun; pendingPhase = phase;
            try
            {
                var bomb = Spawner.i.SpawnMissile(definition, position.ToLocalPosition(), Quaternion.identity, velocity, target, owner);
                Begin(bomb, gun, phase);
                return bomb.gameObject;
            }
            finally { pendingGun = null; pendingPhase = 0f; }
        }

        internal static void Expire(Missile missile)
        {
            if (!missile) return;
            if (Application.isPlaying)
            {
                if (missile.IsServer && Spawner.i) Spawner.i.ServerObjectManager.Destroy(missile.gameObject, true);
            }
            else missile.gameObject.SetActive(false);
        }
    }
}
