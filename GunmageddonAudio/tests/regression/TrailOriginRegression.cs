using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class TrailOriginRegression
{
    public static object Run()
    {
        const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        string folder = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/");
        var oldAssembly = Assembly.Load(System.IO.File.ReadAllBytes(folder + "1.1.0/GunmageddonAudio.dll"));
        var newAssembly = Assembly.Load(System.IO.File.ReadAllBytes(folder + "1.5.0/GunmageddonAudio.dll"));
        var game = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("Datum") != null);
        var datum = game.GetType("Datum");
        var originField = datum.GetField("<origin>k__BackingField", HiddenStatic);
        var positionField = datum.GetField("<originPosition>k__BackingField", HiddenStatic);
        var savedOrigin = originField.GetValue(null);
        var savedPosition = positionField.GetValue(null);
        var conversions = game.GetType("GlobalPositionExtensions");
        var toGlobal = conversions.GetMethod("ToGlobalPosition");
        var toLocal = conversions.GetMethod("ToLocalPosition");
        var preview = EditorSceneManager.NewPreviewScene();
        var anchor = new GameObject("Trail origin regression test");
        SceneManager.MoveGameObjectToScene(anchor, preview);
        GameObject newObject = null;
        var result = new StringBuilder();
        try
        {
            originField.SetValue(null, anchor.transform);
            positionField.SetValue(null, Vector3.zero);
            var points = new[] { new Vector3(100, 100, 100), new Vector3(100, 110, 120),
                new Vector3(100, 120, 140), new Vector3(100, 130, 160) };
            var globals = points.Select(p => toGlobal.Invoke(null, new object[] { p })).ToArray();

            var oldType = oldAssembly.GetType("GunmageddonAudio.CoastalRanging");
            var legacy = (TrailRenderer)oldType.GetMethod("BuildTrail", HiddenStatic)
                .Invoke(null, new object[] { points[0], Color.cyan, 12f });
            var oldController = legacy.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "SpottingTrailOrigin");
            // Edit mode does not call this runtime component's Awake automatically.
            oldController.GetType().GetMethod("Awake", HiddenInstance).Invoke(oldController, null);
            legacy.Clear();
            legacy.AddPositions(points.Take(2).ToArray());

            var newType = newAssembly.GetType("GunmageddonAudio.CoastalRanging");
            var fresh = (Component)newType.GetMethod("BuildTrail", HiddenStatic)
                .Invoke(null, new object[] { points[0], Color.cyan, 12f });
            newObject = fresh.gameObject;
            if (newObject.scene != preview)
            {
                newObject.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(newObject, preview);
            }
            var componentType = fresh.GetType();
            var sample = componentType.GetMethod("Sample");
            var refresh = componentType.GetMethod("Refresh");
            float time = Time.time;
            sample.Invoke(fresh, new object[] { globals[1], time + .02f });
            refresh.Invoke(fresh, new object[] { time + .03f });
            var line = fresh.GetComponent<LineRenderer>();
            if (line.positionCount != 2) throw new Exception("Startup created a spurious origin point.");

            var shift = new Vector3(-1000, 0, 0);
            anchor.transform.position = shift;
            positionField.SetValue(null, shift);
            var rebasedHead = (Vector3)toLocal.Invoke(null, new[] { globals[2] });
            legacy.AddPosition(rebasedHead);
            oldController.GetType().GetMethod("LateUpdate", HiddenInstance).Invoke(oldController, null);
            float oldError = Vector3.Distance(legacy.GetPosition(legacy.positionCount - 1), rebasedHead);
            if (oldError < 900f) throw new Exception("Legacy repro did not expose the double shift.");
            result.AppendLine("Legacy mixed-coordinate rebase reproduced: newest point error=" + oldError + " m.");

            sample.Invoke(fresh, new object[] { globals[2], time + .04f });
            refresh.Invoke(fresh, new object[] { time + .05f });
            CheckPoints(line, globals.Take(3).ToArray(), toLocal);
            sample.Invoke(fresh, new object[] { globals[3], time + .06f });
            componentType.GetMethod("Freeze").Invoke(fresh, null);

            // Rebase after LateUpdate, then invoke the camera-time refresh.
            shift = new Vector3(20000, 100, -30000);
            anchor.transform.position = shift;
            positionField.SetValue(null, shift);
            var cameraObject = new GameObject("Trail regression camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(2000, 1000, -500);
            componentType.GetMethod("BeforeCameraCull", HiddenInstance).Invoke(fresh, new object[] { camera });
            CheckPoints(line, globals, toLocal);
            camera.transform.SetPositionAndRotation(new Vector3(-3000, 200, 5000), Quaternion.Euler(60, 120, 0));
            componentType.GetMethod("BeforeCameraCull", HiddenInstance).Invoke(fresh, new object[] { camera });
            CheckPoints(line, globals, toLocal);
            result.AppendLine("New trail: startup, live/frozen rebases and camera switches passed with no distorted segments.");
            refresh.Invoke(fresh, new object[] { time + 6f });
            if (line.startColor.a > .45f || line.startColor.a < .35f) throw new Exception("Lingering fade is incorrect.");
            refresh.Invoke(fresh, new object[] { time + 13f });
            if (line.enabled || line.positionCount != 0) throw new Exception("Expired trail is still rendered.");
            result.AppendLine("Lingering fade and 12-second expiry passed.");
            return result.ToString();
        }
        finally
        {
            if (newObject) UnityEngine.Object.DestroyImmediate(newObject);
            EditorSceneManager.ClosePreviewScene(preview);
            originField.SetValue(null, savedOrigin);
            positionField.SetValue(null, savedPosition);
        }
    }

    private static void CheckPoints(LineRenderer line, object[] globals, MethodInfo toLocal)
    {
        if (line.positionCount != globals.Length) throw new Exception("Incorrect point count.");
        for (int i = 0; i < globals.Length; i++)
        {
            var expected = (Vector3)toLocal.Invoke(null, new[] { globals[globals.Length - 1 - i] });
            if (Vector3.Distance(line.GetPosition(i), expected) > .01f)
                throw new Exception("Global-coordinate trail point mismatch at " + i);
        }
    }
}
