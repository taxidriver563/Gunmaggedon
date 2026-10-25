using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;

public static class CD203ScannerRegression
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static TargetDetector current;
    static int checks, detected;
    public static bool StopInitialize(TargetDetector __instance) { return __instance != current; }
    public static bool Record(TargetDetector __instance) { if (__instance != current) return true; detected++; return false; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static RaycastHit Query(object request, Type type, PhysicsScene physics)
    {
        var command = (RaycastCommand)type.GetMethod("GetRaycastCommand", F).Invoke(request, null);
        RaycastHit hit;
        physics.Raycast(command.from, command.direction, out hit, command.distance, command.queryParameters.layerMask, command.queryParameters.hitTriggers);
        return hit;
    }
    public static object Run()
    {
        checks = detected = 0;
        var assembly = Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
        var artillery = assembly.GetTypes().Single(t => t.Name == "CoastalArtillery");
        var h = new Harmony("gunmaggedon.cd203.scanner.regression");
        var scene = EditorSceneManager.NewPreviewScene();
        UnitDefinition definition = null;
        try
        {
            var initialize = typeof(TargetDetector).GetMethod("TargetDetector_OnInitialize", F);
            h.Patch(initialize, prefix: new HarmonyMethod(artillery.GetMethod("ClearScanner", S)) { priority = Priority.First });
            h.Patch(initialize, prefix: new HarmonyMethod(typeof(CD203ScannerRegression).GetMethod("StopInitialize")) { priority = Priority.Last });
            h.Patch(typeof(TargetDetector).GetMethod("DetectTarget", F), prefix: new HarmonyMethod(typeof(CD203ScannerRegression).GetMethod("Record")));
            var requestType = typeof(Unit).Assembly.GetType("NuclearOption.Jobs.DetectionRequest");
            var process = requestType.GetMethod("ProcessResult", F);
            definition = ScriptableObject.CreateInstance<UnitDefinition>(); definition.height = 10;
            var targetObject = new GameObject("inactive naval target"); targetObject.SetActive(false); SceneManager.MoveGameObjectToScene(targetObject, scene);
            var target = targetObject.AddComponent<Ship>(); target.definition = definition; target.maxRadius = 20;
            foreach (var name in new[] { "Emplacement_203mmCG", "Emplacement_203mmCG_SPECIAL" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/" + name + ".prefab");
                var stage = new GameObject("inactive native owner"); stage.SetActive(false); SceneManager.MoveGameObjectToScene(stage, scene);
                var copy = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
                current = copy.GetComponentInChildren<TargetDetector>(true);
                var scanner = current.GetScanPoint(); Vector3 original = scanner.localPosition;
                var turret = copy.GetComponentInChildren<Turret>(true);
                var pitch = (Transform)typeof(Turret).GetField("elevationTransform", F).GetValue(turret);
                var root = new GameObject("native Statics collider replica"); SceneManager.MoveGameObjectToScene(root, scene);
                var map = new Dictionary<Transform, Transform> { { prefab.transform, root.transform } };
                foreach (var src in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (src == prefab.transform) continue;
                    var dst = new GameObject(src.name).transform; dst.SetParent(map[src.parent], false);
                    dst.localPosition = src.localPosition; dst.localRotation = src.localRotation; dst.localScale = src.localScale; map.Add(src, dst);
                }
                int statics = (int)typeof(PhysicsLayers).GetField("Statics", S).GetValue(null);
                foreach (var src in prefab.GetComponentsInChildren<Collider>(true))
                {
                    var go = map[src.transform].gameObject; go.layer = statics;
                    if (src is BoxCollider) { var b = (BoxCollider)src; var c = go.AddComponent<BoxCollider>(); c.center = b.center; c.size = b.size; }
                    else { var m = (MeshCollider)src; var c = go.AddComponent<MeshCollider>(); c.sharedMesh = m.sharedMesh; c.convex = m.convex; }
                }
                var originalTurret = prefab.GetComponentInChildren<Turret>(true);
                var geometryYaw = map[originalTurret.transform];
                var geometryPitch = map[(Transform)typeof(Turret).GetField("elevationTransform", F).GetValue(originalTurret)];
                var wall = new GameObject("external terrain obstruction"); SceneManager.MoveGameObjectToScene(wall, scene); wall.layer = statics;
                var box = wall.AddComponent<BoxCollider>(); box.size = Vector3.one * 6;
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                foreach (float angle in new[] { -5f, 0f, 5f, 20f, 23f, 25f, 45f, 65f, 90f })
                {
                    var direction = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    target.transform.position = direction * 9410f;
                    turret.transform.localRotation = geometryYaw.localRotation = Quaternion.Euler(0, yaw, 0);
                    pitch.localRotation = geometryPitch.localRotation = Quaternion.Euler(-angle, 0, 0);
                    scanner.localPosition = original; box.enabled = false;
                    foreach (var collider in root.GetComponentsInChildren<Collider>()) { collider.enabled = false; collider.enabled = true; }
                    Physics.SyncTransforms();
                    Func<object> request = () => Activator.CreateInstance(requestType, F, null, new object[] { current, target, null, 9410f, 1f }, null);
                    if (angle == 23f)
                    {
                        var oldRequest = request(); var oldHit = Query(oldRequest, requestType, scene.GetPhysicsScene());
                        Check(oldHit.collider && oldHit.collider.name == "gun_mesh", "Reproduce scanner occlusion at combat elevation");
                        int before = detected; process.Invoke(oldRequest, new object[] { oldHit }); Check(detected == before, "Native LOS result rejects self-obstructed contact");
                    }
                    initialize.Invoke(current, null);
                    Check(Mathf.Abs(scanner.localPosition.y - 17.5f) < .001f, "Native initialization hook raises scan point");
                    var clearRequest = request(); var clearHit = Query(clearRequest, requestType, scene.GetPhysicsScene());
                    Check(!clearHit.collider, "Own geometry clears scanner at yaw/elevation " + yaw + "/" + angle);
                    int count = detected; process.Invoke(clearRequest, new object[] { clearHit }); Check(detected == count + 1, "Native LOS processing accepts clear contact");
                    wall.transform.position = scanner.position + (target.transform.position + Vector3.up * 4 - scanner.position).normalized * 100;
                    box.enabled = true; Physics.SyncTransforms(); var blockedRequest = request(); var blockedHit = Query(blockedRequest, requestType, scene.GetPhysicsScene());
                    Check(blockedHit.collider == box, "External terrain still obstructs optical acquisition");
                    count = detected; process.Invoke(blockedRequest, new object[] { blockedHit }); Check(detected == count, "Native processing rejects external obstruction");
                }
                var lod = copy.GetComponent<LODGroup>(); Check(lod && lod.enabled && lod.GetLODs().Length >= 3, "Existing LODs retained");
                lod.ForceLOD(2); initialize.Invoke(current, null); Check(scanner.localPosition.y == 17.5f, "Optics stay independent of model LOD");
                var position = scanner.position;
                foreach (var gun in copy.GetComponentsInChildren<Gun>(true)) gun.info = null;
                initialize.Invoke(current, null); Check(scanner.position == position, "Unsupported owners are left unchanged");
                UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(stage);
            }
            return new { passed = true, checks, detections = detected, details = "Native scanner initialization and DetectionRequest command/result; both variants, 36 poses each, self-obstruction reproduced, clear contacts accepted, external terrain rejected, LOD independence." };
        }
        finally { current = null; h.UnpatchSelf(); if (definition) UnityEngine.Object.DestroyImmediate(definition); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
