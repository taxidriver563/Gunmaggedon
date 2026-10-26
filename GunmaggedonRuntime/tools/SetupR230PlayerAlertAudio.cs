using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class SetupR230PlayerAlertAudio
{
 public static object Run(){
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
  var bankPath=dir+"R230_PlayerAlertAudio.prefab";
  if(AssetDatabase.LoadAssetAtPath<GameObject>(bankPath))throw new Exception("Audio bank already exists; inspect before replacing");
  var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
  var root=new GameObject("R230PlayerAlertAudio");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
  try{
   foreach(var name in new[]{"RG_Tracking","RG_Charging","RG_Firing"}){
    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(dir+name+".mp3");if(!clip)throw new Exception("Missing "+name);
    var child=new GameObject(name);child.transform.SetParent(root.transform,false);var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.spatialBlend=0;source.volume=1;source.priority=32;
   }
   var bank=PrefabUtility.SaveAsPrefabAsset(root,bankPath);
   foreach(var file in new[]{"R230_Emplacement.prefab","R230N_Emplacement.prefab"}){
    string path=dir+file;
    if(!AssetDatabase.LoadAssetAtPath<GameObject>(path))throw new Exception("Missing "+file);
    var prefab=PrefabUtility.LoadPrefabContents(path);
    try{
     if(prefab.transform.Find("R230PlayerAlertAudio"))throw new Exception("Existing bank; preserve it");
     var instance=(GameObject)PrefabUtility.InstantiatePrefab(bank,prefab.transform);instance.name="R230PlayerAlertAudio";instance.SetActive(false);
     PrefabUtility.SaveAsPrefabAsset(prefab,path);
    }finally{PrefabUtility.UnloadPrefabContents(prefab);}
   }
   return new{passed=true,clips=3,rawAudioKeptPrivate=true,sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty};
  }finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
 }
}