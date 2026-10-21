using System.Collections.Generic;
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GunmageddonAudio
{
    // BulletSim holds one PrefabEffect per surface for its entire lifetime.
    // Replaying a still-running nonlooping system does not restart its burst.
    // Keep independent bounded splash instances so rapid shots cannot steal
    // or move the previous impact's still-visible spray and foam.
    internal static class CoastalWaterImpacts
    {
        private static GameObject template, parking;
        private static readonly List<CoastalSplashInstance> Pool = new List<CoastalSplashInstance>();
        private const int MaximumSplashes = 64;
        [ThreadStatic] private static Color? activeTint;

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ParticleEffectManager.PrefabEffect), "Play"),
                prefix: new HarmonyMethod(typeof(CoastalWaterImpacts), nameof(Play)));
            harmony.Patch(AccessTools.Method(typeof(BulletSim.Bullet), "TrajectoryTrace"),
                prefix: new HarmonyMethod(typeof(CoastalWaterImpacts), nameof(BeginTrace)),
                finalizer: new HarmonyMethod(typeof(CoastalWaterImpacts), nameof(EndTrace)));
        }

        private static void BeginTrace(BulletSim.Bullet __instance, Unit owner, WeaponInfo info, out Color? __state)
        {
            __state = activeTint;
            Color color;
            activeTint = CoastalRanging.TryGetSpottingTint(__instance, owner, info, out color) ? (Color?)color : null;
        }

        private static Exception EndTrace(Exception __exception, Color? __state)
        { activeTint = __state; return __exception; }

        private static bool Play(ParticleEffectManager.PrefabEffect __instance, Vector3 __0, Quaternion __1)
        {
            var source = __instance.gameObject;
            if (!source || !source.name.StartsWith("203mm_impactEffectWater", System.StringComparison.Ordinal)) return true;
            source.transform.SetPositionAndRotation(__0, __1); // Existing travelling-audio postfix uses this location.
            foreach (var ps in __instance.systems) if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            return !Spawn(source, __0, __1);
        }

        internal static bool Spawn(GameObject source, Vector3 position, Quaternion rotation)
        {
            if (!template)
            {
                parking = new GameObject("Coastal water impact template");
                parking.transform.SetParent(Datum.origin, false); parking.SetActive(false);
                template = Object.Instantiate(source, parking.transform, false); template.SetActive(false);
                // SetGlobalParticles is replaced below with explicit Datum-space
                // setup. Copies cannot own native pooling/lifecycle or audio.
                foreach (var behaviour in template.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var audio in template.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
                foreach (var collider in template.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            }
            CoastalSplashInstance splash = null, oldest = null;
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                var candidate = Pool[i];
                if (!candidate) { Pool.RemoveAt(i); continue; }
                if (!candidate.gameObject.activeSelf) splash = candidate;
                if (!oldest || candidate.RetireAt < oldest.RetireAt) oldest = candidate;
            }
            if (!splash)
            {
                if (Pool.Count >= MaximumSplashes) splash = oldest;
                else
                {
                    var go = Object.Instantiate(template, Datum.origin, false);
                    go.name = "203mm independent water splash";
                    splash = go.AddComponent<CoastalSplashInstance>();
                    splash.Initialize(); Pool.Add(splash);
                }
            }
            splash.Begin(position, rotation, activeTint);
            return true;
        }

        internal static void Shutdown()
        {
            foreach (var p in Pool) if (p) Destroy(p.gameObject);
            Pool.Clear(); if (parking) Destroy(parking);
            template = parking = null;
            activeTint = null;
        }

        private static void Destroy(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }

    public sealed class CoastalSplashInstance : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem.MinMaxGradient[] originalColors;
        private readonly Dictionary<Color, ParticleSystem.MinMaxGradient[]> tintedColors = new Dictionary<Color, ParticleSystem.MinMaxGradient[]>();
        private float seconds;
        internal float RetireAt;

        internal void Initialize()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            originalColors = new ParticleSystem.MinMaxGradient[systems.Length];
            for (int i = 0; i < systems.Length; i++) originalColors[i] = systems[i].main.startColor;
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = false; main.playOnAwake = false; main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpace = ParticleSystemSimulationSpace.Custom;
                main.customSimulationSpace = Datum.origin;
                seconds = Mathf.Max(seconds, (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax) / Mathf.Max(.01f, main.simulationSpeed) + 1f);
            }
        }

        internal void Begin(Vector3 position, Quaternion rotation)
        { Begin(position, rotation, null); }

        internal void Begin(Vector3 position, Quaternion rotation, Color? tint)
        {
            var colors = originalColors;
            if (tint.HasValue && !tintedColors.TryGetValue(tint.Value, out colors))
            {
                colors = new ParticleSystem.MinMaxGradient[originalColors.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = Tint(originalColors[i], tint.Value);
                tintedColors.Add(tint.Value, colors);
            }
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.SetPositionAndRotation(position, rotation);
            RetireAt = Time.time + seconds;
            gameObject.SetActive(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                ps.gameObject.SetActive(true);
                var main = ps.main; main.customSimulationSpace = Datum.origin;
                main.startColor = colors[i];
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); if (renderer) renderer.enabled = true;
                ps.Play(false);
            }
        }

        private static Color Tint(Color original, Color tint)
        { return new Color(original.r * tint.r, original.g * tint.g, original.b * tint.b, original.a); }

        private static Gradient Tint(Gradient original, Color tint)
        {
            if (original == null) return null;
            var keys = original.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Tint(keys[i].color, tint);
            var result = new Gradient(); result.mode = original.mode;
            result.SetKeys(keys, original.alphaKeys); return result;
        }

        private static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient original, Color tint)
        {
            var result = original;
            switch (original.mode)
            {
                case ParticleSystemGradientMode.Color:
                    result.color = Tint(original.color, tint); break;
                case ParticleSystemGradientMode.TwoColors:
                    result.colorMin = Tint(original.colorMin, tint); result.colorMax = Tint(original.colorMax, tint); break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    result.gradient = Tint(original.gradient, tint); break;
                case ParticleSystemGradientMode.TwoGradients:
                    result.gradientMin = Tint(original.gradientMin, tint); result.gradientMax = Tint(original.gradientMax, tint); break;
            }
            result.mode = original.mode; return result;
        }

        private void Update()
        {
            if (Time.time < RetireAt) return;
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
