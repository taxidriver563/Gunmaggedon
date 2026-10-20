using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CoastalDetectionIntegration
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int detections;
    public static bool Capture() { detections++; return false; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static object Run()
    {
        var game = typeof(Unit).Assembly;
        var datum = game.GetType("Datum");
        var origin = datum.GetField("<origin>k__BackingField", Static);
        var position = datum.GetField("<originPosition>k__BackingField", Static);
        object savedOrigin = origin.GetValue(null), savedPosition = position.GetValue(null);
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.detection.regression" });
        var prefix = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { typeof(CoastalDetectionIntegration).GetMethod("Capture") });
        harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5)
            .Invoke(harmony, new object[] { typeof(TargetDetector).GetMethod("DetectTarget", Fields), prefix, null, null, null });
        var scene = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        try
        {
            var root = new GameObject("Detection regression"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            origin.SetValue(null, root.transform); position.SetValue(null, Vector3.zero);
            var target = root.AddComponent<Building>();
            var detectorObject = new GameObject("Bridge optics"); detectorObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(detectorObject, scene);
            detectorObject.transform.position = new Vector3(0, 0, -5000);
            var detector = detectorObject.AddComponent<TargetDetector>();
            typeof(TargetDetector).GetField("scanner", Fields).SetValue(detector, detectorObject.transform);
            typeof(TargetDetector).GetField("magnification", Fields).SetValue(detector, 3f);
            var obstacleObject = new GameObject("Representative coastal bunker collider");
            SceneManager.MoveGameObjectToScene(obstacleObject, scene);
            var box = obstacleObject.AddComponent<BoxCollider>(); box.size = new Vector3(15, 8, 15);
            var ray = new Ray(new Vector3(0, 0, -5000), Vector3.forward);
            RaycastHit ownHit;
            Check(box.Raycast(ray, out ownHit, 6000), "Fixture did not hit the bunker.");
            var requestType = game.GetType("NuclearOption.Jobs.DetectionRequest");
            // Populate the native request directly; avoid starting the game's network/grid lifecycle.
            var request = Activator.CreateInstance(requestType);
            requestType.GetField("Detector", Fields).SetValue(request, detector);
            requestType.GetField("target", Fields).SetValue(request, target);
            var process = requestType.GetMethod("ProcessResult", Fields);
            typeof(Unit).GetField("visibility", Fields).SetValue(target, 500f);
            typeof(Unit).GetField("maxRadius", Fields).SetValue(target, 2.5f);
            Check(!(bool)typeof(TargetDetector).GetMethod("InVisualRange", Fields).Invoke(detector, new object[] { target }), "Old visibility unexpectedly passed at 5 km.");
            detections = 0; process.Invoke(request, new object[] { ownHit });
            Check(detections == 0, "Old undersized definition unexpectedly accepted its bunker hit.");
            report.AppendLine("Original metadata: 5 km optical rejection and own-bunker occlusion reproduced.");
            foreach (var name in new[] { "Emplacement203mmCD", "Emplacement203mmCD_SPECIAL" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/Blueprinter/Mods/Gunmageddon/" + name + ".asset");
                target.definition = definition;
                typeof(Unit).GetField("visibility", Fields).SetValue(target, definition.visibleRange);
                typeof(Unit).GetField("maxRadius", Fields).SetValue(target, .5f * Mathf.Max(definition.length, definition.width, definition.height));
                Check((bool)typeof(TargetDetector).GetMethod("InVisualRange", Fields).Invoke(detector, new object[] { target }), name + " is invisible at 5 km.");
                detections = 0; process.Invoke(request, new object[] { ownHit });
                Check(detections == 1, name + " rejected its own bunker collider.");
                obstacleObject.transform.position = new Vector3(0, 0, -2500);
                Physics.SyncTransforms();
                RaycastHit terrainHit; Check(box.Raycast(ray, out terrainHit, 6000), "Terrain fixture missed.");
                detections = 0; process.Invoke(request, new object[] { terrainHit });
                Check(detections == 0, name + " detected through a distant obstruction.");
                obstacleObject.transform.position = Vector3.zero;
                Physics.SyncTransforms();
                Check(definition.typeIdentity.surface > 0, "Surface target classification missing.");
                report.AppendLine(name + ": optical range and own-collider acceptance passed; intervening obstruction still rejected.");
            }
            return report.ToString();
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            origin.SetValue(null, savedOrigin); position.SetValue(null, savedPosition);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
