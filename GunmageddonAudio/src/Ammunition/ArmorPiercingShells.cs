using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class ArmorPiercingShells
    {
        private static ConfigEntry<CoastalAmmoMode> mode;
        private static ConfigEntry<float> penetration;
        private static readonly HashSet<WeaponInfo> Profiles = new HashSet<WeaponInfo>();
        private static readonly System.Reflection.FieldInfo ArmorEffect = AccessTools.Field(typeof(Gun), "impactEffectArmor");
        internal static readonly Color TracerColor = new Color(32f, 26f, 15f, 1f);

        internal static void Configure(ConfigFile config)
        {
            mode = config.Bind("Coastal ammunition", "Mode", CoastalAmmoMode.Auto,
                "Ordinary 203mm gun only. Auto uses AP against ships at least 65 m long or armor tier 3+, HE otherwise. AP and HE force damaging ammunition after ranging/illumination. Manual Auto fire uses HE.");
            penetration = config.Bind("Coastal ammunition", "APPenetrationAtLaunch", 12000f,
                new ConfigDescription("Native AP penetration at launch, before armor/tolerance and flight-speed loss. Reduced-charge compensation preserves ballistics. No explosive damage. Not hull HP.", new AcceptableValueRange<float>(500f, 30000f)));
        }

        internal static WeaponInfo Prepare(Gun gun, Unit target, Vector3 launchVelocity)
        {
            bool ordinary = CoastalArtillery.IsSupported(gun) && gun.info.name == "203mmEmplacement";
            if (!ordinary || !gun.attachedUnit.IsServer) return null;
            var definition = target ? target.definition : null;
            if (!ArmorPiercingPolicy.UseAP(mode == null ? CoastalAmmoMode.Auto : mode.Value, ordinary,
                target is Ship, definition ? definition.length : 0f, definition ? definition.armorTier : 0)) return null;
            float native = ArmorPiercingPolicy.NativePenetration(penetration == null ? 12000f : penetration.Value,
                gun.info.muzzleVelocity, launchVelocity.magnitude);
            if (native <= 0f) return null;
            // Immutable per shot: a later shot with a different charge cannot alter in-flight damage.
            var profile = Object.Instantiate(gun.info);
            profile.name = "203mm Coastal AP"; profile.weaponName = "203mm Coastal AP"; profile.shortName = "203mm AP";
            profile.pierceDamage = native; profile.blastDamage = 0f; profile.airburstHeight = 0f;
            Profiles.Add(profile); return profile;
        }

        internal static GameObject ImpactAudio(Gun gun) { return gun ? ArmorEffect.GetValue(gun) as GameObject : null; }

        internal static void Impact(Vector3 position, Vector3 velocity, GameObject audioSource)
        {
            SpecialShells.EnsureMaterials();
            Plugin.PlaceholderSound(position, audioSource, "coastal-ap/impact");
            var go = new GameObject("Coastal AP compact impact");
            go.transform.SetParent(Datum.origin, false); go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(velocity.sqrMagnitude > .01f ? -velocity.normalized : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 18;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(.1f, .45f);
            main.startColor = new Color(4f, 2.8f, 1.2f, 1f); main.gravityModifier = .7f;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom; main.customSimulationSpace = Datum.origin;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 55f; shape.radius = .15f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = SpecialShells.SparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            SpecialShells.Track(go); ps.Play(false); ps.Emit(18);
            if (Application.isPlaying) Object.Destroy(go, .75f);
        }

        internal static void Release(WeaponInfo profile)
        {
            if (!profile || !Profiles.Remove(profile)) return;
            if (Application.isPlaying) Object.Destroy(profile); else Object.DestroyImmediate(profile);
        }

        internal static void Shutdown()
        {
            foreach (var profile in Profiles) if (profile) { if (Application.isPlaying) Object.Destroy(profile); else Object.DestroyImmediate(profile); }
            Profiles.Clear();
        }
    }
}
