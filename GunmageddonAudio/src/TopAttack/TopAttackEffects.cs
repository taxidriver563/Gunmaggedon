using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // References are packaged placeholders, resolved to native game assets by Blueprinter.
    internal static class TopAttackEffects
    {
        private static GameObject discharge, armor, ground, water, parking;
        private static readonly Dictionary<GameObject, List<CoastalSplashInstance>> Pools = new Dictionary<GameObject, List<CoastalSplashInstance>>();

        internal static void Configure(Transform reference)
        {
            if (!reference || discharge) return;
            discharge = reference.gameObject;
            var gun = reference.GetComponent<Gun>();
            if (!gun) return;
            armor = (GameObject)AccessTools.Field(typeof(Gun), "impactEffectArmor").GetValue(gun);
            ground = (GameObject)AccessTools.Field(typeof(Gun), "impactEffectGround").GetValue(gun);
            water = (GameObject)AccessTools.Field(typeof(Gun), "impactEffectWater").GetValue(gun);
        }

        internal static void Discharge(Vector3 position)
        {
            Plugin.PlaceholderSound(position, discharge, "top-attack/discharge");
        }

        internal static void MuzzleFlash(Transform muzzle, Vector3 direction)
        {
            var go = new GameObject("Top attack slug muzzle flash");
            go.transform.SetParent(muzzle, false);
            go.transform.rotation = Quaternion.LookRotation(direction);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .15f;
            main.maxParticles = 10;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.07f, .13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .65f);
            main.startColor = new Color(8f, 4.5f, 1.4f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.rateOverTime = 0f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f; shape.radius = .035f;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .4f, .1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SpecialShells.SparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play(false); particles.Emit(10);
            if (Application.isPlaying) Object.Destroy(go, .35f);
        }

        internal static void Impact(Vector3 position, Vector3 normal, bool armored, bool sea)
        {
            var source = sea ? water : armored ? armor : ground;
            if (!source) return;
            Plugin.PlaceholderSound(position, source, "top-attack/" + (sea ? "water" : armored ? "armor" : "ground"));
            if (!parking) { parking = new GameObject("Top attack impact parking"); parking.transform.SetParent(Datum.origin, false); parking.SetActive(false); }
            List<CoastalSplashInstance> pool;
            if (!Pools.TryGetValue(source, out pool)) { pool = new List<CoastalSplashInstance>(16); Pools.Add(source, pool); }
            pool.RemoveAll(p => !p);
            CoastalSplashInstance effect = null, oldest = null;
            foreach (var entry in pool)
            {
                if (!entry.gameObject.activeSelf) effect = entry;
                if (!oldest || entry.RetireAt < oldest.RetireAt) oldest = entry;
            }
            if (!effect && pool.Count >= 16) effect = oldest;
            if (!effect)
            {
                var go = Object.Instantiate(source, parking.transform, false); go.SetActive(false);
                go.name = "Top attack native impact " + source.name;
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
                foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                go.transform.SetParent(Datum.origin, false);
                effect = go.AddComponent<CoastalSplashInstance>(); effect.Initialize(); pool.Add(effect);
            }
            effect.Begin(position, Quaternion.FromToRotation(Vector3.up, normal) * source.transform.rotation);
        }

        internal static void Shutdown()
        {
            foreach (var pool in Pools.Values) foreach (var effect in pool) if (effect) Dispose(effect.gameObject);
            Pools.Clear(); if (parking) Dispose(parking);
            discharge = armor = ground = water = parking = null;
        }

        private static void Dispose(Object obj) { if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj); }
    }
}
