using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArmorPiercingIntegration
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static bool Server(Mirage.NetworkBehaviour __instance, ref bool __result)
    { if (!(__instance is Unit)) return true; __result = true; return false; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static object Run()
    {
        var a = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.6.0/GunmageddonAudio.dll")));
        var ap = a.GetType("GunmageddonAudio.ArmorPiercingShells");
        var ranging = a.GetType("GunmageddonAudio.CoastalRanging");
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType("HarmonyLib.Harmony")).First(x => x != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.ap.integration" });
        var prefix = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { typeof(ArmorPiercingIntegration).GetMethod("Server") });
        harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5)
            .Invoke(harmony, new object[] { typeof(Unit).GetProperty("IsServer").GetMethod, prefix, null, null, null });
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Inactive AP integration fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
        UnityEngine.Object definition = null;
        try
        {
            const string folder = "Assets/Blueprinter/Mods/Gunmageddon/";
            var ordinary = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Emplacement_203mmCG.prefab"), root.transform, false);
            var special = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Emplacement_203mmCG_SPECIAL.prefab"), root.transform, false);
            var gun = ordinary.GetComponentsInChildren<Gun>(true).First(g => g.info.name == "203mmEmplacement");
            var specialGun = special.GetComponentsInChildren<Gun>(true).First(g => g.info.name == "203mmEmplacement_SPECIAL");
            var boat = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/PatrolBoat1_PLACEHOLDER.prefab"), root.transform, false).GetComponent<Ship>();
            boat.definition = UnityEngine.Object.Instantiate(boat.definition); definition = boat.definition;
            boat.definition.length = 100f;
            var prepare = ap.GetMethod("Prepare", Static);
            var first = (WeaponInfo)prepare.Invoke(null, new object[] { gun, boat, Vector3.forward * 377f });
            var second = (WeaponInfo)prepare.Invoke(null, new object[] { gun, boat, Vector3.forward * 166f });
            Check(first && second && first != second, "In-flight AP profiles share mutable damage");
            Check(Mathf.Abs(first.pierceDamage * 377f * 377f / (850f * 850f) - 12000f) < .01f, "AP charge normalization wrong");
            Check(first.blastDamage == 0 && first.muzzleVelocity == gun.info.muzzleVelocity && first.dragCoef == gun.info.dragCoef && first.gravMult == gun.info.gravMult, "AP changed ballistics or retained blast");
            Check(gun.info.pierceDamage == 600f && gun.info.blastDamage == 500f, "Original HE profile mutated");
            Check(prepare.Invoke(null, new object[] { specialGun, boat, Vector3.forward * 377f }) == null, "SPECIAL gun selected AP");
            boat.definition.length = 30f; boat.definition.armorTier = 2f;
            Check(prepare.Invoke(null, new object[] { gun, boat, Vector3.forward * 377f }) == null, "Light boat lost HE preference");

            var bullet = new BulletSim.Bullet(Vector3.forward * 377f, null, 180f, false, false, null);
            bullet.explosive = bullet.proximityFuse = true;
            var shotType = ranging.GetNestedType("Shot", BindingFlags.NonPublic);
            var shot = Activator.CreateInstance(shotType, true);
            shotType.GetField("APInfo", Fields).SetValue(shot, first);
            shotType.GetField("State", Fields).SetValue(shot, ranging.GetMethod("GetState", Static).Invoke(null, new object[] { gun }));
            var shots = (IDictionary)ranging.GetField("Shots", Static).GetValue(null); shots.Add(bullet, shot);
            var water = (ParticleEffectManager.PrefabEffect)FormatterServices.GetUninitializedObject(typeof(ParticleEffectManager.PrefabEffect));
            var effects = new ParticleEffectManager.PrefabEffect[4]; effects[2] = water;
            object[] args = { bullet, gun.info, gun.attachedUnit, false, effects };
            var trace = ranging.GetMethod("BeforeTrace", Static); trace.Invoke(null, args);
            Check(ReferenceEquals(args[1], first) && !(bool)args[3] && !bullet.explosive && !bullet.proximityFuse, "AP trace/fuse routing wrong");
            var routed = (ParticleEffectManager.PrefabEffect[])args[4];
            Check(routed[0] == null && routed[1] == null && ReferenceEquals(routed[2], water), "AP water effect lost or HE land effects retained");
            foreach (var flag in new[] { "Spotting", "Illumination" })
            {
                shotType.GetField(flag, Fields).SetValue(shot, true);
                args = new object[] { bullet, gun.info, gun.attachedUnit, false, effects }; trace.Invoke(null, args);
                Check(((WeaponInfo)args[1]).pierceDamage == 0 && ((WeaponInfo)args[1]).blastDamage == 0 && (bool)args[3], "AP leaked damage into " + flag);
                shotType.GetField(flag, Fields).SetValue(shot, false);
            }
            return "Passed AP profile isolation, native reference-speed compensation, unchanged HE/ballistics, SPECIAL exclusion, light-boat preference, trace/fuse routing, water retention and non-damaging spotting/illumination.";
        }
        finally
        {
            harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null);
            ranging.GetMethod("Shutdown", Static).Invoke(null, null);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
