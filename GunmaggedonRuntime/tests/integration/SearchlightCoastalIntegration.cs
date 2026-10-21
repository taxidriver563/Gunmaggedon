using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SearchlightCoastalIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Dictionary<Unit,FactionHQ> Teams=new Dictionary<Unit,FactionHQ>();
    static readonly Dictionary<Unit,GlobalPosition> KnownPositions=new Dictionary<Unit,GlobalPosition>();
    static bool dark=true,local=true;static int reports,checks;
    public static bool Local(ref bool __result){__result=local;return false;}
    public static bool Dark(ref bool __result){__result=dark;return false;}
    public static bool Team(Unit __instance,ref FactionHQ __result){return !Teams.TryGetValue(__instance,out __result);}
    public static bool Known(Unit __0,ref GlobalPosition __1,ref bool __result)
    {__result=KnownPositions.TryGetValue(__0,out __1);return false;}
    public static bool Grid(){return false;}
    public static bool Report(){reports++;return false;}
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    static object Static(Type t,string n,params object[] args){return t.GetMethod(n,S).Invoke(null,args);}
    // The editor does not run the game's BepInEx bootstrap. Supply only the
    // setting storage needed by this fixture, without initializing ConfigFile.
    static ConfigEntry<T> Setting<T>(T value)
    {
        var entry=(ConfigEntry<T>)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ConfigEntry<T>));
        typeof(ConfigEntry<T>).GetField("_typedValue",F).SetValue(entry,value);return entry;
    }
    static void Align(Component c,Vector3 point)
    {
        var origin=(Transform)Get(c,"origin");var v=c.transform.InverseTransformDirection(point-origin.position);
        ((Transform)Get(c,"yaw")).localRotation=Quaternion.Euler(0,Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg,0);
        ((Transform)Get(c,"pitch")).localRotation=Quaternion.Euler(-Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg,0,0);
    }
    static void Tick(Component c)
    {Set(c,"nextCandidates",float.MaxValue);Set(c,"nextSupport",float.MaxValue);Set(c,"lastCueAt",Time.time);Set(c,"nextSample",-1f);Set(c,"lastSample",Time.time-.1f);Call(c,"Update");}
    public static object Run()
    {
        string root=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??throw new Exception("Set GUNMAGGEDON_ROOT.");
        var helper=Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??Path.Combine(root,"GunmaggedonRuntime/build/1.8.4/Gunmaggedon.Runtime.dll");
        var loaded=Assembly.Load(File.ReadAllBytes(helper));
        var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var runtime=assembly.GetType("GunmageddonAudio.SearchlightRuntime");var policy=assembly.GetType("GunmageddonAudio.CoastalIllumination");
        var ranging=assembly.GetType("GunmageddonAudio.CoastalRanging");var ct=assembly.GetType("GunmageddonAudio.SearchlightController");
        var harmony=new Harmony("gunmaggedon.coastal.light.fixture");
        Action<MethodInfo,string> prefix=(m,n)=>harmony.Patch(m,new HarmonyMethod(typeof(SearchlightCoastalIntegration).GetMethod(n)));
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Coastal support fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var datumPosition=typeof(Datum).GetField("<originPosition>k__BackingField",S);
        object old=datum.GetValue(null),oldPosition=datumPosition.GetValue(null);var random=UnityEngine.Random.state;
        GameObject blocker=null;Component trail=null;checks=reports=0;dark=local=true;
        try {
            datum.SetValue(null,stage.transform);datumPosition.SetValue(null,Vector3.zero);
            prefix(typeof(Unit).GetProperty("LocalSim").GetGetMethod(true),"Local");
            prefix(typeof(Unit).GetProperty("NetworkHQ").GetGetMethod(true),"Team");
            prefix(typeof(FactionHQ).GetMethod("TryGetKnownPosition",F),"Known");
            prefix(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc"),"Grid");
            prefix(typeof(TargetDetector).GetMethod("DetectTarget"),"Report");
            prefix(policy.GetMethod("NeedsLight",S),"Dark");prefix(policy.GetMethod("IsLowLight",S),"Dark");
            var logger=new ManualLogSource("Coastal support fixture");
            ranging.GetField("trailSeconds",S).SetValue(null,Setting(4f));
            policy.GetField("flareShells",S).SetValue(null,Setting(true));
            Static(runtime,"Install",logger);
            Func<string,GameObject> make=name=>{var go=new GameObject(name);go.transform.SetParent(stage.transform,false);return go;};
            var friendly=make("Friendly HQ").AddComponent<FactionHQ>();var hostile=make("Hostile HQ").AddComponent<FactionHQ>();
            Func<float,Component> lamp=x=>{
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/Searchlight_Trailer.prefab"),stage.transform,false);go.transform.localPosition=Vector3.right*x;
                var owner=go.GetComponent<GroundVehicle>();Teams[owner]=friendly;var detector=go.GetComponentInChildren<TargetDetector>(true);detector.detectedTargets=new List<Unit>();
                Static(runtime,"Initialize",detector);var c=go.GetComponent(ct);Check(c!=null,"Current helper controller attached (use an isolated test assembly name if Unity cached an earlier helper)");Call(c,"Awake");Call(c,"SetActive",true);return c;
            };
            var first=lamp(0);var second=lamp(50);
            var battery=make("Battery").AddComponent<Building>();Teams[battery]=friendly;
            var gun=battery.gameObject.AddComponent<Gun>();gun.attachedUnit=battery;gun.info=AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/Mods/Gunmageddon/203mmEmplacement.asset");
            var muzzle=make("Muzzle").transform;muzzle.position=Vector3.up*10;typeof(Gun).GetField("muzzles",F).SetValue(gun,new[]{muzzle});
            var ship=make("Ship").AddComponent<Ship>();Teams[ship]=hostile;
            Vector3 position=((Transform)Get(first,"origin")).position+Vector3.forward*3000-Vector3.up*8f;ship.transform.position=position;
            KnownPositions[ship]=position.ToGlobalPosition();gun.SetTarget(ship);
            stage.SetActive(true);Physics.SyncTransforms();
            var originalYaw=((Transform)Get(first,"yaw")).localRotation;
            Check(!(bool)Static(policy,"ShouldFireFlare",gun,ship),"Idle searchlight should reserve before the first flare decision");
            Check((Unit)Get(first,"attention")==ship&&Get(second,"attention")==null,"One friendly lamp per requested ship");
            Check(((Transform)Get(first,"yaw")).localRotation==originalYaw&&reports==0,"Request must not teleport or detect the ship");
            Check(!(bool)Static(policy,"IsIlluminated",ship)&&(bool)Static(policy,"MustIlluminate",ship),"A reservation must not unlock firing");
            var estimate=(Vector3)Get(first,"cue");Check(Vector3.Distance(estimate,position+Vector3.up*8)<.01f,"Cue uses the faction's reported position");
            ship.transform.position+=Vector3.right*100;Call(first,"CoastalCue");Check((Vector3)Get(first,"cue")==estimate,"Unseen movement must not become perfect tracking");ship.transform.position=position;
            for(int i=0;i<8;i++)Call(first,"Aim",1f);
            Check(Mathf.Abs(Mathf.DeltaAngle(((Transform)Get(first,"pitch")).localEulerAngles.x,0))<1f,"Coastal aim can reach the horizon");
            Align(first,position+Vector3.up*8);Tick(first);Check(reports==0,"One beam sample must not acquire instantly");
            for(int i=0;i<30;i++)Tick(first);
            Check(reports>0&&(bool)Static(policy,"IsIlluminated",ship),"Sustained beam contact illuminates and reports the ship");
            Check(!(bool)Static(policy,"ShouldFireFlare",gun,ship),"Confirmed stationary coverage conserves flares");
            Check((bool)Call(first,"CoversCoastal",friendly,ship,position,20f),"Stable illumination supports a future coverage forecast");
            Check(!(bool)Call(first,"CoversCoastal",hostile,ship,position,20f),"Opposing batteries do not receive support promises");
            Check(!(bool)Call(first,"CoversCoastal",friendly,ship,position+Vector3.forward*12000,20f),"Ship leaving beam range needs area-illumination fallback");
            var plans=(IDictionary)policy.GetField("Plans",S).GetValue(null);var plan=plans[gun];Set(plan,"EstimatedVelocity",Vector3.forward*700);Set(plan,"ForecastUntil",-1f);
            Check((bool)Static(policy,"ShouldFireFlare",gun,ship),"Predictive flare requested while current beam contact is still valid");
            Set(plan,"EstimatedVelocity",Vector3.zero);((IList)policy.GetField("Tokens",S).GetValue(null)).Clear();
            var ship2=make("Other ship").AddComponent<Ship>();Teams[ship2]=hostile;ship2.transform.position=position+Vector3.right*300;
            var gun2=make("Second battery").AddComponent<Gun>();gun2.attachedUnit=battery;gun2.info=gun.info;
            Static(runtime,"RequestCoastal",gun2,ship2,ship2.transform.position.ToGlobalPosition(),Vector3.zero);
            Check((Unit)Get(second,"attention")==ship2,"Free lamp can accept a different coastal target");Call(second,"Lose");Static(runtime,"ReleaseCoastal",gun2);
            // The runtime uses the default physics scene; preview-scene colliders
            // do not participate in Physics.RaycastNonAlloc.
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var origin=(Transform)Get(first,"origin");blocker.transform.position=Vector3.Lerp(origin.position,position+Vector3.up*8,.5f);blocker.transform.localScale=new Vector3(500,500,10);Physics.SyncTransforms();
            Tick(first);Check(!(bool)Static(policy,"IsIlluminated",ship),"Occlusion immediately revokes actual illumination");
            UnityEngine.Object.DestroyImmediate(blocker);blocker=null;Call(first,"Lose");
            Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);Call(first,"CoastalCue");
            var request=Static(runtime,"CoastalFor",friendly,ship);Set(request,"Created",Time.time-13f);
            Check(!(bool)Static(runtime,"AwaitingCoastal",gun,ship),"Repeated requests cannot extend flare deferral indefinitely");
            Call(first,"Lose");Call(second,"OnDisable");
            Check((bool)Static(policy,"ShouldFireFlare",gun,ship),"No effective searchlight falls back to area flares");
            policy.GetField("flareShells",S).SetValue(null,Setting(false));
            Check(!(bool)Static(policy,"ShouldFireFlare",gun,ship)&&(bool)Static(policy,"MustIlluminate",ship),"Optional flares can be disabled without bypassing the darkness gate");policy.GetField("flareShells",S).SetValue(null,Setting(true));
            Static(runtime,"ReleaseCoastal",gun);local=false;Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);local=true;
            Check(Static(runtime,"CoastalFor",friendly,ship)==null,"Remote simulations cannot issue support tasks");
            dark=false;Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);
            Check(Static(runtime,"CoastalFor",friendly,ship)==null,"Daylight cannot issue coastal light tasks");dark=true;
            Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);request=Static(runtime,"CoastalFor",friendly,ship);Set(request,"Updated",Time.time-4f);
            Check(Static(runtime,"CoastalFor",friendly,ship)==null,"Stale requests expire");
            Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);gun.SetTarget(ship2);Static(runtime,"ReleaseCoastalIfChanged",gun);
            Check(Static(runtime,"CoastalFor",friendly,ship)==null,"Target changes release old requests");
            Call(first,"SetActive",true);var aircraft=make("Aircraft").AddComponent<Aircraft>();Teams[aircraft]=hostile;
            Check((bool)Call(first,"Focus",aircraft),"Aircraft assignments remain supported");Static(runtime,"RequestCoastal",gun,ship,position.ToGlobalPosition(),Vector3.zero);
            Check((Unit)Get(first,"attention")==aircraft,"Coastal work does not steal an existing air-defense assignment");Call(first,"Lose");
            trail=(Component)Static(ranging,"CreateTrail",Vector3.up*100,Color.cyan);var line=trail.GetComponent<LineRenderer>();var properties=new MaterialPropertyBlock();line.GetPropertyBlock(properties);
            Check((float)trail.GetType().GetProperty("Lifetime").GetValue(trail)==4f,"Ranging trail uses the four-second setting");
            Check(Mathf.Approximately(properties.GetColor("_Color").r,3f),"Ranging-only shader tint is brighter: "+properties.GetColor("_Color"));
            float now=Time.time;Call(trail,"Sample",(Vector3.up*100+Vector3.forward*100).ToGlobalPosition(),now+.1f);Call(trail,"Refresh",now+.2f);Check(line.enabled,"Shortened trail remains visible in flight");
            Call(trail,"Freeze");Call(trail,"Refresh",now+4.11f);Check(!line.enabled&&line.positionCount==0,"Every trail segment expires after four seconds");
            var raw=(Component)Static(ranging,"BuildTrail",Vector3.up*100,Color.yellow,.18f);var rawProperties=new MaterialPropertyBlock();raw.GetComponent<LineRenderer>().GetPropertyBlock(rawProperties);
            Check(!Mathf.Approximately(rawProperties.GetColor("_Color").r,3f)&&(float)raw.GetType().GetProperty("Lifetime").GetValue(raw)==.18f,"Cluster/slug trails retain their own tint and lifetime");UnityEngine.Object.DestroyImmediate(raw.gameObject);
            return new{passed=true,checks,details="Coastal request estimates, exclusive claims, gradual horizon aiming, sustained optical acquisition, actual-light firing gate, coverage prediction, area flare fallback/disable, lifecycle/authority/daylight guards, air-defense preservation and bright four-second trails"};
        } finally {
            if(blocker)UnityEngine.Object.DestroyImmediate(blocker);if(trail)UnityEngine.Object.DestroyImmediate(trail.gameObject);
            var material=ranging.GetField("trailMaterial",S).GetValue(null) as Material;if(material)UnityEngine.Object.DestroyImmediate(material);ranging.GetField("trailMaterial",S).SetValue(null,null);
            EditorSceneManager.ClosePreviewScene(scene);Static(runtime,"Shutdown");Static(policy,"Shutdown");harmony.UnpatchSelf();
            Teams.Clear();KnownPositions.Clear();datum.SetValue(null,old);datumPosition.SetValue(null,oldPosition);UnityEngine.Random.state=random;dark=local=true;
        }
    }
}
