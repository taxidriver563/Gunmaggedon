using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class SetupL25SoundTracers
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 public static object Run()
 {
  var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Dir+"25mm_Autocannon_Fire.mp3");if(!clip)throw new Exception("Custom 25mm sound missing");
  var backup=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"UnitySetupBackup/L25/BeforeSoundTracers");Directory.CreateDirectory(backup);
  foreach(var name in new[]{"L25_Turret_Rig.prefab","L25_AA_Trailer.prefab","modinfo.json"})if(!File.Exists(Path.Combine(backup,name)))File.Copy(Dir+name,Path.Combine(backup,name));
  int count=0;
  foreach(var name in new[]{"L25_Turret_Rig.prefab","L25_AA_Trailer.prefab"}){
   var root=PrefabUtility.LoadPrefabContents(Dir+name);
   try{foreach(var gun in root.GetComponentsInChildren<Gun>(true)){
    var so=new SerializedObject(gun);so.FindProperty("tracerRatio").intValue=3;so.FindProperty("fireSounds").arraySize=1;so.FindProperty("fireSounds").GetArrayElementAtIndex(0).objectReferenceValue=clip;
    foreach(var field in new[]{"fireStart","fireSustained","fireEnd"})so.FindProperty(field).objectReferenceValue=null;
    so.FindProperty("pitch").floatValue=1;so.FindProperty("pitchVariation").floatValue=.025f;so.FindProperty("modifyPitch").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
    var sources=gun.GetComponents<AudioSource>();if(sources.Length!=1)throw new Exception("Expected one source per feed");sources[0].pitch=1;sources[0].playOnAwake=false;sources[0].loop=false;count++;
   }PrefabUtility.SaveAsPrefabAsset(root,Dir+name);}finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  var metadata=Dir+"modinfo.json";File.WriteAllText(metadata,File.ReadAllText(metadata).Replace("0.0.24","0.0.25"));AssetDatabase.Refresh();AssetDatabase.SaveAssets();return "Custom 25mm sound assigned to "+count+" feeds; native ratio is three for shared runtime phase.";
 }
}
