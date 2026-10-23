using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace GunmageddonAudio
{
    internal static class CoastalRanging
    {
        private sealed class GunState
        {
            internal readonly RangingSolution Solution = new RangingSolution();
            internal Unit Target;
            internal WeaponInfo SpottingInfo;
            internal GlobalPosition SolutionTargetPosition;
            internal float LastImpactTime;
        }

        private sealed class Shot
        {
            internal GunState State;
            internal Unit Target;
            internal int Generation, Sequence;
            internal bool Spotting;
            internal bool Illumination;
            internal SpecialShells.Carrier Payload;
            internal CoastalIllumination.Plan FlarePlan;
            internal float LaunchTime;
            internal Vector3 Forward, Right;
            internal Color Color;
            internal SpottingTrail Trail;
            internal BulletSim Simulation;
            internal WeaponInfo APInfo;
            internal ParticleEffectManager.PrefabEffect[] APEffects;
            internal GameObject APImpactAudio;
            internal bool APImpactShown;
        }

        private sealed class Pending
        {
            internal Pending Previous;
            internal Shot Shot;
            internal Unit Owner;
            internal WeaponInfo Info;
        }

        private static readonly Dictionary<Gun, GunState> Guns = new Dictionary<Gun, GunState>();
        private static readonly Dictionary<BulletSim.Bullet, Shot> Shots = new Dictionary<BulletSim.Bullet, Shot>();
        private static readonly FieldInfo SimGun = AccessTools.Field(typeof(BulletSim), "gun");
        private static readonly FieldInfo VisualOnly = AccessTools.Field(typeof(BulletSim), "visualOnly");
        private static readonly FieldInfo AimTarget = AccessTools.Field(typeof(AimSolver), "currentTarget");
        [ThreadStatic] private static Pending pending;
        private static Material trailMaterial;
        private static ManualLogSource log;
        private static ConfigEntry<bool> enabled, waitForImpact;
        private static ConfigEntry<float> closeEnough, trailSeconds, resetMovement, normalDispersion;
        private static ConfigEntry<int> minimumShots;
        private static readonly Color[] SpotColors = {
            new Color(.1f, .85f, 1f), new Color(1f, .45f, .08f), new Color(.95f, .15f, 1f)
        };

        internal static void Install(Harmony harmony, ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            ArmorPiercingShells.Configure(config);
            CoastalIllumination.Configure(config, logger);
            enabled = config.Bind("Ranging", "Enabled", true, "Enable spotting rounds for building-mounted coastal guns.");
            waitForImpact = config.Bind("Ranging", "WaitForSpottingImpact", true,
                "Wait for a spotting shell to land before firing the next correction, rather than repeating an unobserved estimate.");
            minimumShots = config.Bind("Ranging", "MinimumSpottingShots", 2, new ConfigDescription(
                "Earliest spotting round that can establish a solution. A sufficiently close impact is always required.", new AcceptableValueRange<int>(1, 10)));
            closeEnough = config.Bind("Ranging", "AcceptableMissMetres", 60f, new ConfigDescription(
                "Maximum horizontal distance from the target hull for a successful spotting impact.", new AcceptableValueRange<float>(5f, 300f)));
            resetMovement = config.Bind("Ranging", "TargetMovementResetMetres", 350f, new ConfigDescription(
                "Target displacement after acquiring a solution that requires new ranging.", new AcceptableValueRange<float>(50f, 2000f)));
            normalDispersion = config.Bind("Ranging", "NormalDispersionMetres", 18f, new ConfigDescription(
                "Additional random aim dispersion after ranging, in metres at the target.", new AcceptableValueRange<float>(0f, 100f)));
            trailSeconds = config.Bind("Ranging", "TrailLifetimeSeconds", 4f, new ConfigDescription(
                "How long the colored spotting trail remains behind the moving shell.", new AcceptableValueRange<float>(2f, 30f)));
            // Migrate the previous shipped default without replacing custom values.
            if (Mathf.Approximately(trailSeconds.Value, 12f)) trailSeconds.Value = 4f;
            harmony.Patch(AccessTools.Method(typeof(BulletSim), "AddBullet"),
                prefix: new HarmonyMethod(typeof(CoastalRanging), nameof(BeforeAdd)),
                finalizer: new HarmonyMethod(typeof(CoastalRanging), nameof(AfterAdd)));
            harmony.Patch(AccessTools.Method(typeof(BulletSim.Bullet), "TrajectoryTrace"),
                prefix: new HarmonyMethod(typeof(CoastalRanging), nameof(BeforeTrace)),
                postfix: new HarmonyMethod(typeof(CoastalRanging), nameof(AfterTrace)));
            harmony.Patch(AccessTools.Method(typeof(BulletSim.Bullet), "Remove"),
                prefix: new HarmonyMethod(typeof(CoastalRanging), nameof(BeforeRemove)));
            // Stock correction must not simultaneously compensate for the deliberate spotting error.
            harmony.Patch(AccessTools.Method(typeof(Turret), "SetObservedBullet"),
                prefix: new HarmonyMethod(typeof(CoastalRanging), nameof(ObserveWithCustomSolver)));
            harmony.Patch(AccessTools.Method(typeof(Gun), "SetTarget"),
                postfix: new HarmonyMethod(typeof(CoastalRanging), nameof(TargetChanged)));
            harmony.Patch(AccessTools.Method(typeof(Gun), "Fire"),
                prefix: new HarmonyMethod(typeof(CoastalRanging), nameof(WaitForObservation)));
            harmony.Patch(AccessTools.Method(typeof(AimSolver), "GetAimVector"),
                postfix: new HarmonyMethod(typeof(CoastalRanging), nameof(RaiseForFlare)));
            log.LogInfo("Coastal ranging enabled: colored non-damaging spotting shells, persistent trails and impact-based correction.");
        }

        internal static bool IsCoastal(Gun gun)
        {
            return enabled != null && enabled.Value && gun && gun.info && gun.attachedUnit is Building &&
                (gun.info.name == "203mmEmplacement" ||
                 (gun.info.weaponName ?? "").IndexOf("Coastal", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static bool HasSolution(Gun gun, Unit target)
        {
            GunState state;
            return Guns.TryGetValue(gun, out state) && state.Target == target && state.Solution.Established &&
                HorizontalDistance(state.SolutionTargetPosition.ToLocalPosition(), target.transform.position) <= (resetMovement == null ? 350f : resetMovement.Value);
        }

        private static bool ObserveWithCustomSolver(Turret __instance)
        {
            return !IsCoastal(__instance.GetWeapon() as Gun);
        }

        private static void TargetChanged(Gun __instance)
        {
            SearchlightRuntime.ReleaseCoastalIfChanged(__instance);
            GunState state;
            if (Guns.TryGetValue(__instance, out state) && state.Target != __instance.GetTarget())
            {
                state.Target = null;
                state.Solution.Reset(0, 0f, 0f);
            }
        }

        private static bool WaitForObservation(Gun __instance, Unit target)
        {
            if (target && CoastalArtillery.IsSupported(__instance) && __instance.attachedUnit.LocalSim &&
                !CoastalArtillery.CanFire(__instance, target)) return false;
            if (!IsCoastal(__instance) || !__instance.attachedUnit.LocalSim || !target) return true;
            CleanupDestroyedGuns();
            GunState state;
            Guns.TryGetValue(__instance, out state);
            if (CoastalIllumination.NeedsLight(target))
            {
                CoastalIllumination.Plan plan;
                if (CoastalIllumination.CanLaunch(__instance, target, out plan)) return true;
                if (!CoastalIllumination.IsIlluminated(target)) return false;
            }
            if (!waitForImpact.Value || state == null || state.Target != target || state.Solution.Established) return true;
            return !Shots.Values.Any(s => s.State == state && s.Spotting && s.Generation == state.Solution.Generation);
        }

        private static void RaiseForFlare(AimSolver __instance, ref Vector3 __result)
        {
            var gun = CoastalArtillery.ResolveGun(__instance);
            if (!IsCoastal(gun)) return;
            var target = AimTarget.GetValue(__instance) as Unit;
            if (!target || !CoastalIllumination.ShouldFireFlare(gun, target)) return;
            var plan = CoastalIllumination.GetPlan(gun, target);
            if (plan != null && plan.Reachable) __result = plan.LaunchVelocity.normalized;
        }

        private static bool BeforeAdd(BulletSim __instance, Transform muzzle, ref Vector3 inheritedVelocity,
            ref float spread, ref bool tracer, ref float tracerSize, ref Color tracerColor, out Pending __state)
        {
            __state = null;
            var gun = SimGun.GetValue(__instance) as Gun;
            // Damage and the firing solution are authoritative. Remote visual simulations retain stock behavior.
            if ((bool)VisualOnly.GetValue(__instance) || !muzzle || !gun) return true;
            bool special = SpecialShells.IsSpecial(gun);
            var target = gun.GetTarget();
            bool artillery = CoastalArtillery.IsSupported(gun);
            if (artillery) { tracer = true; tracerSize = Mathf.Min(tracerSize, 4f); }
            if ((!IsCoastal(gun) && !special) || (!target && !special))
            {
                if (artillery && target && !CoastalArtillery.AdjustLaunch(gun, target, muzzle, Vector3.zero, 0f, 0f, ref inheritedVelocity)) return false;
                var ap = ArmorPiercingShells.Prepare(gun, target, inheritedVelocity);
                if (ap)
                {
                    var manualAP = new Shot { Simulation = __instance, Target = target, APInfo = ap,
                        APImpactAudio = ArmorPiercingShells.ImpactAudio(gun), LaunchTime = Time.time };
                    tracer = true; tracerColor = ArmorPiercingShells.TracerColor;
                    __state = new Pending { Previous = pending, Shot = manualAP, Owner = gun.attachedUnit, Info = gun.info };
                    pending = __state;
                }
                return true;
            }
            CleanupDestroyedGuns();
            var state = GetState(gun);
            // Manual firing and disabled ranging still use special payloads, never HE.
            if (special && (!IsCoastal(gun) || !target))
            {
                if (artillery && target && !CoastalArtillery.AdjustLaunch(gun, target, muzzle, Vector3.zero, 0f, 0f, ref inheritedVelocity)) return false;
                var payload = SpecialShells.Prepare(gun, target, muzzle.position, inheritedVelocity);
                var manual = new Shot { State = state, Target = target, Simulation = __instance,
                    Payload = payload, LaunchTime = Time.time };
                tracer = true; tracerColor = SpecialShells.ShellColor(payload.Kind) * 32f; tracerColor.a = 1f;
                __state = new Pending { Previous = pending, Shot = manual, Owner = gun.attachedUnit, Info = gun.info };
                pending = __state;
                return true;
            }
            Vector3 flat = target.transform.position - muzzle.position;
            flat.y = 0f;
            float range = Mathf.Max(100f, flat.magnitude);
            if (flat.sqrMagnitude < 1f) return true;
            Vector3 forward = flat.normalized;
            Vector3 right = new Vector3(forward.z, 0, -forward.x);
            bool displaced = state.Solution.Established &&
                HorizontalDistance(state.SolutionTargetPosition.ToLocalPosition(), target.transform.position) > resetMovement.Value;
            if (state.Target != target || displaced ||
                (state.Solution.Established && Time.time - state.LastImpactTime > 180f))
            {
                state.Target = target;
                float sign = UnityEngine.Random.value < .5f ? -1f : 1f;
                state.Solution.Reset(target.GetInstanceID(), sign * Mathf.Clamp(range * .035f, 150f, 600f),
                    UnityEngine.Random.Range(-1f, 1f) * Mathf.Clamp(range * .01f, 40f, 150f));
            }
            var solution = state.Solution;
            bool illuminate = CoastalIllumination.ShouldFireFlare(gun, target);
            if (!illuminate && CoastalIllumination.MustIlluminate(target)) return false;
            var flarePlan = illuminate ? CoastalIllumination.GetPlan(gun, target) : null;
            if (illuminate && (flarePlan == null || !flarePlan.Reachable)) return false;
            int sequence = illuminate ? 0 : solution.Fire();
            bool spotting = !illuminate && !solution.Established;
            var shot = new Shot { State = state, Target = target, Generation = solution.Generation,
                Sequence = sequence, Spotting = spotting, Forward = forward, Right = right,
                Color = SpotColors[Mathf.Max(0, sequence - 1) % SpotColors.Length], Simulation = __instance,
                Illumination = illuminate, FlarePlan = flarePlan, LaunchTime = Time.time };
            if (special && !spotting && !illuminate)
            {
                shot.Payload = SpecialShells.Prepare(gun, target, muzzle.position, inheritedVelocity);
                tracer = true;
                tracerColor = SpecialShells.ShellColor(shot.Payload.Kind) * 32f;
                tracerColor.a = 1f;
            }
            Vector2 dispersion = spotting ? Vector2.zero : UnityEngine.Random.insideUnitCircle * normalDispersion.Value;
            bool topAttack = shot.Payload != null && shot.Payload.Kind == SpecialShellKind.TopAttack;
            if (topAttack) { inheritedVelocity = shot.Payload.Delivery.LaunchVelocity; spread = 0f; }
            else if (artillery && !illuminate)
            {
                if (!CoastalArtillery.AdjustLaunch(gun, target, muzzle, right,
                    solution.ForwardBias + dispersion.x, solution.RightBias + dispersion.y, ref inheritedVelocity)) return false;
            }
            else inheritedVelocity = OffsetVelocity(inheritedVelocity, right, range,
                solution.ForwardBias + dispersion.x, solution.RightBias + dispersion.y);
            if (spotting)
            {
                tracer = true;
                tracerColor = shot.Color * 32f;
                tracerColor.a = 1f;
                tracerSize = Mathf.Max(tracerSize, 8f);
                spread = Mathf.Min(spread, .1f); // Ranging error is controlled rather than random muzzle jitter.
            }
            if (illuminate)
            {
                inheritedVelocity = flarePlan.LaunchVelocity;
                spread = 0f;
                tracer = true;
                tracerColor = new Color(32f, 22f, 6f, 1f);
                CoastalIllumination.Launched(flarePlan);
            }
            if (!special && !spotting && !illuminate)
            {
                shot.APInfo = ArmorPiercingShells.Prepare(gun, target, inheritedVelocity);
                if (shot.APInfo) { shot.APImpactAudio = ArmorPiercingShells.ImpactAudio(gun); tracer = true; tracerColor = ArmorPiercingShells.TracerColor; }
            }
            __state = new Pending { Previous = pending, Shot = shot, Owner = gun.attachedUnit, Info = gun.info };
            pending = __state;
            return true;
        }

        private static GunState GetState(Gun gun)
        {
            GunState state;
            if (!Guns.TryGetValue(gun, out state))
            {
                state = new GunState();
                state.SpottingInfo = UnityEngine.Object.Instantiate(gun.info);
                state.SpottingInfo.name = gun.info.name + "_NonDamagingCarrier";
                state.SpottingInfo.pierceDamage = state.SpottingInfo.blastDamage = 0f;
                Guns.Add(gun, state);
            }
            state.SpottingInfo.gravMult = gun.info.gravMult;
            state.SpottingInfo.dragCoef = gun.info.dragCoef;
            state.SpottingInfo.muzzleVelocity = gun.info.muzzleVelocity;
            return state;
        }

        public static Vector3 OffsetVelocity(Vector3 velocity, Vector3 right, float range, float forwardError, float rightError)
        {
            float speed = velocity.magnitude;
            if (speed < 1f) return velocity;
            float elevation = Mathf.Asin(Mathf.Clamp(velocity.y / speed, -1f, 1f));
            float derivative = 2f * speed * speed / 9.81f * Mathf.Cos(2f * elevation);
            if (Mathf.Abs(derivative) < 1000f) derivative = derivative < 0 ? -1000f : 1000f;
            float pitch = Mathf.Clamp(forwardError / derivative * Mathf.Rad2Deg, -2f, 2f);
            float yaw = Mathf.Atan2(rightError, Mathf.Max(range, 100f)) * Mathf.Rad2Deg;
            Vector3 adjusted = Quaternion.AngleAxis(-pitch, right) * velocity;
            return Quaternion.AngleAxis(yaw, Vector3.up) * adjusted;
        }

        private static Exception AfterAdd(Exception __exception, Pending __state)
        {
            if (__exception != null && __state != null) ArmorPiercingShells.Release(__state.Shot.APInfo);
            if (__exception != null && __state != null && __state.Shot.Illumination)
                CoastalIllumination.Ended(__state.Shot.FlarePlan);
            if (__exception != null && __state != null) SpecialShells.Cancel(__state.Shot.Payload);
            if (__state != null) pending = __state.Previous;
            return __exception;
        }

        internal static bool TryGetSpottingTint(BulletSim.Bullet bullet, Unit owner, WeaponInfo info, out Color color)
        {
            Shot shot;
            if (!Shots.TryGetValue(bullet, out shot))
                shot = pending != null && pending.Owner == owner && pending.Info == info ? pending.Shot : null;
            if (shot != null && shot.Spotting && !shot.Illumination && shot.Payload == null && !shot.APInfo)
            { color = shot.Color; return true; }
            color = Color.white; return false;
        }

        private static void BeforeTrace(BulletSim.Bullet __instance, ref WeaponInfo info, Unit owner, ref bool visualOnly,
            ref ParticleEffectManager.PrefabEffect[] impactEffects)
        {
            Shot shot;
            if (!Shots.TryGetValue(__instance, out shot))
            {
                if (pending == null || pending.Owner != owner || pending.Info != info) return;
                shot = pending.Shot;
                Shots.Add(__instance, shot);
            }
            if (shot.Payload != null) impactEffects = SpecialShells.NoImpactEffects;
            if (shot.Spotting || shot.Illumination || shot.Payload != null)
            {
                info = shot.State.SpottingInfo;
                visualOnly = true; // Collision and visuals remain, but spotting cannot inflict damage.
            }
            else if (shot.APInfo)
            {
                info = shot.APInfo;
                __instance.explosive = false;
                __instance.proximityFuse = false;
                if (shot.APEffects == null)
                {
                    shot.APEffects = new ParticleEffectManager.PrefabEffect[4];
                    // Native index 2 is water. Retain splashes; replace HE land/armor visuals.
                    if (impactEffects != null && impactEffects.Length > 2) shot.APEffects[2] = impactEffects[2];
                }
                impactEffects = shot.APEffects;
            }
        }

        private static void AfterTrace(BulletSim.Bullet __instance)
        {
            Shot shot;
            if (!Shots.TryGetValue(__instance, out shot)) return;
            Vector3 position = __instance.position.ToLocalPosition();
            if (shot.APInfo)
            {
                if (__instance.impacted && !shot.APImpactShown)
                {
                    shot.APImpactShown = true;
                    if (position.y > Datum.LocalSeaY + .3f) ArmorPiercingShells.Impact(position, __instance.velocity, shot.APImpactAudio);
                }
                // Keep the immutable AP profile through any native ricochet.
                if (!__instance.active) Finish(__instance, shot);
                return;
            }
            if (shot.Illumination)
            {
                if (!__instance.impacted && __instance.active &&
                    CoastalIllumination.AtDeployment(__instance.position, __instance.velocity, shot.FlarePlan, Time.time - shot.LaunchTime))
                {
                    if (CoastalIllumination.Deploy(__instance.position, __instance.velocity, shot.FlarePlan))
                        __instance.active = false;
                }
                if (__instance.impacted || !__instance.active) Finish(__instance, shot);
                return;
            }
            if (shot.Payload != null)
            {
                if (__instance.impacted || (__instance.active && SpecialShells.Fuse(shot.Payload, position, __instance.velocity, Time.time)))
                {
                    SpecialShells.Release(shot.Payload, __instance.position, __instance.velocity);
                    __instance.active = false;
                }
                if (__instance.impacted || !__instance.active) Finish(__instance, shot);
                return;
            }
            if (shot.Spotting)
            {
                if (!shot.Trail) shot.Trail = CreateTrail(position, shot.Color);
                if (shot.Trail) shot.Trail.Sample(__instance.position, Time.time);
            }
            if (__instance.impacted && shot.Spotting && shot.Target && shot.State.Target == shot.Target)
            {
                Vector3 error = position - shot.Target.transform.position;
                float miss = MissFromHull(shot.Target, position);
                if (shot.State.Solution.Observe(shot.Generation, shot.Sequence,
                    Vector3.Dot(error, shot.Forward), Vector3.Dot(error, shot.Right), miss, closeEnough.Value, minimumShots.Value))
                {
                    shot.State.SolutionTargetPosition = shot.Target.transform.position.ToGlobalPosition();
                    log.LogInfo("Coastal ranging solution established after spotting round " + shot.Sequence +
                        "; miss " + miss.ToString("F1") + " m. Normal shells enabled.");
                }
                if (shot.Generation == shot.State.Solution.Generation) shot.State.LastImpactTime = Time.time;
            }
            if (__instance.impacted || !__instance.active) Finish(__instance, shot);
        }

        private static float MissFromHull(Unit target, Vector3 position)
        {
            float miss = HorizontalDistance(position, target.transform.position);
            foreach (var collider in target.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Vector3 projected = position;
                projected.y = collider.bounds.center.y;
                miss = Mathf.Min(miss, HorizontalDistance(position, collider.bounds.ClosestPoint(projected)));
            }
            return miss;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static SpottingTrail CreateTrail(Vector3 position, Color color)
        {
            var trail = BuildTrail(position, color, trailSeconds.Value);
            if (trail) {
                // Boost the shader tint rather than clamping HDR colors into the
                // generated line's vertex-color channel. Other payloads retain
                // their own trail brightness and lifetime.
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_Color", new Color(3f, 3f, 3f, 1f));
                properties.SetColor("_BaseColor", new Color(3f, 3f, 3f, 1f));
                trail.GetComponent<LineRenderer>().SetPropertyBlock(properties);
            }
            return trail;
        }

        internal static SpottingTrail BuildTrail(Vector3 position, Color color, float lifetime)
        {
            if (!trailMaterial)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!shader) { log.LogWarning("No compatible colored-trail shader found."); return null; }
                trailMaterial = new Material(shader) { name = "Coastal spotting trails" };
            }
            return TrailService.Rent(position,color,lifetime,trailMaterial);
        }

        private static void BeforeRemove(BulletSim.Bullet __instance)
        {
            Shot shot;
            if (Shots.TryGetValue(__instance, out shot)) Finish(__instance, shot);
        }

        private static void Finish(BulletSim.Bullet bullet, Shot shot)
        {
            Shots.Remove(bullet);
            ArmorPiercingShells.Release(shot.APInfo);
            SpecialShells.Cancel(shot.Payload);
            if (shot.Illumination) CoastalIllumination.Ended(shot.FlarePlan);
            if (shot.Trail)
            {
                shot.Trail.Freeze();
                shot.Trail = null;
            }
            Shots.Remove(bullet);
        }

        private static void CleanupDestroyedGuns()
        {
            var dead=RemovalBuffer<KeyValuePair<BulletSim.Bullet,Shot>>.Rent();try{foreach(var pair in Shots)if(!pair.Value.Simulation)dead.Add(pair);
                foreach(var pair in dead)if(Shots.ContainsKey(pair.Key)){Shots.Remove(pair.Key);Finish(pair.Key,pair.Value);}
            }finally{RemovalBuffer<KeyValuePair<BulletSim.Bullet,Shot>>.Return(dead);}
        }
        internal static void Maintain()
        {
            CleanupDestroyedGuns();var dead=RemovalBuffer<Gun>.Rent();
            try{foreach(var pair in Guns)if(!pair.Key){bool used=false;foreach(var shot in Shots.Values)if(shot.State==pair.Value){used=true;break;}if(!used)dead.Add(pair.Key);}
                foreach(var gun in dead){var state=Guns[gun];Guns.Remove(gun);if(state.SpottingInfo)UnityEngine.Object.Destroy(state.SpottingInfo);}
            }finally{RemovalBuffer<Gun>.Return(dead);}
        }

        internal static void Shutdown()
        {
            foreach (var shot in Shots.Values) if (shot.Trail) UnityEngine.Object.Destroy(shot.Trail.gameObject);
            foreach (var state in Guns.Values) if (state.SpottingInfo) UnityEngine.Object.Destroy(state.SpottingInfo);
            Shots.Clear();
            ArmorPiercingShells.Shutdown();
            Guns.Clear();
            pending = null;
            if (trailMaterial) UnityEngine.Object.Destroy(trailMaterial);
            trailMaterial = null;
            CoastalIllumination.Shutdown();
        }
    }

}
