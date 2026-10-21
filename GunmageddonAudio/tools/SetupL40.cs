using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupL40
{
 static void Set(UnityEngine.Object o,string n,object v) {
  var s=new SerializedObject(o); var p=s.FindProperty(n); if(p==null) throw new Exception(o.GetType()+" missing "+n);
  if(v is UnityEngine.Object || v==null) p.objectReferenceValue=v as UnityEngine.Object;
  else if(v is float) p.floatValue=(float)v; else if(v is int) p.intValue=(int)v;
  else if(v is bool) p.boolValue=(bool)v; else if(v is Vector3) p.vector3Value=(Vector3)v; else p.stringValue=(string)v;
  s.ApplyModifiedPropertiesWithoutUndo();
 }
 static Transform Pivot(string name,Transform parent,Vector3 world) {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=world;return g.transform;}
 public static object Run() {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
  if(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"L40_AA_Trailer.prefab")) throw new Exception("Already set up; refusing to duplicate.");
  var model=GameObject.Find("Gunmaggedon L40 AA"); if(!model)throw new Exception("Model missing");
  System.IO.Directory.CreateDirectory("Assets/BlueprinterSetupBackups"); AssetDatabase.Refresh();
  PrefabUtility.SaveAsPrefabAsset(model,"Assets/BlueprinterSetupBackups/L40_Original.prefab");
  if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  EditorSceneManager.SaveScene(model.scene);
  var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/CRAMTrailer1_PLACEHOLDER.prefab"));
  PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  root.name="Gunmaggedon L40 AA"; root.transform.SetPositionAndRotation(model.transform.position,model.transform.rotation);
  var vehicle=root.GetComponent<GroundVehicle>();var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))UnityEngine.Object.DestroyImmediate(r);
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))UnityEngine.Object.DestroyImmediate(f);
  var lod=root.GetComponent<LODGroup>();if(lod)UnityEngine.Object.DestroyImmediate(lod);
  foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
  model.name="Model";model.transform.SetParent(root.transform,true);
  var yaw=turret.transform; yaw.name="YawPivot";yaw.position=model.transform.Find("traverse").position;yaw.rotation=root.transform.rotation;
  var pitch=gun.transform;pitch.name="PitchPivot";pitch.position=model.transform.Find("gun").position;pitch.rotation=root.transform.rotation;
  model.transform.Find("traverse").SetParent(yaw,true);
  model.transform.Find("gun").SetParent(pitch,true);model.transform.Find("barrel").SetParent(pitch,true);
  foreach(var entry in new[]{new[]{"elevationcrank","PitchCrankPivot"},new[]{"yawcrank","YawCrankPivot"}}){var part=model.transform.Find(entry[0]);var p=Pivot(entry[1],yaw,part.position);part.SetParent(p,true);}
  var muzzle=pitch.Find("muzzle");muzzle.position=root.transform.TransformPoint(new Vector3(0,1.682f,3.72f));muzzle.rotation=root.transform.rotation;
  foreach(var p in gun.GetComponentsInChildren<ParticleSystem>(true)){p.transform.position=muzzle.position;p.transform.rotation=muzzle.rotation;}
  var detector=turret.GetComponent<TargetDetector>();Set(detector,"visualRange",6000f);
  Set(turret,"traverseRate",20f);Set(turret,"elevationRate",15f);Set(turret,"minElevation",-5f);Set(turret,"maxElevation",90f);
  AssetDatabase.CopyAsset("Assets/Blueprinter/_donotship/MonoBehaviour/Gun40mm_Naval_PLACEHOLDER.asset",dir+"L40_AA_Weapon.asset");
  var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(dir+"L40_AA_Weapon.asset");Set(gun,"info",info);Set(gun,"fireRate",120f);Set(gun,"magazineCapacity",4);Set(gun,"reloadTime",2f);Set(gun,"ammo",240);Set(gun,"magazines",60);Set(gun,"bulletSpread",1f);
  Set(gun,"fireSustained",null);Set(gun,"fireEnd",null);
  Set(gun,"ForceServerAuthority",true);
  var stock40=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Blueprinter/_donotship/GameObject"}).Select(AssetDatabase.GUIDToAssetPath).SelectMany(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Gun>(true)).First(g=>g.info&&g.info.name.Contains("40mm"));
  var audioSource=new SerializedObject(stock40);var audioDest=new SerializedObject(gun);
  foreach(var n in new[]{"fireSounds","fireStart","fireSustained","fireEnd","volume","pitch","pitchVariation","modifyPitch","impactEffectGround","impactEffectArmor","impactEffectWater","selfDestructEffect","tracerColor","tracerSize","bulletSelfDestruct"})audioDest.CopyFromSerializedProperty(audioSource.FindProperty(n));
  audioDest.ApplyModifiedPropertiesWithoutUndo();
  AssetDatabase.CopyAsset("Assets/Blueprinter/_donotship/MonoBehaviour/CRAMTrailer1_PLACEHOLDER.asset",dir+"L40_AA_Definition.asset");
  var def=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(dir+"L40_AA_Definition.asset");
  Set(def,"jsonKey","Gunmaggedon_L40_AA");Set(def,"unitName","L40 AA Trailer");Set(def,"mass",2000f);Set(def,"spawnOffset",new Vector3(0,.92f,0));
  Set(def,"length",8.2f);Set(def,"width",6.9f);Set(def,"height",3.1f);
  Set(def,"description","Towable 40mm anti-aircraft gun with manual traverse and elevation cranks.");
  Set(def,"roleIdentity.antiAir",1f);Set(def,"roleIdentity.antiMissile",0f);
  Set(vehicle,"definition",def);Set(vehicle,"wreckage",null);Set(vehicle,"mobile",false);
  Set(vehicle,"springRate",100000f);Set(vehicle,"dampingRate",20000f);Set(vehicle,"suspensionTravel",.92f+2000f*9.81f/100000f);Set(vehicle,"wheelRadius",.8f);
  Set(root.GetComponent<UnitPart>(),"mass",2000f);root.GetComponent<Rigidbody>().mass=2000f;root.GetComponent<Rigidbody>().angularDrag=.5f;
  var body=root.AddComponent<BoxCollider>();body.center=new Vector3(0,.25f,0);body.size=new Vector3(1.8f,.7f,3.6f);
  var cap=yaw.gameObject.AddComponent<CapsuleCollider>();cap.center=new Vector3(0,1f,0);cap.radius=.85f;cap.height=1.7f;
  var wheels=new GameObject[4];int i=0;
  foreach(float x in new[]{-2.1f,2.1f})foreach(float z in new[]{-3.35f,3.35f}) {var w=Pivot("WheelContact"+(i+1),root.transform,root.transform.TransformPoint(new Vector3(x,-.1f,z)));wheels[i++]=w.gameObject;var c=w.gameObject.AddComponent<SphereCollider>();c.radius=.8f;}
  var ws=new SerializedObject(vehicle);var wp=ws.FindProperty("wheels");wp.arraySize=4;for(i=0;i<4;i++)wp.GetArrayElementAtIndex(i).objectReferenceValue=wheels[i];ws.ApplyModifiedPropertiesWithoutUndo();
  var hook=Pivot("TowHook",root.transform,root.transform.TransformPoint(new Vector3(0,-.1f,4.1f)));
  // Native identity hash belongs to the source prefab; Blueprinter assigns the new prefab its own identity.
  var ni=root.GetComponent<Mirage.NetworkIdentity>();var ns=new SerializedObject(ni);var hash=ns.FindProperty("_prefabHash");if(hash!=null){hash.intValue=0;ns.ApplyModifiedPropertiesWithoutUndo();}
  var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,dir+"L40_AA_Trailer.prefab",InteractionMode.AutomatedAction);
  Set(def,"unitPrefab",prefab);EditorUtility.SetDirty(def);AssetDatabase.SaveAssets();
  EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
  return new {prefab=AssetDatabase.GetAssetPath(prefab),definition=AssetDatabase.GetAssetPath(def),muzzle=muzzle.position};
 }
}
