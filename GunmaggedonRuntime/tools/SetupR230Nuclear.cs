using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class SetupR230Nuclear
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object o,string name,object value)
    {var so=new SerializedObject(o);var p=so.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" lacks "+name);if(value is string)p.stringValue=(string)value;else if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
    public static object Run(string workspace)
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stable edit mode required");
        foreach(var file in new[]{"R230N_Emplacement.prefab","R230N_Definition.asset","R230N_Railgun.asset"})if(AssetDatabase.LoadMainAssetAtPath(Dir+file))throw new Exception("Already exists; use a migration: "+file);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"R230_Emplacement.prefab");if(!source)throw new Exception("Standard R230 reference missing");
        if(string.IsNullOrEmpty(workspace))throw new Exception("Workspace required for backup");
        var backup=Path.Combine(workspace,"UnitySetupBackup/R230N/BeforeSetup");Directory.CreateDirectory(backup);
        foreach(var file in new[]{"R230_Emplacement.prefab","R230_Definition.asset","R230_Railgun.asset","modinfo.json"})File.Copy(Dir+file,Path.Combine(backup,file),false);
        var root=PrefabUtility.LoadPrefabContents(Dir+"R230_Emplacement.prefab");
        try
        {
            root.name="R230N_Emplacement";var gun=root.GetComponentInChildren<Gun>(true);var unit=root.GetComponent<Building>();
            var info=UnityEngine.Object.Instantiate(gun.info);info.name="R230N_Railgun";
            Set(info,"weaponName","230mm Nuclear Railgun");Set(info,"shortName","1.5kt Railgun");Set(info,"description","1.5-kiloton proximity airburst railgun. Requires Gunmaggedon runtime.");
            Set(info,"blastDamage",0f);Set(info,"pierceDamage",0f);Set(info,"targetRequirements.minRange",5000f);AssetDatabase.CreateAsset(info,Dir+"R230N_Railgun.asset");
            Set(gun,"info",info);Set(gun,"recoilTransform",null);Set(gun,"recoilTravel",0f);Set(gun,"recoilImpulse",0f);
            var turret=root.GetComponentInChildren<Turret>(true);var serialized=new SerializedObject(turret);
            var stations=serialized.FindProperty("weaponStations");for(int i=0;i<stations.arraySize;i++)stations.GetArrayElementAtIndex(i).FindPropertyRelative("WeaponInfo").objectReferenceValue=info;serialized.ApplyModifiedPropertiesWithoutUndo();
            var definition=UnityEngine.Object.Instantiate((BuildingDefinition)unit.definition);definition.name="R230N_Definition";
            Set(definition,"jsonKey","Gunmaggedon_R230N");Set(definition,"unitName","R-230N");Set(definition,"code","R-230N");
            Set(definition,"description","Nuclear Stratopenetrator: 1.5-kiloton proximity airbursts, enlarged danger indicator. 5-65km engagement envelope; same threat priorities and capacitor cycle as R-230.");
            AssetDatabase.CreateAsset(definition,Dir+"R230N_Definition.asset");Set(unit,"definition",definition);Set(root.GetComponent<Mirage.NetworkIdentity>(),"_prefabHash",0);
            var group=root.GetComponent<LODGroup>();if(!group||!group.enabled||group.GetLODs().Length<3)throw new Exception("Mandatory inherited LODs missing");
            int previous=int.MaxValue;foreach(var level in group.GetLODs()){int tris=level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);if(tris>=previous)throw new Exception("Distant LOD does not reduce geometry");previous=tris;}
            if(root.GetComponentsInChildren<MeshRenderer>(true).Any(r=>!group.GetLODs().Any(l=>l.renderers.Contains(r))))throw new Exception("Unmanaged model renderer");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+"R230N_Emplacement.prefab");Set(definition,"unitPrefab",prefab);AssetDatabase.SaveAssets();
            return new{prefab=AssetDatabase.GetAssetPath(prefab),designation="R-230N",lods=group.GetLODs().Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray(),sceneChanged=false};
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
