using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class L40PresentationRegression
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Set(object o,string field,object value){o.GetType().GetField(field,F).SetValue(o,value);}
 public static object Run()
 {
  var a=Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.7.1/GunmageddonAudio.dll")));
  var runtime=a.GetType("GunmageddonAudio.L40Runtime");
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("L40 presentation staging");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  object log=null;
  try {
   var install=runtime.GetMethod("Install",S);log=Activator.CreateInstance(install.GetParameters()[0].ParameterType,new object[]{"L40 fixture"});install.Invoke(null,new object[]{log});
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab");
   var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var gun=root.GetComponentInChildren<Gun>(true);var turret=root.GetComponentInChildren<Turret>(true);
   runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});
   var crank=root.GetComponent(a.GetType("GunmageddonAudio.AACrankMotion"));Check(crank,"Native turret initialization installs crank animation");
   crank.GetType().GetMethod("Awake",F).Invoke(crank,null);
   var yaw=turret.transform;var pitch=gun.transform;var left=yaw.Find("PitchCrankPivot");var right=yaw.Find("YawCrankPivot");
   yaw.localRotation=Quaternion.Euler(0,7.5f,0);pitch.localRotation=Quaternion.Euler(-5,0,0);crank.GetType().GetMethod("LateUpdate",F).Invoke(crank,null);
   Check(Mathf.Abs(Mathf.DeltaAngle(right.localEulerAngles.x,45))<.01f,"Traverse handle visibly rotates with yaw");
   Check(Mathf.Abs(Mathf.DeltaAngle(left.localEulerAngles.x,60))<.01f,"Pitch handle rotates with elevation");
   Check(right.Find("yawcrank")&&left.Find("elevationcrank"),"Actual handle meshes follow animated pivots");
   var s=new SerializedObject(gun);var recoil=s.FindProperty("recoilTransform").objectReferenceValue as Transform;
   Check(recoil&&recoil.localPosition==Vector3.zero&&recoil.Find("barrel"),"Zero-rest recoil wrapper carries visible barrel");
   var barrel=recoil.Find("barrel");var rest=barrel.position;var gunRest=gun.transform.position;
   var muzzles=(Transform[])typeof(Gun).GetField("muzzles",F).GetValue(gun);var muzzleRest=muzzles[0].position;
   Set(gun,"sources",new AudioSource[0]);Set(gun,"ticksSinceTriggerPull",100);Set(gun,"queuedBullets",0f);Set(gun,"bulletsLoaded",4);Set(gun,"lastFired",Time.timeSinceLevelLoad);
   Set(gun,"recoilEnergy",1f);Set(gun,"recoilPosition",0f);
   typeof(Gun).GetMethod("FixedUpdate",F).Invoke(gun,null);
   Check(Vector3.Distance(barrel.position,rest)>.01f,"Native gun callback produces visible barrel recoil");
   Check(Vector3.Distance(muzzles[0].position,muzzleRest)>.01f,"Muzzle follows recoiling barrel");
   Check(Vector3.Distance(gun.transform.position,gunRest)<.0001f,"Recoil does not move the elevation trunnion");
   Set(gun,"recoilEnergy",0f);
   for(int i=0;i<120;i++)typeof(Gun).GetMethod("FixedUpdate",F).Invoke(gun,null);
   Check(Vector3.Distance(rest,barrel.position)<.0001f,"Barrel returns to authored position without offset drift");
   var sounds=s.FindProperty("fireSounds");Check(sounds.arraySize==1&&sounds.GetArrayElementAtIndex(0).objectReferenceValue.name=="40mm_Autocannon_Fire_Close","User's 40mm sound assigned");
   Check(gun.GetComponents<AudioSource>().Length==1,"One source required: native ShotSound ignores fireSounds with two sources");
   Check(!s.FindProperty("fireStart").objectReferenceValue&&!s.FindProperty("fireSustained").objectReferenceValue&&!s.FindProperty("fireEnd").objectReferenceValue,"No placeholder rotary layers");
   return new{passed=true,checks="runtime crank attachment, visible yaw/pitch handles, native recoil/muzzle motion, no rest-offset drift, new 40mm sound"};
  }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);EditorSceneManager.ClosePreviewScene(scene);if(log is IDisposable)((IDisposable)log).Dispose();}
 }
}
