using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class SetupR230Legacy
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object o,string name,UnityEngine.Object value){var so=new SerializedObject(o);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    public static object Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stable edit mode required");
        foreach(var file in new[]{"R230NL_Emplacement.prefab","R230NL_Definition.asset","R230NL_Railgun.asset"})if(AssetDatabase.LoadMainAssetAtPath(Dir+file))throw new Exception("Already exists: "+file);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"R230N_Emplacement.prefab");if(!source)throw new Exception("Nuclear reference missing");
        var root=PrefabUtility.LoadPrefabContents(Dir+"R230N_Emplacement.prefab");
        try{
            root.name="R230NL_Emplacement";var unit=root.GetComponent<Building>();var gun=root.GetComponentInChildren<Gun>(true);
            var info=UnityEngine.Object.Instantiate(gun.info);info.name="R230NL_Railgun";info.description="Legacy 1.5-kiloton moving-target proximity railgun. Two-second charge, three-shot capacitor bank, ten-second bank recharge.";
            AssetDatabase.CreateAsset(info,Dir+"R230NL_Railgun.asset");Set(gun,"info",info);
            var turret=root.GetComponentInChildren<Turret>(true);var serialized=new SerializedObject(turret);var stations=serialized.FindProperty("weaponStations");
            for(int i=0;i<stations.arraySize;i++)stations.GetArrayElementAtIndex(i).FindPropertyRelative("WeaponInfo").objectReferenceValue=info;serialized.ApplyModifiedPropertiesWithoutUndo();
            var definition=UnityEngine.Object.Instantiate((BuildingDefinition)unit.definition);definition.name="R230NL_Definition";definition.jsonKey="Gunmaggedon_R230NL";definition.unitName="R-230N-L";
            var so=new SerializedObject(definition);so.FindProperty("code").stringValue="R-230N-L";so.ApplyModifiedPropertiesWithoutUndo();
            definition.description="Legacy nuclear Stratopenetrator: pre-balance continuous aim and moving-target proximity airburst. Two-second warning, three-shot capacitor bank with ten-second refill. Current warning visuals and glassy pressure rings. 5-65km engagement envelope.";
            AssetDatabase.CreateAsset(definition,Dir+"R230NL_Definition.asset");Set(unit,"definition",definition);
            var net=new SerializedObject(root.GetComponent<Mirage.NetworkIdentity>());net.FindProperty("_prefabHash").intValue=0;net.ApplyModifiedPropertiesWithoutUndo();
            var lod=root.GetComponent<LODGroup>();var levels=lod.GetLODs();var triangles=levels.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
            if(!lod.enabled||!triangles.SequenceEqual(new[]{1060,610,288})||root.GetComponentsInChildren<MeshRenderer>(true).Any(r=>!levels.Any(l=>l.renderers.Contains(r))))throw new Exception("Mandatory LODs invalid");
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+"R230NL_Emplacement.prefab");Set(definition,"unitPrefab",prefab);AssetDatabase.SaveAssets();
            return new{prefab=AssetDatabase.GetAssetPath(prefab),definition.jsonKey,definition.unitName,triangles};
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
