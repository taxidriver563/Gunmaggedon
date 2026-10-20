using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace GunmageddonAudio
{
    internal static class CoastalIllumination
    {
        internal sealed class Plan
        {
            internal Gun Gun;
            internal Unit Target;
            internal GlobalPosition EstimatedPosition;
            internal GlobalPosition KnownPosition;
            internal Vector3 EstimatedVelocity;
            internal Vector3 LaunchVelocity;
            internal float FlightSeconds, DeploymentAltitude, Created, LastLaunch = -1000f;
            internal bool Reachable;
            internal float ForecastUntil = -1f, ForecastHorizon;
            internal int ForecastRevision = -1;
            internal bool ForecastResult;
        }
        private static readonly Dictionary<Gun, Plan> Plans = new Dictionary<Gun, Plan>();
        private sealed class Motion
        {
            internal Unit Target;
            internal GlobalPosition Position;
            internal Vector3 Velocity;
            internal float SampleAt, PollAt;
            internal int Measurements;
        }
        private static readonly Dictionary<Gun, Motion> Observations = new Dictionary<Gun, Motion>();
        private static readonly List<ParachuteIllumination> Flares = new List<ParachuteIllumination>();
        private sealed class FlareToken
        {
            internal Gun Gun;
            internal object Team;
            internal Plan Plan;
            internal float Expires, Ready;
            internal bool InFlight;
        }
        private static readonly List<FlareToken> Tokens = new List<FlareToken>();
        private static readonly Dictionary<Gun, float> RetryAfter = new Dictionary<Gun, float>();
        private static int coverageRevision;
        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<float> lightThreshold, deploymentHeight, burnSeconds, lightRange, descentSpeed;
        private static ConfigEntry<float> handoffSeconds, aimAheadSeconds;
        private static ManualLogSource log;
        private static Material glow;
        private static Parachute nativeParachute;
        private static readonly System.Reflection.FieldInfo Muzzles = AccessTools.Field(typeof(Gun), "muzzles");
        private static readonly System.Reflection.FieldInfo Clock = AccessTools.Field(typeof(LevelInfo), "timeOfDay");

        internal static void Configure(ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            enabled = config.Bind("Illumination", "Enabled", true, "Fire parachute illumination shells before engaging surface targets in low light.");
            lightThreshold = config.Bind("Illumination", "DaylightThreshold", .15f, new ConfigDescription(
                "Low-light threshold, additionally limited to dark clock conditions. Cloud cover alone cannot authorize daytime flares; effective threshold is capped at 0.15.", new AcceptableValueRange<float>(.05f, .9f)));
            deploymentHeight = config.Bind("Illumination", "DeploymentHeightMetres", 600f, new ConfigDescription(
                "Aim above the estimated ship position so the parachute flare has time to descend.", new AcceptableValueRange<float>(250f, 1000f)));
            burnSeconds = config.Bind("Illumination", "BurnSeconds", 120f, new ConfigDescription(
                "Parachute flare burn duration.", new AcceptableValueRange<float>(30f, 240f)));
            lightRange = config.Bind("Illumination", "LightRangeMetres", 1000f, new ConfigDescription(
                "Actual point-light range; a ship must be inside it with a clear light path before firing resumes.", new AcceptableValueRange<float>(700f, 1800f)));
            descentSpeed = config.Bind("Illumination", "DescentMetresPerSecond", 2.5f, new ConfigDescription(
                "Descent speed under the parachute.", new AcceptableValueRange<float>(1f, 6f)));
            handoffSeconds = config.Bind("Illumination", "HandoffMarginSeconds", 6f, new ConfigDescription(
                "Request replacement illumination this far before predicted coverage loss, in addition to shell flight and canopy opening time.", new AcceptableValueRange<float>(2f, 15f)));
            aimAheadSeconds = config.Bind("Illumination", "AimAheadSeconds", 20f, new ConfigDescription(
                "Place illumination ahead of observed ship movement after shell arrival. Lead is limited to keep the ship inside the new light at ignition.", new AcceptableValueRange<float>(0f, 40f)));
        }

        internal static bool NeedsLight(Unit target)
        {
            if (enabled == null || !enabled.Value || !target || !LevelInfo.i) return false;
            if (!(target is Ship) && !(target is GroundVehicle) && !(target is Building)) return false;
            return IsLowLight(LevelInfo.i, target.transform.position, lightThreshold.Value);
        }

        internal static bool IsLowLight(LevelInfo level, Vector3 position, float threshold)
        {
            if (!level) return false;
            float hour = Mathf.Repeat((float)Clock.GetValue(level), 24f);
            // Match the native clock's dawn/dusk ramp before its cloud multiplier.
            // Heavy overcast drives GetDaylightFactor to zero even at noon.
            float clockLight = Mathf.Clamp01(Mathf.Min((hour - 5.5f) * 2f, (18.5f - hour) * 2f));
            float darkness = Mathf.Min(threshold, .15f);
            return clockLight < darkness && level.GetDaylightFactor(position) < darkness;
        }

        internal static bool IsIlluminated(Unit target)
        {
            Flares.RemoveAll(f => !f);
            foreach (var flare in Flares)
                if (flare.Illuminates(target)) return true;
            return false;
        }

        internal static bool MustIlluminate(Unit target) { return NeedsLight(target) && !IsIlluminated(target); }

        internal static bool CanLaunch(Gun gun, Unit target, out Plan plan)
        {
            plan = GetPlan(gun, target);
            return ShouldFireFlare(gun, target);
        }

        internal static bool ShouldFireFlare(Gun gun, Unit target)
        {
            if (!gun || !gun.attachedUnit || !gun.attachedUnit.LocalSim) return false;
            if (!NeedsLight(target)) { ReleaseReservation(gun); return false; }
            var plan = GetPlan(gun, target);
            if (plan == null || !plan.Reachable) { ReleaseReservation(gun); return false; }
            float horizon = plan.FlightSeconds + ParachuteMotion.OpeningSeconds + (handoffSeconds == null ? 6f : handoffSeconds.Value);
            // Check the path as well as its endpoint: different lights may cover the
            // endpoints while leaving an unlit gap between them.
            if (ForecastCoveredCached(plan, horizon)) { ReleaseReservation(gun); return false; }
            return RequestToken(gun, plan, Time.time);
        }

        internal static bool ForecastCovered(Plan plan, float horizon)
        {
            Flares.RemoveAll(f => !f);
            for (int i = 0; i <= 4; i++)
            {
                float future = Mathf.Lerp(ParachuteMotion.OpeningSeconds, Mathf.Max(ParachuteMotion.OpeningSeconds, horizon), i / 4f);
                Vector3 predicted = plan.KnownPosition.ToLocalPosition() + plan.EstimatedVelocity * future;
                if (!Flares.Any(f => f.CoversPrediction(predicted, plan.Target, future))) return false;
            }
            return true;
        }

        internal static bool ForecastCoveredCached(Plan plan, float horizon)
        {
            if (Time.time < plan.ForecastUntil && plan.ForecastRevision == coverageRevision && Mathf.Abs(plan.ForecastHorizon - horizon) < .01f)
                return plan.ForecastResult;
            plan.ForecastResult = ForecastCovered(plan, horizon);
            plan.ForecastRevision = coverageRevision; plan.ForecastHorizon = horizon;
            plan.ForecastUntil = Time.time + .25f;
            return plan.ForecastResult;
        }

        internal static void CoverageChanged() { coverageRevision++; }

        private static object TeamFor(Gun gun)
        {
            return gun && gun.attachedUnit ? (object)gun.attachedUnit.NetworkHQ ?? gun.attachedUnit : null;
        }

        private static void ReleaseReservation(Gun gun)
        {
            Tokens.RemoveAll(t => t.Gun == gun && !t.InFlight);
        }

        internal static bool RequestToken(Gun gun, Plan plan, float now)
        {
            foreach (var token in Tokens.Where(t => t.Expires <= now || (!t.InFlight && !t.Gun)).ToArray())
            {
                if (!token.InFlight && token.Gun) RetryAfter[token.Gun] = now + 5f;
                Tokens.Remove(token);
            }
            foreach (var old in RetryAfter.Keys.Where(g => !g).ToArray()) RetryAfter.Remove(old);
            float retry;
            if (RetryAfter.TryGetValue(gun, out retry) && now < retry) return false;
            var own = Tokens.FirstOrDefault(t => t.Gun == gun);
            if (own != null)
            {
                if (own.InFlight) return false;
                own.Plan = plan; own.Ready = now + plan.FlightSeconds + ParachuteMotion.OpeningSeconds;
                return true; // Aiming keeps its token, but cannot extend its 12-second lease indefinitely.
            }
            object team = TeamFor(gun);
            if (Tokens.Any(t => ReferenceEquals(t.Team, team) && TokenCovers(t.Plan, plan, Mathf.Max(0f, t.Ready - now)))) return false;
            if (now - plan.LastLaunch < 6f) return false;
            Tokens.Add(new FlareToken { Gun = gun, Team = team, Plan = plan, Expires = now + 12f,
                Ready = now + plan.FlightSeconds + ParachuteMotion.OpeningSeconds });
            return true;
        }

        private static bool TokenCovers(Plan reserved, Plan requested, float readyIn)
        {
            // Different batteries may have differently aged motion estimates for
            // one ship. That must not allocate several simultaneous flare shots.
            if (reserved.Target && reserved.Target == requested.Target) return true;
            Vector3 center = reserved.EstimatedPosition.ToLocalPosition() + Vector3.up * reserved.DeploymentAltitude;
            float range = (lightRange == null ? 1000f : lightRange.Value) * .85f - 80f;
            float margin = handoffSeconds == null ? 6f : handoffSeconds.Value;
            // Share coverage across nearby ships, not just identical target IDs.
            return Vector3.Distance(center, requested.KnownPosition.ToLocalPosition() + requested.EstimatedVelocity * readyIn + Vector3.up * 8f) <= range &&
                Vector3.Distance(center, requested.KnownPosition.ToLocalPosition() + requested.EstimatedVelocity * (readyIn + margin) + Vector3.up * 8f) <= range;
        }

        internal static Plan GetPlan(Gun gun, Unit target)
        {
            if (!gun || !target) return null;
            foreach (var old in Plans.Keys.Where(g => !g).ToArray()) Plans.Remove(old);
            var muzzles = Muzzles.GetValue(gun) as Transform[];
            if (muzzles == null || muzzles.Length == 0 || !muzzles[0]) return null;
            Plan previous;
            Plans.TryGetValue(gun, out previous);
            if (previous != null && previous.Target == target && Time.time - previous.Created < 2f) return previous;
            var plan = new Plan { Gun = gun, Target = target, Created = Time.time,
                LastLaunch = previous != null && previous.Target == target ? previous.LastLaunch : -1000f };
            Vector3 from = muzzles[0].position;
            Observe(gun, target);
            Motion observation;
            if (!Observations.TryGetValue(gun, out observation) || observation.Target != target)
            {
                Plans[gun] = plan;
                return plan; // No omniscient search of unobserved ships.
            }
            plan.KnownPosition = observation.Position;
            Vector3 estimate = observation.Position.ToLocalPosition();
            plan.EstimatedVelocity = observation.Velocity;
            // Freeze a fallible estimate at launch; never steer the shell toward the live ship afterward.
            Vector2 error = UnityEngine.Random.insideUnitCircle * 80f;
            Vector3 uncertainty = new Vector3(error.x, 0, error.y);
            float preferredHeight = deploymentHeight == null ? 600f : deploymentHeight.Value;
            float range = lightRange == null ? 1000f : lightRange.Value;
            foreach (float altitude in new[] { preferredHeight, 350f, 250f }.Distinct())
            {
                if (altitude > preferredHeight) continue;
                float horizontalRadius = Mathf.Sqrt(Mathf.Max(0f, Mathf.Pow(range * .85f - 100f, 2f) - altitude * altitude));
                float ahead = Mathf.Min(aimAheadSeconds == null ? 20f : aimAheadSeconds.Value,
                    horizontalRadius * .55f / Mathf.Max(1f, plan.EstimatedVelocity.magnitude));
                float flightEstimate = Vector3.Distance(from, estimate) / Mathf.Max(100f, gun.info.muzzleVelocity) * 1.5f;
                for (int iteration = 0; iteration < 5; iteration++)
                {
                    Vector3 predicted = estimate + uncertainty + plan.EstimatedVelocity * (flightEstimate + ahead);
                    Vector3 flat = predicted - from; flat.y = 0f;
                    float angle, flight, charge;
                    if (!ArtilleryBallistics.Solve(flat.magnitude, predicted.y + altitude - from.y, gun.info.muzzleVelocity,
                        gun.info.dragCoef, 9.81f * gun.info.gravMult, Mathf.Max(40f, CoastalArtillery.PreferredElevation),
                        Mathf.Min(gun.info.muzzleVelocity, Mathf.Max(180f, gun.info.muzzleVelocity * .22f)),
                        Time.fixedDeltaTime, out angle, out charge, out flight)) { plan.Reachable = false; break; }
                    float radians = angle * Mathf.Deg2Rad;
                    plan.LaunchVelocity = (flat.normalized * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians)) * charge;
                    plan.EstimatedPosition = predicted.ToGlobalPosition();
                    plan.FlightSeconds = flight;
                    plan.DeploymentAltitude = altitude;
                    plan.Reachable = true;
                    if (Mathf.Abs(flight - flightEstimate) < .05f) break;
                    flightEstimate = flight;
                }
                if (plan.Reachable) break;
            }
            Plans[gun] = plan;
            return plan;
        }

        internal static void Observe(Gun gun, Unit target)
        {
            if (!gun || !target || !gun.attachedUnit) return;
            Motion previous;
            Observations.TryGetValue(gun, out previous);
            if (previous != null && previous.Target == target && Time.time - previous.PollAt < .5f) return;
            var hq = gun.attachedUnit.NetworkHQ; GlobalPosition known;
            if (!hq || !hq.TryGetKnownPosition(target, out known)) { Observations.Remove(gun); return; }
            if (previous == null || previous.Target != target)
            {
                Observations[gun] = new Motion { Target = target, Position = known, SampleAt = Time.time, PollAt = Time.time };
                return;
            }
            previous.PollAt = Time.time;
            Vector3 movement = known.ToLocalPosition() - previous.Position.ToLocalPosition(); movement.y = 0f;
            if (movement.sqrMagnitude > .0001f)
            {
                var measured = Vector3.ClampMagnitude(movement / Mathf.Max(.1f, Time.time - previous.SampleAt), 60f);
                previous.Velocity = previous.Measurements++ == 0 ? measured : Vector3.Lerp(previous.Velocity, measured, .35f);
                previous.Position = known; previous.SampleAt = Time.time;
            }
            else if (Time.time - previous.SampleAt > 5f) previous.Velocity = Vector3.zero;
        }

        internal static void Launched(Plan plan)
        {
            plan.LastLaunch = Time.time;
            var token = Tokens.FirstOrDefault(t => t.Plan == plan);
            if (token == null) return;
            token.InFlight = true;
            token.Ready = Time.time + plan.FlightSeconds + ParachuteMotion.OpeningSeconds;
            token.Expires = token.Ready + 12f;
        }

        internal static void Ended(Plan plan) { Tokens.RemoveAll(t => t.Plan == plan); }

        internal static bool AtDeployment(GlobalPosition position, Vector3 velocity, Plan plan, float age)
        {
            Vector3 point = position.ToLocalPosition(), aim = plan.EstimatedPosition.ToLocalPosition();
            Vector3 flat = point + ParachuteMotion.PredictCoast(velocity) - aim;
            flat.y = 0;
            return point.y - aim.y >= Mathf.Max(150f, plan.DeploymentAltitude * .55f) &&
                (flat.magnitude <= 180f || (age >= plan.FlightSeconds && flat.magnitude <= 500f));
        }

        internal static bool Deploy(GlobalPosition position, Vector3 velocity, Plan plan = null)
        {
            if (!EnsureVisuals()) return false;
            var go = new GameObject("Coastal parachute illumination flare");
            go.transform.SetParent(Datum.origin, false);
            var flare = go.AddComponent<ParachuteIllumination>();
            flare.Initialize(position, velocity, burnSeconds.Value, lightRange.Value, descentSpeed.Value, glow, nativeParachute);
            Flares.Add(flare);
            CoverageChanged();
            Ended(plan);
            log.LogInfo("Native parachute flare opening; fire remains gated by actual target illumination.");
            return true;
        }

        private static bool EnsureVisuals()
        {
            if (!nativeParachute)
            {
                // Resolve installed assets without bundling game models or textures.
                var assets = GameAssets.i;
                var pilot = assets ? AccessTools.Field(typeof(GameAssets), "pilotDismounted").GetValue(assets) as GameObject : null;
                if (pilot) nativeParachute = pilot.GetComponentInChildren<Parachute>(true);
                if (!nativeParachute)
                    nativeParachute = Resources.FindObjectsOfTypeAll<Parachute>().FirstOrDefault(p => p &&
                        p.GetComponentsInChildren<MeshFilter>(true).Any(m => m.sharedMesh && m.sharedMesh.name == "parachute1_canopy"));
            }
            if (!nativeParachute) return false;
            if (!glow)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                glow = new Material(shader) { name = "Coastal illumination glow", color = new Color(8f, 5f, 1.5f, 1f) };
            }
            return true;
        }

        internal static Parachute GetNativeParachute() { return EnsureVisuals() ? nativeParachute : null; }

        internal static void Shutdown()
        {
            foreach (var flare in Flares) if (flare) UnityEngine.Object.Destroy(flare.gameObject);
            Flares.Clear();
            CoverageChanged();
            Plans.Clear();
            Observations.Clear();
            Tokens.Clear(); RetryAfter.Clear();
            if (glow) UnityEngine.Object.Destroy(glow);
            glow = null;
            nativeParachute = null;
        }
    }

    public sealed class ParachuteIllumination : MonoBehaviour
    {
        private GlobalPosition position;
        private float born, lifetime, descent;
        private Light illumination;
        private Transform canopyObject, linesObject;
        private Material parachuteMaterial;
        private AnimationCurve horizontal, vertical, wrinkles;
        private Vector3 velocity;
        private Quaternion chuteRotation;
        private const float LineLength = 8.5f;

        public void Initialize(GlobalPosition location, Vector3 launchVelocity, float seconds, float range, float speed, Material glow, Parachute source)
        {
            position = location;
            velocity = launchVelocity;
            chuteRotation = Quaternion.LookRotation(launchVelocity.sqrMagnitude > 1f ? launchVelocity.normalized : Vector3.down);
            born = Time.time;
            lifetime = seconds;
            descent = speed;
            transform.position = position.ToLocalPosition();
            illumination = gameObject.AddComponent<Light>();
            illumination.type = LightType.Point;
            illumination.color = new Color(1f, .8f, .45f);
            illumination.range = range;
            illumination.intensity = 0f;
            illumination.shadows = LightShadows.None;
            illumination.renderMode = LightRenderMode.ForcePixel;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Burning flare";
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * .8f;
            if (Application.isPlaying) Destroy(body.GetComponent<Collider>());
            else DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = glow;
            var nativeCanopy = (GameObject)AccessTools.Field(typeof(Parachute), "canopy").GetValue(source);
            var nativeLines = (GameObject)AccessTools.Field(typeof(Parachute), "lines").GetValue(source);
            parachuteMaterial = new Material(nativeCanopy.GetComponent<Renderer>().sharedMaterial);
            horizontal = (AnimationCurve)AccessTools.Field(typeof(Parachute), "chuteScaleHorizontal").GetValue(source);
            vertical = (AnimationCurve)AccessTools.Field(typeof(Parachute), "chuteScaleVertical").GetValue(source);
            wrinkles = (AnimationCurve)AccessTools.Field(typeof(Parachute), "wrinkleStrength").GetValue(source);
            canopyObject = CreateNativeMesh("Native parachute canopy", nativeCanopy);
            linesObject = CreateNativeMesh("Native parachute suspension lines", nativeLines);
            UpdateCanopy(0f);
        }

        private Transform CreateNativeMesh(string label, GameObject source)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = parachuteMaterial;
            return go.transform;
        }

        private void UpdateCanopy(float age)
        {
            // Native canopy extends along -Z; line mesh runs from payload to its +Z rim.
            Vector3 direction = velocity.sqrMagnitude > .01f ? velocity.normalized : Vector3.down;
            chuteRotation = Quaternion.Slerp(chuteRotation, Quaternion.LookRotation(direction),
                1f - Mathf.Exp(-Time.deltaTime * 4f));
            Vector3 offset = chuteRotation * (Vector3.back * LineLength);
            canopyObject.localPosition = offset;
            canopyObject.localRotation = chuteRotation;
            linesObject.localPosition = Vector3.zero;
            linesObject.localRotation = chuteRotation * Quaternion.Euler(0f, 180f, 0f);
            float width = horizontal.Evaluate(age);
            canopyObject.localScale = new Vector3(width, width, vertical.Evaluate(age));
            linesObject.localScale = new Vector3(width, width, LineLength);
            parachuteMaterial.SetFloat("_wrinkleStrength", wrinkles.Evaluate(age));
            parachuteMaterial.SetFloat("_wrinkleDisplacement", Mathf.Sin(age * 5f) * .05f);
        }

        private void OnDestroy()
        {
            CoastalIllumination.CoverageChanged();
            if (!parachuteMaterial) return;
            if (Application.isPlaying) Destroy(parachuteMaterial);
            else DestroyImmediate(parachuteMaterial);
        }

        public bool Illuminates(Unit target)
        {
            if (!target || !illumination || !illumination.isActiveAndEnabled || illumination.intensity < 5f ||
                Time.time - born < .8f || Time.time - born > lifetime - 3f) return false;
            Vector3 destination = target.transform.position + Vector3.up * 8f;
            if (Vector3.Distance(transform.position, destination) > illumination.range * .85f) return false;
            RaycastHit hit;
            return !Physics.Linecast(transform.position, destination, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<Unit>() == target;
        }

        internal bool CoversPrediction(Vector3 destination, Unit target, float seconds)
        {
            float age = Time.time - born;
            if (!illumination || !illumination.isActiveAndEnabled || age + seconds < .8f || age + seconds > lifetime - 5f) return false;
            Vector3 wind = LevelInfo.i ? LevelInfo.i.NetworkwindVelocity * .15f : Vector3.zero;
            Vector3 terminal = wind + Vector3.down * descent;
            Vector3 predictedVelocity = velocity;
            float opening = Mathf.Min(seconds, Mathf.Max(0f, 6f - age));
            Vector3 predicted = position.ToLocalPosition() + ParachuteMotion.Advance(ref predictedVelocity, terminal, age, opening)
                + terminal * (seconds - opening);
            if (predicted.y <= Datum.LocalSeaY + 5f || Vector3.Distance(predicted, destination + Vector3.up * 8f) > illumination.range * .85f - 60f) return false;
            RaycastHit hit;
            return !Physics.Linecast(predicted, destination + Vector3.up * 8f, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<Unit>() == target;
        }

        private void Update()
        {
            float age = Time.time - born;
            if (age >= lifetime) { Destroy(gameObject); return; }
            Vector3 wind = LevelInfo.i ? LevelInfo.i.NetworkwindVelocity * .15f : Vector3.zero;
            Vector3 movement = ParachuteMotion.Advance(ref velocity, wind + Vector3.down * descent,
                Mathf.Max(0f, age - Time.deltaTime), Time.deltaTime);
            position = (position.ToLocalPosition() + movement).ToGlobalPosition();
            transform.position = position.ToLocalPosition();
            float fade = Mathf.Min(Mathf.Clamp01(age), Mathf.Clamp01((lifetime - age) / 5f));
            illumination.intensity = 200000f * fade * (1f + Mathf.Sin(age * 17f) * .04f);
            UpdateCanopy(age);
            if (transform.position.y <= Datum.LocalSeaY + 5f) Destroy(gameObject);
        }
    }
}
