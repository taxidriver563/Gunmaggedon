using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

// One-time, narrowly scoped migration. No collider geometry or render rig edits.
public static class FixF120Damage
{
    const string PathName="Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer.prefab";
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required.");
        var workspace=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");
        var backup=Path.Combine(workspace,"UnitySetupBackup/F120/BeforeDamageFix");Directory.CreateDirectory(backup);
        if(!File.Exists(Path.Combine(backup,"F120_AAA_Trailer.prefab")))File.Copy(PathName,Path.Combine(backup,"F120_AAA_Trailer.prefab"));
        var root=PrefabUtility.LoadPrefabContents(PathName);int added=0;
        try{
            var owner=root.GetComponent<GroundVehicle>();var chassis=root.GetComponent<UnitPart>();
            var turret=root.transform.Find("F120YawPivot").GetComponent<UnitPart>();
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)){
                if(collider.isTrigger||collider.GetComponent<IDamageable>()!=null)continue;
                var source=collider.transform.IsChildOf(turret.transform)?turret:chassis;
                var part=collider.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(source,part);
                var so=new SerializedObject(part);
                so.FindProperty("parentUnit").objectReferenceValue=owner;
                so.FindProperty("rb").objectReferenceValue=root.GetComponent<Rigidbody>();
                so.FindProperty("mass").floatValue=0;
                // These collision helpers are not independently detachable pieces.
                so.FindProperty("structuralThreshold").floatValue=-1000000;
                so.FindProperty("integrityThreshold").floatValue=-1000000;
                so.FindProperty("disintegrationEffects").arraySize=0;
                so.FindProperty("disintegrateObjects").arraySize=0;
                so.ApplyModifiedPropertiesWithoutUndo();added++;
            }
            if(root.GetComponentsInChildren<Collider>(true).Any(c=>!c.isTrigger&&c.GetComponent<IDamageable>()==null))throw new Exception("Unrouted collision shape remains.");
            PrefabUtility.SaveAsPrefabAsset(root,PathName);AssetDatabase.SaveAssets();
            return new{added,colliders=root.GetComponentsInChildren<Collider>(true).Length};
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
