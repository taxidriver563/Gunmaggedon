using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupL25Combat
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 static void Set(UnityEngine.Object o,string name,object value){var so=new SerializedObject(o);var p=so.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" missing "+name);if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is string)p.stringValue=(string)value;else if(value is Vector3)p.vector3Value=(Vector3)value;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
 public static object Run()
 {
  if(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L25_AA_Trailer.prefab")||AssetDatabase.LoadAssetAtPath<VehicleDefinition>(Dir+"L25_AA_Definition.asset"))throw new Exception("L25 operational assets already exist");
  var repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");var backup=Path.Combine(repo,"UnitySetupBackup/L25/BeforeCombat");Directory.CreateDirectory(backup);foreach(var n in new[]{"L25_Turret_Rig.prefab","L25_AA_Weapon.asset","modinfo.json"})File.Copy(Dir+n,Path.Combine(backup,n),false);
  var source=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L40_AA_Trailer.prefab");var scene=EditorSceneManager.NewPreviewScene();var root=UnityEngine.Object.Instantiate(source);root.SetActive(false);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
  try{
   root.name="L25 Triple AA Trailer";UnityEngine.Object.DestroyImmediate(root.GetComponentInChildren<Turret>(true).gameObject);UnityEngine.Object.DestroyImmediate(root.transform.Find("Model").gameObject);foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
   var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L25_Turret_Rig.prefab"),root.transform,false);foreach(var child in rig.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==rig.transform).ToArray())child.SetParent(root.transform,false);UnityEngine.Object.DestroyImmediate(rig);
   var unit=root.GetComponent<GroundVehicle>();var turret=root.GetComponentInChildren<Turret>(true);var pitch=turret.transform.Find("PitchPivot");var guns=root.GetComponentsInChildren<Gun>(true);var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir+"L25_AA_Weapon.asset");
   var part=turret.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(source.GetComponentInChildren<Turret>(true).GetComponent<UnitPart>(),part);Set(part,"parentUnit",unit);Set(part,"rb",root.GetComponent<Rigidbody>());var ps=new SerializedObject(part);ps.FindProperty("disintegrateObjects").arraySize=1;ps.FindProperty("disintegrateObjects").GetArrayElementAtIndex(0).objectReferenceValue=turret.gameObject;ps.ApplyModifiedPropertiesWithoutUndo();
   var detector=turret.gameObject.AddComponent<TargetDetector>();EditorUtility.CopySerialized(source.GetComponentInChildren<Turret>(true).GetComponent<TargetDetector>(),detector);var optics=new GameObject("L25Optics").transform;optics.SetParent(turret.transform,false);optics.localPosition=new Vector3(0,1.65f,-.6f);Set(detector,"attachedUnit",unit);Set(detector,"scanner",optics);Set(detector,"part",part);Set(detector,"visualRange",3500f);
   Set(turret,"attachedUnit",unit);Set(turret,"elevationTransform",pitch);Set(turret,"aimSafetyWeapon",guns[0]);Set(turret,"lockTime",.15f);Set(turret,"manual",false);Set(turret,"firesWithoutAiming",false);
   var ts=new SerializedObject(turret);ts.FindProperty("targetDetectors").arraySize=1;ts.FindProperty("targetDetectors").GetArrayElementAtIndex(0).objectReferenceValue=detector;ts.FindProperty("criticalParts").arraySize=1;ts.FindProperty("criticalParts").GetArrayElementAtIndex(0).objectReferenceValue=part;ts.ApplyModifiedPropertiesWithoutUndo();
   foreach(var gun in guns){Set(gun,"attachedUnit",unit);Set(gun,"velocityInherit",root.GetComponent<Rigidbody>());Set(gun,"ammo",300);Set(gun,"magazines",19);Set(gun,"startLoaded",true);Set(gun,"Safety",false);Set(gun,"ForceServerAuthority",true);gun.enabled=true;}turret.enabled=true;
   Set(info,"effectiveness.antiAir",1f);Set(info,"effectiveness.antiSurface",0f);Set(info,"effectiveness.antiMissile",0f);Set(info,"targetRequirements.maxRange",3500f);Set(info,"targetRequirements.minAltitude",0f);Set(info,"targetRequirements.maxAltitude",5000f);Set(info,"targetRequirements.minAlignment",4f);
   if(!AssetDatabase.CopyAsset(Dir+"L40_AA_Definition.asset",Dir+"L25_AA_Definition.asset"))throw new Exception("Cannot create definition");var def=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(Dir+"L25_AA_Definition.asset");
   Set(def,"jsonKey","Gunmaggedon_L25_AA");Set(def,"unitName","L25 Triple AA Trailer");Set(def,"description","Close-defense triple 25mm anti-aircraft gun. Optical tracking is strongest at short range, with a 3.5km engagement limit. Three independent 15-round clips feed staggered barrel fire.");Set(def,"mass",2000f);Set(def,"spawnOffset",new Vector3(0,.92f,0));Set(def,"length",8.2f);Set(def,"width",6.9f);Set(def,"height",3.5f);Set(def,"roleIdentity.antiAir",1f);Set(def,"roleIdentity.antiSurface",0f);Set(def,"roleIdentity.antiMissile",0f);Set(def,"disabled",false);
   Set(unit,"definition",def);Set(unit,"mobile",false);Set(unit,"wreckage",null);Set(unit,"springRate",100000f);Set(unit,"dampingRate",20000f);Set(unit,"suspensionTravel",.92f+2000f*9.81f/100000);Set(unit,"wheelRadius",.8f);Set(root.GetComponent<UnitPart>(),"mass",2000f);root.GetComponent<Rigidbody>().mass=2000;root.GetComponent<Rigidbody>().angularDrag=.5f;
   var chassis=root.AddComponent<BoxCollider>();chassis.center=new Vector3(0,-.05f,0);chassis.size=new Vector3(1.8f,.6f,6.7f);var mount=turret.gameObject.AddComponent<CapsuleCollider>();mount.center=new Vector3(0,.75f,0);mount.height=1.5f;mount.radius=.55f;var cradle=pitch.gameObject.AddComponent<BoxCollider>();cradle.center=new Vector3(0,.37f,.13f);cradle.size=new Vector3(3,.9f,2.3f);
   foreach(var name in new[]{"Left","Middle","Right"}){var wrapper=pitch.Find("Barrel"+name);var r=wrapper.GetComponentInChildren<MeshRenderer>();var collider=wrapper.gameObject.AddComponent<BoxCollider>();collider.center=wrapper.InverseTransformPoint(r.bounds.center);collider.size=new Vector3(.16f,.16f,1.6f);}
   for(int i=1;i<=4;i++)root.transform.Find("WheelContact"+i).gameObject.AddComponent<SphereCollider>().radius=.8f;
   var ni=new SerializedObject(root.GetComponent<Mirage.NetworkIdentity>());ni.FindProperty("_prefabHash").intValue=0;ni.ApplyModifiedPropertiesWithoutUndo();
   root.SetActive(true);var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+"L25_AA_Trailer.prefab");Set(def,"unitPrefab",prefab);AssetDatabase.SaveAssets();Selection.activeObject=prefab;
   return "Created spawnable L25 Triple AA Trailer with three independent native stations, 900 total rounds and stable suspension.";
  }finally{EditorSceneManager.ClosePreviewScene(scene);}
 }
}
