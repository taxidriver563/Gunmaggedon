using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class AAOpticalRangingIntegration
{
 const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static bool Yes(ref bool __result){__result=true;return false;}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Set(object value,string field,object data){value.GetType().GetField(field,F).SetValue(value,data);}
 public static object Run()
 {
  var a=Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmaggedonRuntime/build/1.7.1/GunmageddonAudio.dll")));
  var t=a.GetType("GunmageddonAudio.AAOpticalRanging");
  var ht=AppDomain.CurrentDomain.GetAssemblies().Select(x=>x.GetType("HarmonyLib.Harmony")).First(x=>x!=null);
  var h=Activator.CreateInstance(ht,new object[]{"gunmaggedon.aa.optical.fixture"});
  var patch=ht.GetMethods().Single(m=>m.Name=="Patch"&&m.GetParameters().Length==5);
  var hm=ht.Assembly.GetType("HarmonyLib.HarmonyMethod");
  Action<MethodInfo> yes=m=>patch.Invoke(h,new object[]{m,Activator.CreateInstance(hm,new object[]{typeof(AAOpticalRangingIntegration).GetMethod("Yes")}),null,null,null});
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Optical fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  var origin=typeof(Datum).GetField("<origin>k__BackingField",S);var originPos=typeof(Datum).GetField("<originPosition>k__BackingField",S);
  var oldOrigin=origin.GetValue(null);var oldPos=originPos.GetValue(null);
  try {
   origin.SetValue(null,stage.transform);originPos.SetValue(null,Vector3.zero);
   yes(typeof(Unit).GetProperty("LocalSim").GetGetMethod(true));yes(typeof(Unit).GetMethod("LineOfSight"));
   t.GetMethod("Install",S).Invoke(null,new object[]{h});
   var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab"),stage.transform,false);
   var owner=root.GetComponent<GroundVehicle>();var gun=root.GetComponentInChildren<Gun>(true);
   var from=(Transform)new SerializedObject(gun).FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue;
   var targetObject=new GameObject("Optical target");targetObject.transform.SetParent(stage.transform,false);targetObject.transform.position=new Vector3(2500,750,2200);
   var target=targetObject.AddComponent<Ship>();var rb=targetObject.AddComponent<Rigidbody>();rb.useGravity=false;
   typeof(Unit).GetField("<rb>k__BackingField",F).SetValue(target,rb);
   var solver=new AimSolver();Set(solver,"attachedUnit",owner);Set(solver,"currentTarget",target);Set(solver,"firingTransform",from);Set(solver,"weaponInfo",gun.info);
   var args=new object[]{solver,0f,Vector3.zero};Check(!(bool)t.GetMethod("Aim",S).Invoke(null,args),"L40 takes ownership of aim: local="+owner.LocalSim+" name="+gun.info.name+" match="+t.GetMethod("IsL40",S).Invoke(null,new object[]{gun.info,owner})+" parentGun="+(from.GetComponentInParent<Gun>()!=null));
   var direction=(Vector3)args[2];Check(Mathf.Abs(direction.magnitude-1)<.001f,"Normalized mechanical aim");
   Check(Vector3.Angle(direction,(target.transform.position-from.position).normalized)>.5f,"Deliberate initial ranging error");
   var states=(IDictionary)t.GetField("Solvers",S).GetValue(null);var state=states[solver];
   Check((bool)t.GetMethod("AllowFire",S).Invoke(null,new object[]{gun}),"Visible target permits live fire");
   Set(state,"Visible",false);Check(!(bool)t.GetMethod("AllowFire",S).Invoke(null,new object[]{gun}),"Blind gun cannot fire");Set(state,"Visible",true);
   var sim=gun.gameObject.AddComponent<BulletSim>();Set(sim,"gun",gun);Set(sim,"visualOnly",false);
   var add=new object[]{sim,null};t.GetMethod("Add",S).Invoke(null,add);
   try {
    var bullet=(BulletSim.Bullet)Activator.CreateInstance(typeof(BulletSim.Bullet),new object[]{new Vector3(1000,0,0),null,14f,false,false,null});
    bullet.active=true;bullet.position=(target.transform.position+new Vector3(-100,20,0)).ToGlobalPosition();
    t.GetMethod("TraceStart",S).Invoke(null,new object[]{bullet,owner,gun.info,false});
    t.GetMethod("TraceEnd",S).Invoke(null,new object[]{bullet});
    bullet.position=(target.transform.position+new Vector3(100,20,0)).ToGlobalPosition();
    var velocity=bullet.velocity;var position=bullet.position.ToLocalPosition();
    t.GetMethod("TraceEnd",S).Invoke(null,new object[]{bullet});
    var feedback=(IList)state.GetType().GetField("Feedback",F).GetValue(state);
    Check(feedback.Count==1,"Real swept shell pass produces clip feedback");
    var item=feedback[0];Check((float)item.GetType().GetField("Ready",F).GetValue(item)>Time.time+.8f,"Crew reaction delay");
    Check(bullet.active&&bullet.velocity==velocity&&Vector3.Distance(position,bullet.position.ToLocalPosition())<.001f,"Observation never steers or deletes projectile");
    var policy=state.GetType().GetField("Solution",F).GetValue(state);
    Check((int)policy.GetType().GetProperty("Corrections").GetValue(policy,null)==0,"No immediate correction from in-flight/just-passed shell");
   }finally{t.GetMethod("Added",S).Invoke(null,new object[]{null,add[1]});}
   var stock=AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/_donotship/MonoBehaviour/Gun40mm_Naval_PLACEHOLDER.asset");
   Set(solver,"weaponInfo",stock);args=new object[]{solver,123f,Vector3.left};
   Check((bool)t.GetMethod("Aim",S).Invoke(null,args)&&(float)args[1]==123f&&(Vector3)args[2]==Vector3.left,"Stock AA aiming untouched");
   return new {passed=true,checks="hook installation, L40-only aim, initial error, optical firing gate, swept shell feedback, reaction delay, unmodified live rounds, stock isolation"};
  }finally{
   t.GetMethod("Shutdown",S).Invoke(null,null);ht.GetMethod("UnpatchSelf").Invoke(h,null);
   origin.SetValue(null,oldOrigin);originPos.SetValue(null,oldPos);EditorSceneManager.ClosePreviewScene(scene);
  }
 }
}
