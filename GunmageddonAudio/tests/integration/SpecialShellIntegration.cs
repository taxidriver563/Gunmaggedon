using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpecialShellIntegration
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool success, string message) { if (!success) throw new Exception(message); }
    public static object Run()
    {
        var a = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.5.4/GunmageddonAudio.dll")));
        var feature = a.GetType("GunmageddonAudio.SpecialShells");
        var obscurants = a.GetType("GunmageddonAudio.ShellObscurants");
        var kind = a.GetType("GunmageddonAudio.SpecialShellKind");
        var mode = a.GetType("GunmageddonAudio.SpecialShellMode");
        var carrierType = feature.GetNestedType("Carrier", BindingFlags.NonPublic);
        var ranging = a.GetType("GunmageddonAudio.CoastalRanging");
        var game = typeof(Unit).Assembly;
        var toGlobal = game.GetType("GlobalPositionExtensions").GetMethod("ToGlobalPosition");
        Func<Vector3, object> global = p => toGlobal.Invoke(null, new object[] { p });
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("HarmonyLib.Harmony")).First(x => x != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.special.integration" });
        var preview = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        try
        {
            var choose = feature.GetMethod("Choose", Static);
            Func<string, float, float, float, string> selection = (m, now, smoke, chaff) =>
                choose.Invoke(null, new object[] { Enum.Parse(mode, m), now, smoke, chaff, 90f, 70f, 5, 5 }).ToString();
            Check(selection("Auto", 0, -1000, -1000) == "Chaff", "Overdue support was not selected after five cluster rounds.");
            Check(selection("Auto", 10, -1000, 0) == "Smoke", "Second auto payload was not smoke.");
            Check(selection("Auto", 20, 10, 0) == "Cluster", "Active support did not allow clusters.");
            Check(selection("Auto", 85, 10, 0) == "Chaff", "Expired chaff was not refreshed.");
            foreach (string m in new[] { "Cluster", "Smoke", "Chaff" }) Check(selection(m, 0, -1000, -1000) == m, "Forced payload failed.");
            report.AppendLine("Passed: automatic support/cluster mix, refresh and all forced shell modes.");

            const string folder = "Assets/Blueprinter/Mods/Gunmageddon/";
            var special = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Emplacement_203mmCG_SPECIAL.prefab");
            var standard = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Emplacement_203mmCG.prefab");
            var specialInfo = AssetDatabase.LoadAssetAtPath<WeaponInfo>(folder + "203mmEmplacement_SPECIAL.asset");
            var standardInfo = AssetDatabase.LoadAssetAtPath<WeaponInfo>(folder + "203mmEmplacement.asset");
            var identify = feature.GetMethod("IsSpecial", Static);
            Check((bool)identify.Invoke(null, new object[] { special.GetComponentInChildren<Gun>(true) }), "Special gun not recognized.");
            Check(!(bool)identify.Invoke(null, new object[] { standard.GetComponentInChildren<Gun>(true) }), "Standard gun got special payloads.");
            Check(specialInfo.pierceDamage == 0 && specialInfo.blastDamage == 0, "Special carrier retains HE damage.");
            Check(standardInfo.pierceDamage == 600 && standardInfo.blastDamage == 500, "Standard HE changed.");
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Destroyer1_PLACEHOLDER.prefab");
            var equipment = shipPrefab.GetComponentsInChildren<UnitPart>(true).First(p => p.name == "CIWS_FL");
            var hull = shipPrefab.GetComponentsInChildren<UnitPart>(true).First(p => p.name == "Hull_L");
            var radar = shipPrefab.GetComponentsInChildren<UnitPart>(true).First(p => p.name == "Hull_Radar");
            var damage = feature.GetMethod("DamageFor", Static);
            Check((float)damage.Invoke(null, new object[] { equipment, 0f }) >= 100f, "CIWS not classified as equipment.");
            Check((float)damage.Invoke(null, new object[] { radar, 0f }) >= 100f, "Radar not classified as equipment.");
            Check((float)damage.Invoke(null, new object[] { hull, 0f }) <= .5f, "Excessive hull damage.");
            Check((float)damage.Invoke(null, new object[] { equipment, 7f }) == 0f, "Damage escapes bomblet radius.");
            report.AppendLine("Passed: special-only payload routing, zero-damage carriers, standard HE preserved, native destroyer CIWS/radar versus hull classification and damage falloff.");

            var carrier = Activator.CreateInstance(carrierType, true);
            carrierType.GetField("Kind", Fields).SetValue(carrier, Enum.Parse(kind, "Cluster"));
            carrierType.GetField("Launch", Fields).SetValue(carrier, global(new Vector3(0, 20, 0)));
            carrierType.GetField("Aim", Fields).SetValue(carrier, global(new Vector3(0, 0, 1800)));
            carrierType.GetField("Born", Fields).SetValue(carrier, 0f);
            var fuse = feature.GetMethod("Fuse", Static);
            Check((bool)fuse.Invoke(null, new object[] { carrier, new Vector3(0, 100, 1000), new Vector3(0, -80, 600), 2f }), "Descending airburst failed.");
            Check(!(bool)fuse.Invoke(null, new object[] { carrier, new Vector3(0, 100, 1000), new Vector3(0, 80, 600), 2f }), "Ascending carrier airburst.");
            Check(!(bool)fuse.Invoke(null, new object[] { carrier, new Vector3(0, 600, 1000), new Vector3(0, -80, 600), 2f }), "Carrier opened before its impact lead time.");

            var shotType = ranging.GetNestedType("Shot", BindingFlags.NonPublic);
            var stateType = ranging.GetNestedType("GunState", BindingFlags.NonPublic);
            var state = Activator.CreateInstance(stateType, true);
            var shot = Activator.CreateInstance(shotType, true);
            stateType.GetField("SpottingInfo", Fields).SetValue(state, specialInfo);
            shotType.GetField("State", Fields).SetValue(shot, state);
            shotType.GetField("Payload", Fields).SetValue(shot, carrier);
            var bullet = Activator.CreateInstance(game.GetType("BulletSim+Bullet"), new object[] { new Vector3(0, 0, 850), null, 180f, false, true, null });
            var shots = (IDictionary)ranging.GetField("Shots", Static).GetValue(null); shots.Add(bullet, shot);
            try
            {
                var args = new object[] { bullet, standardInfo, null, false, null };
                ranging.GetMethod("BeforeTrace", Static).Invoke(null, args);
                Check(args[1] == specialInfo && (bool)args[3], "Payload carrier can apply stock damage.");
                Check(((Array)args[4]).Cast<object>().All(x => x == null), "HE impact effects remain on support carriers.");
            }
            finally { shots.Remove(bullet); }
            report.AppendLine("Passed: altitude/descent airburst fuse and carrier damage/HE-impact isolation.");

            obscurants.GetMethod("Install", Static).Invoke(null, new[] { harmony });
            var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod");
            var patchMethod = harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            foreach (var hook in new[] {
                new[] { "BulletSim", "AddBullet", "BeforeAdd", "prefix" },
                new[] { "BulletSim", "AddBullet", "AfterAdd", "finalizer" },
                new[] { "BulletSim+Bullet", "TrajectoryTrace", "BeforeTrace", "prefix" },
                new[] { "BulletSim+Bullet", "TrajectoryTrace", "AfterTrace", "postfix" },
                new[] { "BulletSim+Bullet", "Remove", "BeforeRemove", "prefix" },
                new[] { "Turret", "SetObservedBullet", "ObserveWithCustomSolver", "prefix" },
                new[] { "Gun", "SetTarget", "TargetChanged", "postfix" },
                new[] { "Gun", "Fire", "WaitForObservation", "prefix" },
                new[] { "AimSolver", "GetAimVector", "RaiseForFlare", "postfix" }
            })
            {
                var original = game.GetType(hook[0]).GetMethod(hook[1], Fields);
                var callback = ranging.GetMethod(hook[2], Static);
                var args = new object[patchMethod.GetParameters().Length]; args[0] = original;
                int index = Array.FindIndex(patchMethod.GetParameters(), p => p.Name == hook[3]);
                args[index] = Activator.CreateInstance(harmonyMethodType, new object[] { callback });
                patchMethod.Invoke(harmony, args);
            }
            var shipObject = new GameObject("Optical obscurant test target"); shipObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(shipObject, preview);
            shipObject.transform.position = new Vector3(2000, 50, 0);
            var ship = shipObject.AddComponent<Ship>();
            typeof(Unit).GetField("visibility", Fields).SetValue(ship, 10000f);
            var los = typeof(Unit).GetMethod("LineOfSight", Fields);
            Vector3 observer = new Vector3(0, 50, 0);
            Check((bool)los.Invoke(ship, new object[] { observer, 1f }), "Baseline sightline obstructed.");
            feature.GetMethod("Cloud", Static).Invoke(null, new[] { Enum.Parse(kind, "Smoke"), global(new Vector3(1000, 36, 0)) });
            var clouds = (IList)obscurants.GetField("Clouds", Static).GetValue(null);
            var cloud = (Component)clouds[0]; cloud.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(cloud.gameObject, preview);
            cloud.GetType().GetField("Born", Fields).SetValue(cloud, Time.time - 10f);
            Check(!(bool)los.Invoke(ship, new object[] { observer, 1f }), "Actual patched optical sightline ignored smoke.");
            var factor = obscurants.GetMethod("RadarFactor", Static);
            Check((float)factor.Invoke(null, new object[] { observer, ship.transform.position }) == 1f, "Smoke interferes with radar.");
            cloud.GetType().GetField("Kind", Fields).SetValue(cloud, Enum.Parse(kind, "Chaff"));
            Check((bool)los.Invoke(ship, new object[] { observer, 1f }), "Chaff interferes with optical vision.");
            Check((float)factor.Invoke(null, new object[] { observer, ship.transform.position }) < .15f, "Chaff fails to attenuate distant radar.");
            Check((float)factor.Invoke(null, new object[] { new Vector3(990, 50, 0), new Vector3(1030, 50, 0) }) == 1f, "Close radar has no burn-through.");
            Check((float)factor.Invoke(null, new object[] { new Vector3(0, 500, 0), new Vector3(2000, 500, 0) }) == 1f, "Off-path cloud interferes with radar.");
            var strength = obscurants.GetMethod("Strength", Static);
            Check((float)strength.Invoke(null, new object[] { 0f, 70f }) == 0f && (float)strength.Invoke(null, new object[] { 70f, 70f }) == 0f, "Clouds don't build/fade.");
            report.AppendLine("Passed: all 15 payload/ranging/obscurant Harmony hooks, actual optical sightline obstruction, separate smoke/chaff effects, radar attenuation, close-range burn-through, off-path immunity and cloud build/fade.");

            feature.GetMethod("Release", Static).Invoke(null, new object[] { carrier, global(new Vector3(50000, 1000, 50000)), new Vector3(0, -20, 400) });
            var effects = (IList)feature.GetField("Effects", Static).GetValue(null);
            int effectCount = effects.Count;
            var payload = effects.Cast<GameObject>().Select(go => go.GetComponent(a.GetType("GunmageddonAudio.SpecialPayloadFlight"))).First(c => c);
            payload.transform.SetParent(null); SceneManager.MoveGameObjectToScene(payload.gameObject, preview);
            Check(payload.GetComponentsInChildren<LineRenderer>().Length == 36, "Missing physical cluster bomblets.");
            feature.GetMethod("Release", Static).Invoke(null, new object[] { carrier, global(Vector3.zero), Vector3.zero });
            Check(effects.Count == effectCount, "Duplicate payload release.");
            payload.GetType().GetMethod("FixedUpdate", Fields).Invoke(payload, null);
            payload.GetType().GetMethod("LateUpdate", Fields).Invoke(payload, null);
            var firstLine = payload.GetComponentsInChildren<LineRenderer>()[0];
            Check(firstLine.transform.position.z > 50005f && firstLine.enabled, "Bomblet momentum or visual streak missing.");
            var burst = effects.Cast<GameObject>().First(go => go.name == "Cluster carrier opening burst");
            Check(burst.GetComponentsInChildren<ParticleSystem>().Length == 3 && burst.GetComponent<Light>(), "Carrier opening flash/sparks/smoke missing.");
            Check(!burst.GetComponentsInChildren<Component>().Any(c => c is IDamageable), "Cosmetic opening burst contains damageable gameplay objects.");
            var trails = effects.Cast<GameObject>().Where(go => go.name == "Clusterlet short trail").ToArray();
            Check(trails.Length == 36, "Not every clusterlet has a persistent trail.");
            for (int step = 0; step < 4; step++) payload.GetType().GetMethod("FixedUpdate", Fields).Invoke(payload, null);
            var trail = trails[0].GetComponent(a.GetType("GunmageddonAudio.SpottingTrail"));
            trail.GetType().GetMethod("Refresh").Invoke(trail, new object[] { Time.time });
            var renderer = trail.GetComponent<LineRenderer>();
            Check(renderer.enabled && renderer.positionCount >= 2, "Persistent clusterlet trail failed to render.");
            float length = 0f;
            for (int i = 1; i < renderer.positionCount; i++) length += Vector3.Distance(renderer.GetPosition(i - 1), renderer.GetPosition(i));
            Check(length > 15f && length <= 20.01f && renderer.startWidth <= .3f, "Clusterlet trail is not short and narrow.");
            trail.GetType().GetMethod("Freeze").Invoke(trail, null);
            trail.GetType().GetMethod("Refresh").Invoke(trail, new object[] { Time.time + .2f });
            Check(!renderer.enabled && renderer.positionCount == 0, "Clusterlet trail failed to expire.");
            report.AppendLine("Passed: cosmetic opening flash/sparks/smoke, 36 submunitions with short persistent trails, retained momentum, 20 m length cap, fade/expiry and one-shot release.");
            feature.GetMethod("Detonate", Static).Invoke(null, new object[] { new Vector3(50000, .3f, 51000), null, true });
            var water = effects.Cast<GameObject>().Last(go => go && go.name == "Cluster bomblet water splash");
            var spray = water.GetComponent<ParticleSystem>();
            Check(spray.particleCount == 24 && spray.main.startLifetime.constant >= 1f, "Cluster sea hit did not emit persistent water spray.");
            Check(spray.main.startColor.color.b > spray.main.startColor.color.r &&
                water.GetComponent<ParticleSystemRenderer>().sharedMaterial == feature.GetField("SmokeMaterial", Static).GetValue(null), "Cluster sea hit still uses orange sparks.");
            Check(Vector3.Dot(water.transform.forward, Vector3.up) > .99f, "Cluster sea spray is not directed upward.");
            report.AppendLine("Passed: cluster sea impacts emit upward pale water spray instead of orange land sparks.");
            return report.ToString();
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            var effects = (IList)feature.GetField("Effects", Static).GetValue(null);
            foreach (GameObject go in effects) if (go) UnityEngine.Object.DestroyImmediate(go);
            effects.Clear();
            foreach (string name in new[] { "SmokeMaterial", "ChaffMaterial", "SparkMaterial", "puff" })
            {
                var obj = feature.GetField(name, Static).GetValue(null) as UnityEngine.Object;
                if (obj) UnityEngine.Object.DestroyImmediate(obj);
            }
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
