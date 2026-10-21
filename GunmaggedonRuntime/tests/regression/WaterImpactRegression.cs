using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WaterImpactRegression
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static ParticleEffectManager.PrefabEffect Effect(GameObject source)
    {
        var effect = (ParticleEffectManager.PrefabEffect)FormatterServices.GetUninitializedObject(typeof(ParticleEffectManager.PrefabEffect));
        effect.gameObject = source; effect.systems = source.GetComponentsInChildren<ParticleSystem>(true); return effect;
    }
    public static object Run()
    {
        var assembly = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.6.2/GunmageddonAudio.dll")));
        var feature = assembly.GetType("GunmageddonAudio.CoastalWaterImpacts");
        var pool = (IList)feature.GetField("Pool", Static).GetValue(null);
        var datum = typeof(Unit).Assembly.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var originPosition = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = originPosition.GetValue(null);
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.water.impact.regression" });
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Water impact regression origin"); SceneManager.MoveGameObjectToScene(root, preview);
        var report = new StringBuilder();
        try
        {
            origin.SetValue(null, root.transform); originPosition.SetValue(null, Vector3.zero);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/203mm_impactEffectWater.prefab");
            var old = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            var legacy = Effect(old);
            foreach (var ps in legacy.systems) { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = ps.main; main.simulationSpeed = 1f; }
            legacy.Play(new Vector3(50000, 0, 50000), Quaternion.identity);
            foreach (var ps in legacy.systems) ps.Simulate(2f, false, false, false);
            float before = legacy.systems[0].time;
            legacy.Play(new Vector3(50200, 0, 50000), Quaternion.identity);
            Check(legacy.systems[0].time >= legacy.systems[0].main.duration - .001f && before >= legacy.systems[0].main.duration - .001f,
                "Legacy burst-replay failure did not reproduce.");
            old.SetActive(false);
            report.AppendLine("Reproduced native splash reuse: Play did not reset an emitter already beyond its burst window.");

            feature.GetMethod("Install", Static).Invoke(null, new[] { harmony });
            var source = UnityEngine.Object.Instantiate(prefab, root.transform, false); source.SetActive(false);
            var effect = Effect(source);
            var tintField = feature.GetField("activeTint", Static);
            var palette = new[] { new Color(.1f, .85f, 1f), new Color(1f, .45f, .08f), new Color(.95f, .15f, 1f) };
            for (int i = 0; i < 12; i++)
            {
                tintField.SetValue(null, i < 3 ? (object)palette[i] : null);
                effect.Play(new Vector3(50000 + i * 100, 0, 50000), Quaternion.identity);
                tintField.SetValue(null, null);
                Check(pool.Count == i + 1, "Rapid impacts reused a still-visible splash.");
                var component = (Component)pool[i];
                Check(component.gameObject.activeSelf, "New splash not active.");
                Check(component.transform.position.x == 50000 + i * 100, "Splash at incorrect impact.");
                foreach (var ps in component.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Check(ps.time < .01f && ps.isPlaying, "New splash did not restart emission.");
                    ps.Simulate(.15f, false, false, false);
                    Check(ps.particleCount > 0, "Splash system emitted no visible particles: " + ps.name);
                    Check(ps.main.customSimulationSpace == root.transform, "Splash not anchored to floating origin.");
                }
                Check(component.GetComponentsInChildren<AudioSource>(true).Length == 0, "Visual copy duplicated audio.");
                Check(source.transform.position == component.transform.position, "Travelling audio lost impact origin.");
                var originals = (ParticleSystem.MinMaxGradient[])component.GetType().GetField("originalColors", Fields).GetValue(component);
                var systems = component.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    Color original = originals[s].Evaluate(.5f, .5f), actual = systems[s].main.startColor.Evaluate(.5f, .5f);
                    Color tint = i < 3 ? palette[i] : Color.white;
                    Check(Mathf.Abs(actual.r - original.r * tint.r) < .001f && Mathf.Abs(actual.g - original.g * tint.g) < .001f &&
                        Mathf.Abs(actual.b - original.b * tint.b) < .001f && Mathf.Abs(actual.a - original.a) < .001f, "Splash color/alpha mismatch");
                }
            }
            var first = (Component)pool[0];
            Check(first.transform.position.x == 50000, "Later impact moved an earlier splash.");
            var instanceType = first.GetType();
            instanceType.GetField("RetireAt", Fields).SetValue(first, Time.time - 1f);
            instanceType.GetMethod("Update", Fields).Invoke(first, null);
            Check(!first.gameObject.activeSelf, "Completed splash did not retire.");
            effect.Play(new Vector3(52000, 0, 50000), Quaternion.identity);
            Check(pool.Count == 12 && first.gameObject.activeSelf && first.transform.position.x == 52000, "Retired splash was not safely recycled.");
            var defaults = (ParticleSystem.MinMaxGradient[])instanceType.GetField("originalColors", Fields).GetValue(first);
            var recycled = first.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < recycled.Length; i++) Check(recycled[i].main.startColor.Evaluate(.5f, .5f) == defaults[i].Evaluate(.5f, .5f), "Ranging tint leaked into normal splash reuse");
            report.AppendLine("Passed: 12 simultaneous native water impacts, particles from every spray/foam/ripple system, old-splash positions, isolated audio origin, retirement and clean reuse.");
            report.AppendLine("Passed: cyan/orange/magenta spray/foam/ripples, original alpha retained, and original colors restored on normal splash reuse.");

            var ranging = assembly.GetType("GunmageddonAudio.CoastalRanging");
            var shots = (IDictionary)ranging.GetField("Shots", Static).GetValue(null);
            var bullet = new BulletSim.Bullet(Vector3.down * 300f, null, 180f, false, false, null);
            var other = new BulletSim.Bullet(Vector3.down * 300f, null, 180f, false, false, null);
            var shotType = ranging.GetNestedType("Shot", BindingFlags.NonPublic);
            var shot = Activator.CreateInstance(shotType, true);
            shotType.GetField("Spotting", Fields).SetValue(shot, true);
            shotType.GetField("Color", Fields).SetValue(shot, palette[0]); shots.Add(bullet, shot);
            try
            {
                var begin = feature.GetMethod("BeginTrace", Static); var end = feature.GetMethod("EndTrace", Static);
                object[] outer = { bullet, null, null, null }; begin.Invoke(null, outer);
                Check((Color)tintField.GetValue(null) == palette[0], "Spotting trace did not select its color");
                object[] inner = { other, null, null, null }; begin.Invoke(null, inner);
                Check(tintField.GetValue(null) == null, "Unrelated nested trace inherited ranging color");
                var error = new InvalidOperationException("Fixture exception");
                Check(ReferenceEquals(end.Invoke(null, new[] { (object)error, inner[3] }), error), "Finalizer swallowed original exception");
                Check((Color)tintField.GetValue(null) == palette[0], "Nested trace did not restore parent tint");
                end.Invoke(null, new object[] { null, outer[3] });
                Check(tintField.GetValue(null) == null, "Tint leaked after trace cleanup");
                shotType.GetField("Illumination", Fields).SetValue(shot, true);
                object[] flare = { bullet, null, null, null }; begin.Invoke(null, flare);
                Check(tintField.GetValue(null) == null, "Illumination round received a spotting tint");
                end.Invoke(null, new object[] { null, flare[3] });
            }
            finally { shots.Remove(bullet); tintField.SetValue(null, null); }
            report.AppendLine("Passed: per-shot tint routing, unrelated nested trace isolation, exception restoration and illumination exclusion.");

            root.transform.position = new Vector3(-40000, 0, -40000);
            originPosition.SetValue(null, new Vector3(40000, 0, 40000));
            Check(first.transform.position == new Vector3(12000, 0, 10000), "Splash moved incorrectly with floating origin.");
            for (int i = 0; i < 70; i++) effect.Play(new Vector3(15000 + i * 20, 0, 10000), Quaternion.identity);
            Check(pool.Count == 64, "Splash pool exceeded its bounded budget.");
            var ordinary = new GameObject("Unrelated effect"); ordinary.transform.SetParent(root.transform, false);
            var ordinaryPS = ordinary.AddComponent<ParticleSystem>(); ordinaryPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Effect(ordinary).Play(new Vector3(10, 20, 30), Quaternion.identity);
            Check(ordinaryPS.isPlaying && pool.Count == 64, "Unrelated native effect was intercepted.");
            report.AppendLine("Passed: floating-origin movement, bounded 64-instance reuse and unrelated native effect isolation.");
            return report.ToString();
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            feature.GetMethod("Shutdown", Static).Invoke(null, null);
            origin.SetValue(null, savedOrigin); originPosition.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
