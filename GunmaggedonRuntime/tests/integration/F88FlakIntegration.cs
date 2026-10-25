using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class F88FlakIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks,damage,bursts;static bool local=true,dark=true;
    static readonly List<Unit> OpticalCandidates=new List<Unit>();
    public static bool Grid(GlobalPosition __0,float __1,List<Unit> __2){foreach(var u in OpticalCandidates)if(u&&Vector3.Distance(__0.ToLocalPosition(),u.transform.position)<=__1)__2.Add(u);return false;}
    static readonly Dictionary<Unit,FactionHQ> Teams=new Dictionary<Unit,FactionHQ>();
    public static bool Local(ref bool __result){__result=local;return false;}
    public static bool Team(Unit __instance,ref FactionHQ __result){return !Teams.TryGetValue(__instance,out __result);}
    public static bool Damage(){damage++;return false;}
    public static bool FX(){bursts++;return false;}
    public static bool Dark(ref bool __result){__result=dark;return false;}
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    static object Static(Type t,string n,params object[] a){return t.GetMethod(n,S).Invoke(null,a);}
    public static object Run(){return RunProfile(false);}
    public static object RunHeavy(){return RunProfile(true);}
    public static object RunColdStart(){return RunProfile(false,true);}
    public static object RunHeavyColdStart(){return RunProfile(true,true);}
    static object RunProfile(bool heavy,bool coldStart=false)
    {
        checks=damage=bursts=0;local=true;var repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??Path.Combine(repo,"GunmaggedonRuntime/build/1.9.2/Gunmaggedon.Runtime.dll")));
        var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var runtime=a.GetType("GunmageddonAudio.F88Runtime");var flak=a.GetType("GunmageddonAudio.F88FlakRuntime");var controller=a.GetType("GunmageddonAudio.F88FlakController");var effects=a.GetType("GunmageddonAudio.F88FlakEffects");var network=a.GetType("GunmageddonAudio.F88FlakNetwork");
        var h=new Harmony("gunmaggedon.f88.flak.fixture");Action<MethodInfo,string> prefix=(m,n)=>h.Patch(m,new HarmonyMethod(typeof(F88FlakIntegration).GetMethod(n)));
        var previousScene=SceneManager.GetActiveScene();var scene=coldStart?EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive):EditorSceneManager.NewPreviewScene();SceneManager.SetActiveScene(previousScene);var stage=new GameObject("F88 barrage fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var datumPos=typeof(Datum).GetField("<originPosition>k__BackingField",S);var old=datum.GetValue(null);var oldPos=datumPos.GetValue(null);
        try {
            datum.SetValue(null,stage.transform);datumPos.SetValue(null,Vector3.zero);Static(a.GetType("GunmageddonAudio.SharedWorldQueries"),"Clear");
            prefix(typeof(Unit).GetProperty("LocalSim").GetGetMethod(),"Local");prefix(typeof(Unit).GetProperty("NetworkHQ").GetGetMethod(),"Team");prefix(typeof(DamageEffects).GetMethod("BlastFrag"),"Damage");
            Static(runtime,"Install",new ManualLogSource("F88 flak fixture"));prefix(effects.GetMethod("Play",S),"FX");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/"+(heavy?"F120_AAA_Trailer.prefab":"F88_AA_Trailer.prefab"));Check(prefab,"Operational flak prefab exists");
            var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var owner=root.GetComponent<GroundVehicle>();var gun=root.GetComponentInChildren<Gun>(true);var turret=root.GetComponentInChildren<Turret>(true);var info=gun.info;
            Check(owner.definition.jsonKey==(heavy?"Gunmaggedon_F120_AA":"Gunmaggedon_F88_AA")&&info.name==(heavy?"F120_AA_Weapon":"F88_AA_Weapon"),"Unique unit and ammunition definitions bound");
            Check(gun.enabled&&turret.enabled&&!gun.Safety,"Combat prefab enabled without changing the standalone rig");
            Check(!new SerializedObject(gun).FindProperty("proximityTimer").boolValue&&!info.useWeaponDoors,"Timed fuze only and no aircraft bay-door path");
            Check(Mathf.Abs((float)new SerializedObject(gun).FindProperty("bulletSpread").floatValue-(heavy?.45f:.65f))<.0001f,"Permanent mechanical dispersion configured");
            Check(Mathf.Abs((new SerializedObject(owner).FindProperty("suspensionTravel").floatValue-owner.definition.spawnOffset.y)*(heavy?450000:200000)-owner.definition.mass*9.81f)<.1f,"Suspension preload matches trailer weight at spawn");
            Check(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Combat prefab has no missing scripts");
            var friendly=new GameObject("Friendly HQ").AddComponent<FactionHQ>();friendly.transform.SetParent(stage.transform,false);Teams[owner]=friendly;
            var hostile=new GameObject("Hostile HQ").AddComponent<FactionHQ>();hostile.transform.SetParent(stage.transform,false);
            Static(runtime,"Initialize",turret);var c=turret.GetComponent(controller);Check(c,"Native initialization attaches barrage controller");Call(c,"Awake");Call(turret.GetComponent(a.GetType("GunmageddonAudio.F88TurretMotion")),"Awake");
            Check(!(bool)controller.GetProperty("ReadyToFire",F).GetValue(c),"No firing before any observations");
            var aircraft=new GameObject("Observed aircraft").AddComponent<Aircraft>();aircraft.transform.SetParent(stage.transform,false);aircraft.transform.position=new Vector3(0,1600,4000);Teams[aircraft]=hostile;
            prefix(a.GetType("GunmageddonAudio.CoastalIllumination").GetMethod("IsLowLight",S),"Dark");
            Check(!(bool)Call(c,"OpticalContact",aircraft),"Dark unilluminated aircraft cannot supply optical observations");
            if(coldStart){
                stage.SetActive(true);dark=false;Set(aircraft,"visibility",6000f);
                prefix(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc",S),"Grid");OpticalCandidates.Add(aircraft);
                Check(friendly.trackingDatabase.Count==0,"No pre-existing precise faction contacts");
                Check((bool)Call(c,"OpticalContact",aircraft),"Actual native LOS confirms visible daytime aircraft");
                Call(c,"ChooseContact",Time.time);
                Check((Unit)Get(c,"target")==aircraft,"Visible daytime aircraft acquired without radar or faction tracking entry");
                Call(c,"Observe",Time.time-1.5f);aircraft.transform.position+=Vector3.right*200;Call(c,"Observe",Time.time);
                Check(Get(c,"track")!=null&&(bool)Get(c,"track").GetType().GetProperty("HasTrack").GetValue(Get(c,"track")),"Own optical observations build motion track without HQ knowledge");
                Call(c,"Prepare",Time.time);for(int i=0;i<3;i++)Call(c,"SolveAim",Time.time);
                Check((bool)Get(c,"planned")&&(float)controller.GetProperty("Fuse",F).GetValue(c)>.5f,"Cold acquisition reaches timed firing plan without injecting target or track");
                Set(c,"target",null);Set(c,"track",null);dark=true;Call(c,"ChooseContact",Time.time);Check(Get(c,"target")==null,"Dark unilluminated aircraft remains unacquired");dark=false;
                Teams[aircraft]=friendly;Call(c,"ChooseContact",Time.time);Check(Get(c,"target")==null,"Visible friendlies remain unacquired");Teams[aircraft]=hostile;
                aircraft.transform.position=new Vector3(0,100,4000);Call(c,"ChooseContact",Time.time);Check(Get(c,"target")==null,"Below-minimum-altitude aircraft excluded");
                aircraft.transform.position=new Vector3(0,1600,heavy?11000:9000);Call(c,"ChooseContact",Time.time);Check(Get(c,"target")==null,"No optical acquisition beyond own range without radar");
                aircraft.transform.position=new Vector3(0,1600,4000);var obstacle=new GameObject("Optical obstruction");obstacle.transform.SetParent(stage.transform,false);obstacle.layer=6;obstacle.transform.position=new Vector3(0,800,2000);obstacle.AddComponent<BoxCollider>().size=new Vector3(1000,2000,20);Physics.SyncTransforms();
                Check(!(bool)Call(c,"OpticalContact",aircraft),"Native static obstruction blocks optical evidence");Call(c,"ChooseContact",Time.time);Check(Get(c,"target")==null,"World candidate behind obstruction cannot establish detection");
                return new{passed=true,checks,profile=heavy?"F120 cold optical":"F88 cold optical",details="Empty real faction tracking database, supplied broad candidates only; actual native LOS, own observations and timed plan, darkness/faction/range/altitude/obstruction guards"};
            }

            var radarUnit=new GameObject("Radar truck").AddComponent<GroundVehicle>();radarUnit.transform.SetParent(stage.transform,false);Teams[radarUnit]=friendly;
            var radar=radarUnit.gameObject.AddComponent<Radar>();Set(radar,"attachedUnit",radarUnit);radar.activated=true;radar.detectedTargets=new List<Unit>{aircraft};
            radarUnit.transform.position=owner.transform.position+Vector3.right*300;
            Check((bool)Static(controller,"RadarSupports",owner,radar),"Friendly operational radar exactly 300m away supports knowledge");
            radarUnit.transform.position+=Vector3.right*.01f;Check(!(bool)Static(controller,"RadarSupports",owner,radar),"Radar beyond 300m does not support knowledge");
            radarUnit.transform.position=owner.transform.position+Vector3.right*200;Teams[radarUnit]=hostile;Check(!(bool)Static(controller,"RadarSupports",owner,radar),"Enemy radar does not cue F88");Teams[radarUnit]=friendly;
            radar.activated=false;Check(!(bool)Static(controller,"RadarSupports",owner,radar),"Inactive radar does not cue F88");radar.activated=true;
            var solutionType=a.GetType("GunmageddonAudio.FlakBarrageSolution");var vectorType=a.GetType("GunmageddonAudio.OpticalVector");var solution=Activator.CreateInstance(solutionType,new object[]{1,heavy});
            Func<float,float,object> vec=(x,z)=>Activator.CreateInstance(vectorType,new object[]{(double)x,1600d,(double)z});
            solutionType.GetMethod("Observe").Invoke(solution,new[]{vec(0,4000),(object)(double)(Time.time-1.5),4500d,false});
            solutionType.GetMethod("Observe").Invoke(solution,new[]{vec(300,4000),(object)(double)Time.time,4500d,false});
            Set(c,"track",solution);Set(c,"target",aircraft);Call(c,"Prepare",Time.time);Call(c,"SolveAim",Time.time);Call(c,"SolveAim",Time.time);Call(c,"SolveAim",Time.time);
            Check((bool)Get(c,"planned")&&(float)controller.GetProperty("Fuse",F).GetValue(c)>.5f,"Observed motion creates a reachable timed-area plan");
            stage.SetActive(true);Set(gun,"timeUntilReload",0f);Set(gun,"bulletsLoaded",1);Set(gun,"ticksSinceTriggerPull",100);
            var firingStation=Call(c,"ReadyStation") as WeaponStation;
            Check(firingStation!=null&&firingStation.Ammo>0&&firingStation.GetTurret()==turret,"Custom controller binds loaded native weapon station without native direct-target selection");
            gun.Fire(owner,null,Vector3.zero,firingStation,aircraft.transform.position.ToGlobalPosition());
            Check((int)Get(gun,"ticksSinceTriggerPull")==0,"Native gun accepts untargeted barrage fire command");
            var committed=Get(c,"area");Call(c,"ShotFired");Check(!(bool)Get(c,"planned")&&(int)Get(c,"shots")==1,"Actual shot advances barrage and requests a new shell point");
            if(heavy){
                solutionType.GetMethod("Observe").Invoke(solution,new[]{vec(450,4000),(object)(double)(Time.time+.5f),4500d,false});Call(c,"Prepare",Time.time+.5f);Check(!Get(c,"area").Equals(committed),"Postwar crew revises its next-shot region with fresh observations");
                Call(c,"SolveAim",Time.time+.5f);var beforePoint=(GlobalPosition)Get(c,"point");var fixedOffset=Get(c,"shotOffset");
                solutionType.GetMethod("Observe").Invoke(solution,new[]{vec(1300,4000),(object)(double)(Time.time+1f),4500d,false});
                Call(c,"SolveAim",Time.time+1f);var afterPoint=(GlobalPosition)Get(c,"point");
                Check(Math.Abs(afterPoint.x-beforePoint.x)>100,"F120 refreshes prediction from new observations while aiming the same shot");
                Check(Get(c,"shotOffset").Equals(fixedOffset),"Aim refresh preserves the fixed per-shot suppression offset");
            }
            else{Call(c,"Prepare",Time.time);Check(Get(c,"area").Equals(committed),"F88 barrage area remains committed between shots");}
            if(heavy){
                var missileObject=new GameObject("Observed Piledriver");missileObject.transform.SetParent(stage.transform,false);
                var missile=missileObject.AddComponent<Missile>();var definition=UnityEngine.Object.Instantiate(owner.definition);missile.definition=definition;definition.jsonKey="ballisticMissile1";Teams[missile]=hostile;
                try {
                    Check((bool)Static(controller,"TargetKind",missile,true)&&!(bool)Static(controller,"TargetKind",missile,false),"Only heavy flak accepts Piledriver missiles");
                    definition.jsonKey="ballisticMissile1_tacNuke";Check((bool)Static(controller,"TargetKind",missile,true),"Both Piledriver warheads eligible");
                    definition.jsonKey="cruiseMissile1";Check(!(bool)Static(controller,"TargetKind",missile,true),"Unrelated missiles remain excluded");definition.jsonKey="ballisticMissile1";
                    missile.transform.position=new Vector3(16000,18000,0);Check((bool)Call(c,"Eligible",missile),"Active hostile Piledriver passes ordinary eligibility");
                    Teams[missile]=friendly;Check(!(bool)Call(c,"Eligible",missile),"Friendly Piledriver remains excluded");Teams[missile]=hostile;
                    var ballistic=Activator.CreateInstance(solutionType,new object[]{77,true,6000d});
                    for(int i=0;i<=10;i++){double dt=(i-10)*.2;var observed=Activator.CreateInstance(vectorType,new object[]{16000-1500*dt,18000-1800*dt,0d});solutionType.GetMethod("Observe").Invoke(ballistic,new[]{observed,(object)(Time.time+dt),20000d,true});}
                    Set(c,"track",ballistic);Set(c,"target",missile);Set(c,"area",Activator.CreateInstance(a.GetType("GunmageddonAudio.FlakBarrageArea")));Call(c,"Prepare",Time.time);
                    for(int i=0;i<12;i++)Call(c,"SolveAim",Time.time);
                    Check((bool)Get(c,"planned")&&(float)controller.GetProperty("Fuse",F).GetValue(c)>.5f,"Observed fast descending Piledriver creates a reachable timed potshot");
                    Check((float)controller.GetProperty("MaximumRange",F).GetValue(c)==20000,"Controller engagement range is20km");
                } finally {Teams.Remove(missile);UnityEngine.Object.DestroyImmediate(missileObject);UnityEngine.Object.DestroyImmediate(definition);}
                Set(c,"target",aircraft);Set(c,"track",solution);
            }
            Set(c,"planned",true);local=false;Check(!(bool)controller.GetProperty("ReadyToFire",F).GetValue(c),"Remote clients cannot authorize firing");local=true;
            var sim=new GameObject("F88 bullet simulation").AddComponent<BulletSim>();sim.transform.SetParent(stage.transform,false);Set(sim,"gun",gun);Set(sim,"visualOnly",false);
            // Exercise native AddBullet prefix arguments, then real native trajectory
            // trace with its actual timed-expiry path and Harmony callbacks.
            var add=flak.GetMethod("Add",S);object[] args={sim,0f,24f,true,aircraft,null};add.Invoke(null,args);
            Check((float)args[1]==(heavy?.20f:.65f)&&Mathf.Approximately((float)args[2],(float)controller.GetProperty("Fuse",F).GetValue(c))&&!(bool)args[3]&&args[4]==null,"Launch fixes spread/fuze and removes proximity target/tracer");
            var b=new BulletSim.Bullet(Vector3.forward*820,null,2,true,false,null){active=true,position=new GlobalPosition(0,5000,0),velocity=Vector3.forward*820,destructTimer=.01f,explosive=true,proximityFuse=true,target=aircraft};
            b.TrajectoryTrace(null,info,owner,new ParticleEffectManager.PrefabEffect[4],1,false,.02f,false,default(GlobalPosition),Vector3.zero,1,true,b.position);
            Check(!b.active&&!b.proximityFuse&&b.target==null,"Native expiry retires shell with no target-seeking/proximity behavior");
            Check(damage==1&&bursts==1,"Timed expiry produces exactly one authoritative fragmentation event and puff");
            flak.GetMethod("Added",S).Invoke(null,new[]{null,args[5]});Static(flak,"TraceEnd",b);Check(damage==1,"Repeated retirement cannot duplicate damage");
            Set(sim,"visualOnly",true);args=new object[]{sim,0f,24f,true,aircraft,null};add.Invoke(null,args);
            var remote=new BulletSim.Bullet(Vector3.forward*820,null,2,true,false,null){active=true,position=new GlobalPosition(0,5000,0),velocity=Vector3.forward*820,destructTimer=.01f,explosive=true};
            remote.TrajectoryTrace(null,info,owner,new ParticleEffectManager.PrefabEffect[4],1,true,.02f,false,default(GlobalPosition),Vector3.zero,1,false,default(GlobalPosition));
            flak.GetMethod("Added",S).Invoke(null,new[]{null,args[5]});Check(damage==1&&bursts==1,"Remote visual shells cannot cause damage or duplicate authoritative puff");
            Set(sim,"visualOnly",false);args=new object[]{sim,0f,24f,true,aircraft,null};add.Invoke(null,args);
            var early=new BulletSim.Bullet(Vector3.forward*820,null,2,true,false,null){active=true,position=new GlobalPosition(0,5000,0),velocity=Vector3.forward*820,destructTimer=2,explosive=true,proximityFuse=true,target=aircraft};
            early.TrajectoryTrace(null,info,owner,new ParticleEffectManager.PrefabEffect[4],1,false,.02f,false,default(GlobalPosition),Vector3.zero,1,true,early.position);
            flak.GetMethod("Added",S).Invoke(null,new[]{null,args[5]});Check(early.active&&damage==1,"Aircraft proximity cannot prematurely trigger timed shell");early.Remove();
            // Real serializers, not a hand-built substitute.
            var burstType=a.GetType("GunmageddonAudio.FlakBurst");var message=Activator.CreateInstance(burstType);burstType.GetField("Position",F).SetValue(message,new GlobalPosition(100,2000,300));burstType.GetField("Time",F).SetValue(message,123d);
            burstType.GetField("Heavy",F).SetValue(message,heavy);
            var packer=typeof(Mirage.Serialization.MessagePacker);
            Func<Type,object,object> roundtrip=(type,value)=>{
                var bytes=(byte[])packer.GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1).MakeGenericMethod(type).Invoke(null,new[]{value});
                return packer.GetMethod("Unpack").MakeGenericMethod(type).Invoke(null,new object[]{bytes,null});
            };
            var copy=roundtrip(burstType,message);
            Check((double)burstType.GetField("Time",F).GetValue(copy)==123d&&((GlobalPosition)burstType.GetField("Position",F).GetValue(copy)).y==2000,"Native Mirage burst serializer round trip");
            Check((bool)burstType.GetField("Heavy",F).GetValue(copy)==heavy,"Native Mirage burst preserves F88/F120 presentation profile");
            var poseType=a.GetType("GunmageddonAudio.FlakPose");var pose=Activator.CreateInstance(poseType);poseType.GetField("Unit",F).SetValue(pose,123u);poseType.GetField("Yaw",F).SetValue(pose,325f);poseType.GetField("Pitch",F).SetValue(pose,280f);
            var poseCopy=roundtrip(poseType,pose);Check((uint)poseType.GetField("Unit",F).GetValue(poseCopy)==123u&&(float)poseType.GetField("Pitch",F).GetValue(poseCopy)==280f,"Native Mirage gun pose serializer round trip");
            var cueType=a.GetType("GunmageddonAudio.R230ChargeCue");var cue=Activator.CreateInstance(cueType);cueType.GetField("Unit",F).SetValue(cue,456u);cueType.GetField("Point",F).SetValue(cue,new GlobalPosition(100,50000,200));cueType.GetField("Progress",F).SetValue(cue,.75f);cueType.GetField("Active",F).SetValue(cue,true);var cueCopy=roundtrip(cueType,cue);
            Check((uint)cueType.GetField("Unit",F).GetValue(cueCopy)==456u&&(float)cueType.GetField("Progress",F).GetValue(cueCopy)==.75f&&(bool)cueType.GetField("Active",F).GetValue(cueCopy),"Charge cue round-trip preserves owner/progress/active state");
            Static(network,"ReceiveBurst",null,message);Check(bursts==1&&damage==1,"Unconnected/host receiver cannot duplicate network burst or damage");
            h.Unpatch(effects.GetMethod("Play",S),HarmonyPatchType.Prefix,h.Id);
            stage.SetActive(true);var source=new SerializedObject(gun).FindProperty("selfDestructEffect").objectReferenceValue as GameObject;
            Static(effects,"Play",source,new GlobalPosition(0,5000,0),0f);Static(effects,"Play",source,new GlobalPosition(100,5000,0),0f);
            var banks=(IDictionary)effects.GetField("Banks",S).GetValue(null);var bank=banks[source];
            var pool=(IList)bank.GetType().GetField("Pool",F).GetValue(bank);Check(pool.Count==2,"Simultaneous bursts have independent particle instances");
            var first=(Component)pool[0];var second=(Component)pool[1];var originalPosition=first.transform.position;
            foreach(var ps in first.GetComponentsInChildren<ParticleSystem>(true))ps.Simulate(11,false,true,false);
            Check(first.GetComponentsInChildren<ParticleSystem>().Single(ps=>ps.name=="persistentSmoke").particleCount>0,"First puff remains alive while next burst exists");
            Check(first.transform.position==originalPosition&&Vector3.Distance(first.transform.position,second.transform.position)>90,"Later airburst cannot move an earlier lingering puff");
            first.gameObject.SetActive(false);Static(effects,"Play",source,new GlobalPosition(200,5000,0),0f);
            Check(pool.Count==2&&second.gameObject.activeSelf&&Vector3.Distance(second.transform.position,new Vector3(100,5000,0))<.01f,"Pool reuse preserves other active puffs");
            Check(damage==1,"Lingering and recycled smoke never causes ongoing damage");
            return new{passed=true,checks,damageEvents=damage,profile=heavy?"F120":"F88",details="Native prefab, radar boundaries/faction, night gate, committed barrage, fixed launch errors, timed expiry and authority"};
        }finally{h.UnpatchSelf();Static(runtime,"Shutdown");
            var registry=(IDictionary)typeof(Mirage.Serialization.MessagePacker).GetProperty("MessageTypes").GetValue(null);
            foreach(var name in new[]{"FlakHello","FlakBurst","FlakPose","R230ChargeCue","R230NuclearBurst"})registry.Remove(Mirage.Serialization.MessagePacker.GetId(a.GetType("GunmageddonAudio."+name)));if(coldStart)EditorSceneManager.CloseScene(scene,true);else EditorSceneManager.ClosePreviewScene(scene);Static(a.GetType("GunmageddonAudio.SharedWorldQueries"),"Clear");Teams.Clear();OpticalCandidates.Clear();dark=true;datum.SetValue(null,old);datumPos.SetValue(null,oldPos);local=true;}
    }
}

public static class F88ColdOpticalIntegration {public static object Run(){return F88FlakIntegration.RunColdStart();}}
public static class F120ColdOpticalIntegration {public static object Run(){return F88FlakIntegration.RunHeavyColdStart();}}
