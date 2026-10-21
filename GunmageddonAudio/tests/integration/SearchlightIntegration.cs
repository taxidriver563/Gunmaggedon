using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SearchlightIntegration
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 static bool dark;static int reports;
 static readonly Dictionary<Unit,FactionHQ> Teams=new Dictionary<Unit,FactionHQ>();
 public static bool Local(ref bool __result){__result=true;return false;}
 public static bool Dark(ref bool __result){__result=dark;return false;}
 public static bool Team(Unit __instance,ref FactionHQ __result){return !Teams.TryGetValue(__instance,out __result);}
 public static bool Report(){reports++;return false;}
 public static bool Grid(){return false;}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
 static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
 static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
 public static object Run()
 {
  Assembly.LoadFrom("C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/BepInEx.dll");
  var a=Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.8.0/GunmageddonAudio.dll")));
  var runtime=a.GetType("GunmageddonAudio.SearchlightRuntime");var ct=a.GetType("GunmageddonAudio.SearchlightController");
  var lightPolicy=a.GetType("GunmageddonAudio.CoastalIllumination");
  var ht=Assembly.LoadFrom("C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/0Harmony.dll").GetType("HarmonyLib.Harmony");
  var h=Activator.CreateInstance(ht,new object[]{"gunmaggedon.searchlight.fixture"});var hm=ht.Assembly.GetType("HarmonyLib.HarmonyMethod");
  var patch=ht.GetMethods().Single(m=>m.Name=="Patch"&&m.GetParameters().Length==5);
  Action<MethodInfo,string> prefix=(m,n)=>patch.Invoke(h,new object[]{m,Activator.CreateInstance(hm,new object[]{typeof(SearchlightIntegration).GetMethod(n)}),null,null,null});
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Searchlight fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var old=datum.GetValue(null);
  GameObject blocker=null;object c=null;
  try {
   datum.SetValue(null,stage.transform);
   // Exercise the real environment policy before stubbing the update gate.
   var env=stage.AddComponent<LevelInfo>();var cloud=stage.AddComponent<CloudLayer>();cloud.transform.position=Vector3.up*1000;
   typeof(LevelInfo).GetField("cloudLayer",F).SetValue(env,cloud);
   typeof(LevelInfo).GetField("conditions",F).SetValue(env,0f);
   var hour=typeof(LevelInfo).GetField("timeOfDay",F);var low=lightPolicy.GetMethod("IsLowLight",S);
   hour.SetValue(env,0f);Check((bool)low.Invoke(null,new object[]{env,Vector3.zero,.15f}),"Midnight activation");
   hour.SetValue(env,12f);Check(!(bool)low.Invoke(null,new object[]{env,Vector3.zero,.15f}),"Noon shutdown");
   typeof(LevelInfo).GetField("conditions",F).SetValue(env,.7f);Check(!(bool)low.Invoke(null,new object[]{env,Vector3.zero,.15f}),"Same daytime overcast guard as flares");
   prefix(typeof(Unit).GetProperty("LocalSim").GetGetMethod(true),"Local");prefix(typeof(Unit).GetProperty("NetworkHQ").GetGetMethod(true),"Team");
   prefix(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc"),"Grid");
   prefix(lightPolicy.GetMethod("IsLowLight",S),"Dark");prefix(typeof(TargetDetector).GetMethod("DetectTarget"),"Report");
   var logType=Assembly.LoadFrom("C:/Program Files (x86)/Steam/steamapps/common/Nuclear Option/BepInEx/core/BepInEx.dll").GetType("BepInEx.Logging.ManualLogSource");
   runtime.GetMethod("Install",S).Invoke(null,new[]{Activator.CreateInstance(logType,new object[]{"Searchlight fixture"})});
   var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/Searchlight_Trailer.prefab"),stage.transform,false);
   var owner=root.GetComponent<GroundVehicle>();var detector=root.GetComponentInChildren<TargetDetector>(true);detector.detectedTargets=new List<Unit>();
   Check(owner.definition.jsonKey=="Gunmaggedon_Searchlight"&&root.GetComponentsInChildren<Gun>(true).Length==0,"Nonweapon vehicle definition");
   runtime.GetMethod("Initialize",S).Invoke(null,new object[]{detector});c=root.GetComponent(ct);Check(c!=null,"Runtime initializes from detector");Call(c,"Awake");
   var beam=(MeshRenderer)Get(c,"beam");var lens=(MeshRenderer)Get(c,"lens");var spot=(Light)Get(c,"spot");var origin=(Transform)Get(c,"origin");
   dark=false;Call(c,"Update");Check(!beam.enabled&&!lens.enabled&&!spot.enabled,"Day disables all emitters");
   Set(c,"nextCandidates",float.MaxValue);Set(c,"nextSupport",float.MaxValue);dark=true;Call(c,"Update");Check(beam.enabled&&lens.enabled&&spot.enabled,"Night enables emitters");
   var yaw=(Transform)Get(c,"yaw");float before=yaw.localEulerAngles.y;Call(c,"Aim",1f);Check(Mathf.Abs(Mathf.DeltaAngle(before,yaw.localEulerAngles.y))>1,"Visible sweeping");
   var teamObject=new GameObject("Friendly HQ");teamObject.transform.SetParent(stage.transform,false);var friendly=teamObject.AddComponent<FactionHQ>();
   var enemyObject=new GameObject("Enemy HQ");enemyObject.transform.SetParent(stage.transform,false);var hostile=enemyObject.AddComponent<FactionHQ>();Teams[owner]=friendly;
   var targetObject=new GameObject("Aircraft");targetObject.transform.SetParent(stage.transform,false);var target=targetObject.AddComponent<Aircraft>();Teams[target]=hostile;
   target.transform.position=origin.position+origin.forward*3000;
   Check((bool)Call(c,"Enemy",target),"Hostile aircraft eligible");Teams[target]=friendly;Check(!(bool)Call(c,"Enemy",target),"Friendly aircraft ignored");Teams[target]=hostile;
   Set(c,"attention",target);Set(c,"cue",target.transform.position);var solution=Get(c,"solution");
   var observe=solution.GetType().GetMethod("Observe",F);Check(!(bool)observe.Invoke(solution,new object[]{true,false,.1f}),"No instant optical report");
   Check((bool)Call(c,"Inside",target,10000f),"Aircraft inside beam");target.transform.position=origin.position+origin.right*3000;Check(!(bool)Call(c,"Inside",target,10000f),"Offbeam aircraft excluded");target.transform.position=origin.position+origin.forward*3000;
   var radarObject=new GameObject("Radar truck");radarObject.transform.SetParent(stage.transform,false);var truck=radarObject.AddComponent<GroundVehicle>();Teams[truck]=friendly;
   var rd=ScriptableObject.CreateInstance<VehicleDefinition>();rd.jsonKey="HLT-R";truck.definition=rd;
   var radar=radarObject.AddComponent<Radar>();typeof(TargetDetector).GetField("attachedUnit",F).SetValue(radar,truck);radar.activated=true;radar.detectedTargets=new List<Unit>();radar.detectedTargets.Add(target);
   var supports=(IList)Get(c,"support");supports.Add(radar);truck.transform.position=owner.transform.position+Vector3.right*300;
   Call(c,"RadarCue");Check((bool)Get(c,"radarCued"),"Friendly radar contact at 300m cues");
   truck.transform.position+=Vector3.right;Call(c,"RadarCue");Check(!(bool)Get(c,"radarCued"),"301m radar rejected");
   truck.transform.position=owner.transform.position+Vector3.right*100;Teams[truck]=hostile;Call(c,"RadarCue");Check(!(bool)Get(c,"radarCued"),"Enemy radar rejected");Teams[truck]=friendly;
   radar.detectedTargets.Clear();Call(c,"RadarCue");Check(!(bool)Get(c,"radarCued"),"Radar without contact grants no omniscient cue");
   radar.detectedTargets.Add(target);truck.disabled=true;Call(c,"RadarCue");Check(!(bool)Get(c,"radarCued"),"Destroyed radar rejected");truck.disabled=false;
   var ry=yaw.localRotation;Set(c,"attention",null);Call(c,"RadarCue");Check(yaw.localRotation==ry,"Radar cue never teleports lamp");
   for(int i=0;i<30;i++)observe.Invoke(solution,new object[]{true,false,.1f});
   reports=0;Set(c,"lastSample",Time.time-.1f);Set(c,"nextSample",0f);Set(c,"lastCueAt",Time.time);Call(c,"Update");Check(reports==1,"Sustained beam contact publishes native detection");
   blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=origin.position+origin.forward*1000;blocker.transform.rotation=origin.rotation;blocker.transform.localScale=new Vector3(100,100,10);Physics.SyncTransforms();
   float clipped=(float)Call(c,"Clip");Check(clipped<1000&&clipped>990,"Runtime terrain clipping");Check(!(bool)Call(c,"Clear",target),"Obstructed target rejected");UnityEngine.Object.DestroyImmediate(blocker);blocker=null;
   dark=false;Call(c,"Update");Check(!beam.enabled&&!spot.enabled&&Get(c,"attention")==null&&detector.detectedTargets.Count==0,"Dawn switches off and clears acquired state");
   UnityEngine.Object.DestroyImmediate(rd);
   return new{passed=true,checks="clock/daylight gate, definition, attachment hooks, sweeps, delayed observation, beam cone, faction filtering, 300m radar cues, no-contact/destroyed radar rejection, gradual aim, native detection handoff, occlusion and dawn cleanup"};
  }finally {
   if(blocker)UnityEngine.Object.DestroyImmediate(blocker);
   runtime.GetMethod("Shutdown",S).Invoke(null,null);ht.GetMethod("UnpatchSelf").Invoke(h,null);Teams.Clear();datum.SetValue(null,old);EditorSceneManager.ClosePreviewScene(scene);
  }
 }
}
