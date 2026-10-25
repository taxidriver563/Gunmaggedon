using System.Collections.Generic;
using System;
using System.Reflection.Emit;
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
        private static readonly List<Gun> GunBuffer = new List<Gun>();
        private static ConfigEntry<float> elevation;
        internal static void Maintain()
        {
            var dead=RemovalBuffer<Gun>.Rent();try{foreach(var pair in Plans)if(!pair.Key)dead.Add(pair.Key);foreach(var gun in dead)Plans.Remove(gun);}finally{RemovalBuffer<Gun>.Return(dead);}
            var bindings=RemovalBuffer<AimSolver>.Rent();try{foreach(var pair in Bindings)if(pair.Key==null||!pair.Value.Owner||!pair.Value.Gun)bindings.Add(pair.Key);foreach(var key in bindings)Bindings.Remove(key);}finally{RemovalBuffer<AimSolver>.Return(bindings);}
        }
        internal static float PreferredElevation { get { return elevation == null ? 23f : elevation.Value; } }
        internal const float OpticalScanHeight = 17.5f;
#if GUNMAGGEDON_DIAGNOSTICS
        internal static string DiagnosticPlan(Gun gun)
        {
            Plan plan;
            return Plans.TryGetValue(gun, out plan) ? "reachable=" + plan.Reachable + "/age=" + (Time.time - plan.Created).ToString("F1") : "none";
        }
#endif

        internal static void Install(Harmony harmony, ConfigFile config)
        {
            elevation = config.Bind("Artillery", "PreferredElevationDegrees", 23f, new ConfigDescription(
                "Preferred combat elevation, between 20 and 25 degrees. Very close targets use a shallower safe arc; illumination shells can aim higher.", new AcceptableValueRange<float>(20f, 25f)));
            harmony.Patch(AccessTools.Method(typeof(AimSolver), "GetAimVector"),
                prefix: new HarmonyMethod(typeof(CoastalArtillery), nameof(Aim)));
            harmony.Patch(AccessTools.Method(typeof(Turret), "FixedUpdate"),
                transpiler: new HarmonyMethod(typeof(CoastalArtillery), nameof(MuzzleClearance)));
            harmony.Patch(AccessTools.Method(typeof(TargetDetector), "TargetDetector_OnInitialize"),
                prefix: new HarmonyMethod(typeof(CoastalArtillery), nameof(ClearScanner)));
        }

        private static void ClearScanner(TargetDetector __instance)
        {
            var owner = __instance.GetAttachedUnit();
            if (!(owner is Building)) return;
            bool coastal = false;
            GunBuffer.Clear();
            try
            {
                owner.GetComponentsInChildren<Gun>(true, GunBuffer);
                foreach (var gun in GunBuffer)
                    if (IsSupported(gun) && gun.attachedUnit == owner) { coastal = true; break; }
            }
            finally { GunBuffer.Clear(); }
            if (!coastal) return;
            var scanner = __instance.GetScanPoint();
            if (!scanner || scanner == owner.transform || !scanner.IsChildOf(owner.transform)) return;
            // Building initialization places all hitboxes on the native Statics
            // layer. At combat elevation the gun body crosses the old 7.14m
            // scan origin's view. Keep optics above the complete pitch envelope,
            // including vertical aim; retain native range, cadence and LOS jobs.
            var position = owner.transform.InverseTransformPoint(scanner.position);
            position.y = Mathf.Max(position.y, OpticalScanHeight);
            scanner.position = owner.transform.TransformPoint(position);
        }

        // The stock check starts at the pitch pivot, inside our articulated gun.
        // Preserve the native firing gates; change only this clearance segment.
        private static IEnumerable<CodeInstruction> MuzzleClearance(IEnumerable<CodeInstruction> instructions)
        {
            var linecast = AccessTools.Method(typeof(Physics), "Linecast", new[] {
                typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(int) });
            var replacement = AccessTools.Method(typeof(CoastalArtillery), nameof(CheckClearance));
            int replaced = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(linecast))
                {
                    var owner = new CodeInstruction(OpCodes.Ldarg_0);
                    owner.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    owner.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return owner;
                    instruction.opcode = OpCodes.Call; instruction.operand = replacement;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Native turret clearance contract changed; expected one Linecast.");
        }

        private static bool CheckClearance(Vector3 start, Vector3 end, out RaycastHit hit, int mask, Turret turret)
        {
            var gun = turret ? turret.GetWeapon() as Gun : null;
            if (!IsSupported(gun)) return Physics.Linecast(start, end, out hit, mask);
            var muzzles = Muzzles.GetValue(gun) as Transform[];
            if (muzzles == null || muzzles.Length == 0 || !muzzles[0])
                return Physics.Linecast(start, end, out hit, mask);
            var muzzle = muzzles[0];
            float length = Vector3.Distance(start, end);
            bool blocked = muzzle.gameObject.scene.GetPhysicsScene().Raycast(muzzle.position, muzzle.forward,
                out hit, length, mask, QueryTriggerInteraction.UseGlobal);
            // Native code compares this distance with the range from the pivot.
            if (blocked) hit.distance += Vector3.Distance(start, muzzle.position);
            return blocked;
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

        internal static void Shutdown() { Plans.Clear(); Bindings.Clear(); GunBuffer.Clear(); }
    }
}
