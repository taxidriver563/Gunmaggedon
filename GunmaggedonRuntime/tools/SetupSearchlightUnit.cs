using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupSearchlightUnit
{
 static void Set(UnityEngine.Object o,string n,object value)
 {
  var s=new SerializedObject(o);var p=s.FindProperty(n);if(p==null)throw new Exception(o.GetType()+" missing "+n);
  if(value is UnityEngine.Object||value==null)p.objectReferenceValue=value as UnityEngine.Object;
  else if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;
  else if(value is bool)p.boolValue=(bool)value;else if(value is Vector3)p.vector3Value=(Vector3)value;else p.stringValue=(string)value;
  s.ApplyModifiedPropertiesWithoutUndo();
 }
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/",path=dir+"Searchlight_Trailer.prefab";
  if(AssetDatabase.LoadAssetAtPath<GameObject>(path))throw new Exception("Searchlight unit already exists");
  var source=GameObject.Find("Gunmaggedon Air Defense Searchlight");if(!source)throw new Exception("Scene model missing");
  var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"L40_AA_Trailer.prefab"));
  PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  root.name="Gunmaggedon Air Defense Searchlight";root.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
  var vehicle=root.GetComponent<GroundVehicle>();var part=root.GetComponent<UnitPart>();
  var originalDetector=root.GetComponentInChildren<TargetDetector>(true);
  var dg=new GameObject("SearchlightDetector");dg.transform.SetParent(root.transform,false);var detector=dg.AddComponent<TargetDetector>();
  EditorUtility.CopySerialized(originalDetector,detector);
  foreach(var child in root.transform.Cast<Transform>().ToArray())if(child!=dg.transform&&!child.name.StartsWith("WheelContact")&&child.name!="TowHook")UnityEngine.Object.DestroyImmediate(child.gameObject);
  var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"Searchlight_Visual.prefab"),root.transform,false);model.name="Model";
  foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))if(r.name=="SearchlightBeam"||r.name=="LensGlow")r.enabled=false;
  foreach(var l in model.GetComponentsInChildren<Light>(true))l.enabled=false;
  var origin=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BeamOrigin");
  Set(detector,"attachedUnit",vehicle);Set(detector,"part",part);Set(detector,"scanner",origin);Set(detector,"visualRange",0f);Set(detector,"shared",true);Set(detector,"activated",true);
  var ds=new SerializedObject(detector);ds.FindProperty("rotators").arraySize=0;ds.ApplyModifiedPropertiesWithoutUndo();
  Set(vehicle,"radar",null);Set(vehicle,"obstacleTopTransform",model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LampPitch"));Set(vehicle,"cockpitViewPoint",origin);
  Set(vehicle,"springRate",125000f);Set(vehicle,"dampingRate",25000f);Set(vehicle,"suspensionTravel",.92f+2500f*9.81f/125000f);
  Set(part,"mass",2500f);root.GetComponent<Rigidbody>().mass=2500;
  var body=root.GetComponent<BoxCollider>();body.center=new Vector3(0,.1f,0);body.size=new Vector3(2.2f,.65f,5.5f);
  var housing=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LampPitch").gameObject.AddComponent<BoxCollider>();housing.size=new Vector3(2.2f,1.8f,1.5f);
  var ni=new SerializedObject(root.GetComponent<Mirage.NetworkIdentity>());ni.FindProperty("_prefabHash").intValue=0;ni.ApplyModifiedPropertiesWithoutUndo();
  AssetDatabase.CopyAsset(dir+"L40_AA_Definition.asset",dir+"Searchlight_Definition.asset");
  var def=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(dir+"Searchlight_Definition.asset");
  Set(def,"jsonKey","Gunmaggedon_Searchlight");Set(def,"unitName","Air Defense Searchlight Trailer");Set(def,"code","SLT");Set(def,"vehicleType",0);
  Set(def,"description","Night air-defense searchlight. Sweeps for aircraft and gradually acquires illuminated contacts. Friendly HLT/MSV radar trucks within 300 metres provide coarse search cues.");
  Set(def,"mass",2500f);Set(def,"height",3.6f);Set(def,"roleIdentity.antiAir",0f);Set(def,"unitPrefab",null);Set(vehicle,"definition",def);
  var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,path,InteractionMode.AutomatedAction);Set(def,"unitPrefab",prefab);EditorUtility.SetDirty(def);
  UnityEngine.Object.DestroyImmediate(source);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
  return new{prefab=path,definition=AssetDatabase.GetAssetPath(def),missing=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),weapons=root.GetComponentsInChildren<Gun>(true).Length};
 }
}
