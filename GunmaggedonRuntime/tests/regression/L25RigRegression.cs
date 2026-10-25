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
using NuclearOption.Networking;
public static class L25RigRegression
{
 const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.FlattenHierarchy;
 static int checks,bullets,sounds,flashes,audioCalls;static AudioClip playedClip;static readonly System.Collections.Generic.List<bool> Tracers=new System.Collections.Generic.List<bool>();static Transform launched;static bool fail,skip,local;static NetworkManagerNuclearOption manager;
 public static bool Local(ref bool __result){__result=local;return false;}
 public static bool Manager(ref NetworkManagerNuclearOption __result){__result=manager;return false;}
 public static bool Sight(ref bool __result){__result=true;return false;}
 public static bool Noop(){return false;}
 public static bool Sound(){sounds++;return false;}
 public static bool Flash(){flashes++;return false;}
 public static bool Audio(AudioClip clip){audioCalls++;playedClip=clip;return false;}
 public static bool Add(Transform __0,bool __4){if(fail)throw new InvalidOperationException("fixture interrupted spawn");bullets++;launched=__0;Tracers.Add(__4);return false;}
 public static bool Skip(){return !skip;}
 static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static object Call(object o,string name,params object[] a){return o.GetType().GetMethod(name,F).Invoke(o,a);}
 static void Set(object o,string name,object value){AccessTools.Field(o.GetType(),name).SetValue(o,value);}
 static object Get(object o,string name){return AccessTools.Field(o.GetType(),name).GetValue(o);}
 static void CheckFeeds(object motion,Gun[] guns,Turret turret,Unit unit)
 {
  var stations=turret.GetWeaponStations();Check(stations.Count()==3&&stations.All(s=>s.Weapons.Count()==1),"One native station and status per barrel");
  for(int i=0;i<3;i++){guns[i].Safety=false;Set(guns[i],"sources",guns[i].GetComponents<AudioSource>());Set(guns[i],"ammo",30);Set(guns[i],"magazines",1);Set(guns[i],"bulletsLoaded",15);Set(guns[i],"timeUntilReload",0f);Set(guns[i],"lastFired",Time.timeSinceLevelLoad-1);Set(guns[i],"fireInterval",.3f);Set(guns[i],"weaponStation",stations[i]);stations[i].Ammo=30;}
  Set(motion,"next",0);Set(motion,"nextShot",0f);Set(guns[0],"bulletsLoaded",1);
  int before=bullets;guns[0].Fire(unit,null,Vector3.zero,stations[0],default(GlobalPosition));Call(guns[0],"FixedUpdate");
  Check(bullets==before+1&&(int)Call(motion,"RoundsInClip",0)==0&&(float)Call(motion,"ReloadSeconds",0)>0,"Empty left clip starts its own native reload");
  Check((int)Call(motion,"RoundsInClip",1)==15&&(int)Call(motion,"RoundsInClip",2)==15,"Other clips retain their rounds while left reloads");
  Set(motion,"nextShot",0f);before=bullets;guns[1].Fire(unit,null,Vector3.zero,stations[1],default(GlobalPosition));Call(guns[1],"FixedUpdate");
  Check(bullets==before+1&&(int)Call(motion,"RoundsInClip",1)==14&&(float)Call(motion,"ReloadSeconds",0)>0,"Middle keeps firing while left reloads");
  Set(motion,"next",0);Set(motion,"nextShot",0f);Check(!(bool)Call(motion,"CanTrigger",guns[0])&&(bool)Call(motion,"CanTrigger",guns[2]),"Sequence skips empty/reloading gun and a barrel still on cooldown");
  Set(guns[0],"timeUntilReload",.001f);Set(guns[0],"ticksSinceTriggerPull",100);Call(guns[0],"FixedUpdate");
  Check((int)Call(motion,"RoundsInClip",0)==15&&(float)Call(motion,"ReloadSeconds",0)==0&&(int)Call(motion,"RoundsInClip",1)==14,"Native refill restores only the reloaded clip");
  Set(motion,"nextShot",0f);Set(motion,"next",0);Set(guns[0],"lastFired",Time.timeSinceLevelLoad-1);Set(guns[2],"queuedBullets",1f);Set(guns[2],"ticksSinceTriggerPull",0);
  before=bullets;guns[0].Fire(unit,null,Vector3.zero,stations[0],default(GlobalPosition));Call(guns[0],"FixedUpdate");Call(guns[2],"FixedUpdate");
  Check(bullets==before+1&&(int)Call(motion,"RoundsInClip",2)==15,"Same-tick stale trigger cannot double fire or consume the wrong clip");
  Set(guns[1],"ammo",0);Set(guns[1],"bulletsLoaded",0);Set(motion,"nextShot",0f);Set(motion,"next",1);Check(!(bool)Call(motion,"CanTrigger",guns[1]),"Exhausted feed cannot fire against a neighboring station's ammo");
 }
 static void CheckTracking(object motion,Gun[] guns,Unit unit)
 {
  var def=ScriptableObject.CreateInstance<VehicleDefinition>();def.jsonKey="Gunmaggedon_L25_AA";Set(unit,"definition",def);local=true;
  try{
   var go=new GameObject("Inactive L25 aircraft");go.SetActive(false);go.transform.SetParent(unit.transform.parent,false);var target=go.AddComponent<Aircraft>();
   var from=((Transform[])Get(guns[0],"muzzles"))[0];target.transform.position=from.position+Vector3.forward*800;
   Set(motion,"Target",target);Set(motion,"Visible",true);Set(motion,"LastSeen",Time.time);Set(motion,"NextSight",Time.time+1);
   var track=Get(motion,"Track");Call(track,"Reset",target.GetInstanceID(),(double)Time.time-1,42);var a=motion.GetType().Assembly;var v=a.GetType("GunmageddonAudio.OpticalVector");var relative=target.transform.position-from.position;
   Func<Vector3,object> vec=p=>Activator.CreateInstance(v,new object[]{(double)p.x,(double)p.y,(double)p.z});Call(track,"Sample",vec(relative),(double)Time.time-.4);Call(track,"Sample",vec(relative),(double)Time.time-.2);
   var solver=new AimSolver();Set(solver,"attachedUnit",unit);Set(solver,"currentTarget",target);Set(solver,"firingTransform",from);Set(solver,"weaponInfo",guns[0].info);var aim=a.GetType("GunmageddonAudio.L25Tracking").GetMethod("Aim",S);
   object[] args={solver,0f,Vector3.zero};Check(!(bool)aim.Invoke(null,args)&&Mathf.Abs((float)args[1]-800)<.01f&&Mathf.Abs(((Vector3)args[2]).magnitude-1)<.001f,"L25 optical adapter produces normalized sampled aircraft lead");
   Set(motion,"nextShot",0f);Set(motion,"next",0);Set(guns[0],"lastFired",Time.timeSinceLevelLoad-1);Check((bool)Call(motion,"CanTrigger",guns[0]),"Established close optical track allows a ready feed");
   Call(motion,"PrepareShot",guns[0]);float near=(float)Get(guns[0],"bulletSpread");target.transform.position=from.position+Vector3.forward*3499;Call(motion,"PrepareShot",guns[0]);Check((float)Get(guns[0],"bulletSpread")>near*3,"Actual native launch spread becomes substantially tighter nearby");
   args=new object[]{solver,0f,Vector3.zero};Check(!(bool)aim.Invoke(null,args)&&(float)args[1]>3498,"Aircraft just inside 3.5km remains in engagement envelope");
   target.transform.position=from.position+Vector3.forward*3501;args=new object[]{solver,0f,Vector3.zero};Check(!(bool)aim.Invoke(null,args)&&(float)args[1]==0&&!(bool)Call(motion,"CanTrigger",guns[0]),"Beyond 3.5km both aiming and fire authorization stop");
   target.transform.position=from.position+Vector3.forward*800;Set(motion,"Visible",true);Set(motion,"LastSeen",Time.time-1);Check(!(bool)Call(motion,"CanTrigger",guns[0]),"Stale sighting cannot authorize fire at a nearby aircraft");
  }finally{local=false;Set(unit,"definition",null);UnityEngine.Object.DestroyImmediate(def);}
 }
 public static object Run()
 {
  checks=bullets=sounds=flashes=audioCalls=0;playedClip=null;Tracers.Clear();fail=skip=local=false;
  var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
  var runtime=a.GetType("GunmageddonAudio.L25Runtime");var motionType=a.GetType("GunmageddonAudio.L25TurretMotion");
  var h=new Harmony("gunmaggedon.l25.fixture");Action<MethodInfo,string> prefix=(m,n)=>h.Patch(m,new HarmonyMethod(typeof(L25RigRegression).GetMethod(n)));
  var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("L25 rig fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
  try{
   prefix(typeof(Unit).GetProperty("LocalSim").GetGetMethod(),"Local");prefix(typeof(Unit).GetMethod("LineOfSight"),"Sight");prefix(AccessTools.PropertyGetter(typeof(NetworkManagerNuclearOption),"i"),"Manager");
   prefix(AccessTools.Method(typeof(Gun),"TrackFiringVisibility"),"Noop");prefix(AccessTools.Method(typeof(Gun),"ShotSound"),"Sound");prefix(AccessTools.Method(typeof(BulletSim),"AddBullet"),"Add");prefix(typeof(ParticleSystem).GetMethod("Play",Type.EmptyTypes),"Flash");
   var managerObject=new GameObject("Inactive network fixture");managerObject.SetActive(false);managerObject.transform.SetParent(stage.transform,false);manager=managerObject.AddComponent<NetworkManagerNuclearOption>();Set(manager,"Server",manager.gameObject.AddComponent<Mirage.NetworkServer>());
   runtime.GetMethod("Install",S).Invoke(null,new object[]{new ManualLogSource("L25 fixture")});
   const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"L25_Turret_Rig.prefab");Check(prefab,"Saved L25 rig exists");
   var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);var pitch=gun.transform.parent;var guns=new[]{"Left","Middle","Right"}.Select(n=>pitch.Find("Feed"+n).GetComponent<Gun>()).ToArray();var yaw=turret.transform;var authoredRatios=guns.Select(g=>(int)Get(g,"tracerRatio")).ToArray();Check(authoredRatios.Distinct().Count()==1,"Authored feed tracer intervals agree");
   Check(!root.GetComponent<Unit>()&&guns.All(g=>!g.enabled&&g.Safety)&&!turret.enabled,"Rig is mechanically staged without a spawnable/combat unit");
   Check(!gun.attachedUnit&&(int)Get(gun,"ammo")==0&&(int)Get(gun,"magazines")==0,"No borrowed owner or live ammunition");
   Check((Transform)Get(gun,"recoilTransform")==null&&(float)Get(gun,"recoilTravel")==0,"Shared native recoil cannot move all three barrels together");
   Check(gun.info.name=="L25_AA_Weapon"&&!gun.info.useWeaponDoors&&Mathf.Approximately((float)Get(gun,"fireRate"),200)&&Mathf.Approximately(gun.info.targetRequirements.maxRange,3500),"Staging 25mm weapon and aggregate cadence are explicit");
   Check(gun.GetComponents<AudioSource>().Length==1&&((AudioClip[])Get(gun,"fireSounds")).Length==1&&((AudioClip[])Get(gun,"fireSounds"))[0].name=="25mm_Autocannon_Fire"&&!((AudioClip)Get(gun,"fireSustained")),"Single one-shot source with custom 25mm firing sound");
   var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BlueprinterSetupBackups/L25/L25_Original.prefab");
   // Compare authored near meshes; generated distant meshes have separate LOD validation.
   foreach(var r in root.GetComponent<LODGroup>().GetLODs()[0].renderers){
    var source=original.transform.Find(r.name);Check(source,"Authored mesh retained: "+r.name);var before=original.transform.worldToLocalMatrix*source.localToWorldMatrix;var after=root.transform.worldToLocalMatrix*r.localToWorldMatrix;
    bool same=true;for(int i=0;i<16;i++)same&=Mathf.Abs(before[i]-after[i])<.0001f;Check(same,"Authored rest pose preserved: "+r.name);Check(r.sharedMaterial&&r.sharedMaterial.mainTexture,"Textured material retained: "+r.name);
   }
   Check(guns.Length==3&&guns.All(g=>(int)Get(g,"magazineCapacity")==15&&(float)Get(g,"reloadTime")==2),"Independent 15-round native clips and reload timers");
   var names=new[]{"Left","Middle","Right"};var barrels=names.Select(n=>pitch.Find("Barrel"+n)).ToArray();var muzzles=barrels.Select(b=>b.Find("Muzzle")).ToArray();
   foreach(var muzzle in muzzles){Check(Vector3.Dot(muzzle.forward,root.transform.forward)>.999f,"Muzzle points along bore");Check(muzzle.GetComponentsInChildren<ParticleSystem>(true).Length==2,"Each muzzle has flash and smoke");}
   Check(muzzles[0].position.x<muzzles[1].position.x&&muzzles[1].position.x<muzzles[2].position.x,"Physical left/middle/right order");
   var fixedBase=root.transform.Find("Model/base");var basePose=fixedBase.localToWorldMatrix;var originalTip=muzzles[0].position;yaw.localRotation=Quaternion.Euler(0,70,0);pitch.localRotation=Quaternion.Euler(-40,0,0);
   Check(Vector3.Distance(originalTip,muzzles[0].position)>1&&fixedBase.localToWorldMatrix==basePose,"Visible barrels aim while carriage stays fixed");
   runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});var motion=turret.GetComponent(motionType);Check(motion&&turret.GetComponents(motionType).Length==1,"One sequencing controller per turret");Call(motion,"Awake");
   var crankLeft=yaw.Find("PitchCrankPivot");var crankRight=yaw.Find("YawCrankPivot");var leftRest=crankLeft.localEulerAngles.x;var rightRest=crankRight.localEulerAngles.x;
   yaw.localRotation=Quaternion.Euler(0,77.5f,0);pitch.localRotation=Quaternion.Euler(-45,0,0);Call(motion,"LateUpdate");
   Check(Mathf.Abs(Mathf.DeltaAngle(leftRest,crankLeft.localEulerAngles.x)-60)<.01f&&Mathf.Abs(Mathf.DeltaAngle(rightRest,crankRight.localEulerAngles.x)-45)<.01f,"Both crank handles follow real aim changes");
   var owner=root;var unit=owner.AddComponent<Building>();foreach(var g in guns)g.attachedUnit=unit;
   var sim=new GameObject("Inactive bullet fixture");sim.SetActive(false);sim.transform.SetParent(stage.transform,false);var bulletSim=sim.AddComponent<BulletSim>();foreach(var g in guns)Set(g,"bulletSim",bulletSim);
   for(int i=0;i<9;i++){
    var firing=guns[i%3];int before=bullets;Call(firing,"SpawnBullet",0f);Check(bullets==before+1&&launched==muzzles[i%3],"Actual native SpawnBullet emits one round from ordered barrel "+i);
    var active=(ParticleSystem[])Get(firing,"muzzleParticles");Check(((Transform[])Get(firing,"muzzles")).Length==1&&active.All(p=>p.transform.parent==muzzles[i%3]),"One barrel's muzzle and particles selected per round");
    Check(barrels[i%3].localPosition.z<-.119f&&barrels.Where((b,j)=>j!=i%3).All(b=>b.localPosition==Vector3.zero),"Only fired barrel recoils");
    Call(motion,"StepRecoil",.2f);Check(barrels.All(b=>b.localPosition==Vector3.zero),"Independent recoil recovers without drift");
   }
   Check(sounds==9&&flashes==18,"Nine native rounds play nine shot events and exactly one flash/smoke pair each");
   int next=(int)Get(motion,"next");gun=guns[next];fail=true;try{Call(gun,"SpawnBullet",0f);throw new Exception("Expected spawn interruption");}catch(TargetInvocationException){}finally{fail=false;}
   Check((int)Get(motion,"next")==next,"Interrupted native spawn does not advance sequence");
   prefix(AccessTools.Method(typeof(Gun),"SpawnBullet"),"Skip");skip=true;Call(gun,"SpawnBullet",0f);skip=false;Check((int)Get(motion,"next")==next,"Suppressed native shot does not advance sequence");
   Call(gun,"SpawnBullet",0f);Check(launched==muzzles[next],"Sequence resumes from unfired barrel after interruption");
   Call(motion,"OnDisable");Check(barrels.All(b=>b.localPosition==Vector3.zero),"Disable restores all barrels");
   var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(dir+"L25_Mechanism_Preview.anim");clip.SampleAnimation(root,3.02f);Check(barrels[0].localPosition.z<-.119f&&barrels[1].localPosition==Vector3.zero&&barrels[2].localPosition==Vector3.zero,"Editor preview first kick is left barrel only");
   clip.SampleAnimation(root,3.12f);Check(barrels[1].localPosition.z<-.119f&&barrels[2].localPosition==Vector3.zero,"Editor preview second kick is middle barrel");clip.SampleAnimation(root,3.22f);Check(barrels[2].localPosition.z<-.119f,"Editor preview third kick is right barrel");
   clip.SampleAnimation(root,8f);Check(barrels.All(b=>b.localPosition==Vector3.zero)&&yaw.localRotation==Quaternion.identity&&pitch.localRotation==Quaternion.identity,"Editor preview ends at rest");
   Check(!root.GetComponent<Animation>().playAutomatically,"Preview does not override native aiming");Check(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts on saved rig");
   CheckFeeds(motion,guns,turret,unit);CheckTracking(motion,guns,unit);
   // Mix feed order to represent unavailable barrels. Native tracer flags must
   // follow actual mount shots, including interrupted/recovered shots above.
   for(int i=0;i<45;i++)Call(guns[i%7==0?2:i%3],"SpawnBullet",0f);
   for(int i=0;i<Tracers.Count;i++)Check(Tracers[i]==((i+1)%Mathf.Max(1,authoredRatios[0])==0),"Authored shared tracer interval honored on successful shot "+(i+1));
   Check(guns.Select(g=>(int)Get(g,"tracerRatio")).SequenceEqual(authoredRatios),"Runtime preserves authored tracer settings");foreach(int interval in new[]{1,2,3}){foreach(var feed in guns)Set(feed,"tracerRatio",interval);Set(motion,"tracerPhase",0);Tracers.Clear();for(int i=0;i<18;i++)Call(guns[i%5==0?2:i%3],"SpawnBullet",0f);for(int i=0;i<Tracers.Count;i++)Check(Tracers[i]==((i+1)%interval==0),"Mixed feeds honor interval "+interval+" shot "+(i+1));}for(int i=0;i<3;i++)Set(guns[i],"tracerRatio",authoredRatios[i]);
   h.Unpatch(AccessTools.Method(typeof(Gun),"ShotSound"),HarmonyPatchType.Prefix,h.Id);prefix(typeof(AudioSource).GetMethod("PlayOneShot",new[]{typeof(AudioClip)}),"Audio");Set(unit,"displayDetail",1f);
   var custom=AssetDatabase.LoadAssetAtPath<AudioClip>(dir+"25mm_Autocannon_Fire.mp3");custom.LoadAudioData();var samples=new float[custom.samples*custom.channels];Check(custom.GetData(samples,0)&&samples.Any(v=>Mathf.Abs(v)>.001f),"Custom 25mm clip decodes non-silent audio");
   foreach(var g in guns){int before=audioCalls;Call(g,"ShotSound");Check(audioCalls==before+1&&playedClip==custom,"Native shot sound plays the supplied 25mm clip on "+g.name);}

   return new{passed=true,checks,nativeRounds=bullets,sequence="Left -> Middle -> Right; skip reloading guns",spawnable=false};
  }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();manager=null;EditorSceneManager.ClosePreviewScene(scene);}
 }
}
