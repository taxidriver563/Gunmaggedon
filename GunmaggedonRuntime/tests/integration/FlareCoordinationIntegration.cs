using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlareCoordinationIntegration
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static GlobalPosition observed;
    static int forecastComputations;
    public static void CountForecast() { forecastComputations++; }
    public static bool Known(ref GlobalPosition __1, ref bool __result) { __1 = observed; __result = true; return false; }
    public static bool Night(ref bool __result) { __result = true; return false; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        var a = Assembly.Load(System.IO.File.ReadAllBytes(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER") ?? System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.8.4/Gunmaggedon.Runtime.dll")));
        var feature = a.GetType("GunmageddonAudio.CoastalIllumination");
        var planType = feature.GetNestedType("Plan", BindingFlags.NonPublic);
        var plans = (IDictionary)feature.GetField("Plans", Static).GetValue(null);
        var tokens = (IList)feature.GetField("Tokens", Static).GetValue(null);
        var flares = (IList)feature.GetField("Flares", Static).GetValue(null);
        var retries = (IDictionary)feature.GetField("RetryAfter", Static).GetValue(null);
        var game = typeof(Unit).Assembly;
        var datum = game.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var originPosition = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = originPosition.GetValue(null);
        var randomState = UnityEngine.Random.state;
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.flare.coordination.regression" });
        Action<MethodInfo, string> patch = (method, callback) =>
        {
            var prefix = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { typeof(FlareCoordinationIntegration).GetMethod(callback) });
            harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(harmony, new object[] { method, prefix, null, null, null });
        };
        patch(typeof(FactionHQ).GetMethod("TryGetKnownPosition", Fields), "Known");
        patch(feature.GetMethod("NeedsLight", Static), "Night");
        patch(feature.GetMethod("ForecastCovered", Static), "CountForecast");
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Flare token regression"); SceneManager.MoveGameObjectToScene(root, preview);
        var report = new StringBuilder();
        try
        {
            origin.SetValue(null, root.transform); originPosition.SetValue(null, Vector3.zero);
            Func<string, GameObject> inactive = name => { var go = new GameObject(name); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, preview); return go; };
            var hq = inactive("Friendly HQ").AddComponent<FactionHQ>();
            var otherHQ = inactive("Other HQ").AddComponent<FactionHQ>();
            Func<string, FactionHQ, Gun> makeGun = (name, team) =>
            {
                var go = inactive(name); var owner = go.AddComponent<Building>();
                typeof(Unit).GetField("<remoteSim>k__BackingField", Fields).SetValue(owner, false);
                var hqField = typeof(Unit).GetField("HQ", Fields); object sync = hqField.GetValue(owner);
                sync.GetType().GetField("_component", Fields).SetValue(sync, team); hqField.SetValue(owner, sync);
                Check(owner.NetworkHQ == team, "Fixture did not assign actual faction HQ.");
                var gun = go.AddComponent<Gun>(); gun.attachedUnit = owner;
                gun.info = AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/Mods/Gunmageddon/203mmEmplacement.asset");
                var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(go.transform, false);
                typeof(Gun).GetField("muzzles", Fields).SetValue(gun, new[] { muzzle });
                return gun;
            };
            var first = makeGun("First battery", hq); var second = makeGun("Second battery", hq); var opponent = makeGun("Other side", otherHQ);
            var target = inactive("Moving ship").AddComponent<Ship>(); target.transform.position = new Vector3(5000, 0, 5000);
            observed = target.transform.position.ToGlobalPosition();
            Action<object, string, object> set = (p, name, value) => planType.GetField(name, Fields).SetValue(p, value);
            Func<Gun, Vector3, Vector3, object> plan = (gun, known, velocity) =>
            {
                var p = Activator.CreateInstance(planType, true); set(p, "Gun", gun); set(p, "Target", target);
                set(p, "KnownPosition", known.ToGlobalPosition()); set(p, "EstimatedPosition", known.ToGlobalPosition());
                set(p, "EstimatedVelocity", velocity); set(p, "FlightSeconds", 15f); set(p, "DeploymentAltitude", 600f);
                set(p, "Reachable", true); set(p, "Created", Time.time); plans[gun] = p; return p;
            };
            Func<Gun, object, float, bool> request = (gun, p, now) => (bool)feature.GetMethod("RequestToken", Static).Invoke(null, new object[] { gun, p, now });
            var position = target.transform.position;
            var p1 = plan(first, position, Vector3.zero); var p2 = plan(second, position + Vector3.right * 100, Vector3.zero); var pe = plan(opponent, position, Vector3.zero);
            float now = Time.time;
            Check(request(first, p1, now), "First gun did not receive a token.");
            set(p2, "KnownPosition", (position + Vector3.right * 3000).ToGlobalPosition());
            Check(!request(second, p2, now), "Conflicting motion estimates duplicated a flare for one ship.");
            set(p2, "KnownPosition", (position + Vector3.right * 100).ToGlobalPosition());
            set(p2, "Target", inactive("Neighboring ship").AddComponent<Ship>());
            Check(!request(second, p2, now), "Nearby friendly target got a duplicate token.");
            Check(request(opponent, pe, now), "Opposing factions incorrectly shared reservations.");
            Check(request(first, p1, now) && tokens.Count == 2, "Repeated aiming allocated extra tokens.");
            feature.GetMethod("Launched", Static).Invoke(null, new[] { p1 });
            Check(!request(second, p2, now + 10f), "Friendly gun duplicated an in-flight flare.");
            feature.GetMethod("Ended", Static).Invoke(null, new[] { p1 });
            Check(request(second, p2, now + 10f), "Failed/intercepted shell did not release token.");
            tokens.Clear(); retries.Clear();
            set(p1, "LastLaunch", -1000f);
            Check(request(first, p1, now), "Fresh reservation failed.");
            Check(request(second, p2, now + 13f), "Stalled aiming token did not expire.");
            Check(!request(first, p1, now + 13f), "Expired owner immediately monopolized token again.");
            tokens.Clear(); retries.Clear();
            var distant = plan(second, position + Vector3.right * 3000, Vector3.zero);
            var distantTarget = inactive("Distant ship").AddComponent<Ship>();
            set(distant, "Target", distantTarget);
            Check(request(first, p1, now) && request(second, distant, now), "Separate target areas incorrectly blocked each other.");
            report.AppendLine("Passed: shared faction/spatial tokens, neighboring ships, opposing factions, in-flight suppression, failure release, expiring leases and independent distant areas.");

            tokens.Clear(); retries.Clear(); plans.Clear();
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/pilotDismounted_PLACEHOLDER.prefab");
            Check((bool)feature.GetMethod("EnsureVisuals", Static).Invoke(null, null), "Native parachute not available.");
            var flareGo = new GameObject("Coverage forecast flare"); SceneManager.MoveGameObjectToScene(flareGo, preview);
            var flare = flareGo.AddComponent(a.GetType("GunmageddonAudio.ParachuteIllumination"));
            flare.GetType().GetMethod("Initialize").Invoke(flare, new object[] { (position + Vector3.up * 600).ToGlobalPosition(), Vector3.down * 2.5f, 120f, 1000f, 2.5f,
                feature.GetField("glow", Static).GetValue(null), feature.GetField("nativeParachute", Static).GetValue(null) });
            flare.GetType().GetField("born", Fields).SetValue(flare, Time.time - 10f);
            flareGo.GetComponent<Light>().intensity = 200000f; flares.Add(flare);
            target.transform.position = position + Vector3.right * 500;
            p1 = plan(first, target.transform.position, Vector3.right * 20);
            p2 = plan(second, target.transform.position, Vector3.right * 20);
            Check((bool)feature.GetMethod("IsIlluminated", Static).Invoke(null, new object[] { target }), "Moving ship was not currently illuminated.");
            Check(!(bool)feature.GetMethod("ForecastCovered", Static).Invoke(null, new object[] { p1, 23f }), "Ship about to leave light was considered covered.");
            Check((bool)feature.GetMethod("ShouldFireFlare", Static).Invoke(null, new object[] { first, target }), "Predictive flare not requested while ship still lit.");
            Check(!(bool)feature.GetMethod("ShouldFireFlare", Static).Invoke(null, new object[] { second, target }), "Predictive replacement duplicated across battery.");
            typeof(Unit).GetField("<remoteSim>k__BackingField", Fields).SetValue(opponent.attachedUnit, true);
            Check(!(bool)feature.GetMethod("ShouldFireFlare", Static).Invoke(null, new object[] { opponent, target }), "Remote visual simulation reserved authoritative flare work.");
            tokens.Clear(); target.transform.position = position;
            p1 = plan(first, position, Vector3.zero);
            Check(!(bool)feature.GetMethod("ShouldFireFlare", Static).Invoke(null, new object[] { first, target }), "Stationary well-covered ship triggered unnecessary flare.");
            forecastComputations = 0;
            set(p1, "ForecastUntil", -1f);
            var cached = feature.GetMethod("ForecastCoveredCached", Static);
            for (int i = 0; i < 100; i++) cached.Invoke(null, new object[] { p1, 23f });
            Check(forecastComputations == 1, "Forecast repeated within its cache interval.");
            feature.GetMethod("CoverageChanged", Static).Invoke(null, null);
            cached.Invoke(null, new object[] { p1, 23f });
            Check(forecastComputations == 2, "Flare appearance/disappearance did not invalidate forecast.");
            set(p1, "ForecastUntil", Time.time - 1f);
            cached.Invoke(null, new object[] { p1, 23f });
            Check(forecastComputations == 3, "Forecast did not refresh after its quarter-second window.");
            report.AppendLine("Passed: 100 repeated forecast requests use one calculation; coverage changes and expiry invalidate the cache.");
            flare.GetType().GetField("born", Fields).SetValue(flare, Time.time - 100f);
            set(p1, "ForecastUntil", -1f);
            Check((bool)feature.GetMethod("ShouldFireFlare", Static).Invoke(null, new object[] { first, target }), "Burnout was not forecast early.");
            flareGo.GetComponent<Light>().enabled = false;
            Check(!(bool)feature.GetMethod("IsIlluminated", Static).Invoke(null, new object[] { target }), "Prediction unlocked firing with no actual light.");
            flares.Clear(); tokens.Clear(); retries.Clear(); plans.Clear();
            report.AppendLine("Passed: replacement requested before moving ship leaves light; duplicate replacement blocked; stationary coverage conserved; burnout predicted; actual-light gate preserved.");

            observed = position.ToGlobalPosition();
            var getPlan = feature.GetMethod("GetPlan", Static);
            p1 = getPlan.Invoke(null, new object[] { first, target });
            Check((bool)planType.GetField("Reachable", Fields).GetValue(p1), "Native flare trajectory solve failed.");
            set(p1, "Created", Time.time - 2.1f);
            var motion = ((IDictionary)feature.GetField("Observations", Static).GetValue(null))[first];
            motion.GetType().GetField("SampleAt", Fields).SetValue(motion, Time.time - 2.1f);
            motion.GetType().GetField("PollAt", Fields).SetValue(motion, Time.time - 2.1f);
            observed = (position + Vector3.right * 63).ToGlobalPosition();
            var led = getPlan.Invoke(null, new object[] { first, target });
            var v = (Vector3)planType.GetField("EstimatedVelocity", Fields).GetValue(led);
            var knownPosition = ((GlobalPosition)planType.GetField("KnownPosition", Fields).GetValue(led)).ToLocalPosition();
            var aim = ((GlobalPosition)planType.GetField("EstimatedPosition", Fields).GetValue(led)).ToLocalPosition();
            float flight = (float)planType.GetField("FlightSeconds", Fields).GetValue(led);
            Check(v.x > 29f && v.x < 31f, "Observed movement was not estimated.");
            Check(aim.x - knownPosition.x > v.x * flight + 70f, "Aim omitted post-arrival forward lead.");
            Check((bool)planType.GetField("Reachable", Fields).GetValue(led), "Led trajectory became unreachable.");
            report.AppendLine(string.Format("Passed: faction-known positions drive velocity estimate ({0:F1}m/s), iterated ballistic flight ({1:F1}s) and ahead-of-arrival placement ({2:F0}m lead).", v.x, flight, aim.x - knownPosition.x));
            return report.ToString();
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            tokens.Clear(); retries.Clear(); plans.Clear(); flares.Clear();
            UnityEngine.Random.state = randomState;
            origin.SetValue(null, savedOrigin); originPosition.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
