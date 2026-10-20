using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

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

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ParticleEffectManager.PrefabEffect), "Play"),
                prefix: new HarmonyMethod(typeof(CoastalWaterImpacts), nameof(Play)));
        }

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
            splash.Begin(position, rotation);
            return true;
        }

        internal static void Shutdown()
        {
            foreach (var p in Pool) if (p) Destroy(p.gameObject);
            Pool.Clear(); if (parking) Destroy(parking);
            template = parking = null;
        }

        private static void Destroy(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }

    public sealed class CoastalSplashInstance : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private float seconds;
        internal float RetireAt;

        internal void Initialize()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
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
        {
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            transform.SetPositionAndRotation(position, rotation);
            RetireAt = Time.time + seconds;
            gameObject.SetActive(true);
            foreach (var ps in systems)
            {
                ps.gameObject.SetActive(true);
                var main = ps.main; main.customSimulationSpace = Datum.origin;
                var renderer = ps.GetComponent<ParticleSystemRenderer>(); if (renderer) renderer.enabled = true;
                ps.Play(false);
            }
        }

        private void Update()
        {
            if (Time.time < RetireAt) return;
            foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
