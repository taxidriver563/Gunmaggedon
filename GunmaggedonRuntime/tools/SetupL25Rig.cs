using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupL25Rig
{
 const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
 static void Set(UnityEngine.Object o,string name,object value){var s=new SerializedObject(o);var p=s.FindProperty(name);if(p==null)throw new Exception("Missing "+name);if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is string)p.stringValue=(string)value;else p.objectReferenceValue=value as UnityEngine.Object;s.ApplyModifiedPropertiesWithoutUndo();}
 static Transform Pivot(string name,Transform parent,Vector3 world){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=world;t.rotation=parent.rotation;return t;}
 static void Curve(AnimationClip clip,string path,string prop,params float[] v){var keys=new Keyframe[v.Length/2];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(v[2*i],v[2*i+1]);clip.SetCurve(path,typeof(Transform),prop,new AnimationCurve(keys));}
 public static object Run()
 {
  if(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L25_Turret_Rig.prefab"))throw new Exception("L25 rig already exists");
  var root=GameObject.Find("Gunmaggedon L25 AA");if(!root)throw new Exception("Scene model missing");
  string repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(repo))throw new Exception("Set GUNMAGGEDON_ROOT");
  string backup=Path.Combine(repo,"UnitySetupBackup/L25/BeforeRig");Directory.CreateDirectory(backup);
  Directory.CreateDirectory("Assets/BlueprinterSetupBackups/L25");AssetDatabase.Refresh();
  PrefabUtility.SaveAsPrefabAsset(root,"Assets/BlueprinterSetupBackups/L25/L25_Original.prefab");File.Copy("Assets/BlueprinterSetupBackups/L25/L25_Original.prefab",Path.Combine(backup,"L25_Original.prefab"),false);
  if(File.Exists(root.scene.path))File.Copy(root.scene.path,Path.Combine(backup,"scene-before-L25.unity"),false);
  var original=root.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r=>r.name,r=>r.localToWorldMatrix);
  if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  var parts=root.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==root.transform).ToDictionary(t=>t.name);
  var model=Pivot("Model",root.transform,root.transform.position);foreach(var part in parts.Values)part.SetParent(model,true);
  var yaw=Pivot("L25YawPivot",root.transform,parts["traverse"].position);parts["traverse"].SetParent(yaw,true);
  var pitch=Pivot("PitchPivot",yaw,parts["gun"].position);parts["gun"].SetParent(pitch,true);
  foreach(var e in new[]{new[]{"elevationcrank","PitchCrankPivot"},new[]{"yawcrank","YawCrankPivot"}}){var p=Pivot(e[1],yaw,parts[e[0]].position);parts[e[0]].SetParent(p,true);}
  var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/turret_25mm_flexpod_PLACEHOLDER.prefab").GetComponentInChildren<Gun>(true);
  var refTurret=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L40_AA_Trailer.prefab").GetComponentInChildren<Turret>(true);
  var turret=yaw.gameObject.AddComponent<Turret>();EditorUtility.CopySerialized(refTurret,turret);
  var gun=pitch.gameObject.AddComponent<Gun>();EditorUtility.CopySerialized(source,gun);
  Set(turret,"attachedUnit",null);Set(turret,"fireControl",null);Set(turret,"elevationTransform",pitch);Set(turret,"aimSafetyWeapon",gun);Set(turret,"traverseRate",30f);Set(turret,"elevationRate",25f);Set(turret,"minElevation",-5f);Set(turret,"maxElevation",85f);
  var ts=new SerializedObject(turret);foreach(var n in new[]{"weaponStations","criticalParts","targetDetectors"})ts.FindProperty(n).arraySize=0;ts.ApplyModifiedPropertiesWithoutUndo();
  var muzzles=new Transform[3];var effects=new ParticleSystem[3][];string[] labels={"Left","Middle","Right"},meshNames={"barrel_left","barrel_middle","barrel_right"};
  for(int i=0;i<3;i++){
   var barrel=parts[meshNames[i]];var end=barrel.GetComponent<MeshRenderer>().bounds.max.z;var muzzleWorld=new Vector3(barrel.position.x,barrel.position.y,end+.01f);
   var recoil=Pivot("Barrel"+labels[i],pitch,pitch.position);barrel.SetParent(recoil,true);var muzzle=Pivot("Muzzle",recoil,muzzleWorld);muzzles[i]=muzzle;
   var ps=source.GetComponentsInChildren<ParticleSystem>(true);effects[i]=new ParticleSystem[ps.Length];
   for(int j=0;j<ps.Length;j++){var p=UnityEngine.Object.Instantiate(ps[j].gameObject,muzzle,false);foreach(var nested in p.GetComponentsInChildren<ParticleSystem>(true).Where(x=>x.gameObject!=p).ToArray())UnityEngine.Object.DestroyImmediate(nested.gameObject);p.name=j==0?"L25MuzzleSmoke":"L25MuzzleFlash";p.transform.localPosition=Vector3.zero;p.transform.localRotation=Quaternion.identity;effects[i][j]=p.GetComponent<ParticleSystem>();var main=effects[i][j].main;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;}
  }
  if(!AssetDatabase.CopyAsset("Assets/Blueprinter/_donotship/MonoBehaviour/Gun25mm_Autocannon_PLACEHOLDER.asset",Dir+"L25_AA_Weapon.asset"))throw new Exception("Cannot create staging weapon");
  var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir+"L25_AA_Weapon.asset");Set(info,"weaponName","L25 Triple 25mm");Set(info,"shortName","25mm Triple");Set(info,"description","Triple 25mm light antiaircraft mount with sequential barrel firing.");Set(info,"useWeaponDoors",false);Set(info,"fireInterval",.1f);
  Set(gun,"attachedUnit",null);Set(gun,"info",info);Set(gun,"velocityInherit",null);Set(gun,"ammo",0);Set(gun,"magazines",0);Set(gun,"Safety",true);Set(gun,"fireRate",600f);Set(gun,"magazineCapacity",45);Set(gun,"startLoaded",false);Set(gun,"proximityTimer",false);Set(gun,"ForceServerAuthority",true);
  Set(gun,"recoilTransform",null);Set(gun,"recoilTravel",0f);Set(gun,"recoilImpulse",0f);Set(gun,"spinTransform",null);Set(gun,"spinRate",0f);Set(gun,"ejectionTransform",null);Set(gun,"ejectionPrefab",null);Set(gun,"fireStart",null);Set(gun,"fireSustained",null);Set(gun,"fireEnd",null);
  var audio=pitch.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=1;audio.dopplerLevel=0;audio.maxDistance=6000;
  var gs=new SerializedObject(gun);gs.FindProperty("muzzles").arraySize=1;gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzles[0];gs.FindProperty("muzzleParticles").arraySize=effects[0].Length;for(int j=0;j<effects[0].Length;j++)gs.FindProperty("muzzleParticles").GetArrayElementAtIndex(j).objectReferenceValue=effects[0][j];
  gs.FindProperty("fireSounds").arraySize=3;for(int j=0;j<3;j++)gs.FindProperty("fireSounds").GetArrayElementAtIndex(j).objectReferenceValue=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Blueprinter/_donotship/AudioClip/25mmCannonB"+(j+1)+"_PLACEHOLDER.ogg");gs.ApplyModifiedPropertiesWithoutUndo();
  gun.enabled=false;turret.enabled=false;
  foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)){var before=original[r.name];var after=r.localToWorldMatrix;for(int i=0;i<16;i++)if(Mathf.Abs(before[i]-after[i])>.0001f)throw new Exception("Original pose changed: "+r.name);}
  var clip=new AnimationClip{name="L25_Mechanism_Preview",legacy=true,frameRate=60};
  Curve(clip,"L25YawPivot","localEulerAnglesRaw.y",0,0,2,45,6,45,8,0);Curve(clip,"L25YawPivot/PitchPivot","localEulerAnglesRaw.x",0,0,2,-45,6,-45,8,0);
  Curve(clip,"L25YawPivot/PitchCrankPivot","localEulerAnglesRaw.x",0,0,2,540,6,540,8,0);Curve(clip,"L25YawPivot/YawCrankPivot","localEulerAnglesRaw.x",0,0,2,270,6,270,8,0);
  for(int i=0;i<3;i++){string path="L25YawPivot/PitchPivot/Barrel"+labels[i];var keys=new System.Collections.Generic.List<float>{0,0};for(int shot=0;shot<3;shot++){float t=3+i*.1f+shot*.3f;keys.AddRange(new[]{t,0,t+.02f,-.12f,t+.16f,0});}keys.AddRange(new[]{8f,0f});Curve(clip,path,"localPosition.z",keys.ToArray());Curve(clip,path,"localPosition.x",0,0,8,0);Curve(clip,path,"localPosition.y",0,0,8,0);}
  AssetDatabase.CreateAsset(clip,Dir+"L25_Mechanism_Preview.anim");var animation=root.AddComponent<Animation>();animation.clip=clip;animation.AddClip(clip,clip.name);animation.playAutomatically=false;
  PrefabUtility.SaveAsPrefabAssetAndConnect(root,Dir+"L25_Turret_Rig.prefab",InteractionMode.AutomatedAction);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
  return new{prefab=Dir+"L25_Turret_Rig.prefab",preview=Dir+"L25_Mechanism_Preview.anim",mechanicalParts=original.Count,muzzles=3,sequence="Left -> Middle -> Right",previewAggregateRpm=600,spawnable=false};
 }
}
