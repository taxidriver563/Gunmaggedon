using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AATrailerIntegration
{
 const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static int stows, releases;
 public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=true;return false;}
 public static bool Stow(bool __0){if(__0)stows++;else releases++;return false;}
 public static bool Surface(ref bool __result){__result=false;return false;}
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,Fields).SetValue(o,v);}
 static void HQ(Unit unit,FactionHQ hq){var f=typeof(Unit).GetField("HQ",Fields);var s=f.GetValue(unit);s.GetType().GetField("_component",Fields).SetValue(s,hq);f.SetValue(unit,s);}
 static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,Fields).Invoke(o,args);}
 public static object Run()
 {
  var assembly=Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.7.0/GunmageddonAudio.dll")));
  var module=assembly.GetType("GunmageddonAudio.AATrailer");var towType=assembly.GetType("GunmageddonAudio.AATowCoupling");
  var crankType=assembly.GetType("GunmageddonAudio.AACrankMotion");
  var harmonyType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HarmonyLib.Harmony")).First(t=>t!=null);
  var hmType=harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod");
  var harmony=Activator.CreateInstance(harmonyType,new object[]{"gunmaggedon.aa.fixture"});
  var patch=harmonyType.GetMethods().First(m=>m.Name=="Patch"&&m.GetParameters().Length==5);
  Action<MethodBase,string> prefix=(m,n)=>patch.Invoke(harmony,new object[]{m,Activator.CreateInstance(hmType,new object[]{typeof(AATrailerIntegration).GetMethod(n,Static)}),null,null,null});
  var stage=new GameObject("AA fixture staging");stage.SetActive(false);
  var physicsScene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
  bool oldAuto=Physics.autoSimulation;Physics.autoSimulation=false;
  var created=new List<GameObject>();
  try {
   prefix(typeof(Mirage.NetworkBehaviour).GetProperty("IsServer").GetGetMethod(),"Server");
   prefix(typeof(GroundVehicle).GetMethod("StowTurrets",Fields),"Stow");
   prefix(typeof(GroundVehicle).GetMethod("TryAttachToSurface",Fields),"Surface");
   module.GetMethod("Install",Static).Invoke(null,new object[]{harmony});
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab");
   Check(prefab&&prefab.GetComponent<GroundVehicle>()&&!prefab.GetComponent<Building>(),"Vehicle classification");
   var model=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var vehicle=model.GetComponent<GroundVehicle>();
   var turret=model.GetComponentInChildren<Turret>(true);var gun=model.GetComponentInChildren<Gun>(true);
   Check(gun.attachedUnit==vehicle&&new SerializedObject(turret).FindProperty("attachedUnit").objectReferenceValue==vehicle,"Owner refs");
   var gs=new SerializedObject(gun);var muzzle=gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue as Transform;
   Check(muzzle&&Vector3.Distance(muzzle.position,new Vector3(0,1.682f,3.72f))<.01f,"Muzzle alignment");
   var cranks=model.AddComponent(crankType);Call(cranks,"Awake");
   var yaw=turret.transform;var pitch=yaw.Find("PitchPivot");var left=yaw.Find("PitchCrankPivot");var right=yaw.Find("YawCrankPivot");
   yaw.localRotation=Quaternion.Euler(0,10,0);pitch.localRotation=Quaternion.Euler(-5,0,0);Call(cranks,"LateUpdate");
   Check(Mathf.Abs(Mathf.DeltaAngle(left.localEulerAngles.x,60))<.1f&&Mathf.Abs(Mathf.DeltaAngle(right.localEulerAngles.x,60))<.1f,"Crank gearing");
   var lr=left.localRotation;Call(cranks,"LateUpdate");Check(Quaternion.Angle(lr,left.localRotation)<.001f,"Crank stops with aim");
   var hq=stage.AddComponent<FactionHQ>();var enemy=new GameObject("Enemy");enemy.transform.SetParent(stage.transform);var enemyHQ=enemy.AddComponent<FactionHQ>();
   HQ(vehicle,hq);
   var tractorModel=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/HLT-T_PLACEHOLDER.prefab"),stage.transform,false);
   var truck=tractorModel.GetComponent<GroundVehicle>();HQ(truck,hq);
   // Real rigid bodies in an isolated physics scene; native units stay inactive to avoid starting a mission.
   var physical=new GameObject("AA physical trailer");created.Add(physical);SceneManager.MoveGameObjectToScene(physical,physicsScene);
   var rb=physical.AddComponent<Rigidbody>();rb.mass=2000;rb.useGravity=false;
   var hook=new GameObject("TowHook");hook.transform.SetParent(physical.transform,false);hook.transform.localPosition=new Vector3(0,0,4.1f);
   var tow=physical.AddComponent(towType);Set(tow,"Vehicle",vehicle);Set(tow,"body",rb);Set(tow,"hook",hook.transform);
   var tractorBody=new GameObject("Tractor physical body");created.Add(tractorBody);SceneManager.MoveGameObjectToScene(tractorBody,physicsScene);
   var trb=tractorBody.AddComponent<Rigidbody>();trb.isKinematic=true;
   typeof(Unit).GetField("<rb>k__BackingField",Fields).SetValue(vehicle,rb);
   typeof(Unit).GetField("<rb>k__BackingField",Fields).SetValue(truck,trb);
   truck.transform.position=new Vector3(0,0,8.645f); // length 10.1, hook z=4.1
   tractorBody.transform.position=truck.transform.position; Physics.SyncTransforms(); trb.position=truck.transform.position;
   Check((bool)Call(tow,"CanHitch",truck),"Nearby tractor accepted");
   HQ(truck,enemyHQ);Check(!(bool)Call(tow,"CanHitch",truck),"Enemy rejected");HQ(truck,hq);
   truck.transform.position+=Vector3.forward*20;Check(!(bool)Call(tow,"CanHitch",truck),"Distant rejected");truck.transform.position-=Vector3.forward*20;
   trb.isKinematic=false;trb.velocity=Vector3.forward*5;Check(!(bool)Call(tow,"CanHitch",truck),"Moving rejected");trb.velocity=Vector3.zero;trb.isKinematic=true;
   Check((bool)Call(tow,"Hitch",truck),"Hitch succeeded");Check(stows==1,"Gun stowed");
   var joint=physical.GetComponent<ConfigurableJoint>();Check(joint&&joint.connectedBody==trb,"Joint connected");
   var sentinel=model.AddComponent(towType);Call(sentinel,"Awake");Set(sentinel,"Tractor",truck);Set(sentinel,"joint",joint);
   Check(!(bool)module.GetMethod("NativeMotion",Static).Invoke(null,new object[]{vehicle}),"Native movement suspended while coupled");
   Check(!(bool)module.GetMethod("CanFire",Static).Invoke(null,new object[]{gun}),"Firing blocked while coupled");
   Set(sentinel,"Tractor",null);Set(sentinel,"joint",null);
   Check((bool)module.GetMethod("CanFire",Static).Invoke(null,new object[]{gun}),"Firing resumes after release");
   UnityEngine.Object.DestroyImmediate(sentinel);
   rb.useGravity=false;
   Physics.SyncTransforms(); var start=rb.position;
   for(int i=0;i<100;i++){trb.MovePosition(trb.position+Vector3.forward*.02f);physicsScene.GetPhysicsScene().Simulate(.02f);}
   float pullDistance=rb.position.z-start.z; Check(rb.position.z>start.z+1f,"Joint physically tows trailer: start="+start+" end="+rb.position+" tractor="+trb.position+" anchor="+joint.anchor+" connected="+joint.connectedAnchor);
   Check(!(bool)Call(tow,"CanHitch",truck),"Attached trailer cannot double hitch");
   trb.isKinematic=false;trb.velocity=Vector3.forward*3;Check(!(bool)Call(tow,"Unhitch",false),"Moving unhitch rejected");
   trb.velocity=Vector3.zero;trb.isKinematic=true;
   Check((bool)Call(tow,"Unhitch",false),"Unhitch succeeded");Check(releases==1&&!rb.useGravity,"Detach restores state");
   physical.transform.position=Vector3.zero;rb.position=Vector3.zero;truck.transform.position=new Vector3(0,0,8.645f);
   tractorBody.transform.position=truck.transform.position;Physics.SyncTransforms();
   Check((bool)Call(tow,"Hitch",truck),"Can hitch again");
   typeof(Unit).GetField("disabled",Fields).SetValue(truck,true);Call(tow,"FixedUpdate");
   Check(!(bool)towType.GetProperty("Attached",Fields).GetValue(tow,null)&&releases==2,"Destroyed tractor releases trailer");
   return new {passed=true,cranks="pitch/yaw gearing and stop",coupling="friendly, range, stopped, double hitch, physical pull, release",travel=pullDistance};
  }finally{
   foreach(var go in created)if(go)UnityEngine.Object.DestroyImmediate(go);
   UnityEngine.Object.DestroyImmediate(stage);
   module.GetField("Trailers",Static).GetValue(null).GetType().GetMethod("Clear").Invoke(module.GetField("Trailers",Static).GetValue(null),null);
   harmonyType.GetMethod("UnpatchSelf").Invoke(harmony,null);
   UnityEditor.SceneManagement.EditorSceneManager.CloseScene(physicsScene,true); Physics.autoSimulation=oldAuto;
  }
 }
}
