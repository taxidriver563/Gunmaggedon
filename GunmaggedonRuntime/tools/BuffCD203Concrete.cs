using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class BuffCD203Concrete
{
    static void Set(UnityEngine.Object target,string path,float value){var s=new SerializedObject(target);s.FindProperty(path).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var workspace=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");
        var backup=Path.Combine(workspace,"UnitySetupBackup/CD203/BeforeConcreteBuff");Directory.CreateDirectory(backup);
        foreach(var name in new[]{"Emplacement_203mmCG","Emplacement_203mmCG_SPECIAL"}){
            var path="Assets/Blueprinter/Mods/Gunmageddon/"+name+".prefab";
            if(!File.Exists(Path.Combine(backup,name+".prefab")))File.Copy(path,Path.Combine(backup,name+".prefab"));
            var root=PrefabUtility.LoadPrefabContents(path);
            try{
                var owner=root.GetComponent<Building>();var original=root.GetComponent<UnitPart>();
                // The old 15x8x15 box covered the open pit and exposed weapon.
                foreach(var c in root.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(c);
                foreach(var partPath in new[]{"concrete","traverse/traverse_mesh","traverse/gun/gun_mesh","traverse/gun/barrel_mount/barrel/barrel_mesh"}){
                    var mesh=root.transform.Find(partPath);if(!mesh)throw new Exception("Missing "+partPath);
                    var part=mesh.GetComponent<UnitPart>();if(!part){part=mesh.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(original,part);}
                    var so=new SerializedObject(part);so.FindProperty("parentUnit").objectReferenceValue=owner;
                    so.FindProperty("structuralThreshold").floatValue=-1000000;so.FindProperty("integrityThreshold").floatValue=-float.MaxValue;so.ApplyModifiedPropertiesWithoutUndo();
                    var geometry=mesh.GetComponent<MeshFilter>().sharedMesh;
                    if(partPath=="concrete"){
                        var collider=mesh.GetComponent<MeshCollider>();if(!collider)collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=geometry;collider.convex=false;
                        Set(part,"armorProperties.pierceArmor",300);Set(part,"armorProperties.blastArmor",150);Set(part,"armorProperties.fireArmor",100);
                        Set(part,"armorProperties.pierceTolerance",6);Set(part,"armorProperties.blastTolerance",4);Set(part,"armorProperties.fireTolerance",10);Set(part,"armorProperties.overpressureLimit",30);
                    }else{
                        var collider=mesh.GetComponent<BoxCollider>();if(!collider)collider=mesh.gameObject.AddComponent<BoxCollider>();collider.center=geometry.bounds.center;collider.size=geometry.bounds.size;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Updated standard and SPECIAL CD-203 concrete armor and separate gun hitboxes.";
    }
}
