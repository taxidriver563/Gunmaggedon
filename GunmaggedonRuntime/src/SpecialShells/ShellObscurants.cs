using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class ShellObscurants
    {
        internal static readonly List<ShellCloud> Clouds = new List<ShellCloud>();
        private static readonly System.Reflection.FieldInfo SeekerTarget = AccessTools.Field(typeof(ARHSeeker), "targetUnit");

        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(Unit), "LineOfSight"), postfix: new HarmonyMethod(typeof(ShellObscurants), nameof(OpticalLine)));
            harmony.Patch(AccessTools.Method(typeof(TargetDetector), "InVisualRange"), postfix: new HarmonyMethod(typeof(ShellObscurants), nameof(VisualRange)));
            var manager = AccessTools.TypeByName("NuclearOption.Jobs.DetectorManager");
            harmony.Patch(AccessTools.Method(manager, "RequestLoSCheck"), prefix: new HarmonyMethod(typeof(ShellObscurants), nameof(RequestVisual)));
            harmony.Patch(AccessTools.Method(manager, "RequestRadarCheck"), prefix: new HarmonyMethod(typeof(ShellObscurants), nameof(RequestRadar)));
            harmony.Patch(AccessTools.Method(typeof(Radar), "CanSeeRadarReturn"), postfix: new HarmonyMethod(typeof(ShellObscurants), nameof(RadarVisibility)));
            harmony.Patch(AccessTools.Method(typeof(ARHSeeker), "GetRadarReturn"), postfix: new HarmonyMethod(typeof(ShellObscurants), nameof(SeekerReturn)));
        }

        // Analytic segment/ellipsoid intersection. Distant clouds off the sightline
        // have no effect, and an observer inside a cloud is handled naturally.
        internal static float PathLength(Vector3 from, Vector3 to, Vector3 centre, Vector3 radii)
        {
            Vector3 d = to - from;
            Vector3 p = from - centre;
            var n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
            var q = new Vector3(p.x / radii.x, p.y / radii.y, p.z / radii.z);
            float a = Vector3.Dot(n, n);
            if (a < .000001f) return 0f;
            float b = 2f * Vector3.Dot(q, n), c = Vector3.Dot(q, q) - 1f;
            float discriminant = b * b - 4f * a * c;
            if (discriminant <= 0f) return 0f;
            float root = Mathf.Sqrt(discriminant);
            float start = Mathf.Clamp01((-b - root) / (2f * a));
            float end = Mathf.Clamp01((-b + root) / (2f * a));
            return Mathf.Max(0f, end - start) * d.magnitude;
        }

        internal static float Strength(float age, float lifetime)
        {
            return Mathf.Clamp01(age / 3f) * Mathf.Clamp01((lifetime - age) / 10f);
        }

        internal static float Density(SpecialShellKind kind, Vector3 from, Vector3 to)
        {
            float density = 0f;
            for (int i = Clouds.Count - 1; i >= 0; i--)
            {
                var cloud = Clouds[i];
                if (!cloud) { Clouds.RemoveAt(i); continue; }
                if (cloud.Kind != kind || !cloud.isActiveAndEnabled) continue;
                var center = cloud.Position.ToLocalPosition();
                var radii = cloud.Radii;
                if (!QueryGeometry.AxisOverlaps(from.x, to.x, center.x, radii.x) ||
                    !QueryGeometry.AxisOverlaps(from.y, to.y, center.y, radii.y) ||
                    !QueryGeometry.AxisOverlaps(from.z, to.z, center.z, radii.z)) continue;
                float strength = Strength(Time.time - cloud.Born, cloud.Lifetime);
                if (strength <= 0f) continue;
                density += PathLength(from, to, center, radii) / 35f * strength;
                if (density >= 1f) return 1f;
            }
            return Mathf.Clamp01(density);
        }

        internal static bool SmokeBlocks(Vector3 from, Vector3 to) { return Density(SpecialShellKind.Smoke, from, to) >= .55f; }

        internal static float RadarFactor(Vector3 from, Vector3 to)
        {
            if (Clouds.Count == 0) return 1f;
            // Short-range burn-through prevents a cloud granting radar immunity.
            float rangeFactor = Mathf.InverseLerp(200f, 1200f, Vector3.Distance(from, to));
            return Mathf.Lerp(1f, .12f, Density(SpecialShellKind.Chaff, from, to) * rangeFactor);
        }

        private static void OpticalLine(Unit __instance, Vector3 __0, ref bool __result)
        {
            if (__result && __instance && SmokeBlocks(__0, __instance.transform.position)) __result = false;
        }

        private static void VisualRange(TargetDetector __instance, Unit __0, ref bool __result)
        {
            if (__result && __instance && __0 && SmokeBlocks(__instance.GetScanPoint().position, __0.transform.position)) __result = false;
        }

        private static bool RequestVisual(TargetDetector __0, Unit __1)
        {
            return !__0 || !__1 || !SmokeBlocks(__0.GetScanPoint().position, __1.transform.position);
        }

        private static bool RequestRadar(TargetDetector __0, Unit __1)
        {
            return !__0 || !__1 || RadarFactor(__0.GetScanPoint().position, __1.transform.position) > .4f;
        }

        private static void RadarVisibility(Radar __instance, IRadarReturn __0, ref bool __result)
        {
            var target = __0 as Unit;
            if (__result && __instance && target && RadarFactor(__instance.GetScanPoint().position, target.transform.position) <= .4f) __result = false;
        }

        private static void SeekerReturn(ARHSeeker __instance, ref float __result)
        {
            if (__result <= 0f || Clouds.Count == 0) return;
            var target = SeekerTarget.GetValue(__instance) as Unit;
            if (target) __result *= RadarFactor(__instance.transform.position, target.transform.position);
        }

        internal static void Clear() { Clouds.Clear(); }
    }

    public sealed class ShellCloud : MonoBehaviour
    {
        internal SpecialShellKind Kind;
        internal GlobalPosition Position;
        internal float Born, Lifetime;
        internal Vector3 Radii;
        internal bool Remote;
        internal uint NetworkId;
        private ParticleSystem particles;
        private float nextUpdate;

        internal void PresentAge(float age)
        {
            SetTint(ShellObscurants.Strength(age, Lifetime));
            particles.Simulate(Mathf.Min(age, 10f), true, true, false);
            if (age < Lifetime - 10f) particles.Play();
        }

        internal void Initialize(SpecialShellKind kind, GlobalPosition position, float lifetime)
        {
            Kind = kind; Position = position; Born = Time.time; Lifetime = lifetime;
            Radii = kind == SpecialShellKind.Smoke ? new Vector3(65f, 32f, 65f) : new Vector3(95f, 60f, 95f);
            transform.position = position.ToLocalPosition() + (kind == SpecialShellKind.Smoke ? Vector3.up * 14f : Vector3.zero);
            Position = transform.position.ToGlobalPosition();
            particles = gameObject.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.startLifetime = 10f; main.maxParticles = 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = .8f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSize = kind == SpecialShellKind.Smoke ? new ParticleSystem.MinMaxCurve(28f, 50f) : new ParticleSystem.MinMaxCurve(.3f, 1.2f);
            SetTint(1f);
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 1f; shape.scale = Radii * .8f;
            var emission = particles.emission; emission.rateOverTime = kind == SpecialShellKind.Smoke ? 45f : 65f;
            var colour = particles.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = kind == SpecialShellKind.Smoke ? SpecialShells.SmokeMaterial : SpecialShells.ChaffMaterial;
            particles.Play();
            ShellObscurants.Clouds.RemoveAll(c => !c);
            if (ShellObscurants.Clouds.Count >= 64)
            {
                var oldest = ShellObscurants.Clouds[0]; ShellObscurants.Clouds.RemoveAt(0);
                if (oldest) Destroy(oldest.gameObject);
            }
            ShellObscurants.Clouds.Add(this);
        }

        private void Update()
        {
            float age = Time.time - Born;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            Vector3 wind = LevelInfo.i ? LevelInfo.i.NetworkwindVelocity : Vector3.zero;
            // Horizontal wind advection affects visuals and the functional volume together.
            wind.y = 0f;
            Position = (Position.ToLocalPosition() + wind * Time.deltaTime * .5f).ToGlobalPosition();
            transform.position = Position.ToLocalPosition();
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + .25f;
            float strength = ShellObscurants.Strength(age, Lifetime);
            SetTint(strength);
            if (age >= Lifetime - 10f && particles.isEmitting) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void SetTint(float strength)
        {
            var main = particles.main;
            main.startColor = Kind == SpecialShellKind.Smoke ?
                new ParticleSystem.MinMaxGradient(new Color(.3f, .33f, .32f, .7f * strength), new Color(.65f, .68f, .65f, .9f * strength)) :
                new ParticleSystem.MinMaxGradient(new Color(.6f, .68f, .8f, .6f * strength), new Color(1f, 1f, 1f, strength));
        }

        private void OnDestroy() { ShellObscurants.Clouds.Remove(this); }
    }
}
