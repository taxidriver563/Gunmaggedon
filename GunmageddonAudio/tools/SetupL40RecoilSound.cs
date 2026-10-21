using System;
using UnityEngine;
using UnityEditor;

public static class SetupL40RecoilSound
{
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
  var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(dir+"40mm_Autocannon_Fire_Close.mp3");if(!clip)throw new Exception("New 40mm clip missing");
  string backup=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/L40AA/BeforeRecoilSound");
  System.IO.Directory.CreateDirectory(backup);if(!System.IO.File.Exists(backup+"/L40_AA_Trailer.prefab"))System.IO.File.Copy(dir+"L40_AA_Trailer.prefab",backup+"/L40_AA_Trailer.prefab");
  var root=PrefabUtility.LoadPrefabContents(dir+"L40_AA_Trailer.prefab");
  try {
   var gun=root.GetComponentInChildren<Gun>(true);var pitch=gun.transform;var recoil=pitch.Find("RecoilPivot");
   // Gun.ShotSound selects the looping rotary path whenever there are >=2
   // sources, regardless of fireSustained being null. Single shots need one.
   var emitters=gun.GetComponents<AudioSource>();
   if(emitters.Length==0)emitters=new[]{gun.gameObject.AddComponent<AudioSource>()};
   for(int i=1;i<emitters.Length;i++)UnityEngine.Object.DestroyImmediate(emitters[i]);
   emitters[0].loop=false;emitters[0].playOnAwake=false;emitters[0].mute=false;
   if(!recoil){recoil=new GameObject("RecoilPivot").transform;recoil.SetParent(pitch,false);}
   var barrel=pitch.Find("barrel");if(barrel)barrel.SetParent(recoil,true);
   foreach(var name in new[]{"muzzle","muzzle_R","muzzleFlashR"}){var part=pitch.Find(name);if(part)part.SetParent(recoil,true);}
   var s=new SerializedObject(gun);s.FindProperty("recoilTransform").objectReferenceValue=recoil;
   s.FindProperty("recoilTravel").floatValue=.22f;s.FindProperty("recoilRate").floatValue=18f;s.FindProperty("recoilReturnRate").floatValue=5f;
   s.FindProperty("recoilImpulse").floatValue=0;
   var sounds=s.FindProperty("fireSounds");sounds.arraySize=1;sounds.GetArrayElementAtIndex(0).objectReferenceValue=clip;
   foreach(var name in new[]{"fireStart","fireSustained","fireEnd"})s.FindProperty(name).objectReferenceValue=null;
   s.FindProperty("pitch").floatValue=1;s.FindProperty("pitchVariation").floatValue=.025f;s.FindProperty("volume").floatValue=1;
   s.FindProperty("modifyPitch").boolValue=false;s.ApplyModifiedPropertiesWithoutUndo();
   PrefabUtility.SaveAsPrefabAsset(root,dir+"L40_AA_Trailer.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return new{sound=clip.name,clipSeconds=clip.length,travelMetres=.22f};
 }
}
