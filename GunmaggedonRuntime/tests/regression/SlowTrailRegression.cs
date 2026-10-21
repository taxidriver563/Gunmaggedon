using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SlowTrailRegression
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        string folder = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/");
        var oldAssembly = Assembly.Load(System.IO.File.ReadAllBytes(folder + "1.4.0/GunmageddonAudio.dll"));
        var updated = Assembly.Load(System.IO.File.ReadAllBytes(folder + "1.5.0/GunmageddonAudio.dll"));
        var game = typeof(Unit).Assembly;
        var datum = game.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var originPosition = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = originPosition.GetValue(null);
        var toGlobal = game.GetType("GlobalPositionExtensions").GetMethod("ToGlobalPosition");
        var toLocal = game.GetType("GlobalPositionExtensions").GetMethod("ToLocalPosition");
        var preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Slow trail regression anchor"); SceneManager.MoveGameObjectToScene(root, preview);
        var report = new StringBuilder();
        try
        {
            origin.SetValue(null, root.transform); originPosition.SetValue(null, Vector3.zero);
            foreach (float speed in new[] { 50f, 153f, 235f, 377f, 600f })
            {
                var start = new Vector3(100, 100, 100);
                var legacy = (Component)oldAssembly.GetType("GunmageddonAudio.CoastalRanging").GetMethod("BuildTrail", Static)
                    .Invoke(null, new object[] { start, Color.cyan, 12f });
                var fresh = (Component)updated.GetType("GunmageddonAudio.CoastalRanging").GetMethod("BuildTrail", Static)
                    .Invoke(null, new object[] { start, Color.cyan, 12f });
                float born = Time.time;
                object last = null;
                for (int tick = 1; tick <= 100; tick++)
                {
                    float seconds = tick * .02f;
                    Vector3 position = start + new Vector3(speed * seconds, -.5f * 19.62f * seconds * seconds, 0);
                    last = toGlobal.Invoke(null, new object[] { position });
                    foreach (var trail in new[] { legacy, fresh })
                    {
                        trail.GetType().GetMethod("Sample").Invoke(trail, new object[] { last, born + seconds });
                        trail.GetType().GetMethod("Refresh").Invoke(trail, new object[] { born + seconds });
                    }
                    Check(fresh.GetComponent<LineRenderer>().enabled && fresh.GetComponent<LineRenderer>().positionCount >= 2,
                        "Reduced-speed trail disappeared at " + speed + " m/s, tick " + tick);
                }
                if (speed < 400f) Check(!legacy.GetComponent<LineRenderer>().enabled, "Failed to reproduce old sample-threshold bug.");
                else Check(legacy.GetComponent<LineRenderer>().enabled, "Fast-shot control is invalid.");
                var line = fresh.GetComponent<LineRenderer>();
                var shift = new Vector3(-1000, 0, 0);
                root.transform.position = shift; originPosition.SetValue(null, shift);
                fresh.GetType().GetMethod("BeforeCameraCull", Fields).Invoke(fresh, new object[] { null });
                fresh.GetType().GetMethod("BeforeCameraCull", Fields).Invoke(fresh, new object[] { null });
                Vector3 expected = (Vector3)toLocal.Invoke(null, new[] { last });
                Check(Vector3.Distance(line.GetPosition(0), expected) < .01f, "Slow trail moved incorrectly after a camera/origin change.");
                fresh.GetType().GetMethod("Freeze").Invoke(fresh, null);
                fresh.GetType().GetMethod("Refresh").Invoke(fresh, new object[] { born + 2.1f });
                Check(line.enabled, "Impact immediately hid the trail.");
                fresh.GetType().GetMethod("Refresh").Invoke(fresh, new object[] { born + 14.1f });
                Check(!line.enabled && line.positionCount == 0, "Lingering trail did not expire.");
                report.AppendLine(speed + " m/s: continuous trail, origin/camera refresh and frozen expiry passed" + (speed < 400 ? "; old invisible-trail bug reproduced." : "; fast-shot control passed."));
                UnityEngine.Object.DestroyImmediate(legacy.gameObject); UnityEngine.Object.DestroyImmediate(fresh.gameObject);
                root.transform.position = Vector3.zero; originPosition.SetValue(null, Vector3.zero);
            }
            return report.ToString();
        }
        finally
        {
            origin.SetValue(null, savedOrigin); originPosition.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
