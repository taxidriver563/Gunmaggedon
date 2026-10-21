using System;
using UnityEngine;
using UnityEditor;

public static class SetupL40Casing
{
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
  const string casingPath=dir+"L40_SpentCasing.prefab";
  var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Shell_76mm_casing_PLACEHOLDER.prefab");
  if(!source)throw new Exception("Stock casing prefab missing");
  var staging=new GameObject("Inactive casing setup");staging.SetActive(false);
  try {
   var casing=UnityEngine.Object.Instantiate(source,staging.transform,false);casing.name="L40_SpentCasing";
   casing.transform.localPosition=Vector3.zero;casing.transform.localRotation=Quaternion.identity;casing.transform.localScale=Vector3.one*.45f;
   casing.GetComponent<Rigidbody>().mass=.45f;
   // Retain the native brass mesh, material and ground-contact behavior, with a
   // modest smoke budget per empty case rather than 76mm-scale particle counts.
   foreach(var ps in casing.GetComponentsInChildren<ParticleSystem>(true)){var main=ps.main;main.maxParticles=16;var emission=ps.emission;emission.rateOverTime=8f;}
   PrefabUtility.SaveAsPrefabAsset(casing,casingPath);
  }finally{UnityEngine.Object.DestroyImmediate(staging);}
  var backup=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/L40AA/BeforeCasing");
  System.IO.Directory.CreateDirectory(backup);if(!System.IO.File.Exists(backup+"/L40_AA_Trailer.prefab"))System.IO.File.Copy(dir+"L40_AA_Trailer.prefab",backup+"/L40_AA_Trailer.prefab");
  var root=PrefabUtility.LoadPrefabContents(dir+"L40_AA_Trailer.prefab");
  try {
   var gun=root.GetComponentInChildren<Gun>(true);var port=gun.transform.Find("CasingEject");
   if(!port){port=new GameObject("CasingEject").transform;port.SetParent(gun.transform,false);}
   // At the rear rectangular opening, outside the coarse turret collision hull.
   port.position=root.transform.TransformPoint(new Vector3(0,1.50f,-1.57f));
   port.rotation=root.transform.rotation*Quaternion.Euler(0,180,0);port.localScale=Vector3.one;
   var s=new SerializedObject(gun);s.FindProperty("ejectionTransform").objectReferenceValue=port;
   s.FindProperty("ejectionPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(casingPath);
   s.FindProperty("ejectionDelay").floatValue=.12f;s.FindProperty("ejectionLifetime").floatValue=5f;
   // Native SpawnEjection uses AddRelativeForce/Torque in VelocityChange
   // mode: these are direct changes of 0.6 m/s rearward,
   // 0.1 m/s upward and a slow tumble.
   s.FindProperty("ejectionLinearVelocity").vector3Value=new Vector3(0,.1f,.6f);
   s.FindProperty("ejectionRotationalVelocity").vector3Value=new Vector3(.48f,.24f,.08f);
   s.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,dir+"L40_AA_Trailer.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return new{casing=casingPath,sourceMesh=source.GetComponent<MeshFilter>().sharedMesh.name,port=new Vector3(0,1.50f,-1.57f),delay=.12f,lifetime=5};
 }
}
