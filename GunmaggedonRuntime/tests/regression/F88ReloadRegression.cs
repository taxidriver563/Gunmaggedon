using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class F88ReloadRegression
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static int checks;static bool local=true;
 public static bool Local(ref bool __result){__result=local;return false;}
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,F).Invoke(obj,args);}
 static void Set(object obj,string name,object value){obj.GetType().GetField(name,F).SetValue(obj,value);}
 static object Get(object obj,string name){return obj.GetType().GetField(name,F).GetValue(obj);}
 public static object Run()
 {
  checks=0;local=true;var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));
  var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
  var runtime=a.GetType("GunmageddonAudio.F88Runtime");var motionType=a.GetType("GunmageddonAudio.F88TurretMotion");var props=a.GetType("GunmageddonAudio.F88Casings");
  var h=new Harmony("gunmaggedon.f88.reload.fixture");h.Patch(typeof(Unit).GetProperty("LocalSim").GetGetMethod(),new HarmonyMethod(typeof(F88ReloadRegression).GetMethod("Local")));
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("F88 reload fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var oldDatum=datum.GetValue(null);
  try{
   datum.SetValue(null,stage.transform);runtime.GetMethod("Install",S).Invoke(null,new object[]{new ManualLogSource("F88 reload fixture")});
   const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
   foreach(var name in new[]{"F88_Turret_Rig.prefab","F88_AA_Trailer.prefab"}){
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+name);var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var gun=root.GetComponentInChildren<Gun>(true);var turret=root.GetComponentInChildren<Turret>(true);var so=new SerializedObject(gun);
    Check(so.FindProperty("reloadTime").floatValue>=6.2f,"Native reload cannot finish ahead of the visual sequence");
    Check(!so.FindProperty("ejectionTransform").objectReferenceValue,"Native immediate/delayed ejection disabled to avoid duplicate prop");
    var source=so.FindProperty("ejectionPrefab").objectReferenceValue as GameObject;
    Check(source&&source.GetComponent<Rigidbody>()&&source.GetComponent<BoxCollider>()&&!source.GetComponent<Unit>(),"Custom casing is a physics prop without combat scripts");
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"Gunmaggedon F88 AAA Shells.fbx");
    Check(source.GetComponentInChildren<MeshFilter>().sharedMesh==model.transform.Find("shell_casing").GetComponent<MeshFilter>().sharedMesh,"Physical casing uses the supplied casing mesh");
    runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});var motion=turret.GetComponent(motionType);Call(motion,"Awake");
    Check((bool)motionType.GetProperty("Ready",F).GetValue(motion),"Preloaded spawn starts with closed breech");
    var cycle=Get(motion,"cycle");var pitch=gun.transform;var visuals=pitch.Find("ReloadVisuals");var empty=visuals.Find("ExtractingCasing");var live=visuals.Find("LoadingShell");var clear=visuals.Find("CasingClear");var seat=visuals.Find("LoadingSeated");
    Check(!empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"Reload meshes hidden while ready");
    Check(live.GetComponentInChildren<MeshFilter>(true).sharedMesh==model.transform.Find("shell_loaded").GetComponent<MeshFilter>().sharedMesh,"Loading visual uses the supplied complete round mesh");
    turret.transform.localRotation=Quaternion.Euler(0,67,0);pitch.localRotation=Quaternion.Euler(-50,0,0);
    Call(motion,"BeginReload");double now=Time.time;
    Action<double> tick=t=>{Call(cycle,"Tick",t);Call(motion,"ApplyPose",t);};
    tick(now+.6);tick(now+1);Check(empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"Casing extraction starts only after door opens");
    tick(now+1.79);Check(empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"No live round during extraction");
    tick(now+1.8);Check(!empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"Case clear ends extraction and begins empty-breech pause");
    Check(Vector3.Dot(clear.position-visuals.Find("CasingSeated").position,pitch.forward)<-2.4f,"Extraction travels aft along aimed bore");
    var roundBounds=empty.GetComponentInChildren<Renderer>(true).bounds;empty.localPosition=clear.localPosition;
    var nativeCollider=pitch.GetComponent<BoxCollider>();
    if(nativeCollider)Check(!nativeCollider.bounds.Intersects(empty.GetComponentInChildren<Renderer>(true).bounds),"Casing handoff is outside native gun collision hull");
    tick(now+3.799);Check(!live.gameObject.activeSelf,"Live shell stays hidden for full two seconds after clearance");
    tick(now+3.8);Check(live.gameObject.activeSelf,"Loading starts after clearance pause");
    tick(now+4.6);Check(live.gameObject.activeSelf&&Vector3.Distance(live.position,seat.position)>.5f,"Shell visibly moves into chamber");
    Set(gun,"timeUntilReload",0f);Set(gun,"bulletsLoaded",1);Call(motion,"BeforeGunUpdate");
    Check((int)Get(gun,"bulletsLoaded")==0&&(float)Get(gun,"timeUntilReload")>0,"Premature native loaded state is held empty during insertion");
    Set(gun,"sources",new AudioSource[0]);Set(gun,"bulletsLoaded",1);Set(gun,"queuedBullets",1f);Set(gun,"ticksSinceTriggerPull",0);Call(gun,"FixedUpdate");
    Check((int)Get(gun,"bulletsLoaded")==0&&(float)Get(gun,"queuedBullets")==0,"Native physics hook blocks a stale queued shot while loading");
    tick(now+5.4);Check(!live.gameObject.activeSelf&&!(bool)motionType.GetProperty("Ready",F).GetValue(motion),"Seated shell alone does not authorize fire while breech closes");
    tick(now+6);Check(!(bool)motionType.GetProperty("Ready",F).GetValue(motion),"Closed breech must complete secured dwell");
    tick(now+6.2);Check((bool)motionType.GetProperty("Ready",F).GetValue(motion),"Only seated shell plus secured breech becomes ready");
    if(gun.attachedUnit){
     var controller=turret.GetComponent(a.GetType("GunmageddonAudio.F88FlakController"));Call(controller,"Awake");var station=Call(controller,"ReadyStation");Set(gun,"weaponStation",station);
     Set(gun,"timeUntilReload",.001f);Set(gun,"bulletsLoaded",0);Set(gun,"ticksSinceTriggerPull",100);Set(gun,"queuedBullets",0f);Set(gun,"lastFired",Time.timeSinceLevelLoad);Call(gun,"FixedUpdate");
     Check((int)Get(gun,"bulletsLoaded")==1&&(float)Get(gun,"timeUntilReload")<=0,"Native ammunition becomes loaded after insertion, closure and secured dwell");
    }
    // Actual runtime handoff, once: drive the Extract boundary using its clock.
    Call(motion,"BeginReload");Call(cycle,"Tick",Time.time+.6);Call(cycle,"Tick",Time.time+1);Set(cycle,"started",(double)Time.time-1);
    int before=((System.Collections.ICollection)props.GetField("Pool",S).GetValue(null)).Count;
    Call(motion,"UpdateReload");var pool=(System.Collections.IList)props.GetField("Pool",S).GetValue(null);Check(pool.Count==before+1,"Extract completion creates exactly one independent casing");
    var prop=(Component)pool[pool.Count-1];Check(Vector3.Distance(prop.transform.position,clear.position)<.0001f&&Quaternion.Angle(prop.transform.rotation,clear.rotation)<.001f,"Physics prop precisely matches extraction endpoint under nonzero aim");
    Check(prop.GetComponent<Rigidbody>().velocity.magnitude<1,"Casing handoff uses gentle velocity");Call(motion,"UpdateReload");Check(pool.Count==before+1,"Repeated update cannot spawn duplicate casing");
    Check(!empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"Handoff hides animated casing without starting loader");
    if(gun.attachedUnit){local=false;Set(gun,"timeUntilReload",0f);Set(gun,"bulletsLoaded",1);Call(motion,"BeforeGunUpdate");Check((float)Get(gun,"timeUntilReload")==0&&(int)Get(gun,"bulletsLoaded")==1,"Remote visual cycle cannot block native remote shot events");local=true;}
    Call(motion,"OnDisable");Check(!empty.gameObject.activeSelf&&!live.gameObject.activeSelf,"Disabled gun hides transient reload models");
   }
   return new{passed=true,checks,details="Ordered extraction, exact casing handoff, two-second clearance, insertion, secured breech, native loaded-state gate and remote presentation"};
  }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();datum.SetValue(null,oldDatum);EditorSceneManager.ClosePreviewScene(scene);}
 }
}
