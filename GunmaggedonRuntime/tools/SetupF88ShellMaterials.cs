using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class SetupF88ShellMaterials
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 public static object Run()
 {
  var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Materials/Gunmaggedon_F88_Shell_BaseColor.mat");
  if(!material||!material.GetTexture("_BaseMap"))throw new Exception("Textured F88 shell material missing");
  var backup=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"UnitySetupBackup/F88/BeforeShellMaterials");Directory.CreateDirectory(backup);
  foreach(var name in new[]{"F88_Turret_Rig.prefab","F88_AA_Trailer.prefab","F88_SpentCasing.prefab","Gunmaggedon F88 AAA Shells.fbx.meta","modinfo.json"})if(!File.Exists(Path.Combine(backup,name)))File.Copy(Dir+name,Path.Combine(backup,name));
  var importer=(ModelImporter)AssetImporter.GetAtPath(Dir+"Gunmaggedon F88 AAA Shells.fbx");
  importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Gunmaggedon_F88_Shell"),material);importer.SaveAndReimport();
  int count=0;
  foreach(var name in new[]{"F88_Turret_Rig.prefab","F88_AA_Trailer.prefab","F88_SpentCasing.prefab"}){
   var root=PrefabUtility.LoadPrefabContents(Dir+name);
   try{foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.name=="shell_casing"||r.name=="shell_loaded"){
    var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;r.sharedMaterials=materials;count++;
   }PrefabUtility.SaveAsPrefabAsset(root,Dir+name);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  AssetDatabase.SaveAssets();return "Assigned textured shell material to "+count+" prefab renderers and remapped FBX import.";
 }
}
