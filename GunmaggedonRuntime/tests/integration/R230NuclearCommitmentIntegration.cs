using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class R230NuclearCommitmentIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Dictionary<Unit,FactionHQ> teams=new Dictionary<Unit,FactionHQ>();
    static int checks,fireRequests;static bool legacy;
    public static object RunLegacy(){legacy=true;try{return Run();}finally{legacy=false;}}
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object value){o.GetType().GetField(n,F).SetValue(o,value);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    public static bool Team(Unit __instance,ref FactionHQ __result){return !teams.TryGetValue(__instance,out __result);}
    public static bool Own(ref bool __result){__result=true;return false;}
    public static bool Sight(ref bool __result){__result=true;return false;}
    public static bool Fire(){fireRequests++;return false;}
    static void Align(Component c,Turret turret,Gun gun)
    {
        var direction=(Vector3)Get(c,"direction");var local=turret.transform.parent.InverseTransformDirection(direction);
        turret.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,0);
        gun.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,0,0);
    }
    static string FireState(Component c,Gun gun,object capacitor)
    {
        var acquisition=Get(c,"acquisition");var station=Get(c,"station") as WeaponStation;
        return " requests="+fireRequests+", solution="+Get(c,"solution")+", aligned="+c.GetType().GetProperty("Aligned",F).GetValue(c)
            +", acquired="+acquisition.GetType().GetProperty("Ready").GetValue(acquisition)+", nativeReady="+c.GetType().GetProperty("NativeReady",F).GetValue(c)
            +", operational="+c.GetType().GetProperty("Operational",F).GetValue(c)+", stationAmmo="+(station==null?"null":station.Ammo.ToString())
            +", charging="+capacitor.GetType().GetProperty("Charging").GetValue(capacitor)+", chargeStarted="+capacitor.GetType().GetProperty("ChargeStarted").GetValue(capacitor)
            +", now="+Time.time+", committed="+Get(c,"committed")+", trackingStarted="+Get(c,"trackingStarted");
    }
    public static object Run()
    {
        checks=fireRequests=0;teams.Clear();var a=Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
        var controllerType=a.GetTypes().Single(t=>t.Name=="R230Controller");var runtime=a.GetTypes().Single(t=>t.Name=="R230Runtime");
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Nuclear commitment fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var origin=typeof(Datum).GetField("<origin>k__BackingField",S);var old=origin.GetValue(null);var pos=typeof(Datum).GetField("<originPosition>k__BackingField",S);var oldPos=pos.GetValue(null);
        var h=new Harmony("gunmaggedon.nuclear.commit."+Guid.NewGuid());AircraftDefinition definition=null;
        try
        {
            origin.SetValue(null,stage.transform);pos.SetValue(null,Vector3.zero);
            h.Patch(typeof(Unit).GetProperty("NetworkHQ").GetMethod,prefix:new HarmonyMethod(typeof(R230NuclearCommitmentIntegration).GetMethod("Team")));
            h.Patch(typeof(Unit).GetProperty("LocalSim").GetMethod,prefix:new HarmonyMethod(typeof(R230NuclearCommitmentIntegration).GetMethod("Own")));
            h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(R230NuclearCommitmentIntegration).GetMethod("Own")));
            h.Patch(typeof(Unit).GetMethod("LineOfSight"),prefix:new HarmonyMethod(typeof(R230NuclearCommitmentIntegration).GetMethod("Sight")));
            h.Patch(typeof(Gun).GetMethod("Fire"),prefix:new HarmonyMethod(typeof(R230NuclearCommitmentIntegration).GetMethod("Fire")));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/"+(legacy?"R230NL_Emplacement.prefab":"R230N_Emplacement.prefab"));
            var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var unit=root.GetComponent<Unit>();var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);
            var friendly=new GameObject("Friendly").AddComponent<FactionHQ>();friendly.transform.SetParent(stage.transform,false);var enemy=new GameObject("Enemy").AddComponent<FactionHQ>();enemy.transform.SetParent(stage.transform,false);teams[unit]=friendly;
            var go=new GameObject("Predictable aircraft",typeof(Mirage.NetworkIdentity));go.transform.SetParent(stage.transform,false);go.transform.position=new Vector3(0,6000,20000);
            var body=go.AddComponent<Rigidbody>();body.useGravity=false;body.velocity=Vector3.right*200;var target=go.AddComponent<Aircraft>();typeof(Unit).GetField("<rb>k__BackingField",F).SetValue(target,body);definition=ScriptableObject.CreateInstance<AircraftDefinition>();definition.jsonKey="FixtureAircraft";((Unit)target).definition=definition;teams[target]=enemy;
            var c=(Component)turret.gameObject.AddComponent(controllerType);Call(c,"Awake");Set(c,"target",target);Set(c,"nextScan",float.MaxValue);stage.SetActive(true);c.GetComponent<Behaviour>().enabled=false;
            typeof(Gun).GetField("bulletsLoaded",F).SetValue(gun,1);typeof(Gun).GetField("timeUntilReload",F).SetValue(gun,0f);
            var capacitor=Get(c,"capacitor");
            if(legacy){
                Check((int)capacitor.GetType().GetProperty("Cells").GetValue(capacitor)==3&&(double)capacitor.GetType().GetProperty("ChargeDuration").GetValue(capacitor)==2&&(double)capacitor.GetType().GetProperty("RechargeDuration").GetValue(capacitor)==10,"Legacy prefab selects original three-cell/two-second/ten-second bank");
                Call(c,"FixedUpdate");Check((bool)Get(c,"solution")&&!(bool)Get(c,"reaction").GetType().GetProperty("Ready").GetValue(Get(c,"reaction")),"Legacy reacts immediately without five-second gate");
                // Applying the pose moves the muzzle and changes the next solve.
                // Converge that real aim before starting an explicit acquisition
                // interval; a lone future Track call can only start, rather than
                // finish, acquisition if the preceding solve was not yet aligned.
                for(int i=0;i<8;i++){
                    Align(c,turret,gun);Call(c,"FixedUpdate");
                    if((bool)c.GetType().GetProperty("Aligned",F).GetValue(c))break;
                }
                Check((bool)c.GetType().GetProperty("Aligned",F).GetValue(c),"Legacy fixture converges actual native muzzle alignment;"+FireState(c,gun,capacitor));
                var legacyAcquisition=Get(c,"acquisition");double acquiredAt=Time.time;
                Call(legacyAcquisition,"Reset");Call(legacyAcquisition,"Track",target.GetInstanceID(),true,acquiredAt-.76);Call(legacyAcquisition,"Track",target.GetInstanceID(),true,acquiredAt);
                Check((bool)legacyAcquisition.GetType().GetProperty("Ready").GetValue(legacyAcquisition),"Legacy aligned acquisition completes its explicit .76-second interval;"+FireState(c,gun,capacitor));
                Call(capacitor,"Cancel");Call(capacitor,"Tick",target.GetInstanceID(),true,(double)Time.time-2.01);Call(c,"FixedUpdate");
                Check(fireRequests==1&&!(bool)Get(c,"committed")&&float.IsNaN((float)Get(c,"trackingStarted")),"Legacy native fire request uses two-second charge without tracking delay or point commitment;"+FireState(c,gun,capacitor));
                var first=(GlobalPosition)Get(c,"aimPoint");Call(c,"ShotFired");
                Check((int)capacitor.GetType().GetProperty("Remaining").GetValue(capacitor)==2&&(double)capacitor.GetType().GetProperty("RechargeUntil").GetValue(capacitor)==0,"Legacy first discharge retains two cells and no bank recharge");
                go.transform.position+=Vector3.right*300;Call(c,"FixedUpdate");Check((GlobalPosition)Get(c,"aimPoint")!=first,"Legacy continues updating solution when aircraft maneuvers");
                Check(gun.info.muzzleVelocity==15000&&Get(c,"warning")!=null,"Legacy retains original velocity and current nuclear warning presentation");
                return new{passed=true,checks,fireRequests,details="Separate legacy prefab: immediate reaction, live aim, original capacitor and native Fire request; modern balanced prefab separately tested"};
            }
            Check((int)capacitor.GetType().GetProperty("Cells").GetValue(capacitor)==1,"Nuclear prefab controller has a single-cell bank");
            Check((double)capacitor.GetType().GetProperty("ChargeDuration").GetValue(capacitor)==3&&(double)capacitor.GetType().GetProperty("RechargeDuration").GetValue(capacitor)==20,"Nuclear charge/recharge timings selected by actual prefab identity");
            var originalPose=turret.transform.localRotation;Call(c,"FixedUpdate");
            Check(!(bool)Get(c,"solution")&&turret.transform.localRotation==originalPose&&Get(c,"warning")==null,"Nuclear target must pass initial reaction before any aim or warning");
            Call(Get(c,"reaction"),"Track",target.GetInstanceID(),true,(double)Time.time+5.01);
            Call(c,"FixedUpdate");Align(c,turret,gun);Call(c,"FixedUpdate");
            var acquisition=Get(c,"acquisition");Call(acquisition,"Track",target.GetInstanceID(),true,(double)Time.time+.76);Call(c,"FixedUpdate");
            Check(!(bool)Get(c,"committed")&&!(bool)capacitor.GetType().GetProperty("Charging").GetValue(capacitor),"Ready nuclear gun first enters live tracking, without freezing or charging");
            var movingPoint=(GlobalPosition)Get(c,"aimPoint");
            go.transform.position+=Vector3.right*200;Call(c,"FixedUpdate");Align(c,turret,gun);
            Check((GlobalPosition)Get(c,"aimPoint")!=movingPoint,"Firing solution continues following aircraft during live tracking");
            Set(c,"trackingStarted",Time.time-4.99f);Call(c,"FixedUpdate");Check(!(bool)Get(c,"committed"),"Cannot freeze before full five-second tracking interval");
            Set(c,"trackingStarted",Time.time-5.01f);Call(c,"FixedUpdate");
            Check((bool)Get(c,"committed"),"Completed five-second live tracking enters committed three-second charge");
            var point=(GlobalPosition)Get(c,"committedPoint");var warning=(Component)Get(c,"warning");
            Check(point.ToLocalPosition().x>600,"Committed point includes aircraft motion during three-second charge plus projectile flight");
            Check(((GlobalPosition)Get(warning,"endpoint"))==point,"Warning orb shows committed burst location");
            Check(fireRequests==0,"Full commitment warning cannot be bypassed at alignment");
            // A violent turn, position change and due target scan cannot move the
            // committed point, steal the target or restart its charging clock.
            double started=(double)capacitor.GetType().GetProperty("ChargeStarted").GetValue(capacitor);
            go.transform.position+=Vector3.left*5000;body.velocity=Vector3.back*600;Set(c,"nextScan",0f);
            for(int i=0;i<12;i++){Call(c,"FixedUpdate");Align(c,turret,gun);}
            Check((GlobalPosition)Get(c,"aimPoint")==point&&(GlobalPosition)Get(c,"committedPoint")==point,"Evasive turn cannot drag burst point after commitment");
            Check((double)capacitor.GetType().GetProperty("ChargeStarted").GetValue(capacitor)==started,"Committed solution does not repeatedly restart capacitor");
            Check(((GlobalPosition)Get(warning,"endpoint"))==point&&(Unit)Get(c,"target")==target,"Fixed warning and chosen engagement survive due scan");
            capacitor.GetType().GetField("<ChargeStarted>k__BackingField",F).SetValue(capacitor,(double)Time.time-3.01);
            Align(c,turret,gun);Call(c,"FixedUpdate");Check(fireRequests==1,"Completed charge issues native fire request toward committed solution");
            Call(c,"ShotFired");Check(!(bool)Get(c,"committed")&&(int)capacitor.GetType().GetProperty("Remaining").GetValue(capacitor)==0,"Discharge consumes one cell and releases commitment");
            Check((double)capacitor.GetType().GetProperty("RechargeUntil").GetValue(capacitor)>Time.time+19.9,"Actual discharge starts twenty-second recharge");
            Set(c,"target",target);Set(c,"nextScan",float.MaxValue);Call(c,"FixedUpdate");Check(!(bool)Get(c,"committed")&&fireRequests==1,"No immediate second charge/fire after nuclear discharge");
            Call(c,"CancelCharge");Check(!(bool)Get(c,"committed")&&float.IsNaN((float)Get(c,"trackingStarted"))&&!(bool)capacitor.GetType().GetProperty("Charging").GetValue(capacitor),"Cancellation clears tracking timer, point ownership and charge");
            Check(gun.info.muzzleVelocity==15000,"Native muzzle velocity remains unchanged");
            return new{passed=true,checks,fireRequests,details="Actual prefab/controller readiness, stable warning and burst solution, evasion, native fire request, one-shot recharge; mission balance pending"};
        }
        finally
        {
            runtime.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);if(definition)UnityEngine.Object.DestroyImmediate(definition);teams.Clear();origin.SetValue(null,old);pos.SetValue(null,oldPos);
        }
    }
}

public static class R230LegacyControllerIntegration { public static object Run(){return R230NuclearCommitmentIntegration.RunLegacy();} }
