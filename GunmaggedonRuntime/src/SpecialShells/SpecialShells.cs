using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    internal static class SpecialShells
    {
        internal sealed class Carrier
        {
            internal SpecialShellKind Kind;
            internal Unit Owner;
            internal Unit Target;
            internal Gun Gun;
            internal CoastalIllumination.Plan Delivery;
            internal bool TopAttackReserved;
            internal GlobalPosition Aim, Launch;
            internal float Born;
            internal bool Released;
            internal float Gravity = 9.81f;
        }
        private sealed class Loadout
        {
            internal Unit Target;
            internal float Smoke = -1000f, Chaff = -1000f;
            internal int ClustersSinceSupport;
        }
        private static readonly Dictionary<Gun, Loadout> Loadouts = new Dictionary<Gun, Loadout>();
        private static readonly List<GameObject> Effects = new List<GameObject>();
        private static ConfigEntry<SpecialShellMode> mode;
        private static ConfigEntry<int> bomblets, clustersBetweenSupport;
        private static ConfigEntry<float> mountDamage, hullDamage, smokeSeconds, chaffSeconds;
        private static ConfigEntry<float> clusterBurstLead, clusterCone;
        private static ManualLogSource log;
        internal static readonly ParticleEffectManager.PrefabEffect[] NoImpactEffects = new ParticleEffectManager.PrefabEffect[4];
        private static readonly System.Reflection.FieldInfo Critical = AccessTools.Field(typeof(UnitPart), "criticalPart");
        internal static Material SmokeMaterial, ChaffMaterial, SparkMaterial;
        private static Texture2D puff;

        internal static void Install(Harmony harmony, ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            mode = config.Bind("Special shells", "Mode", SpecialShellMode.Auto,
                "Auto prioritizes clusters on large ships and parachute top attack on small ships, with occasional support rounds. TopAttack requests this payload explicitly; exhausted submunition budgets fall back to clusters. Applies only to the special coastal gun.");
            TopAttackMunitions.Configure(config, logger);
            clustersBetweenSupport = config.Bind("Special shells", "ClustersBetweenSupport", 5, new ConfigDescription(
                "Minimum cluster payloads before the first support shell and between any smoke/chaff shells in Auto mode.", new AcceptableValueRange<int>(1, 20)));
            bomblets = config.Bind("Special shells", "ClusterBomblets", 36, new ConfigDescription("Physical submunitions in one cluster shell.", new AcceptableValueRange<int>(12, 64)));
            clusterBurstLead = config.Bind("Special shells", "ClusterBurstLeadSeconds", 4f, new ConfigDescription("Release clusterlets this many seconds before predicted target-height impact, on the descending arc.", new AcceptableValueRange<float>(2f, 6f)));
            clusterCone = config.Bind("Special shells", "ClusterConeHalfAngleDegrees", 12f, new ConfigDescription("Cluster dispersal cone half-angle. Lateral ejection speed is capped at 45 m/s to avoid excessive dilution at long range.", new AcceptableValueRange<float>(5f, 25f)));
            mountDamage = config.Bind("Special shells", "EquipmentBlastDamage", 140f, new ConfigDescription("Maximum close-hit damage to a noncritical weapon/sensor part, before native armor/tolerance.", new AcceptableValueRange<float>(10f, 400f)));
            hullDamage = config.Bind("Special shells", "HullBlastDamage", .5f, new ConfigDescription("Maximum incidental damage per bomblet to other parts; no penetrating damage.", new AcceptableValueRange<float>(0f, 2f)));
            smokeSeconds = config.Bind("Special shells", "SmokeSeconds", 90f, new ConfigDescription("Smoke screen duration, including gradual build and fade.", new AcceptableValueRange<float>(20f, 180f)));
            chaffSeconds = config.Bind("Special shells", "ChaffSeconds", 70f, new ConfigDescription("Radar clutter duration, including gradual build and fade.", new AcceptableValueRange<float>(15f, 150f)));
            ShellObscurants.Install(harmony);
        }

        internal static void Maintain(){var dead=RemovalBuffer<Gun>.Rent();try{foreach(var pair in Loadouts)if(!pair.Key)dead.Add(pair.Key);foreach(var gun in dead)Loadouts.Remove(gun);}finally{RemovalBuffer<Gun>.Return(dead);}}
        internal static bool IsSpecial(Gun gun)
        {
            return gun && gun.info && gun.attachedUnit is Building && gun.info.name == "203mmEmplacement_SPECIAL";
        }

        private static SpecialShellKind NextKind(Gun gun, Unit target)
        {
            Loadout state; Loadouts.TryGetValue(gun, out state);
            bool same = state != null && state.Target == target;
            var selection = mode == null ? SpecialShellMode.Auto : mode.Value;
            var kind = Choose(selection, Time.time, same ? state.Smoke : -1000f, same ? state.Chaff : -1000f,
                smokeSeconds == null ? 90f : smokeSeconds.Value, chaffSeconds == null ? 70f : chaffSeconds.Value,
                same ? state.ClustersSinceSupport : 0, clustersBetweenSupport == null ? 5 : clustersBetweenSupport.Value);
            if (kind == SpecialShellKind.TopAttack || (selection == SpecialShellMode.Auto && kind == SpecialShellKind.Cluster && TopAttackMunitions.Preferred(target)))
                return target && CoastalRanging.HasSolution(gun, target) && TopAttackMunitions.Available(gun) ? SpecialShellKind.TopAttack : SpecialShellKind.Cluster;
            return kind;
        }

        internal static bool WantsTopAttack(Gun gun, Unit target)
        {
            return IsSpecial(gun) && target && CoastalRanging.HasSolution(gun, target) && NextKind(gun, target) == SpecialShellKind.TopAttack;
        }

        internal static SpecialShellKind Choose(SpecialShellMode selection, float now, float lastSmoke, float lastChaff, float smokeLife, float chaffLife,
            int clustersSinceSupport, int minimumClusters)
        {
            return SpecialShellPolicy.Choose(selection, now, lastSmoke, lastChaff, smokeLife, chaffLife, clustersSinceSupport, minimumClusters);
        }

        internal static Carrier Prepare(Gun gun, Unit target, Vector3 launch, Vector3 velocity)
        {
            Loadout state;
            if (!Loadouts.TryGetValue(gun, out state)) Loadouts.Add(gun, state = new Loadout());
            if (state.Target != target)
            {
                state.Target = target; state.Smoke = state.Chaff = -1000f;
                state.ClustersSinceSupport = 0;
            }
            var kind = NextKind(gun, target);
            CoastalIllumination.Plan delivery = null;
            bool reserved = false;
            if (kind == SpecialShellKind.TopAttack)
            {
                delivery = CoastalIllumination.GetPlan(gun, target);
                reserved = delivery != null && delivery.Reachable && TopAttackMunitions.Reserve(gun);
                if (!reserved) kind = SpecialShellKind.Cluster;
            }
            state.ClustersSinceSupport = kind == SpecialShellKind.Cluster || kind == SpecialShellKind.TopAttack ? Math.Min(1000000, state.ClustersSinceSupport + 1) : 0;
            if (kind == SpecialShellKind.Smoke) state.Smoke = Time.time;
            if (kind == SpecialShellKind.Chaff) state.Chaff = Time.time;
            // A fuse estimate is frozen at launch. Submunitions never steer toward equipment.
            var aim = (target ? target.transform.position : launch + velocity.normalized * 2000f).ToGlobalPosition();
            if (target && CoastalArtillery.IsSupported(gun))
            {
                var plan = CoastalArtillery.GetPlan(gun, target, launch);
                if (plan.Reachable) aim = plan.Aim;
            }
            return new Carrier { Kind = kind, Gun = gun, Owner = gun.attachedUnit, Target = target, Delivery = delivery, TopAttackReserved = reserved, Born = Time.time, Gravity = 9.81f * gun.info.gravMult,
                Launch = launch.ToGlobalPosition(), Aim = aim };
        }

        internal static Color ShellColor(SpecialShellKind kind)
        {
            return kind == SpecialShellKind.Cluster ? new Color(1f, .55f, .12f) :
                kind == SpecialShellKind.TopAttack ? new Color(1f, .15f, .08f) :
                kind == SpecialShellKind.Smoke ? new Color(.8f, .9f, .85f) : new Color(.55f, .7f, 1f);
        }

        internal static bool Fuse(Carrier shot, Vector3 position, Vector3 velocity, float now)
        {
            if (shot.Released) return false;
            if (shot.Kind == SpecialShellKind.TopAttack)
                return shot.Delivery != null && now - shot.Born > .5f && CoastalIllumination.AtDeployment(position.ToGlobalPosition(), velocity, shot.Delivery, now - shot.Born);
            float floor = Mathf.Max(Datum.LocalSeaY, shot.Aim.ToLocalPosition().y);
            Vector3 remaining = shot.Aim.ToLocalPosition() - position; remaining.y = 0f;
            Vector3 flatVelocity = velocity; flatVelocity.y = 0f;
            if (shot.Kind == SpecialShellKind.Cluster)
            {
                // Use time remaining rather than a low fixed altitude: lofted/high-speed
                // rounds need to open much higher to leave time for area saturation.
                float lead = clusterBurstLead == null ? 4f : clusterBurstLead.Value;
                float height = Mathf.Max(0f, position.y - floor);
                float gravity = Mathf.Max(.01f, shot.Gravity);
                float timeToImpact = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * height)) / gravity;
                return now - shot.Born > .5f && velocity.y < 0f && timeToImpact <= lead &&
                    Vector3.Distance(shot.Launch.ToLocalPosition(), position) > 250f &&
                    remaining.magnitude <= Mathf.Max(250f, flatVelocity.magnitude * (lead + 1f));
            }
            return now - shot.Born > .5f && velocity.y < 0f && position.y <= floor + 120f &&
                Vector3.Distance(shot.Launch.ToLocalPosition(), position) > 250f &&
                remaining.magnitude <= Mathf.Max(250f, flatVelocity.magnitude * 2.5f);
        }

        internal static Vector3 ClusterVelocity(Vector3 carrierVelocity, int index, int count, float phase)
        {
            Vector3 forward = carrierVelocity.sqrMagnitude > .01f ? carrierVelocity.normalized : Vector3.down;
            Vector3 right = Vector3.Cross(forward, Mathf.Abs(forward.y) > .95f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, forward);
            float angle = (clusterCone == null ? 12f : clusterCone.Value) * Mathf.Deg2Rad;
            float spread = Mathf.Min(45f, carrierVelocity.magnitude * Mathf.Tan(angle));
            // Equal-area radial bands and golden-angle spacing cover the cone instead
            // of random clumps. All pieces remain ballistic after release.
            float radius = Mathf.Sqrt((index + UnityEngine.Random.value) / Mathf.Max(1, count));
            float azimuth = phase + index * 2.39996323f;
            // A circular cone becomes a very long ellipse on the sea at shallow
            // incidence. Compress its vertical axis so rounds cover the ship's area
            // rather than stretching most of the payload hundreds of metres past it.
            float verticalSpread = Mathf.Max(.25f, Mathf.Abs(forward.y));
            return carrierVelocity + (right * Mathf.Cos(azimuth) + up * (Mathf.Sin(azimuth) * verticalSpread)) * (spread * radius);
        }

        internal static void Release(Carrier shot, GlobalPosition location, Vector3 velocity)
        {
            if (shot.Released) return;
            shot.Released = true;
            EnsureMaterials();
            if (shot.Kind == SpecialShellKind.Cluster || shot.Kind == SpecialShellKind.TopAttack)
            {
                ClusterVisualPools.Opening(location);
            }
            if (shot.Kind == SpecialShellKind.TopAttack)
            {
                TopAttackMunitions.Release(shot, location, velocity);
                if (log != null) log.LogInfo("Special coastal gun released two parachute top attack submunitions.");
                return;
            }
            var go = PayloadObject(shot.Kind, location, false);
            go.GetComponent<SpecialPayloadFlight>().Initialize(shot.Kind, shot.Owner, location, velocity,
                shot.Kind == SpecialShellKind.Cluster ? (bomblets == null ? 36 : bomblets.Value) : 5, shot.Gravity);
            if (log != null) log.LogInfo("Special coastal gun released " + shot.Kind + " payload.");
        }

        internal static GameObject PayloadObject(SpecialShellKind kind, GlobalPosition location, bool burst)
        {
            OptimizationRuntime.EnsureMission();
            EnsureMaterials();
            if(burst)ClusterVisualPools.Opening(location);
            var lease=ClusterVisualPools.Flights.Rent();var flight=lease.Value;flight.Lease=lease.Id;RuntimePool<SpecialPayloadFlight>.Place(flight,Datum.origin,location.ToLocalPosition());return flight.gameObject;
        }

        internal static bool IsEquipment(UnitPart part)
        {
            if (!part || (bool)Critical.GetValue(part)) return false;
            // Do not propagate equipment damage up into a hull that merely carries a mount.
            return part.GetComponent<Turret>() || part.GetComponent<TargetDetector>() ||
                part.GetComponentsInChildren<Weapon>(true).Any(w => w.GetComponentInParent<UnitPart>() == part);
        }

        internal static float DamageFor(UnitPart part, float distance)
        {
            float damage = IsEquipment(part) ? (mountDamage == null ? 140f : mountDamage.Value) : (hullDamage == null ? .5f : hullDamage.Value);
            return damage * Mathf.Clamp01(1f - distance / 7f);
        }

        internal static void Detonate(Vector3 position, Unit owner, bool water = false, bool applyDamage = true)
        {
            // Keep scratch per active invocation: damage callbacks may trigger another explosion.
            if (applyDamage) {
            var scratch = ClusterDamageQuery.Rent(position);
            try
            {
                var hits = scratch.Hits;
                var damaged = scratch.Damaged;
                for (int i = 0; i < scratch.Count; i++)
                {
                    var collider = hits[i];
                    if (!collider) continue;
                    var part = collider.GetComponentInParent<UnitPart>();
                    if (!part || !part.parentUnit || !part.parentUnit.LocalSim || !part.parentUnit.IsServer || damaged.Contains(part)) continue;
                    Vector3 nearest = collider.ClosestPoint(position);
                    RaycastHit obstruction;
                    if (Physics.Linecast(position + Vector3.up * .15f, nearest, out obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        obstruction.collider.GetComponentInParent<UnitPart>() != part) continue;
                    float damage = DamageFor(part, Vector3.Distance(position, nearest));
                    damaged.Add(part);
                    if (damage > 0f) ((IDamageable)part).TakeDamage(0f, damage, 1f, 0f, 0f, owner ? owner.persistentID : PersistentID.None);
                }
            }
            finally
            {
                scratch.Release();
            }
            }
            ClusterVisualPools.Impact(position,water);
        }

        internal static void Cloud(SpecialShellKind kind, GlobalPosition position)
        {
            EnsureMaterials();
            var go = new GameObject(kind == SpecialShellKind.Smoke ? "Coastal smoke screen" : "Coastal chaff cloud");
            go.transform.SetParent(Datum.origin, false); Track(go);
            go.AddComponent<ShellCloud>().Initialize(kind, position,
                kind == SpecialShellKind.Smoke ? (smokeSeconds == null ? 90f : smokeSeconds.Value) : (chaffSeconds == null ? 70f : chaffSeconds.Value));
            SpecialPayloadNetwork.CloudsChanged();
        }

        internal static void Track(GameObject go) { Effects.RemoveAll(e => !e); Effects.Add(go); }

        internal static void Cancel(Carrier shot)
        {
            if (shot == null || !shot.TopAttackReserved) return;
            shot.TopAttackReserved = false; TopAttackMunitions.Return(shot.Gun, 2);
        }

        internal static void EnsureMaterials()
        {
            if (SmokeMaterial) return;
            puff = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Coastal shell soft particle", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                float noise = Mathf.Lerp(.45f, 1f, Mathf.PerlinNoise(x * .23f, y * .23f));
                puff.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f) * noise));
            }
            puff.Apply();
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            SmokeMaterial = new Material(shader) { name = "Coastal smoke", mainTexture = puff };
            ChaffMaterial = new Material(shader) { name = "Coastal chaff", mainTexture = puff };
            SparkMaterial = new Material(shader) { name = "Coastal submunition spark", mainTexture = puff };
        }

        internal static void Shutdown()
        {
            TopAttackMunitions.Shutdown();
            ShellObscurants.Clear();
            foreach (var go in Effects) if (go) UnityEngine.Object.Destroy(go);
            Effects.Clear(); Loadouts.Clear();
            ClusterDamageQuery.Clear();
            foreach (var m in new[] { SmokeMaterial, ChaffMaterial, SparkMaterial }) if (m) UnityEngine.Object.Destroy(m);
            if (puff) UnityEngine.Object.Destroy(puff);
            SmokeMaterial = ChaffMaterial = SparkMaterial = null; puff = null;
        }
    }

    public sealed class SpecialPayloadFlight : MonoBehaviour
    {
        private sealed class Piece
        {
            internal GlobalPosition Position;
            internal Vector3 Velocity;
            internal LineRenderer Line;
            internal SpottingTrail Trail;
            internal long HeadLease;
            internal readonly Vector3[] HeadPositions=new Vector3[2];
        }
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly Stack<Piece> spare=new Stack<Piece>();internal long Lease;
        private SpecialShellKind kind;
        private Unit owner;
        private float age;
        private float gravity;
        private bool remote;
        private float catchUp;

        internal void CatchUp(float seconds) { if (remote) catchUp = seconds; }

        internal void Initialize(SpecialShellKind shell, Unit source, GlobalPosition position, Vector3 velocity, int count, float gravityAcceleration, Vector3[] receivedVelocities = null)
        {
            OptimizationRuntime.EnsureMission();
            ResetFlight();age=catchUp=0;gameObject.SetActive(true);kind = shell; owner = source; gravity = gravityAcceleration; remote = receivedVelocities != null;
            var velocities = receivedVelocities ?? new Vector3[count];
            float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                var headLease=ClusterVisualPools.Heads.Rent();var line=headLease.Value;var go=line.gameObject;go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;go.SetActive(true); line.useWorldSpace = false; line.positionCount = 2;
                line.sharedMaterial = SpecialShells.SparkMaterial; line.startWidth = .35f; line.endWidth = .08f;
                line.startColor = SpecialShells.ShellColor(shell); line.endColor = new Color(1f, 1f, 1f, 0f);
                line.enabled = false;
                Vector3 pieceVelocity;
                if (remote) pieceVelocity = velocities[i];
                else if (shell == SpecialShellKind.Cluster) pieceVelocity = SpecialShells.ClusterVelocity(velocity, i, count, phase);
                else
                {
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * 40f;
                    pieceVelocity = velocity + new Vector3(scatter.x, UnityEngine.Random.Range(-12f, 5f), scatter.y);
                }
                velocities[i] = pieceVelocity;
                SpottingTrail trail = null;
                if (shell == SpecialShellKind.Cluster)
                {
                    // Use the same global-coordinate/camera-rebase handling as ranging trails.
                    trail = CoastalRanging.BuildTrail(position.ToLocalPosition(), new Color(3f, 1.9f, .8f), .18f);
                    if (trail)
                    {
                        trail.name = "Clusterlet short trail";
                        trail.MaximumLength = 20f;
                        trail.MinimumSampleDistance = .25f;
                        var renderer = trail.GetComponent<LineRenderer>();
                        renderer.startWidth = .28f; renderer.endWidth = .025f;
                    }
                    // A compact bright head remains visible at the front of the trail.
                    line.startColor = new Color(4f, 2.8f, 1.2f, 1f);
                    line.endColor = new Color(2f, 1f, .3f, .3f);
                }
                var piece=spare.Count>0?spare.Pop():new Piece();piece.Position=position;piece.Velocity=pieceVelocity;piece.Line=line;piece.Trail=trail;piece.HeadLease=headLease.Id;pieces.Add(piece);
            }
            if (!remote) SpecialPayloadNetwork.Release(shell, position, gravityAcceleration, velocities);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime + catchUp; catchUp = 0f;
            age += dt;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                Vector3 from = p.Position.ToLocalPosition();
                Vector3 to = from + p.Velocity * dt + Vector3.down * (gravity * dt * (dt + Time.fixedDeltaTime) * .5f);
                p.Velocity += Vector3.down * (gravity * dt);
                RaycastHit hit;
                bool impact = Physics.Linecast(from, to, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (impact) to = hit.point;
                bool sea = to.y <= Datum.LocalSeaY + .3f;
                if (sea && !impact)
                {
                    float t = Mathf.InverseLerp(from.y, to.y, Datum.LocalSeaY + .3f);
                    to = Vector3.Lerp(from, to, t);
                }
                p.Position = to.ToGlobalPosition();
                if (p.Trail) p.Trail.Sample(p.Position, Time.time);
                if (impact || sea || (kind == SpecialShellKind.Chaff && age >= .65f) || age >= 15f)
                {
                    if (kind == SpecialShellKind.Cluster) { if (impact || sea) SpecialShells.Detonate(to, owner, sea, !remote); }
                    else if (!remote) SpecialShells.Cloud(kind, p.Position);
                    RetireTrail(p.Trail);
                    ClusterVisualPools.Heads.Release(p.Line,p.HeadLease);p.Line=null;p.Trail=null;spare.Push(p);
                    pieces.RemoveAt(i);
                }
            }
            if(pieces.Count==0&&!ClusterVisualPools.Flights.Release(this,Lease))RuntimePool<SpecialPayloadFlight>.Remove(gameObject);
        }

        private void LateUpdate()
        {
            // Local bright heads plus independent global-coordinate trailing histories.
            foreach (var p in pieces)
            {
                p.Line.transform.position = p.Position.ToLocalPosition();
                p.HeadPositions[0]=Vector3.zero;
                p.HeadPositions[1]=Vector3.ClampMagnitude(-p.Velocity*.025f,kind==SpecialShellKind.Cluster?1.8f:12f);
                p.Line.SetPositions(p.HeadPositions);
                p.Line.enabled = true;
            }
        }

        private static void RetireTrail(SpottingTrail trail)
        {
            if (!trail) return;
            trail.Freeze();

        }

        internal void ResetFlight()
        {
            SpecialPayloadNetwork.UntrackFlight(gameObject);
            foreach(var piece in pieces){RetireTrail(piece.Trail);if(piece.Line)ClusterVisualPools.Heads.Release(piece.Line,piece.HeadLease);piece.Line=null;piece.Trail=null;spare.Push(piece);}
            pieces.Clear();owner=null;age=catchUp=gravity=0;remote=false;
        }
        private void OnDestroy(){ResetFlight();}
    }
}
