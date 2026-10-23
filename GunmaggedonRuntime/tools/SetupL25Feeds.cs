using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class SetupL25Feeds
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 static void Set(UnityEngine.Object o,string name,object value){var so=new SerializedObject(o);var p=so.FindProperty(name);if(p==null)throw new Exception("Missing "+name);if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
 public static object Run()
 {
  var backup=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"UnitySetupBackup/L25/BeforeIndependentFeeds");Directory.CreateDirectory(backup);
  foreach(var n in new[]{"L25_Turret_Rig.prefab","L25_AA_Weapon.asset"})if(!File.Exists(Path.Combine(backup,n)))File.Copy(Dir+n,Path.Combine(backup,n));
  var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir+"L25_AA_Weapon.asset");Set(info,"targetRequirements.maxRange",3500f);Set(info,"fireInterval",.3f);Set(info,"boresight",false);
  var root=PrefabUtility.LoadPrefabContents(Dir+"L25_Turret_Rig.prefab");
  try{
   var turret=root.GetComponentInChildren<Turret>(true);var pitch=turret.transform.Find("PitchPivot");var old=pitch.GetComponent<Gun>();if(!old)throw new Exception("Shared gun missing or migration already applied");
   var guns=new Gun[3];string[] names={"Left","Middle","Right"};
   for(int i=0;i<3;i++){
    var feed=new GameObject("Feed"+names[i]);feed.transform.SetParent(pitch,false);var gun=feed.AddComponent<Gun>();EditorUtility.CopySerialized(old,gun);guns[i]=gun;
    var audio=feed.AddComponent<AudioSource>();EditorUtility.CopySerialized(pitch.GetComponent<AudioSource>(),audio);
    Set(gun,"fireRate",200f);Set(gun,"magazineCapacity",15);Set(gun,"reloadTime",2f);Set(gun,"bulletSelfDestruct",14f);Set(gun,"bulletSpread",.18f);
    var muzzle=pitch.Find("Barrel"+names[i]+"/Muzzle");var effects=muzzle.GetComponentsInChildren<ParticleSystem>(true);var so=new SerializedObject(gun);so.FindProperty("muzzles").arraySize=1;so.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;so.FindProperty("muzzleParticles").arraySize=effects.Length;for(int j=0;j<effects.Length;j++)so.FindProperty("muzzleParticles").GetArrayElementAtIndex(j).objectReferenceValue=effects[j];so.ApplyModifiedPropertiesWithoutUndo();
   }
   var ts=new SerializedObject(turret);ts.FindProperty("aimSafetyWeapon").objectReferenceValue=guns[0];var stations=ts.FindProperty("weaponStations");stations.arraySize=3;for(int i=0;i<3;i++){var station=stations.GetArrayElementAtIndex(i);station.FindPropertyRelative("Weapons").arraySize=1;station.FindPropertyRelative("Weapons").GetArrayElementAtIndex(0).objectReferenceValue=guns[i];station.FindPropertyRelative("WeaponInfo").objectReferenceValue=info;station.FindPropertyRelative("Turrets").arraySize=0;station.FindPropertyRelative("Number").intValue=i;}ts.ApplyModifiedPropertiesWithoutUndo();
   UnityEngine.Object.DestroyImmediate(old);UnityEngine.Object.DestroyImmediate(pitch.GetComponent<AudioSource>());
   PrefabUtility.SaveAsPrefabAsset(root,Dir+"L25_Turret_Rig.prefab");AssetDatabase.SaveAssets();return "Three independent 15-round native guns; 3.5km range, 200rpm each / 600rpm aggregate, provisional two-second clip reload.";
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}
