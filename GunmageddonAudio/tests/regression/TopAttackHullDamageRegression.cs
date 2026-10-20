using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TopAttackHullDamageRegression
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static ShipPart hull;
    static float applied;
    public static bool Server(Mirage.NetworkBehaviour __instance, ref bool __result) { if (!(__instance is Unit)) return true; __result = true; return false; }
    public static bool Damage(Unit __instance, byte __0, DamageInfo __1)
    {
        if (__instance != hull.parentUnit) return true;
        float pierce = NetworkFloatHelper.Decompress(__1.pierceDamage);
        float blast = NetworkFloatHelper.Decompress(__1.blastDamage);
        float fire = NetworkFloatHelper.Decompress(__1.fireDamage);
        float impact = NetworkFloatHelper.Decompress(__1.impactDamage);
        applied = pierce + blast + fire + impact;
        hull.ApplyDamage(pierce, blast, fire, impact);
        return false;
    }
    public static object Run()
    {
        var harmonyType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("HarmonyLib.Harmony")).First(t => t != null);
        var harmony = Activator.CreateInstance(harmonyType, new object[] { "gunmageddon.native.surf.damage" });
        Action<MethodInfo, string> patch = (method, name) =>
        {
            var callback = Activator.CreateInstance(harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod"), new object[] { typeof(TopAttackHullDamageRegression).GetMethod(name) });
            harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5).Invoke(harmony, new object[] { method, callback, null, null, null });
        };
        patch(typeof(Unit).GetProperty("IsServer").GetMethod, "Server");
        patch(typeof(Unit).GetMethod("RpcDamage", Fields), "Damage");
        var scene = EditorSceneManager.NewPreviewScene();
        var parent = new GameObject("Inactive real SURF hull test"); parent.SetActive(false); SceneManager.MoveGameObjectToScene(parent, scene);
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/PatrolBoat1_PLACEHOLDER.prefab");
            var go = UnityEngine.Object.Instantiate(prefab, parent.transform, false);
            hull = go.GetComponent<ShipPart>();
            typeof(ShipPart).GetField("damageControlActive", Fields).SetValue(hull, true);
            var effects = (System.Collections.IList)typeof(UnitPart).GetField("damageEffects", Fields).GetValue(hull); effects.Clear();
            var materials = typeof(UnitPart).GetField("damageMaterial", Fields).GetValue(hull);
            ((System.Collections.IList)materials.GetType().GetField("renderers", Fields).GetValue(materials)).Clear();
            var hp = typeof(UnitPart).GetField("hitPoints", Fields);
            hp.SetValue(hull, 100f);
            ((IDamageable)hull).TakeDamage(1100f, 20f, 1f, 0f, 0f, PersistentID.None);
            float old = applied;
            hp.SetValue(hull, 100f);
            ((IDamageable)hull).TakeDamage(15000f, 20f, 1f, 0f, 0f, PersistentID.None);
            float revised = applied, remaining = (float)hp.GetValue(hull);
            if (old >= 7f || revised < 60f || remaining >= 40f) throw new Exception("Native damage scaling did not match expected hull loss.");
            return string.Format("Real SURF ShipPart TakeDamage -> local RPC ApplyDamage: old slug {0:F2} HP; revised slug {1:F2} HP; hull HP 100 -> {2:F2}. Native armor, tolerance and damage compression retained.", old, revised, remaining);
        }
        finally { harmonyType.GetMethod("UnpatchSelf").Invoke(harmony, null); EditorSceneManager.ClosePreviewScene(scene); hull = null; }
    }
}
