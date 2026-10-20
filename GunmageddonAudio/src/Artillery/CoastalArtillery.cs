using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class CoastalArtillery
    {
        internal sealed class Plan
        {
            internal Unit Target;
            internal GlobalPosition From, Aim;
            internal GlobalPosition TargetPosition;
            internal Vector3 TargetVelocity;
            internal Vector3 Velocity;
            internal float Created, FlightSeconds, Gravity, Drag, MaximumSpeed, Step;
            internal bool Reachable;
        }
        private static readonly Dictionary<Gun, Plan> Plans = new Dictionary<Gun, Plan>();
        private static readonly System.Reflection.FieldInfo Muzzles = AccessTools.Field(typeof(Gun), "muzzles");
        private static readonly System.Reflection.FieldInfo AimTransform = AccessTools.Field(typeof(AimSolver), "firingTransform");
        private static readonly System.Reflection.FieldInfo AimOwner = AccessTools.Field(typeof(AimSolver), "attachedUnit");
        private static readonly System.Reflection.FieldInfo AimInfo = AccessTools.Field(typeof(AimSolver), "weaponInfo");
        private static readonly System.Reflection.FieldInfo AimTarget = AccessTools.Field(typeof(AimSolver), "currentTarget");
        private sealed class Binding
        {
            internal Transform From;
            internal Unit Owner;
            internal WeaponInfo Info;
            internal Gun Gun;
        }
        private static readonly Dictionary<AimSolver, Binding> Bindings = new Dictionary<AimSolver, Binding>();
        private static readonly List<AimSolver> ExpiredBindings = new List<AimSolver>();
        private static readonly List<Gun> GunBuffer = new List<Gun>();
        private static float nextBindingCleanup;
        private static ConfigEntry<float> elevation;
        internal static float PreferredElevation { get { return elevation == null ? 23f : elevation.Value; } }

        internal static void Install(Harmony harmony, ConfigFile config)
        {
            elevation = config.Bind("Artillery", "PreferredElevationDegrees", 23f, new ConfigDescription(
                "Preferred combat elevation, between 20 and 25 degrees. Very close targets use a shallower safe arc; illumination shells can aim higher.", new AcceptableValueRange<float>(20f, 25f)));
            harmony.Patch(AccessTools.Method(typeof(AimSolver), "GetAimVector"),
                prefix: new HarmonyMethod(typeof(CoastalArtillery), nameof(Aim)));
        }

        internal static bool IsSupported(Gun gun)
        {
            return gun && gun.info && gun.attachedUnit is Building &&
                (gun.info.name == "203mmEmplacement" || gun.info.name == "203mmEmplacement_SPECIAL");
        }

        internal static Gun ResolveGun(AimSolver solver)
        {
            var info = AimInfo.GetValue(solver) as WeaponInfo;
            // This hook runs for stock weapons too. Reject them before hierarchy searches.
            if (!info || (info.name != "203mmEmplacement" && info.name != "203mmEmplacement_SPECIAL")) return null;
            var from = AimTransform.GetValue(solver) as Transform;
            var owner = AimOwner.GetValue(solver) as Unit;
            Binding cached;
            if (Bindings.TryGetValue(solver, out cached) && cached.From == from && cached.Owner == owner &&
                cached.Info == info && cached.Gun && cached.Gun.info == info && cached.Gun.attachedUnit == owner)
                return cached.Gun;
            if (Time.time >= nextBindingCleanup)
            {
                nextBindingCleanup = Time.time + 5f;
                ExpiredBindings.Clear();
                foreach (var pair in Bindings) if (!pair.Value.Owner || !pair.Value.Gun) ExpiredBindings.Add(pair.Key);
                foreach (var key in ExpiredBindings) Bindings.Remove(key);
                ExpiredBindings.Clear();
            }
            var gun = from ? from.GetComponentInParent<Gun>() : null;
            if (!gun && owner)
            {
                GunBuffer.Clear(); owner.GetComponentsInChildren<Gun>(false, GunBuffer);
                foreach (var candidate in GunBuffer) if (candidate.info == info && IsSupported(candidate)) { gun = candidate; break; }
                GunBuffer.Clear();
            }
            if (gun) Bindings[solver] = new Binding { From = from, Owner = owner, Info = info, Gun = gun };
            else Bindings.Remove(solver);
            return gun;
        }

        private static bool Aim(AimSolver __instance, ref float __0, ref Vector3 __result)
        {
            var gun = ResolveGun(__instance);
            if (!IsSupported(gun)) return true;
            var target = AimTarget.GetValue(__instance) as Unit;
            var from = AimTransform.GetValue(__instance) as Transform;
            if (!target || !from) return true;
            var muzzles = Muzzles.GetValue(gun) as Transform[];
            if (muzzles != null && muzzles.Length > 0 && muzzles[0]) from = muzzles[0];
            __0 = Vector3.Distance(from.position, target.transform.position);
            if (CoastalRanging.IsCoastal(gun) && CoastalIllumination.ShouldFireFlare(gun, target))
            {
                var flare = CoastalIllumination.GetPlan(gun, target);
                __result = flare != null && flare.Reachable ? flare.LaunchVelocity.normalized : from.forward;
                return false;
            }
            if (SpecialShells.WantsTopAttack(gun, target))
            {
                var delivery = CoastalIllumination.GetPlan(gun, target);
                __result = delivery != null && delivery.Reachable ? delivery.LaunchVelocity.normalized : from.forward;
                return false;
            }
            var plan = GetPlan(gun, target, from.position);
            __result = plan.Reachable ? plan.Velocity.normalized : from.forward;
            return false;
        }

        internal static bool CanFire(Gun gun, Unit target)
        {
            if (!IsSupported(gun) || !target || (CoastalRanging.IsCoastal(gun) && CoastalIllumination.ShouldFireFlare(gun, target))) return true;
            if (SpecialShells.WantsTopAttack(gun, target))
            { var delivery = CoastalIllumination.GetPlan(gun, target); return delivery != null && delivery.Reachable; }
            var muzzles = Muzzles.GetValue(gun) as Transform[];
            return muzzles != null && muzzles.Length > 0 && muzzles[0] && GetPlan(gun, target, muzzles[0].position).Reachable;
        }

        internal static Plan GetPlan(Gun gun, Unit target, Vector3 from)
        {
            CoastalIllumination.Observe(gun, target);
            float gravity = 9.81f * gun.info.gravMult;
            float step = Time.fixedDeltaTime;
            Vector3 initial = target.transform.position;
            Vector3 targetVelocity = target.rb ? Vector3.ClampMagnitude(target.rb.velocity, 60f) : Vector3.zero;
            targetVelocity.y = 0f;
            Plan cached;
            if (Plans.TryGetValue(gun, out cached) && cached.Target == target &&
                (Time.time - cached.Created < .25f ||
                    ((cached.TargetPosition.ToLocalPosition() - initial).sqrMagnitude < .0025f && (cached.TargetVelocity - targetVelocity).sqrMagnitude < .0025f)) &&
                cached.Gravity == gravity && cached.Drag == gun.info.dragCoef && cached.MaximumSpeed == gun.info.muzzleVelocity &&
                cached.Step == step && Vector3.Distance(cached.From.ToLocalPosition(), from) < 2f) return cached;
            foreach (var old in Plans.Keys.Where(g => !g).ToArray()) Plans.Remove(old);
            var plan = new Plan { Target = target, From = from.ToGlobalPosition(), Created = Time.time,
                TargetPosition = initial.ToGlobalPosition(), TargetVelocity = targetVelocity,
                Gravity = gravity, Drag = gun.info.dragCoef, MaximumSpeed = gun.info.muzzleVelocity, Step = step };
            Vector3 predicted = initial;
            for (int iteration = 0; iteration < 6; iteration++)
            {
                Vector3 velocity; float flight;
                if (!Solve(from, predicted, gun.info, step, out velocity, out flight)) { plan.Reachable = false; break; }
                plan.Velocity = velocity; plan.FlightSeconds = flight; plan.Aim = predicted.ToGlobalPosition(); plan.Reachable = true;
                Vector3 next = initial + targetVelocity * flight;
                if (Vector3.Distance(next, predicted) < 1f) break;
                predicted = next;
            }
            Plans[gun] = plan;
            return plan;
        }

        private static bool Solve(Vector3 from, Vector3 to, WeaponInfo info, float step, out Vector3 velocity, out float flight)
        {
            Vector3 flat = to - from; flat.y = 0f;
            float angle, speed;
            velocity = Vector3.zero;
            float minimum = Mathf.Min(info.muzzleVelocity, Mathf.Max(150f, info.muzzleVelocity * .18f));
            if (!ArtilleryBallistics.Solve(flat.magnitude, to.y - from.y, info.muzzleVelocity, info.dragCoef,
                9.81f * info.gravMult, PreferredElevation, minimum, step, out angle, out speed, out flight, 25f)) return false;
            float radians = angle * Mathf.Deg2Rad;
            velocity = (flat.normalized * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)) * speed;
            return true;
        }

        internal static bool AdjustLaunch(Gun gun, Unit target, Transform muzzle, Vector3 right, float forwardBias, float rightBias, ref Vector3 velocity)
        {
            var plan = GetPlan(gun, target, muzzle.position);
            if (!plan.Reachable) return false;
            Vector3 flat = plan.Aim.ToLocalPosition() - muzzle.position; flat.y = 0f;
            Vector3 aim = plan.Aim.ToLocalPosition() + flat.normalized * forwardBias + right * rightBias;
            Vector3 corrected; float flight;
            if (!Solve(muzzle.position, aim, gun.info, Time.fixedDeltaTime, out corrected, out flight)) corrected = plan.Velocity;
            // Retain the actual barrel direction/alignment error. Apply only the
            // deliberate ranging/dispersion delta, and set the matching charge.
            velocity = Quaternion.FromToRotation(plan.Velocity.normalized, corrected.normalized) * velocity.normalized * corrected.magnitude;
            return true;
        }

        internal static void Shutdown() { Plans.Clear(); Bindings.Clear(); ExpiredBindings.Clear(); GunBuffer.Clear(); nextBindingCleanup = 0f; }
    }
}
