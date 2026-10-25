using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;

public static class CD203ClearanceRegression
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static object Run()
    {
        checks = 0;
        var assembly = Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
        var artillery = assembly.GetTypes().Single(t => t.Name == "CoastalArtillery");
        var clearance = artillery.GetMethod("CheckClearance", S);
        var native = typeof(Turret).GetMethod("FixedUpdate", F);
        var harmony = new Harmony("gunmaggedon.cd203.clearance.regression");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            harmony.Patch(native, transpiler: new HarmonyMethod(artillery.GetMethod("MuzzleClearance", S)));
            Check(Harmony.GetPatchInfo(native).Transpilers.Any(p => p.owner == harmony.Id), "Native clearance hook installed");
            int mask = ~((LayerMask)typeof(Unit).Assembly.GetType("PhysicsLayers").GetField("ExclusionZonesMask", S).GetValue(null)).value;
            foreach (string name in new[] { "Emplacement_203mmCG", "Emplacement_203mmCG_SPECIAL" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/" + name + ".prefab");
                var root = new GameObject("CD203 collider-only preview"); SceneManager.MoveGameObjectToScene(root, scene);
                var map = new Dictionary<Transform, Transform> { { prefab.transform, root.transform } };
                foreach (var source in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (source == prefab.transform) continue;
                    var dest = new GameObject(source.name).transform; dest.SetParent(map[source.parent], false);
                    dest.localPosition = source.localPosition; dest.localRotation = source.localRotation; dest.localScale = source.localScale;
                    dest.gameObject.layer = source.gameObject.layer; map.Add(source, dest);
                }
                foreach (var source in prefab.GetComponentsInChildren<Collider>(true))
                {
                    Collider dest;
                    if (source is BoxCollider)
                    {
                        var box = (BoxCollider)source; var copy = map[source.transform].gameObject.AddComponent<BoxCollider>();
                        copy.center = box.center; copy.size = box.size; dest = copy;
                    }
                    else
                    {
                        var mesh = (MeshCollider)source; var copy = map[source.transform].gameObject.AddComponent<MeshCollider>();
                        copy.sharedMesh = mesh.sharedMesh; copy.convex = mesh.convex; dest = copy;
                    }
                    dest.isTrigger = source.isTrigger; dest.enabled = source.enabled;
                }
                // Inactive native bindings avoid Awake, networking and mission singletons.
                var bindings = new GameObject("inactive native bindings"); bindings.SetActive(false); SceneManager.MoveGameObjectToScene(bindings, scene);
                var owner = bindings.AddComponent<Building>(); var gun = bindings.AddComponent<Gun>(); var turret = bindings.AddComponent<Turret>();
                gun.attachedUnit = owner; gun.info = prefab.GetComponentInChildren<Gun>(true).info;
                var nativeTurret = prefab.GetComponentInChildren<Turret>(true);
                var pitch = map[(Transform)typeof(Turret).GetField("elevationTransform", F).GetValue(nativeTurret)];
                var muzzle = map[((Transform[])typeof(Gun).GetField("muzzles", F).GetValue(prefab.GetComponentInChildren<Gun>(true)))[0]];
                typeof(Gun).GetField("muzzles", F).SetValue(gun, new[] { muzzle });
                typeof(Turret).GetField("aimSafetyWeapon", F).SetValue(turret, gun);
                var wall = new GameObject("external obstruction"); SceneManager.MoveGameObjectToScene(wall, scene);
                var blocker = wall.AddComponent<BoxCollider>(); blocker.size = Vector3.one * 4f;
                foreach (float elevation in new[] { 0f, 5f, 20f, 23f, 25f, 45f, 65f })
                {
                    pitch.localRotation = Quaternion.Euler(-elevation, 0, 0);
                    blocker.enabled = false;
                    foreach (var col in root.GetComponentsInChildren<Collider>()) { col.enabled = false; col.enabled = true; }
                    Physics.SyncTransforms(); RaycastHit hit;
                    Check(scene.GetPhysicsScene().Raycast(pitch.position, pitch.forward, out hit, 200, mask) && hit.collider.name == "barrel_mesh", "Reproduce original own-barrel obstruction");
                    object[] args = { pitch.position, pitch.position + pitch.forward * 200f, default(RaycastHit), mask, turret };
                    Check(!(bool)clearance.Invoke(null, args), "Clear muzzle is allowed: " + name + " " + elevation);
                    wall.transform.position = muzzle.position + muzzle.forward * 100f; blocker.enabled = true; Physics.SyncTransforms();
                    Check((bool)clearance.Invoke(null, args) && ((RaycastHit)args[2]).collider == blocker, "External obstruction still blocks firing");
                    Check(((RaycastHit)args[2]).distance > 90f, "Distance remains suitable for native target-range comparison");
                    args[3] = 0; Check(!(bool)clearance.Invoke(null, args), "Native layer mask is respected"); args[3] = mask;
                    wall.transform.position = muzzle.position + muzzle.forward * 205f; blocker.enabled = false; blocker.enabled = true; Physics.SyncTransforms();
                    Check(!(bool)clearance.Invoke(null, args), "Original 200m segment length is retained");
                }
                Check(root.GetComponentsInChildren<Collider>().All(c => c.enabled), "Hitboxes remain enabled");
                typeof(Gun).GetField("muzzles", F).SetValue(gun, new Transform[0]);
                object[] fallback = { Vector3.one * 90000f, Vector3.one * 90000f + Vector3.forward * 200, default(RaycastHit), mask, turret };
                RaycastHit expected;
                Check((bool)clearance.Invoke(null, fallback) == Physics.Linecast((Vector3)fallback[0], (Vector3)fallback[1], out expected, mask), "Missing muzzle retains native fallback");
                gun.info = null;
                Check((bool)clearance.Invoke(null, fallback) == Physics.Linecast((Vector3)fallback[0], (Vector3)fallback[1], out expected, mask), "Unsupported weapon retains native fallback");
                UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(bindings); UnityEngine.Object.DestroyImmediate(root);
            }
            return new { passed = true, checks, details = "Native transpiler installed; both variants, seven elevations, own-barrel regression, external obstacles, mask, segment length and fallback. Live mission firing remains unverified." };
        }
        finally { harmony.UnpatchSelf(); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
