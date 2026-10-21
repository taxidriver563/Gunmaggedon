using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class L40FiringAudioRegression
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static AudioClip played;static int calls;
 public static bool Capture(AudioClip clip){played=clip;calls++;return false;}
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static object Run()
 {
  var ht=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HarmonyLib.Harmony")).First(t=>t!=null);
  var h=Activator.CreateInstance(ht,new object[]{"gunmaggedon.l40.audio.regression"});
  var hm=Activator.CreateInstance(ht.Assembly.GetType("HarmonyLib.HarmonyMethod"),new object[]{typeof(L40FiringAudioRegression).GetMethod("Capture")});
  ht.GetMethods().Single(m=>m.Name=="Patch"&&m.GetParameters().Length==5).Invoke(h,new object[]{typeof(AudioSource).GetMethod("PlayOneShot",new[]{typeof(AudioClip)}),hm,null,null,null});
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("L40 audio fixture staging");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  try {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab");
   var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var gun=root.GetComponentInChildren<Gun>(true);
   var sources=gun.GetComponents<AudioSource>();Check(sources.Length==1,"Native one-shot routing requires exactly one emitter");
   Check(!sources[0].loop&&!sources[0].mute&&sources[0].enabled,"Emitter is enabled, unmuted and not looping");
   typeof(Gun).GetField("sources",F).SetValue(gun,sources);typeof(Unit).GetField("displayDetail",F).SetValue(gun.attachedUnit,1f);
   var shot=typeof(Gun).GetMethod("ShotSound",F);
   calls=0;played=null;shot.Invoke(gun,null);
   Check(calls==1&&played&&played.name=="40mm_Autocannon_Fire_Close","Native firing callback reaches PlayOneShot with the new user clip");
   for(int i=0;i<3;i++)shot.Invoke(gun,null);
   Check(calls==4,"Each round in a four-round clip requests its own sound");
   float[] samples=new float[played.samples*played.channels];bool decoded=played.GetData(samples,0);
   Check(decoded&&samples.Any(x=>Mathf.Abs(x)>.001f),"Imported clip decodes non-silent audio");
   return new{passed=true,emitterCount=sources.Length,nativeShotRequests=calls,clip=played.name,seconds=played.length,nonSilent=decoded};
  }finally{ht.GetMethod("UnpatchSelf").Invoke(h,null);EditorSceneManager.ClosePreviewScene(scene);played=null;}
 }
}
