using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class L25TrailerIntegration
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static int checks;public static bool NotServer(ref bool __result){__result=false;return false;}
 static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static object Get(object o,string n){return AccessTools.Field(o.GetType(),n).GetValue(o);}
 static void Set(object o,string n,object value){AccessTools.Field(o.GetType(),n).SetValue(o,value);}
 static object Call(object o,string n,params object[] values){return AccessTools.Method(o.GetType(),n).Invoke(o,values);}
 public static object Run()
 {
  checks=0;var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);var runtime=a.GetType("GunmageddonAudio.L25Runtime");var motionType=a.GetType("GunmageddonAudio.L25TurretMotion");
  var h=new Harmony("gunmaggedon.l25.trailer.fixture");h.Patch(typeof(Unit).GetProperty("IsServer").GetGetMethod(),new HarmonyMethod(typeof(L25TrailerIntegration).GetMethod("NotServer")));
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("L25 trailer fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  try{
   runtime.GetMethod("Install",S).Invoke(null,new object[]{new ManualLogSource("L25 operational fixture")});
   const string dir="Assets/Blueprinter/Mods/Gunmageddon/";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"L25_AA_Trailer.prefab");var def=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(dir+"L25_AA_Definition.asset");Check(prefab&&def&&def.unitPrefab==prefab&&def.jsonKey=="Gunmaggedon_L25_AA"&&!new SerializedObject(def).FindProperty("disabled").boolValue,"Spawnable unique unit definition links to L25 trailer");
   var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var unit=root.GetComponent<GroundVehicle>();var turret=root.GetComponentInChildren<Turret>(true);var guns=new[]{"Left","Middle","Right"}.Select(n=>turret.transform.Find("PitchPivot/Feed"+n).GetComponent<Gun>()).ToArray();
   Check(unit.definition==def&&root.GetComponent<Mirage.NetworkIdentity>(),"Vehicle and network identity belong to L25");Check(guns.All(g=>g.attachedUnit==unit&&g.enabled&&!g.Safety)&&turret.enabled,"Operational gun/turret components enabled with correct ownership");
   Check(guns.All(g=>(int)Get(g,"ammo")==300&&(int)Get(g,"magazineCapacity")==15&&(int)Get(g,"magazines")==19&&(bool)Get(g,"startLoaded")),"Three 300-round feeds configured with 15-round initial clips");
   Check(guns.All(g=>g.info.targetRequirements.maxRange==3500&&!g.info.useWeaponDoors&&(bool)Get(g,"ForceServerAuthority")),"3.5km combat settings and native damage authority retained");
   Check((Transform)Get(turret,"elevationTransform")==turret.transform.Find("PitchPivot"),"Common pitch pivot controls all feeds");
   var detector=root.GetComponentInChildren<TargetDetector>(true);Check(detector&&(Unit)Get(detector,"attachedUnit")==unit&&(float)Get(detector,"visualRange")==3500&&((Transform)Get(detector,"scanner")).IsChildOf(turret.transform),"Native 3.5km optics wired inside L25");
   Check(Mathf.Abs(((float)Get(unit,"suspensionTravel")-def.spawnOffset.y)*(float)Get(unit,"springRate")-def.mass*9.81f)<.1f,"Suspension preload balances trailer weight at spawn height");
   Check(Mathf.Abs(root.GetComponent<Rigidbody>().mass-def.mass)<.01f&&!((bool)Get(unit,"mobile")),"Deployable trailer mass and stationary behavior configured");
   Check(root.GetComponentsInChildren<SphereCollider>(true).Length==4&&root.GetComponentsInChildren<Collider>(true).All(c=>!c.isTrigger),"Four wheel contacts and physical hitboxes present");
   Check(root.GetComponent<Animation>()==null,"Editor demonstration cannot drive the operational trailer");
   Check(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Operational prefab has no missing scripts");
   foreach(var c in root.GetComponentsInChildren<Component>(true))if(c){var so=new SerializedObject(c);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue){var path=AssetDatabase.GetAssetPath(p.objectReferenceValue);Check(!path.EndsWith("L40_AA_Trailer.prefab")&&!path.EndsWith("L40_AA_Definition.asset")&&!path.EndsWith("L40_AA_Weapon.asset"),"No borrowed L40 owner/definition/weapon reference: "+c.GetType().Name+"/"+p.propertyPath);}}
   Set(unit,"skill",1f);Set(unit,"weaponStations",new List<WeaponStation>());
   foreach(var gun in guns){Call(gun,"Awake");Check((int)Call(gun,"GetAmmoLoaded")==15&&(int)Call(gun,"GetAmmoTotal")==300&&(int)Call(gun,"GetFullAmmo")==300,"Native Awake stocks each clip and reserve correctly");}
   Call(turret,"Turret_OnInitialize");var stations=turret.GetWeaponStations();Check(stations.Length==3&&((List<WeaponStation>)Get(unit,"weaponStations")).Count==3,"Native turret initialization registers all three separate stations");
   Check(stations.Select(s=>s.Number).Distinct().Count()==3&&stations.All(s=>s.GetTurret()==turret),"Native station IDs are distinct and correctly assigned");
   var motion=turret.GetComponent(motionType);Check(motion,"Native init attaches L25 fire/recoil controller");Call(motion,"Awake");
   foreach(var gun in guns)Set(gun,"lastFired",Time.timeSinceLevelLoad-1);
   for(int i=0;i<3;i++){Set(motion,"next",i);Call(motion,"SelectStation",turret);Check(ReferenceEquals(Get(turret,"currentWeaponStation"),stations[i]),"Native current station follows staggered barrel "+i);}
   Set(motion,"next",0);Set(guns[0],"bulletsLoaded",0);Set(guns[0],"timeUntilReload",2f);Call(motion,"SelectStation",turret);Check(ReferenceEquals(Get(turret,"currentWeaponStation"),stations[1]),"Native station selection skips reloading left gun");
   Set(guns[1],"bulletsLoaded",0);Set(guns[1],"timeUntilReload",2f);Call(motion,"SelectStation",turret);Check(ReferenceEquals(Get(turret,"currentWeaponStation"),stations[2]),"Native station selection keeps right gun available during two reloads");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Turret),"FixedUpdate")).Prefixes.Any(p=>p.owner=="gunmaggedon.l25.presentation"),"Station selector is installed on the actual native turret tick");
   return new{passed=true,checks,stations=3,rounds=900,unitName=def.unitName,details="Definition, native initialization, ammo stocks, independent station IDs, selector cycling, ownership and suspension"};
  }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);}
 }
}
