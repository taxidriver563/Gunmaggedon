using System;
using UnityEngine;
using UnityEditor;
public static class F120EnvelopePatch {
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
 foreach(var name in new[]{"F120_AA_Weapon.asset","F120S_AA_Weapon.asset"}){
 var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(dir+name);if(!info)throw new Exception(name);
 Undo.RecordObject(info,"F12020km envelope");var so=new SerializedObject(info);
 so.FindProperty("targetRequirements.maxRange").floatValue=20000;
 so.FindProperty("targetRequirements.maxAltitude").floatValue=20000;
 so.ApplyModifiedProperties();AssetDatabase.SaveAssetIfDirty(info);
 }
 return new{range=20000,altitude=20000,sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty};
 }
}