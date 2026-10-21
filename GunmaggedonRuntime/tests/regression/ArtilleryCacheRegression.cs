using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArtilleryCacheRegression
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        string folder = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/");
        var datum = typeof(Unit).Assembly.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var position = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = position.GetValue(null);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Stationary solution cache regression"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
        var info = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/Mods/Gunmageddon/203mmEmplacement.asset"));
        var report = new StringBuilder();
        try
        {
            origin.SetValue(null, root.transform); position.SetValue(null, Vector3.zero);
            var owner = root.AddComponent<Building>(); var gun = root.AddComponent<Gun>(); gun.attachedUnit = owner; gun.info = info;
            var targetObject = new GameObject("Stationary then moving target"); targetObject.SetActive(false); SceneManager.MoveGameObjectToScene(targetObject, scene);
            var target = targetObject.AddComponent<Ship>(); target.transform.position = new Vector3(50000, 50, 55000);
            var rb = targetObject.AddComponent<Rigidbody>(); rb.useGravity = false;
            typeof(Unit).GetField("<rb>k__BackingField", Fields).SetValue(target, rb);
            Vector3 from = new Vector3(50000, 50, 50000);
            foreach (string version in new[] { "1.4.4", "1.4.5" })
            {
                var a = Assembly.Load(System.IO.File.ReadAllBytes(folder + version + "/GunmageddonAudio.dll"));
                var feature = a.GetType("GunmageddonAudio.CoastalArtillery");
                var get = feature.GetMethod("GetPlan", Static);
                object current = get.Invoke(null, new object[] { gun, target, from });
                var created = current.GetType().GetField("Created", Fields);
                int rebuilt = 0;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 30; i++)
                {
                    created.SetValue(current, Time.time - 1f); // Simulate the old quarter-second cache expiring.
                    object next = get.Invoke(null, new object[] { gun, target, from });
                    if (!ReferenceEquals(current, next)) rebuilt++;
                    current = next;
                }
                clock.Stop();
                Check(rebuilt == (version == "1.4.4" ? 30 : 0), "Unchanged target cache behavior incorrect: " + version);
                report.AppendLine(string.Format("{0}: {1} stationary recalculations in 30 requests, {2:F3}ms/request (Editor microbenchmark).", version, rebuilt, clock.Elapsed.TotalMilliseconds / 30));
                if (version == "1.4.5")
                {
                    Action expire = () => created.SetValue(current, Time.time - 1f);
                    Func<object> getNext = () => get.Invoke(null, new object[] { gun, target, from });
                    rb.velocity = Vector3.right * 10f; expire(); object next = getNext();
                    Check(!ReferenceEquals(current, next), "Velocity change did not invalidate cache."); current = next;
                    target.transform.position += Vector3.right * 10f; expire(); next = getNext();
                    Check(!ReferenceEquals(current, next), "Target movement did not invalidate cache."); current = next;
                    info.gravMult = 1f; expire(); next = getNext();
                    Check(!ReferenceEquals(current, next), "Gravity change did not invalidate cache."); current = next;
                    from += Vector3.up * 5f; expire(); next = getNext();
                    Check(!ReferenceEquals(current, next), "Muzzle displacement did not invalidate cache.");
                    report.AppendLine("Passed: velocity, target movement, gravity and muzzle displacement invalidate the solution.");
                }
                feature.GetMethod("Shutdown", Static).Invoke(null, null);
            }
            return report.ToString();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(info);
            origin.SetValue(null, savedOrigin); position.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
