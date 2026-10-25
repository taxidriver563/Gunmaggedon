using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupF120Special
{
    const string Dir = "Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    { var s = new SerializedObject(target); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    public static object UpdateSceneInstance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "F120_AAA_Trailer Special");
        var vehicle = root.GetComponent<GroundVehicle>(); var gun = root.GetComponentInChildren<Gun>(true); var turret = root.GetComponentInChildren<Turret>(true);
        Undo.RecordObjects(new UnityEngine.Object[] { vehicle, gun, turret }, "Connect F120S special ammunition");
        Set(vehicle, "definition", AssetDatabase.LoadAssetAtPath<VehicleDefinition>(Dir + "F120S_AA_Definition.asset"));
        var info = AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir + "F120S_AA_Weapon.asset"); Set(gun, "info", info);
        Set(gun, "selfDestructEffect", AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "F120S_Canister_Opening.prefab"));
        var ts = new SerializedObject(turret);
        foreach (SerializedProperty station in ts.FindProperty("weaponStations")) station.FindPropertyRelative("WeaponInfo").objectReferenceValue = info;
        ts.ApplyModifiedPropertiesWithoutUndo();
        var barrel = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "barrel");
        foreach (var r in barrel.GetComponentsInChildren<MeshRenderer>(true)) { Undo.RecordObject(r, "Preserve F120S stripe LOD"); r.sharedMaterials = barrel.sharedMaterials; }
        EditorSceneManager.MarkSceneDirty(root.scene);
        return new { definition = vehicle.definition.jsonKey, weapon = gun.info.name, sceneSaved = false };
    }
    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required");
        string workspace = Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new Exception("Set GUNMAGGEDON_ROOT");
        var source = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "F120_AAA_Trailer Special");
        const string special = Dir + "F120_AAA_Trailer Special.prefab";
        const string weaponPath = Dir + "F120S_AA_Weapon.asset", definitionPath = Dir + "F120S_AA_Definition.asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponInfo>(weaponPath) || AssetDatabase.LoadAssetAtPath<VehicleDefinition>(definitionPath))
            throw new Exception("Special definitions already exist; inspect instead of rerunning setup");
        string backup = Path.Combine(workspace, "UnitySetupBackup/F120S/BeforeSetup"); Directory.CreateDirectory(backup);
        foreach (string path in new[] { special, Dir + "F120_AAA_Trailer.prefab", Dir + "modinfo.json" })
            File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
        // Preserve the user's unsaved stripe/material overrides without saving
        // or replacing their active scene. Work only on a preview clone.
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var stage = new GameObject("inactive F120S setup"); stage.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage, scene);
            var root = UnityEngine.Object.Instantiate(source, stage.transform, false); root.name = "F120S_AAA_Trailer";
            var gun = root.GetComponentInChildren<Gun>(true); var vehicle = root.GetComponent<GroundVehicle>();
            var turret = root.GetComponentInChildren<Turret>(true);
            var info = UnityEngine.Object.Instantiate(gun.info); info.name = "F120S_AA_Weapon";
            info.weaponName = "120mm Incendiary Canister"; info.shortName = "120mm IC";
            info.description = "Timed forward-dispersing incendiary canister. Moving fragments inflict native penetration and fire damage; no ordinary spherical flak burst.";
            info.blastDamage = 0; info.pierceDamage = 700;
            AssetDatabase.CreateAsset(info, weaponPath); Set(gun, "info", info);
            var ts = new SerializedObject(turret);
            foreach (SerializedProperty station in ts.FindProperty("weaponStations")) station.FindPropertyRelative("WeaponInfo").objectReferenceValue = info;
            ts.ApplyModifiedPropertiesWithoutUndo();
            var definition = UnityEngine.Object.Instantiate((VehicleDefinition)vehicle.definition); definition.name = "F120S_AA_Definition";
            definition.jsonKey = "Gunmaggedon_F120S_AA"; definition.unitName = "F-120S";
            definition.description = "Special-munitions F-120. Advanced optical/radar-supported predictions place incendiary canisters ahead of aircraft. Burning fragments reward predictable flight paths; agile aircraft can evade the cone.";
            AssetDatabase.CreateAsset(definition, definitionPath); Set(vehicle, "definition", definition);
            // Small cosmetic opening effect, borrowing the existing custom sound
            // and Blueprinter native material remaps from the regular F120.
            var opening = UnityEngine.Object.Instantiate(new SerializedObject(gun).FindProperty("selfDestructEffect").objectReferenceValue as GameObject, stage.transform, false);
            opening.name = "F120S_Canister_Opening";
            foreach (var particles in opening.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main; main.startSizeMultiplier *= .25f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 2.5f);
            }
            var effect = PrefabUtility.SaveAsPrefabAsset(opening, Dir + "F120S_Canister_Opening.prefab"); Set(gun, "selfDestructEffect", effect);
            var barrel = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "barrel");
            var stripe = barrel.sharedMaterials;
            foreach (var renderer in barrel.GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterials = stripe;
            // Existing meaningful LOD geometry and reload visibility are retained.
            var lod = root.GetComponent<LODGroup>();
            if (!lod || !lod.enabled || lod.GetLODs().Length < 3) throw new Exception("F120S must inherit three model LODs");
            var result = PrefabUtility.SaveAsPrefabAsset(root, special); Set(definition, "unitPrefab", result);
            AssetDatabase.SaveAssetIfDirty(info); AssetDatabase.SaveAssetIfDirty(definition);
            return new { prefab = special, key = definition.jsonKey, designation = definition.unitName, regularFlakRemoved = info.blastDamage == 0,
                lods = lod.GetLODs().Length, sceneUntouched = true, redStripeLods = barrel.GetComponentsInChildren<MeshRenderer>(true).Length };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
