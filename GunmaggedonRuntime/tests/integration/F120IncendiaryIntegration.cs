using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class F120IncendiaryIntegration
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static bool server = true, local = true;
    static int checks, calls, blasts; static float fire, pierce;
    static UnitPart hitPart;
    public static bool Server(Mirage.NetworkBehaviour __instance, ref bool __result) { if (!(__instance is Unit)) return true; __result = server; return false; }
    public static bool Local(ref bool __result) { __result = local; return false; }
    public static bool Blast() { blasts++; return false; }
    public static bool Rpc(Unit __instance, byte __0, DamageInfo __1)
    {
        calls++; float p = NetworkFloatHelper.Decompress(__1.pierceDamage), f = NetworkFloatHelper.Decompress(__1.fireDamage);
        pierce += p; fire += f;
        if (hitPart && __instance == hitPart.parentUnit)
            ((IDamageable)hitPart).ApplyDamage(p, NetworkFloatHelper.Decompress(__1.blastDamage), f, NetworkFloatHelper.Decompress(__1.impactDamage));
        return false;
    }
    static void Check(bool b, string message) { checks++; if (!b) throw new Exception(message); }
    public static object Run()
    {
        checks = calls = blasts = 0; fire = pierce = 0; server = local = true;
        var a = Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
        var runtime = a.GetTypes().Single(t => t.Name == "IncendiaryCanisterRuntime");
        var flak = a.GetTypes().Single(t => t.Name == "F88FlakRuntime");
        var optimization = a.GetTypes().Single(t => t.Name == "OptimizationRuntime");
        var h = new Harmony("gunmaggedon.f120.incendiary.fixture");
        var previousScene = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(previousScene);
        var stage = new GameObject("Incendiary fixture"); SceneManager.MoveGameObjectToScene(stage, scene); stage.transform.position = Vector3.one * 30000;
        var origin = typeof(Datum).GetField("<origin>k__BackingField", S); var originPosition = typeof(Datum).GetField("<originPosition>k__BackingField", S);
        object old = origin.GetValue(null), oldPosition = originPosition.GetValue(null);
        try
        {
            h.Patch(typeof(Unit).GetProperty("LocalSim").GetMethod, prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("Local")));
            h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod, prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("Server")));
            h.Patch(typeof(Unit).GetMethod("RpcDamage", F), prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("Rpc")));
            if (typeof(Aircraft).GetMethod("RpcDamage", F).DeclaringType != typeof(Unit))
                h.Patch(typeof(Aircraft).GetMethod("RpcDamage", F), prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("Rpc")));
            h.Patch(typeof(DamageEffects).GetMethod("BlastFrag", S), prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("Blast")));
            origin.SetValue(null, stage.transform); originPosition.SetValue(null, Vector3.zero);
            var planeObject = new GameObject("inactive native aircraft", typeof(Mirage.NetworkIdentity)); planeObject.SetActive(false); planeObject.transform.SetParent(stage.transform, false);
            var plane = planeObject.AddComponent<Aircraft>();
            var receiver = new GameObject("Native part receiver"); receiver.transform.SetParent(stage.transform, false); receiver.transform.localPosition = Vector3.forward * 100;
            receiver.SetActive(false); hitPart = receiver.AddComponent<UnitPart>(); hitPart.parentUnit = plane;
            typeof(UnitPart).GetProperty("xform", F).SetValue(hitPart, receiver.transform);
            typeof(UnitPart).GetField("armorProperties", F).SetValue(hitPart, new ArmorProperties { pierceArmor = 20, pierceTolerance = 100, fireTolerance = 10 });
            typeof(UnitPart).GetField("hitPoints", F).SetValue(hitPart, 100000f); typeof(UnitPart).GetField("criticalPart", F).SetValue(hitPart, false);
            var materialField = typeof(UnitPart).GetField("damageMaterial", F);
            var material = Activator.CreateInstance(materialField.FieldType);
            materialField.FieldType.GetField("renderers", F).SetValue(material, new System.Collections.Generic.List<Renderer>());
            materialField.SetValue(hitPart, material);
            var collider = receiver.AddComponent<BoxCollider>(); collider.size = new Vector3(300, 300, 2);
            // Collider-only proxy routes native damage on the same object,
            // without running UnitPart.Awake/native aircraft mission setup.
            var proxy = new GameObject("Active collider proxy"); proxy.transform.SetParent(stage.transform, false); proxy.transform.localPosition = Vector3.forward * 100;
            var proxyCollider = proxy.AddComponent<BoxCollider>(); proxyCollider.size = collider.size;
            UnityEngine.Object.DestroyImmediate(proxy);
            // Native receiver can be enabled after its Awake is suppressed.
            h.Patch(typeof(UnitPart).GetMethod("Awake", F), prefix: new HarmonyMethod(typeof(F120IncendiaryIntegration).GetMethod("SkipAwake")));
            receiver.SetActive(true); Physics.SyncTransforms();
            var begin = runtime.GetMethod("Begin", S); var tick = runtime.GetMethod("Tick", S); var clear = runtime.GetMethod("Clear", S);
            Action<bool> launch = authority => begin.Invoke(null, new object[] { stage.transform.position.ToGlobalPosition(), Vector3.forward * 950, PersistentID.None, 42, authority, 0f });
            Action advance = () => { for (int i = 0; i < 400; i++) tick.Invoke(null, new object[] { .02f }); };
            launch(true); Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 1, "One pooled flight owns96 fragments");
            var firstFlight = ((IList)runtime.GetField("flights", S).GetValue(null))[0];
            var smokeField = firstFlight.GetType().GetField("Smoke", F);
            var smoke = (ParticleSystem)smokeField.GetValue(firstFlight);
            Check(smoke.main.startLifetime.constant == 5 && smoke.main.simulationSpace == ParticleSystemSimulationSpace.Custom && smoke.main.customSimulationSpace == stage.transform,
                "Five-second smoke uses floating-origin simulation space");
            tick.Invoke(null, new object[] { .02f });
            Check(smoke.particleCount > 0 && !smoke.emission.enabled && smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                "Fragments emit textured visual-only smoke");
            for (int i = 0; i < 150; i++) tick.Invoke(null, new object[] { .02f });
            Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 1 && smoke.particleCount > 0,
                "Smoke outlives fragment collisions and retired heads");
            smoke.Simulate(5.1f, false, false, false);
            Check(smoke.particleCount == 0, "Native smoke particles expire after five seconds");
            advance(); Check(calls > 0 && fire > 0 && pierce > 0, "Swept fragments cause native penetration and fire damage");
            Check((float)typeof(UnitPart).GetField("hitPoints", F).GetValue(hitPart) < 100000, "Native armor and HP reduction executed");
            Check(blasts == 0, "No ordinary HE/flak blast added to canister");
            Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 0, "Flight retires after collisions/lifetime");
            int before = calls; launch(false);
            var reusedFlight = ((IList)runtime.GetField("flights", S).GetValue(null))[0];
            var reusedSmoke = (ParticleSystem)smokeField.GetValue(reusedFlight);
            Check(reusedSmoke == smoke && reusedSmoke.particleCount == 0, "Smoke pool reuses a cleared emitter without stale particles");
            advance(); Check(calls == before, "Remote presentation never damages");
            server = false; launch(true); advance(); Check(calls == before, "Non-server native target cannot receive damage"); server = true;
            local = false; launch(true); advance(); Check(calls > before, "Authoritative host also damages remote-pilot aircraft"); local = true; before = calls;
            var wall = new GameObject("Fragment barrier"); wall.transform.SetParent(stage.transform, false); wall.transform.localPosition = Vector3.forward * 50; wall.AddComponent<BoxCollider>().size = new Vector3(1000, 1000, 2); Physics.SyncTransforms();
            launch(true); advance(); Check(calls == before, "Solid geometry stops fragments before aircraft"); UnityEngine.Object.DestroyImmediate(wall);
            launch(true); clear.Invoke(null, null); Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 0, "Mission cleanup releases active flights and visual leases");
            launch(true); origin.SetValue(null, null); tick.Invoke(null, new object[] { .02f });
            Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 0, "A changed mission origin clears flights before the next damage tick");
            origin.SetValue(null, stage.transform);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer Special.prefab");
            var gun = prefab.GetComponentInChildren<Gun>(true);
            Check((bool)runtime.GetMethod("Matches", S).Invoke(null, new object[] { gun }) && (bool)flak.GetMethod("Heavy", S).Invoke(null, new object[] { gun }), "Special uses advanced F120 fire control");
            Check(gun.info.blastDamage == 0 && prefab.GetComponent<GroundVehicle>().definition.jsonKey == "Gunmaggedon_F120S_AA", "Separate spawn identity and no ordinary flak payload");
            var barrel = prefab.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "barrel");
            Check(barrel.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterial == barrel.sharedMaterial), "Red stripe persists across barrel LODs");
            Check(prefab.GetComponentsInChildren<Collider>(true).All(c => c.isTrigger || c.GetComponent<IDamageable>() != null), "Native same-object damage receiver on every collision shape");
            var record = Activator.CreateInstance(flak.GetNestedType("Flight", F), true);
            record.GetType().GetField("Authority", F).SetValue(record, true);
            record.GetType().GetField("Incendiary", F).SetValue(record, true);
            record.GetType().GetField("Blast", F).SetValue(record, 999f);
            var bullet = new BulletSim.Bullet(Vector3.forward * 950, null, 1, false, false, null) { position = stage.transform.position.ToGlobalPosition(), velocity = Vector3.forward * 950, active = false, destructTimer = 0 };
            var carriers = (IDictionary)flak.GetField("Flights", S).GetValue(null); carriers.Add(bullet, record);
            flak.GetMethod("TraceEnd", S).Invoke(null, new object[] { bullet });
            Check(blasts == 0 && ((IList)runtime.GetField("flights", S).GetValue(null)).Count == 1, "Real carrier-expiry hook dispatches canister instead of regular flak");
            flak.GetMethod("TraceEnd", S).Invoke(null, new object[] { bullet });
            Check(((IList)runtime.GetField("flights", S).GetValue(null)).Count == 1, "Carrier dispatch happens exactly once"); clear.Invoke(null, null);
            var network = a.GetTypes().Single(t => t.Name == "F88FlakNetwork"); network.GetMethod("Install", S).Invoke(null, null);
            var mt = a.GetTypes().Single(t => t.Name == "F120IncendiaryBurst"); var message = Activator.CreateInstance(mt);
            mt.GetField("Point", F).SetValue(message, new GlobalPosition(10, 1000, 20)); mt.GetField("Velocity", F).SetValue(message, Vector3.forward * 950);
            mt.GetField("Seed", F).SetValue(message, 1234); mt.GetField("Time", F).SetValue(message, 100d);
            var packer = typeof(Mirage.Serialization.MessagePacker);
            var bytes = (byte[])packer.GetMethods().Single(m => m.Name == "Pack" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1).MakeGenericMethod(mt).Invoke(null, new[] { message });
            var decoded = packer.GetMethod("Unpack").MakeGenericMethod(mt).Invoke(null, new object[] { bytes, null });
            Check((int)mt.GetField("Seed", F).GetValue(decoded) == 1234 && ((Vector3)mt.GetField("Velocity", F).GetValue(decoded)).z == 950, "Native Mirage burst seed/velocity serializer round-trip");
            Check((byte)network.GetField("ProtocolVersion", S).GetRawConstantValue() == 6, "Protocol separates incompatible clients"); network.GetMethod("Shutdown", S).Invoke(null, null);
            return new { passed = true, checks, nativeHits = calls, nativeFire = fire, nativePierce = pierce, details = "Actual swept physics and native UnitPart armor/HP/fire channel; RPC delivery replayed locally, no live mission ignition/transport certification." };
        }
        finally
        {
            runtime.GetMethod("Shutdown", S).Invoke(null, null); optimization.GetMethod("Clear", S).Invoke(null, null);
            origin.SetValue(null, old); originPosition.SetValue(null, oldPosition); hitPart = null; h.UnpatchSelf(); EditorSceneManager.CloseScene(scene, true);
        }
    }
    public static bool SkipAwake() { return false; }
}
