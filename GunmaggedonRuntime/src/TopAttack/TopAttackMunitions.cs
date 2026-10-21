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
    internal static class TopAttackMunitions
    {
        private static readonly Dictionary<Gun, int> Slots = new Dictionary<Gun, int>();
        private static readonly Dictionary<Unit, float> Claims = new Dictionary<Unit, float>();
        private static readonly List<GameObject> Objects = new List<GameObject>();
        private static ConfigEntry<bool> againstBoats;
        private static ConfigEntry<float> damage;
        private static ConfigEntry<int> maximum;
        private static MissileDefinition bombDefinition;
        private static int active;
        private static ManualLogSource logger;
        internal const float SlugSpeed = 1500f;
        internal static float PierceDamage { get { return damage == null ? 15000f : damage.Value; } }

        internal static void Configure(ConfigFile config, ManualLogSource log)
        {
            logger = log;
            againstBoats = config.Bind("Top attack", "PreferAgainstSmallShips", true, "In Auto mode, favor parachute top-attack ammunition against ships up to 65 metres long. Larger ships retain cluster priority.");
            damage = config.Bind("Top attack", "SlugPierceDamage", 15000f, new ConfigDescription("Native penetrating damage, before hull armor/tolerance. 15000 gives about 60 HP on a SURF main hull; this is not 15000 hull HP.", new AcceptableValueRange<float>(100f, 40000f)));
            if (Mathf.Approximately(damage.Value, 1100f)) damage.Value = 15000f;
            maximum = config.Bind("Top attack", "MaximumActiveSubmunitions", 24, new ConfigDescription("Global budget including reserved carrier payloads. At most two per coastal gun. Full budgets fall back to cluster ammunition.", new AcceptableValueRange<int>(2, 64)));
        }

        internal static bool Preferred(Unit target)
        {
            return (againstBoats == null || againstBoats.Value) && target is Ship && target.definition && target.definition.length <= 65f;
        }

        internal static bool Available(Gun gun)
        {
            int slots;
            return gun && active + 2 <= (maximum == null ? 24 : maximum.Value) &&
                (!Slots.TryGetValue(gun, out slots) || slots == 0) &&
                (bombDefinition || (gun.attachedUnit && gun.attachedUnit.transform.Find("TopAttackSubmunitionTemplate")));
        }

        internal static bool Reserve(Gun gun)
        {
            if (!Available(gun)) return false;
            if (!bombDefinition)
            {
                var source = gun.attachedUnit.transform.Find("TopAttackSubmunitionTemplate");
                var reference = source.GetComponentInChildren<MissileLauncher>(true);
                if (!reference) return false;
                bombDefinition = (MissileDefinition)AccessTools.Field(typeof(MissileLauncher), "missile").GetValue(reference);
                if (!bombDefinition) return false;
            }
            Slots[gun] = 2; active += 2; return true;
        }

        internal static void Return(Gun gun, int count)
        {
            int held;
            if (ReferenceEquals(gun, null) || !Slots.TryGetValue(gun, out held)) return;
            int returned = Mathf.Min(count, held); active = Mathf.Max(0, active - returned);
            if (held == returned) Slots.Remove(gun); else Slots[gun] = held - returned;
        }

        internal static bool Enemy(Unit owner, Unit candidate)
        {
            return owner && candidate && !candidate.disabled && candidate is Ship &&
                owner.NetworkHQ && candidate.NetworkHQ && owner.NetworkHQ != candidate.NetworkHQ;
        }

        internal static bool Claim(Unit target, float seconds)
        {
            float until;
            if (Claims.TryGetValue(target, out until) && Time.time < until) return false;
            Claims[target] = Time.time + seconds; return true;
        }

        internal static void Release(SpecialShells.Carrier shot, GlobalPosition location, Vector3 velocity)
        {
            if (!shot.TopAttackReserved) return;
            shot.TopAttackReserved = false;
            if (!bombDefinition || location.ToLocalPosition().y < Datum.LocalSeaY + 30f)
            { Return(shot.Gun, 2); return; }
            var parachute = CoastalIllumination.GetNativeParachute();
            if (!parachute) { Return(shot.Gun, 2); return; }
            Vector3 side = Vector3.Cross(velocity.normalized, Vector3.up).normalized;
            for (int i = 0; i < 2; i++)
            {
                var go = NativeTopAttackBomb.Spawn(bombDefinition, shot.Gun, shot.Owner, shot.Target,
                    location, velocity + side * (i == 0 ? -14f : 14f), i * Mathf.PI);
                if (!go) Return(shot.Gun, 1);
            }
        }

        internal static void Track(GameObject go) { Objects.RemoveAll(o => !o); Objects.Add(go); }

        internal static void Contact(Unit target, IDamageable part, float pierce)
        {
            if (logger != null) logger.LogInfo("Top-attack slug contacted " + target.name + " / " + (part as Component)?.name + " (native penetration " + pierce.ToString("F0") + ").");
        }

        internal static void Fire(Unit owner, Vector3 from, Vector3 aim)
        {
            TopAttackEffects.Discharge(from);
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Coastal tungsten top attack projectile"; go.transform.SetParent(Datum.origin, false);
            go.transform.position = from; go.transform.localScale = new Vector3(.08f, .24f, .08f);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (aim - from).normalized);
            var collider = go.GetComponent<CapsuleCollider>(); collider.isTrigger = true;
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.GetComponent<Renderer>().sharedMaterial = SpecialShells.SparkMaterial;
            Track(go); go.AddComponent<TopAttackSlug>().Initialize(owner, from, (aim - from).normalized * SlugSpeed, PierceDamage);
        }

        internal static void Shutdown()
        {
            foreach (var go in Objects) if (go) { if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go); }
            Objects.Clear(); Slots.Clear(); Claims.Clear(); active = 0;
            bombDefinition = null;
            TopAttackEffects.Shutdown();
        }
    }

    public sealed class TopAttackSubmunition : MonoBehaviour
    {
        private sealed class Candidate
        {
            internal Unit Unit;
            internal Collider[] Colliders;
            internal GlobalPosition PreviousCenter;
            internal int Seen;
        }
        private readonly List<Unit> nearby = new List<Unit>(32);
        private readonly Dictionary<Unit, Candidate> candidates = new Dictionary<Unit, Candidate>();
        private readonly List<Unit> remove = new List<Unit>();
        private Gun gun;
        private Unit owner;
        private GlobalPosition center, previousOrigin;
        private Vector3 velocity, previousDirection;
        private float age, phase, nextDiscovery, spentAt = -1;
        private int discovery;
        private bool ownsSlot;
        private bool retired;
        private Missile bomb;
        private TopAttackParachute parachuteVisual;
        private static RaycastHit[] LandingHits = new RaycastHit[32];
        private Transform model;
        private LineRenderer beam;

        internal void Initialize(Gun source, Unit shooter, GlobalPosition location, Vector3 initialVelocity, float initialPhase, Parachute parachute)
        {
            gun = source; owner = shooter; center = location; velocity = initialVelocity; phase = initialPhase;
            ownsSlot = !ReferenceEquals(source, null); bomb = GetComponent<Missile>();
            transform.position = location.ToLocalPosition(); previousOrigin = location;
            model = transform.Find("SubmunitionBody");
            parachuteVisual = gameObject.AddComponent<TopAttackParachute>(); parachuteVisual.Initialize(parachute);
            beam = gameObject.AddComponent<LineRenderer>(); beam.useWorldSpace = false; beam.positionCount = 2;
            beam.sharedMaterial = SpecialShells.SparkMaterial; beam.startWidth = .12f; beam.endWidth = .35f;
            beam.startColor = new Color(1.5f, .2f, .05f, .7f); beam.endColor = new Color(1f, .1f, .03f, .15f);
            beam.shadowCastingMode = ShadowCastingMode.Off; beam.receiveShadows = false; beam.enabled = false;
            previousDirection = Direction(phase);
        }

        private static Vector3 Direction(float angle)
        {
            float tilt = Mathf.Lerp(5f, 35f, .5f - .5f * Mathf.Cos(angle * .21f)) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * Mathf.Sin(tilt), -Mathf.Cos(tilt), Mathf.Sin(angle) * Mathf.Sin(tilt));
        }

        private Bounds Hull(Candidate candidate)
        {
            bool found = false; Bounds bounds = new Bounds(candidate.Unit.transform.position, Vector3.zero);
            foreach (var collider in candidate.Colliders)
            {
                if (!collider || collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds);
            }
            if (!found)
            {
                var d = candidate.Unit.definition;
                bounds = new Bounds(candidate.Unit.transform.position + Vector3.up * (d ? d.height * .3f : 2f),
                    d ? new Vector3(d.width, Mathf.Max(2f, d.height), d.length) : new Vector3(5, 4, 20));
            }
            return bounds;
        }

        private void Discover()
        {
            nextDiscovery = age + .25f; discovery++;
            float height = Mathf.Max(0f, center.ToLocalPosition().y - Datum.LocalSeaY);
            nearby.Clear(); BattlefieldGrid.GetUnitsInRangeNonAlloc(center, Mathf.Max(1200f, height * 1.3f + 200f), nearby);
            foreach (var unit in nearby)
            {
                if (!TopAttackMunitions.Enemy(owner, unit)) continue;
                Candidate candidate;
                if (!candidates.TryGetValue(unit, out candidate))
                {
                    candidate = new Candidate { Unit = unit, Colliders = unit.GetComponentsInChildren<Collider>(true) };
                    var hull = Hull(candidate);
                    candidate.PreviousCenter = (hull.center - (unit.rb ? unit.rb.velocity * Time.fixedDeltaTime : Vector3.zero)).ToGlobalPosition();
                    candidates.Add(unit, candidate);
                }
                candidate.Seen = discovery;
            }
            remove.Clear(); foreach (var pair in candidates) if (!pair.Key || pair.Value.Seen != discovery) remove.Add(pair.Key);
            foreach (var key in remove) candidates.Remove(key);
        }

        private static Vector3 Footprint(Vector3 origin, Vector3 direction, float height)
        {
            return origin + direction * Mathf.Max(0f, (height - origin.y) / direction.y);
        }

        private void FixedUpdate()
        {
            if (retired) return;
            float dt = Time.fixedDeltaTime; age += dt;
            if (bomb && bomb.disabled) { retired = true; beam.enabled = false; if (bomb.LocalSim) NativeTopAttackBomb.Expire(bomb); return; }
            if (bomb && !bomb.LocalSim)
            {
                phase += dt * Mathf.PI * 2f * .65f;
                beam.enabled = true; beam.SetPosition(0, Vector3.down * .24f);
                beam.SetPosition(1, Footprint(transform.position, Direction(phase), Datum.LocalSeaY + .3f) - transform.position);
                if (model) model.localRotation = Quaternion.Euler(8f, phase * Mathf.Rad2Deg, 10f) * Quaternion.Euler(270f, 0f, 0f);
                return;
            }
            if ((spentAt < 0f && (age >= 70f || transform.position.y <= Datum.LocalSeaY + 12f)) ||
                (spentAt >= 0f && age - spentAt >= 25f))
            { retired = true; if (bomb) NativeTopAttackBomb.Expire(bomb); else if (Application.isPlaying) Destroy(gameObject); else gameObject.SetActive(false); return; }
            if (spentAt >= 0f) { FallSpent(dt); return; }
            Vector3 wind = LevelInfo.i ? LevelInfo.i.NetworkwindVelocity * .35f : Vector3.zero;
            Vector3 movement = ParachuteMotion.Advance(ref velocity, wind + Vector3.down * 8f, age - dt, dt);
            center = (center.ToLocalPosition() + movement).ToGlobalPosition();
            phase += dt * Mathf.PI * 2f * .65f;
            Vector3 origin = center.ToLocalPosition() + new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase)) * Mathf.SmoothStep(0f, 4f, age / 3f);
            transform.position = origin;
            if (bomb) { bomb.rb.position = origin; bomb.speed = velocity.magnitude; }
            if (model) model.localRotation = Quaternion.Euler(8f, phase * Mathf.Rad2Deg, 10f) * Quaternion.Euler(270f, 0f, 0f);
            Vector3 direction = Direction(phase);
            beam.enabled = age >= ParachuteMotion.OpeningSeconds && spentAt < 0;
            beam.SetPosition(0, Vector3.down * .24f);
            beam.SetPosition(1, Footprint(origin, direction, Datum.LocalSeaY + .3f) - origin);
            if (age >= ParachuteMotion.OpeningSeconds && spentAt < 0)
            {
                if (age >= nextDiscovery) Discover();
                foreach (var candidate in candidates.Values)
                {
                    if (!TopAttackMunitions.Enemy(owner, candidate.Unit)) continue;
                    Bounds hull = Hull(candidate);
                    Vector3 previousHull = candidate.PreviousCenter.ToLocalPosition();
                    Vector3 previousBeam = Footprint(previousOrigin.ToLocalPosition(), previousDirection, previousHull.y);
                    Vector3 currentBeam = Footprint(origin, direction, hull.center.y);
                    float fraction;
                    float sagitta = Vector3.Distance(origin, currentBeam) * Mathf.Pow(dt * Mathf.PI * 2f * .65f, 2f) / 8f;
                    bool seen = TopAttackScan.SweptFootprint(previousBeam, currentBeam, previousHull, hull.center, hull.extents, 1.25f + sagitta, out fraction);
                    candidate.PreviousCenter = hull.center.ToGlobalPosition();
                    if (!seen || origin.y <= hull.max.y + 20f) continue;
                    Vector3 aimPoint = new Vector3(hull.center.x, hull.max.y - .3f, hull.center.z);
                    RaycastHit obstruction;
                    if (Physics.Linecast(origin, aimPoint, out obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        obstruction.collider.GetComponentInParent<Unit>() != candidate.Unit) continue;
                    if (ShellObscurants.SmokeBlocks(origin, aimPoint)) continue;
                    Vector3 targetVelocity = candidate.Unit.rb ? candidate.Unit.rb.velocity : Vector3.zero;
                    float flight = Vector3.Distance(origin, aimPoint) / TopAttackMunitions.SlugSpeed;
                    if (!TopAttackMunitions.Claim(candidate.Unit, flight + .7f)) continue;
                    var muzzle = transform.Find("ProjectileMuzzle");
                    Vector3 launch = muzzle ? muzzle.position : origin + Vector3.down * .25f;
                    Vector3 aim = TopAttackScan.Intercept(launch, aimPoint, targetVelocity, TopAttackMunitions.SlugSpeed);
                    TopAttackMunitions.Fire(owner, launch, aim);
                    TopAttackEffects.MuzzleFlash(muzzle ? muzzle : transform, (aim - launch).normalized);
                    ReleaseParachute(dt); break;
                }
            }
            previousOrigin = origin.ToGlobalPosition(); previousDirection = direction;
        }

        private void ReleaseParachute(float dt)
        {
            if (spentAt >= 0f) return;
            spentAt = age; beam.enabled = false;
            // Retain the actual sway/drift velocity rather than the slow descent alone.
            velocity = (transform.position - previousOrigin.ToLocalPosition()) / Mathf.Max(.001f, dt);
            center = transform.position.ToGlobalPosition();
            if (parachuteVisual) parachuteVisual.Detach(velocity);
            candidates.Clear(); nearby.Clear(); remove.Clear();
        }

        private void FallSpent(float dt)
        {
            Vector3 from = center.ToLocalPosition();
            Vector3 movement = velocity * dt + Vector3.down * (4.905f * dt * dt);
            velocity += Vector3.down * (9.81f * dt);
            float distance = movement.magnitude;
            Vector3 to = from + movement, normal = Vector3.up;
            float nearest = distance + 1f; bool landed = false, water = false;
            if (distance > .0001f)
            {
                int count;
                while (true)
                {
                    count = Physics.SphereCastNonAlloc(from, .1f, movement / distance, LandingHits,
                        distance, ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore);
                    if (count < LandingHits.Length || LandingHits.Length >= 256) break;
                    System.Array.Resize(ref LandingHits, LandingHits.Length * 2);
                }
                var hits = count == LandingHits.Length ? Physics.SphereCastAll(from, .1f, movement / distance,
                    distance, ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore) : LandingHits;
                if (!ReferenceEquals(hits, LandingHits)) count = hits.Length;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (!hit.collider || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                    nearest = hit.distance; to = hit.point; normal = hit.normal; landed = true;
                    water = hit.point.y <= Datum.LocalSeaY + .15f;
                }
                System.Array.Clear(LandingHits, 0, LandingHits.Length);
                // Ocean may have no collider. Prefer it to geometry below the sea surface.
                float sea = Datum.LocalSeaY + .05f;
                if (movement.y < 0f && from.y >= sea && from.y + movement.y <= sea)
                {
                    float fraction = (sea - from.y) / movement.y;
                    if (distance * fraction < nearest)
                    { to = from + movement * fraction; normal = Vector3.up; landed = water = true; }
                }
            }
            center = to.ToGlobalPosition(); transform.position = to;
            if (bomb) { bomb.rb.position = to; bomb.speed = velocity.magnitude; }
            if (model) model.Rotate(new Vector3(85f, 25f, 35f) * dt, Space.Self);
            if (!landed) return;
            retired = true;
            TopAttackEffects.CanisterImpact(to, normal, water);
            if (bomb) NativeTopAttackBomb.Expire(bomb);
            else if (Application.isPlaying) Destroy(gameObject); else gameObject.SetActive(false);
        }

        private void OnDestroy() { if (ownsSlot) { ownsSlot = false; TopAttackMunitions.Return(gun, 1); } }
    }

    public sealed class TopAttackParachute : MonoBehaviour
    {
        private Transform canopy, lines;
        private Transform assemblyRoot;
        private Material material;
        private AnimationCurve horizontal, vertical, wrinkles;
        private float age;
        private const float CordLength = 2.6f;

        internal void Initialize(Parachute source)
        {
            var nativeCanopy = (GameObject)AccessTools.Field(typeof(Parachute), "canopy").GetValue(source);
            var nativeLines = (GameObject)AccessTools.Field(typeof(Parachute), "lines").GetValue(source);
            material = new Material(nativeCanopy.GetComponent<Renderer>().sharedMaterial);
            var root = new GameObject("Top attack parachute assembly"); root.transform.SetParent(transform, false);
            assemblyRoot = root.transform;
            horizontal = (AnimationCurve)AccessTools.Field(typeof(Parachute), "chuteScaleHorizontal").GetValue(source);
            vertical = (AnimationCurve)AccessTools.Field(typeof(Parachute), "chuteScaleVertical").GetValue(source);
            wrinkles = (AnimationCurve)AccessTools.Field(typeof(Parachute), "wrinkleStrength").GetValue(source);
            canopy = Mesh("Top attack native parachute", nativeCanopy);
            lines = Mesh("Top attack suspension lines", nativeLines);
            canopy.localPosition = Vector3.up * (CordLength + .25f);
            canopy.localRotation = Quaternion.LookRotation(Vector3.down);
            lines.localPosition = Vector3.up * .25f;
            lines.localRotation = Quaternion.LookRotation(Vector3.up);
            Animate(0f);
        }

        private Transform Mesh(string label, GameObject source)
        {
            var go = new GameObject(label); go.transform.SetParent(assemblyRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private void Animate(float time)
        {
            float width = horizontal.Evaluate(time) * .28f;
            canopy.localScale = new Vector3(width, width, vertical.Evaluate(time) * .28f);
            lines.localScale = new Vector3(width, width, CordLength);
            material.SetFloat("_wrinkleStrength", wrinkles.Evaluate(time));
        }

        private void Update() { age += Time.deltaTime; if (age <= 3f) Animate(age); }
        internal void Detach(Vector3 velocity)
        {
            if (!assemblyRoot || !material) return;
            assemblyRoot.SetParent(Datum.origin, true);
            assemblyRoot.name = "Discarded top attack parachute";
            var discarded = assemblyRoot.gameObject.AddComponent<DiscardedTopAttackParachute>();
            discarded.Initialize(canopy, lines, material, velocity);
            TopAttackMunitions.Track(assemblyRoot.gameObject);
            material = null; assemblyRoot = null; enabled = false;
        }
        private void OnDestroy() { if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); } }
    }

    public sealed class TopAttackSlug : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private readonly List<IDamageable> damaged = new List<IDamageable>(8);
        private Unit owner;
        private FactionHQ firingHQ;
        private PersistentID sourceID;
        private bool authority;
        private GlobalPosition position;
        private Vector3 velocity;
        private float age, penetration;
        private bool impactShown;
        private SpottingTrail trail;
        internal void Initialize(Unit source, Vector3 from, Vector3 initialVelocity, float damage)
        {
            owner = source; position = from.ToGlobalPosition(); velocity = initialVelocity; penetration = damage;
            firingHQ = source ? source.NetworkHQ : null; sourceID = source ? source.persistentID : PersistentID.None;
            var network = NuclearOption.Networking.NetworkManagerNuclearOption.i;
            authority = network ? network.Server.Active : source && source.IsServer;
            trail = CoastalRanging.BuildTrail(from, new Color(6f, 4.8f, 2.4f), .14f);
            if (trail) { trail.MaximumLength = 25f; trail.MinimumSampleDistance = .1f;
                var line = trail.GetComponent<LineRenderer>(); line.startWidth = .18f; line.endWidth = .02f; }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime; age += dt;
            Vector3 from = position.ToLocalPosition(); velocity += Vector3.down * (9.81f * dt);
            Vector3 to = from + velocity * dt; float distance = Vector3.Distance(from, to);
            int count = Physics.RaycastNonAlloc(from, velocity.normalized, Hits, distance, ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore);
            // Sort the small hit set in place, preserving physical near-to-far penetration.
            for (int i = 1; i < count; i++) { var h = Hits[i]; int j = i - 1; while (j >= 0 && Hits[j].distance > h.distance) { Hits[j + 1] = Hits[j]; j--; } Hits[j + 1] = h; }
            bool stop = false;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i]; var part = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
                if (!impactShown) { impactShown = true; TopAttackEffects.Impact(hit.point, hit.normal, part != null, part == null && hit.point.y <= Datum.LocalSeaY + .2f); }
                if (part == null) { to = hit.point; stop = true; break; }
                if (damaged.Contains(part)) continue;
                var target = part.GetUnit();
                if (!target || !(target is Ship) || target.disabled || !firingHQ || !target.NetworkHQ || firingHQ == target.NetworkHQ)
                { to = hit.point; stop = true; break; }
                if (authority)
                {
                    part.TakeDamage(penetration, 20f, 1f, 0f, 0f, sourceID);
                    if (damaged.Count == 0) TopAttackMunitions.Contact(target, part, penetration);
                }
                damaged.Add(part);
                penetration -= Mathf.Max(100f, part.GetArmorProperties().pierceArmor);
                if (penetration <= 0f) { to = hit.point; stop = true; break; }
            }
            if (!stop && from.y > Datum.LocalSeaY + .1f && to.y <= Datum.LocalSeaY + .1f)
            {
                float fraction = (from.y - Datum.LocalSeaY - .1f) / (from.y - to.y);
                to = Vector3.Lerp(from, to, fraction);
                if (!impactShown) { impactShown = true; TopAttackEffects.Impact(to, Vector3.up, false, true); }
                stop = true;
            }
            position = to.ToGlobalPosition(); transform.position = to;
            if (trail) trail.Sample(position, Time.time);
            if (stop || age >= 2f || to.y <= Datum.LocalSeaY + .1f)
            { if (Application.isPlaying) Destroy(gameObject); else gameObject.SetActive(false); }
        }

        private void OnDestroy()
        {
            if (!trail) return; trail.Freeze();
            if (Application.isPlaying) Destroy(trail.gameObject, .25f); else DestroyImmediate(trail.gameObject);
        }
    }
}
