using System;
using System.IO;
using UnityEngine;
using UnityEditor;
public static class SetupF88Reload
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 static Transform Point(Transform parent,string name,Vector3 local){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
 static Transform Mesh(Transform parent,string name,Transform source){var wrapper=Point(parent,name,Vector3.zero);var go=UnityEngine.Object.Instantiate(source.gameObject,wrapper,false);go.name=source.name;var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"Materials/Gunmaggedon_F88_Shell_BaseColor.mat");if(!material)throw new Exception("F88 textured shell material missing");go.GetComponent<MeshRenderer>().sharedMaterial=material;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;var center=go.GetComponent<MeshFilter>().sharedMesh.bounds.center;go.transform.localPosition=-(go.transform.localRotation*Vector3.Scale(center,go.transform.localScale));return wrapper;}
 static void Curve(AnimationClip clip,string path,string property,params float[] values){var keys=new Keyframe[values.Length/2];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(values[2*i],values[2*i+1]);clip.SetCurve(path,typeof(Transform),property,new AnimationCurve(keys));}
 static void Visible(AnimationClip clip,string path,params float[] values){var keys=new Keyframe[values.Length/2];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(values[2*i],values[2*i+1],float.PositiveInfinity,float.PositiveInfinity);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(GameObject),"m_IsActive"),new AnimationCurve(keys));}
 public static object Run()
 {
  var backup=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"UnitySetupBackup/F88/BeforeReloadShells");Directory.CreateDirectory(backup);
  foreach(var name in new[]{"F88_Turret_Rig.prefab","F88_AA_Trailer.prefab","F88_Mechanism_Preview.anim","modinfo.json"})if(!File.Exists(Path.Combine(backup,name)))File.Copy(Dir+name,Path.Combine(backup,name));
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Gunmaggedon F88 AAA Shells.fbx");if(!model)throw new Exception("Shell model missing");
  var parking=new GameObject("Inactive casing setup");parking.SetActive(false);
  try{
   var shell=Mesh(parking.transform,"F88_SpentCasing",model.transform.Find("shell_casing"));
   var body=shell.gameObject.AddComponent<Rigidbody>();body.mass=5;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.Discrete;
   var box=shell.gameObject.AddComponent<BoxCollider>();box.size=new Vector3(.32f,.32f,.74f);
   PrefabUtility.SaveAsPrefabAsset(shell.gameObject,Dir+"F88_SpentCasing.prefab");
  }finally{UnityEngine.Object.DestroyImmediate(parking);}
  foreach(var name in new[]{"F88_Turret_Rig.prefab","F88_AA_Trailer.prefab"}){
   var path=Dir+name;var root=PrefabUtility.LoadPrefabContents(path);
   try{
    var pitch=root.transform.Find("F88YawPivot/PitchPivot");if(pitch.Find("ReloadVisuals"))throw new Exception("Reload visuals already configured: "+name);
    var visuals=Point(pitch,"ReloadVisuals",Vector3.zero);
    // Local Z points down the bore. Both rims seat just forward of the door.
    var casing=Mesh(visuals,"ExtractingCasing",model.transform.Find("shell_casing"));
    var live=Mesh(visuals,"LoadingShell",model.transform.Find("shell_loaded"));
    var chamberY=.1f;var casingSeat=new Vector3(0,chamberY,-.38f);var casingClear=new Vector3(0,chamberY,-2.85f);
    var loadStart=new Vector3(0,chamberY,-3.1f);var loadSeat=new Vector3(0,chamberY,-.11f);
    casing.localPosition=casingSeat;live.localPosition=loadStart;casing.gameObject.SetActive(false);live.gameObject.SetActive(false);
    Point(visuals,"CasingSeated",casingSeat);Point(visuals,"CasingClear",casingClear);Point(visuals,"LoadingStart",loadStart);Point(visuals,"LoadingSeated",loadSeat);
    var gun=pitch.GetComponent<Gun>();var so=new SerializedObject(gun);so.FindProperty("ejectionPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"F88_SpentCasing.prefab");
    // Runtime owns the handoff; never spawn a second casing via native delay.
    so.FindProperty("ejectionTransform").objectReferenceValue=null;so.FindProperty("reloadTime").floatValue=6.2f;so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root,path);
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir+"F88_Mechanism_Preview.anim");
  foreach(var b in AnimationUtility.GetCurveBindings(clip)){var c=AnimationUtility.GetEditorCurve(clip,b);var keys=c.keys;for(int i=0;i<keys.Length;i++)if(Mathf.Approximately(keys[i].time,8)){keys[i].time=10;}c.keys=keys;AnimationUtility.SetEditorCurve(clip,b,c);}
  const string pitchPath="F88YawPivot/PitchPivot/";var slide=pitchPath+"BreechSlidePivot";
  Curve(clip,slide,"localPosition.x",0,0,3.4f,0,3.8f,.36f,8.2f,.36f,8.8f,0,10,0);
  const string visualsPath=pitchPath+"ReloadVisuals/";
  foreach(var part in new[]{"ExtractingCasing","LoadingShell"}){
   var path=visualsPath+part;Curve(clip,path,"localPosition.x",0,0,10,0);Curve(clip,path,"localPosition.y",0,.1f,10,.1f);
   if(part=="ExtractingCasing"){Curve(clip,path,"localPosition.z",0,-.38f,3.8f,-.38f,4.6f,-2.85f,10,-2.85f);Visible(clip,path,0,0,3.8f,1,4.6f,0,10,0);}
   else{Curve(clip,path,"localPosition.z",0,-3.1f,6.6f,-3.1f,8.2f,-.11f,10,-.11f);Visible(clip,path,0,0,6.6f,1,8.2f,0,10,0);}
  }
  EditorUtility.SetDirty(clip);AssetDatabase.SaveAssets();return "Both F88 prefabs configured; custom physics casing, ordered reload visuals and 10-second preview saved.";
 }
}
