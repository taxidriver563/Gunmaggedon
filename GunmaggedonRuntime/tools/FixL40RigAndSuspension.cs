using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class FixL40RigAndSuspension
{
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
  const string path=dir+"L40_AA_Trailer.prefab";
  string backup=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/L40AA/BeforeRigPhysicsFix");
  System.IO.Directory.CreateDirectory(backup);
  foreach(var n in new[]{"L40_AA_Trailer.prefab","L40_AA_Definition.asset"}) {
   if(!System.IO.File.Exists(backup+"/"+n))System.IO.File.Copy(dir+n,backup+"/"+n);
  }
  var root=PrefabUtility.LoadPrefabContents(path);
  try {
   var model=root.transform.Find("Model");
   // Imported FBX instances cannot have their internal hierarchy restructured.
   // Unpack the model, preserving mesh/material references, before moving its parts.
   if(PrefabUtility.IsPartOfPrefabInstance(model.gameObject))
    PrefabUtility.UnpackPrefabInstance(model.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   var yaw=root.transform.Find("YawPivot");var pitch=yaw.Find("PitchPivot");
   foreach(var item in new[]{new[]{"traverse","YawPivot"},new[]{"gun","YawPivot/PitchPivot"},new[]{"barrel","YawPivot/PitchPivot"},new[]{"elevationcrank","YawPivot/PitchCrankPivot"},new[]{"yawcrank","YawPivot/YawCrankPivot"}}){
    var part=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==item[0]);
    var destination=item[0]=="barrel"&&pitch.Find("RecoilPivot")?pitch.Find("RecoilPivot"):root.transform.Find(item[1]);
    part.SetParent(destination,true);
   }
   var vehicle=root.GetComponent<GroundVehicle>();var def=(VehicleDefinition)vehicle.definition;
   float spawnHeight=def.spawnOffset.y,mass=def.mass,spring=100000f;
   var so=new SerializedObject(vehicle);
   so.FindProperty("springRate").floatValue=spring;
   // Native suspension applies one spring at the root, not one per wheel:
   // F = k * (travel - root altitude). Balance weight at the authored spawn height.
   so.FindProperty("suspensionTravel").floatValue=spawnHeight+mass*9.81f/spring;
   so.FindProperty("dampingRate").floatValue=20000f;
   so.FindProperty("wheelRadius").floatValue=.8f;
   so.ApplyModifiedPropertiesWithoutUndo();
   var body=root.GetComponent<Rigidbody>();body.angularDrag=.5f;
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  var yawSaved=saved.transform.Find("YawPivot");
  if(!yawSaved.Find("traverse")||!yawSaved.Find("PitchPivot/gun")||(!yawSaved.Find("PitchPivot/barrel")&&!yawSaved.Find("PitchPivot/RecoilPivot/barrel")))throw new Exception("Saved rig did not retain moving meshes");
  return new {movingMeshes=yawSaved.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.name).ToArray(),spring=100000,travel=((VehicleDefinition)saved.GetComponent<GroundVehicle>().definition).spawnOffset.y+2000*9.81f/100000f};
 }
}
