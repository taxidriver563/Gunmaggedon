using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TopAttackDamageFixture : MonoBehaviour, IDamageable
{
    public Unit Unit;
    public int Calls;
    public float Pierce;
    public void TakeDamage(float pierce, float blast, float multiplier, float fire, float pressure, PersistentID source) { Calls++; Pierce += pierce; }
    public void ApplyDamage(float a, float b, float c, float d) { }
    public void TakeShockwave(Vector3 a, float b, float c) { }
    public void Detach(Vector3 a, Vector3 b) { }
    public ArmorProperties GetArmorProperties() { return new ArmorProperties { pierceArmor = 250f }; }
    public Unit GetUnit() { return Unit; }
    public float GetMass() { return 1000f; }
    public Transform GetTransform() { return transform; }
}

public static class TopAttackIntegration
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Unit scanned;
    static GameObject bombStaging;
    static Assembly featureAssembly;
    public static bool Spawn(MissileDefinition __0, Gun __1, Unit __2, Unit __3, GlobalPosition __4, Vector3 __5, float __6, ref GameObject __result)
    {
        var go = UnityEngine.Object.Instantiate(__0.unitPrefab, bombStaging.transform, false); go.SetActive(false);
        go.transform.position = __4.ToLocalPosition(); var bomb = go.GetComponent<Missile>();
        typeof(Unit).GetField("<rb>k__BackingField", Fields).SetValue(bomb, go.GetComponent<Rigidbody>());
        typeof(Unit).GetField("<remoteSim>k__BackingField", Fields).SetValue(bomb, false);
        var field = typeof(Unit).GetField("HQ", Fields); var sync = field.GetValue(bomb);
        sync.GetType().GetField("_component", Fields).SetValue(sync, __2.NetworkHQ); field.SetValue(bomb, sync);
        typeof(Missile).GetField("<owner>k__BackingField", Fields).SetValue(bomb, __2);
        typeof(Missile).GetField("startingVelocity", Fields).SetValue(bomb, __5);
        featureAssembly.GetType("GunmageddonAudio.NativeTopAttackBomb").GetMethod("Begin", Static).Invoke(null, new object[] { bomb, __1, __6 });
        __result = go; return false;
    }
    public static bool Grid(List<Unit> __2) { __2.Clear(); if (scanned) __2.Add(scanned); return false; }
    public static bool Known(Unit __0, ref GlobalPosition __1, ref bool __result) { __1 = __0.transform.position.ToGlobalPosition(); __result = true; return false; }
    public static bool Server(Mirage.NetworkBehaviour __instance, ref bool __result) { if (!(__instance is Unit)) return true; __result = true; return false; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        var assembly = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.6.2/GunmageddonAudio.dll")));
        featureAssembly = assembly;
        var top = assembly.GetType("GunmageddonAudio.TopAttackMunitions");
        var special = assembly.GetType("GunmageddonAudio.SpecialShells");
        var carrierType = special.GetNestedType("Carrier", BindingFlags.NonPublic);
        var kind = assembly.GetType("GunmageddonAudio.SpecialShellKind");
        var scan = assembly.GetType("GunmageddonAudio.TopAttackScan");
        var illumination = assembly.GetType("GunmageddonAudio.CoastalIllumination");
        var datum = typeof(Unit).Assembly.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var originPosition = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = originPosition.GetValue(null);
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.top.attack.integration" });
        Action<MethodInfo, string> patch = (method, name) =>
        {
            var callback = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { typeof(TopAttackIntegration).GetMethod(name) });
            harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(harmony, new object[] { method, callback, null, null, null });
        };
        patch(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc", Static), "Grid");
        patch(typeof(Unit).GetProperty("IsServer").GetMethod, "Server");
        patch(typeof(FactionHQ).GetMethod("TryGetKnownPosition", Fields), "Known");
        var native = assembly.GetType("GunmageddonAudio.NativeTopAttackBomb");
        native.GetMethod("Install", Static).Invoke(null, new[] { harmony });
        patch(native.GetMethod("Spawn", Static), "Spawn");
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Top attack integration datum"); SceneManager.MoveGameObjectToScene(root, preview);
        var reports = new List<string>();
        GameObject sinkObject = null;
        var definition = ScriptableObject.CreateInstance<ShipDefinition>(); definition.length = 20; definition.width = 3; definition.height = 4;
        try
        {
            origin.SetValue(null, root.transform); originPosition.SetValue(null, Vector3.zero);
            bombStaging = new GameObject("Inactive native bomb fixture staging"); bombStaging.SetActive(false);
            bombStaging.transform.SetParent(root.transform, false);
            Func<string, GameObject> inactive = n => { var go = new GameObject(n); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, preview); return go; };
            var friendly = inactive("Friendly fixture HQ").AddComponent<FactionHQ>();
            var hostile = inactive("Hostile fixture HQ").AddComponent<FactionHQ>();
            Action<Unit, FactionHQ> team = (unit, hq) =>
            {
                var field = typeof(Unit).GetField("HQ", Fields); var sync = field.GetValue(unit);
                sync.GetType().GetField("_component", Fields).SetValue(sync, hq); field.SetValue(unit, sync);
                typeof(Unit).GetField("<remoteSim>k__BackingField", Fields).SetValue(unit, false);
            };
            var owner = inactive("Source building").AddComponent<Building>(); team(owner, friendly);
            var boat = inactive("Small fast boat").AddComponent<Ship>(); boat.definition = definition; team(boat, hostile);
            var rb = boat.gameObject.AddComponent<Rigidbody>(); rb.useGravity = false;
            typeof(Unit).GetField("<rb>k__BackingField", Fields).SetValue(boat, rb);
            Check((bool)top.GetMethod("Preferred", Static).Invoke(null, new object[] { boat }), "Small ship not preferred.");
            definition.length = 162f;
            Check(!(bool)top.GetMethod("Preferred", Static).Invoke(null, new object[] { boat }), "Large ship incorrectly preferred over clusters.");
            definition.length = 20f;
            Check((bool)top.GetMethod("Enemy", Static).Invoke(null, new object[] { owner, boat }), "Hostile ship rejected.");
            team(boat, friendly);
            Check(!(bool)top.GetMethod("Enemy", Static).Invoke(null, new object[] { owner, boat }), "Friendly ship accepted.");
            team(boat, hostile);
            foreach (var args in new[] {
                new object[] { new Vector3(-20, 0, 0), new Vector3(20, 0, 0), Vector3.zero, Vector3.zero, new Vector3(.5f, 1, 1), .1f, 0f },
                new object[] { Vector3.zero, Vector3.zero, new Vector3(-20, 0, 0), new Vector3(20, 0, 0), new Vector3(.5f, 1, 1), .1f, 0f } })
                Check((bool)scan.GetMethod("SweptFootprint", Static).Invoke(null, args), "Between-tick crossing was missed.");
            var miss = new object[] { new Vector3(-20, 0, 10), new Vector3(20, 0, 10), Vector3.zero, Vector3.zero, Vector3.one, .1f, 0f };
            Check(!(bool)scan.GetMethod("SweptFootprint", Static).Invoke(null, miss), "Off-scan target incorrectly detected.");
            reports.Add("Passed: beam and small hull crossing between ticks (both endpoints outside), off-scan rejection, small-ship preference and faction exclusion.");

            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/pilotDismounted_PLACEHOLDER.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/Emplacement_203mmCG_SPECIAL.prefab");
            var gun = prefab.GetComponentInChildren<Gun>(true);
            Check(prefab.transform.Find("TopAttackSubmunitionTemplate"), "SPECIAL model template missing.");
            var staging = inactive("Inactive gun delivery fixture");
            var cloned = UnityEngine.Object.Instantiate(prefab, staging.transform, false); cloned.transform.position = new Vector3(50000, 20, 50000);
            var deliveryGun = cloned.GetComponentInChildren<Gun>(true); var deliveryOwner = cloned.GetComponent<Building>(); team(deliveryOwner, friendly);
            boat.transform.position = new Vector3(50000, 0, 55000);
            illumination.GetMethod("Observe", Static).Invoke(null, new object[] { deliveryGun, boat });
            var motion = ((IDictionary)illumination.GetField("Observations", Static).GetValue(null))[deliveryGun];
            motion.GetType().GetField("SampleAt", Fields).SetValue(motion, Time.time - 2f);
            motion.GetType().GetField("PollAt", Fields).SetValue(motion, Time.time - 2f);
            boat.transform.position += Vector3.forward * 80f; rb.velocity = Vector3.forward * 40f;
            assembly.GetType("GunmageddonAudio.CoastalArtillery").GetMethod("GetPlan", Static).Invoke(null, new object[] { deliveryGun, boat, cloned.transform.position });
            var ranging = assembly.GetType("GunmageddonAudio.CoastalRanging");
            var stateType = ranging.GetNestedType("GunState", BindingFlags.NonPublic);
            var state = Activator.CreateInstance(stateType, true);
            stateType.GetField("Target", Fields).SetValue(state, boat);
            stateType.GetField("SolutionTargetPosition", Fields).SetValue(state, boat.transform.position.ToGlobalPosition());
            var solution = stateType.GetField("Solution", Fields).GetValue(state);
            solution.GetType().GetMethod("Reset").Invoke(solution, new object[] { boat.GetInstanceID(), 0f, 0f });
            solution.GetType().GetMethod("Observe").Invoke(solution, new object[] { 1, 2, 0f, 0f, 0f, 60f, 2 });
            ((IDictionary)ranging.GetField("Guns", Static).GetValue(null))[deliveryGun] = state;
            Check((bool)special.GetMethod("WantsTopAttack", Static).Invoke(null, new object[] { deliveryGun, boat }), "Ranged small boat did not select top attack.");
            var delivery = illumination.GetMethod("GetPlan", Static).Invoke(null, new object[] { deliveryGun, boat });
            var deliveryVelocity = (Vector3)delivery.GetType().GetField("LaunchVelocity", Fields).GetValue(delivery);
            Check(((Vector3)delivery.GetType().GetField("EstimatedVelocity", Fields).GetValue(delivery)).z > 39f, "Ranging-stage motion was not available to the first carrier.");
            Check((bool)delivery.GetType().GetField("Reachable", Fields).GetValue(delivery) && deliveryVelocity.y > 0, "Elevated deployment trajectory is unreachable.");
            var muzzle = ((Transform[])typeof(Gun).GetField("muzzles", Fields).GetValue(deliveryGun))[0];
            var solver = new AimSolver(); typeof(AimSolver).GetMethod("SetTarget", Fields).Invoke(solver, new object[] { deliveryOwner, boat, muzzle, deliveryGun.info });
            var aimArguments = new object[] { solver, 0f, Vector3.zero };
            Check(!(bool)assembly.GetType("GunmageddonAudio.CoastalArtillery").GetMethod("Aim", Static).Invoke(null, aimArguments) &&
                Vector3.Angle((Vector3)aimArguments[2], deliveryVelocity) < .01f, "Gun aiming did not follow the elevated delivery solution.");
            var prepared = special.GetMethod("Prepare", Static).Invoke(null, new object[] { deliveryGun, boat, cloned.transform.position, deliveryVelocity });
            Check(carrierType.GetField("Kind", Fields).GetValue(prepared).ToString() == "TopAttack", "Prepared carrier lost selected payload.");
            var aim = ((GlobalPosition)delivery.GetType().GetField("EstimatedPosition", Fields).GetValue(delivery)).ToLocalPosition();
            float bornAt = (float)carrierType.GetField("Born", Fields).GetValue(prepared);
            Check((bool)special.GetMethod("Fuse", Static).Invoke(null, new object[] { prepared, aim + Vector3.up * 600, Vector3.down * 20, bornAt + 10 }), "Overhead carrier did not deploy.");
            Check(!(bool)special.GetMethod("Fuse", Static).Invoke(null, new object[] { prepared, aim + Vector3.up * 100, Vector3.down * 20, bornAt + 10 }), "Carrier deployed too low.");
            special.GetMethod("Cancel", Static).Invoke(null, new[] { prepared });
            reports.Add("Passed: ranged SPECIAL gun selects top attack against small ships, solves an elevated carrier trajectory and deploys overhead rather than near sea level.");
            Check((bool)top.GetMethod("Reserve", Static).Invoke(null, new object[] { gun }), "Payload reservation failed.");
            var carrier = Activator.CreateInstance(carrierType, true);
            Action<string, object> set = (n, v) => carrierType.GetField(n, Fields).SetValue(carrier, v);
            set("Kind", Enum.Parse(kind, "TopAttack")); set("Gun", gun); set("Owner", owner); set("TopAttackReserved", true);
            special.GetMethod("Cancel", Static).Invoke(null, new[] { carrier });
            Check((bool)top.GetMethod("Reserve", Static).Invoke(null, new object[] { gun }), "Aborted carrier leaked slots.");
            set("TopAttackReserved", true);
            special.GetMethod("Release", Static).Invoke(null, new object[] { carrier, new Vector3(50000, 400, 50000).ToGlobalPosition(), new Vector3(50, -30, 0) });
            var objects = (IList)top.GetField("Objects", Static).GetValue(null);
            Check(objects.Count == 2, "Carrier did not release exactly two submunitions.");
            special.GetMethod("Release", Static).Invoke(null, new object[] { carrier, Vector3.zero.ToGlobalPosition(), Vector3.zero });
            Check(objects.Count == 2 && !(bool)top.GetMethod("Available", Static).Invoke(null, new object[] { gun }), "Duplicate release or per-gun limit failed.");
            foreach (GameObject pod in objects)
            {
                var meshes = pod.GetComponentsInChildren<MeshFilter>(true);
                Check(meshes.Any(m => m.sharedMesh.name == "SADARM") && meshes.Any(m => m.sharedMesh.name == "parachute1_canopy"), "User body or native canopy missing.");
                var missile = pod.GetComponent<Missile>();
                Check(missile && missile.definition.code == "BOMB" && missile.definition.typeIdentity.missile == 1f && pod.GetComponent<Mirage.NetworkIdentity>(), "Submunition is not a native networked bomb.");
                Check((float)typeof(Missile).GetMethod("InterceptPriority", Fields).Invoke(missile, new object[] { owner, 10000f, .001f }) > 0, "Native bomb cannot be prioritized for interception.");
                Check(pod.GetComponentsInChildren<Light>().Length == 0, "Submunition created expensive illumination lights.");
            }
            reports.Add("Passed: packaged user model, two native parachutes, no added lights, one-shot release, aborted-carrier cleanup and per-gun budget.");

            var first = (GameObject)objects[0]; var component = first.GetComponent(assembly.GetType("GunmageddonAudio.TopAttackSubmunition"));
            component.GetType().GetField("age", Fields).SetValue(component, 2.1f);
            component.GetType().GetField("phase", Fields).SetValue(component, 0f);
            component.GetType().GetField("velocity", Fields).SetValue(component, Vector3.down * 8f);
            float dt = Time.fixedDeltaTime, nextAge = 2.1f + dt, angle = dt * Mathf.PI * 2 * .65f;
            var direction = (Vector3)component.GetType().GetMethod("Direction", Static).Invoke(null, new object[] { angle });
            Vector3 predictedOrigin = new Vector3(50000, 400 - 8 * dt, 50000) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Mathf.SmoothStep(0, 4, nextAge / 3);
            Vector3 footprint = predictedOrigin + direction * ((1.2f - predictedOrigin.y) / direction.y);
            boat.transform.position = new Vector3(footprint.x, 0, footprint.z); rb.velocity = Vector3.right * 40;
            scanned = boat;
            component.GetType().GetMethod("FixedUpdate", Fields).Invoke(component, null);
            Check(objects.Count == 4 && (float)component.GetType().GetField("spentAt", Fields).GetValue(component) >= 0, "Swept scan did not fire a projectile and detach the parachute.");
            var discarded = (GameObject)objects[3];
            Check(discarded.GetComponent(assembly.GetType("GunmageddonAudio.DiscardedTopAttackParachute")) && discarded.transform.parent == root.transform,
                "Discarded parachute did not become independent under the world origin.");
            Check(!first.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh.name == "parachute1_canopy"), "Parachute remains attached to spent body.");
            var slugMuzzle = first.transform.Find("ProjectileMuzzle");
            var flash = slugMuzzle.Find("Top attack slug muzzle flash");
            Check(flash && flash.localPosition.sqrMagnitude < .00001f && flash.GetComponent<ParticleSystem>().main.maxParticles == 10,
                "Discharge flash missing or not positioned at the projectile muzzle.");
            Check(Vector3.Dot(flash.forward, (((GameObject)objects[2]).transform.up)) > .999f, "Flash does not point along the launched slug.");
            component.GetType().GetMethod("FixedUpdate", Fields).Invoke(component, null);
            Check(objects.Count == 4, "Submunition fired more than once.");
            Check(slugMuzzle.childCount == 1, "Repeated scan duplicated the muzzle flash.");
            Check(!(bool)top.GetMethod("Claim", Static).Invoke(null, new object[] { boat, 1f }), "Target reservation allowed an immediate duplicate shot.");
            reports.Add("Passed: actual physics-tick spiral scan acquires a 40m/s boat, launches one projectile and reserves that target.");
            float initialDownward = ((Vector3)component.GetType().GetField("velocity", Fields).GetValue(component)).y;
            for (int i=0;i<100;i++) component.GetType().GetMethod("FixedUpdate", Fields).Invoke(component, null);
            Check(first && first.transform.position.y > Datum.LocalSeaY + 100f && ((Vector3)component.GetType().GetField("velocity", Fields).GetValue(component)).y < initialDownward - 15f,
                "Spent canister disappeared after 1.5 seconds or did not accelerate under gravity.");
            Check(discarded && discarded.GetComponentInChildren<MeshFilter>(true), "Detached canopy vanished with the body.");
            reports.Add("Passed: canopy disconnects on discharge and remains separate; inert spent canister retains momentum and accelerates rather than despawning after 1.5 seconds.");

            sinkObject = new GameObject("Physical projectile target collider");
            sinkObject.transform.position = new Vector3(50100, 10, 50000);
            sinkObject.AddComponent<BoxCollider>().size = new Vector3(4, 2, 4);
            var sink = sinkObject.AddComponent<TopAttackDamageFixture>(); sink.Unit = boat;
            Physics.SyncTransforms();
            top.GetMethod("Fire", Static).Invoke(null, new object[] { owner, new Vector3(50100, 60, 50000), new Vector3(50100, 0, 50000) });
            var projectile = (GameObject)objects[objects.Count - 1];
            Check(projectile.GetComponent<Rigidbody>() && projectile.GetComponent<Collider>(), "Slug is not a physical modeled projectile.");
            var slug = projectile.GetComponent(assembly.GetType("GunmageddonAudio.TopAttackSlug"));
            slug.GetType().GetMethod("FixedUpdate", Fields).Invoke(slug, null);
            Check(sink.Calls == 0 && projectile.transform.position.y < 60f && projectile.transform.position.y > 11f, "Slug applied instant damage before reaching the collider.");
            slug.GetType().GetMethod("FixedUpdate", Fields).Invoke(slug, null);
            Check(sink.Calls == 1 && sink.Pierce >= 1000f, "Physical collision did not apply native penetrating damage exactly once. Calls=" + sink.Calls + " pierce=" + sink.Pierce + " y=" + projectile.transform.position.y + " active=" + projectile.activeSelf + " server=" + owner.IsServer + "/" + boat.IsServer + " sink=" + sinkObject.GetComponent<IDamageable>());
            sink.Calls = 0; team(boat, friendly);
            top.GetMethod("Fire", Static).Invoke(null, new object[] { owner, new Vector3(50100, 60, 50000), new Vector3(50100, 0, 50000) });
            var friendlySlug = ((GameObject)objects[objects.Count - 1]).GetComponent(assembly.GetType("GunmageddonAudio.TopAttackSlug"));
            for (int i = 0; i < 2; i++) friendlySlug.GetType().GetMethod("FixedUpdate", Fields).Invoke(friendlySlug, null);
            Check(sink.Calls == 0, "Friendly hull took top-attack damage.");
            reports.Add("Passed: modeled Rigidbody/collider projectile, real flight before impact, native IDamageable penetration on collision and friendly-hit exclusion.");
            var effectType = assembly.GetType("GunmageddonAudio.TopAttackEffects");
            var canisterLand = (IList)effectType.GetField("CanisterLand", Static).GetValue(null);
            var canisterWater = (IList)effectType.GetField("CanisterWater", Static).GetValue(null);
            Action<Vector3,Vector3> placeSpent = (at,vel) =>
            {
                first.transform.position=at;
                component.GetType().GetField("center",Fields).SetValue(component,at.ToGlobalPosition());
                component.GetType().GetField("velocity",Fields).SetValue(component,vel);
                component.GetType().GetField("retired",Fields).SetValue(component,false);
            };
            placeSpent(new Vector3(50100,12,50000),Vector3.down*100f);
            component.GetType().GetMethod("FixedUpdate",Fields).Invoke(component,null);
            Check(canisterLand.Count==1 && canisterWater.Count==0 && sink.Calls==0,"Canister hull landing failed or applied damage");
            Check((bool)component.GetType().GetField("retired",Fields).GetValue(component),"Landed canister did not retire");
            component.GetType().GetMethod("FixedUpdate",Fields).Invoke(component,null);
            Check(canisterLand.Count==1,"Landing repeated after retirement");
            placeSpent(new Vector3(50500,1,50000),Vector3.down*100f);
            component.GetType().GetMethod("FixedUpdate",Fields).Invoke(component,null);
            Check(canisterWater.Count==1 && Mathf.Abs(first.transform.position.y-(Datum.LocalSeaY+.05f))<.001f,"Ocean crossing did not create a small water impact at the surface");
            foreach(var pool in new[]{canisterLand,canisterWater}) foreach(Component effect in pool)
            {
                var ps=effect.GetComponent<ParticleSystem>();
                Check(ps.main.maxParticles==16 && ps.main.startSize.constantMax<=.4f && effect.GetComponentsInChildren<Light>().Length==0 && effect.GetComponentsInChildren<AudioSource>().Length==0,
                    "Spent casing effect is oversized or contains explosive light/audio");
            }
            reports.Add("Passed: spent casing lands on a hull or ocean plane, emits only a small puff/splash, retires once and inflicts no extra damage.");
            var chuteType=assembly.GetType("GunmageddonAudio.DiscardedTopAttackParachute");
            var ownedMaterial=chuteType.GetField("material",Fields).GetValue(discarded.GetComponent(chuteType)) as Material;
            UnityEngine.Object.DestroyImmediate(first);
            Check(discarded && ownedMaterial,"Destroying spent body also destroyed detached canopy/material");
            // Inactive edit-mode native fixtures do not get runtime lifecycle callbacks.
            chuteType.GetMethod("OnDestroy",Fields).Invoke(discarded.GetComponent(chuteType),null);
            UnityEngine.Object.DestroyImmediate(discarded);
            Check(!ownedMaterial,"Detached canopy leaked its owned material");
            reports.Add("Passed: body retirement preserves detached canopy; canopy cleanup releases its material.");
            return string.Join("\n", reports);
        }
        finally
        {
            scanned = null;
            if (sinkObject) UnityEngine.Object.DestroyImmediate(sinkObject);
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            top.GetMethod("Shutdown", Static).Invoke(null, null);
            var effects = (IList)special.GetField("Effects", Static).GetValue(null);
            foreach (GameObject go in effects) if (go) UnityEngine.Object.DestroyImmediate(go);
            effects.Clear();
            foreach (string name in new[] { "SmokeMaterial", "ChaffMaterial", "SparkMaterial", "puff" })
            { var obj = special.GetField(name, Static).GetValue(null) as UnityEngine.Object; if (obj) UnityEngine.Object.DestroyImmediate(obj); }
            UnityEngine.Object.DestroyImmediate(definition);
            origin.SetValue(null, savedOrigin); originPosition.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
