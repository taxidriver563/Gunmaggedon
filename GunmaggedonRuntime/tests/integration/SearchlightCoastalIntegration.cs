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
    public static bool KnownTargets(List<TrackingInfo> __0,ref List<TrackingInfo> __result)
    {
        __0.Clear();foreach(var unit in KnownPositions.Keys) {
            var contact=(TrackingInfo)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TrackingInfo));
            typeof(TrackingInfo).GetField("unit",F).SetValue(contact,unit);__0.Add(contact);
        }
        __result=__0;return false;
    }
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
    sealed class AudibleEngine:IEngine
    {
        public float Rpm;public Transform transform{get;set;}
        public event Action OnEngineDisable{add{}remove{}}public event Action OnEngineDamage{add{}remove{}}
        public float GetThrust(){return Rpm*10000;}public float GetMaxThrust(){return 10000;}public float GetRPM(){return Rpm*10000;}public float GetRPMRatio(){return Rpm;}public void SetInteriorSounds(bool value){}
    }
    static void HearingAndTracking(Component first,Component second,Aircraft aircraft,Radar radar,FactionHQ friendly,FactionHQ hostile)
    {
        Call(first,"Lose");Call(second,"Lose");Call(first,"SetActive",true);Call(second,"SetActive",true);
        var origin=(Transform)Get(first,"origin");var yaw=(Transform)Get(first,"yaw");var pitch=(Transform)Get(first,"pitch");var engine=new AudibleEngine{Rpm=1,transform=aircraft.transform};Set(aircraft,"engines",new List<IEngine>{engine});
        ((List<Unit>)Get(first,"nearby")).Clear();((List<Unit>)Get(first,"nearby")).Add(aircraft);((List<Unit>)Get(second,"nearby")).Clear();((List<Unit>)Get(second,"nearby")).Add(aircraft);
        Action hear=()=>{Set(first,"nextAcousticAt",0f);Call(first,"AcousticCue");};
        aircraft.transform.position=origin.position+new Vector3(3000,1500,2000);int before=reports;var startYaw=yaw.localRotation;hear();
        Check((Unit)Get(first,"attention")==aircraft&&(bool)Get(first,"acousticCued")&&!(bool)Get(first,"radarCued"),"Running engine within four kilometers provides an acoustic search sector");
        Check(Vector3.Distance((Vector3)Get(first,"cue"),aircraft.transform.position)>100,"Engine sound supplies an uncertain bearing/range estimate");
        var solution=Get(first,"solution");Check(!(bool)Get(solution,"Acquired")&&(float)Get(solution,"Evidence")==0&&reports==before,"Hearing neither acquires nor reports an aircraft");
        Check(yaw.localRotation==startYaw,"Engine cue does not teleport lamp aim");
        Set(second,"nextAcousticAt",0f);Call(second,"AcousticCue");Check(Get(second,"attention")==null,"Acoustic search respects exclusive aircraft tokens");Call(first,"Lose");
        aircraft.transform.position=origin.position+Vector3.forward*5001;hear();Check(Get(first,"attention")==null,"Full-power engine beyond five kilometers is inaudible");
        engine.Rpm=.2f;aircraft.transform.position=origin.position+Vector3.forward*3001;hear();Check(Get(first,"attention")==null,"Low-RPM engine beyond three kilometers is inaudible");
        aircraft.transform.position=origin.position+Vector3.forward*2999;hear();Check((Unit)Get(first,"attention")==aircraft,"Low-RPM engine can cue within three kilometers");Call(first,"Lose");
        engine.Rpm=0;hear();Check(Get(first,"attention")==null,"Stopped engines cannot supply an acoustic cue");
        engine.Rpm=1;Teams[aircraft]=friendly;hear();Check(Get(first,"attention")==null,"Friendly engines are not assigned as hostile search targets");Teams[aircraft]=hostile;
        aircraft.transform.position=origin.position+new Vector3(2000,1500,2000);((List<Radar>)Get(first,"support")).Add(radar);Call(first,"RadarCue");var radarCue=(Vector3)Get(first,"cue");hear();
        Check((bool)Get(first,"radarCued")&&!(bool)Get(first,"acousticCued")&&(Vector3)Get(first,"cue")==radarCue,"Radar cue takes priority over rough engine direction");
        Set(first,"lastVisualAt",Time.time);Set(first,"lastSeen",Time.time);Set(first,"claimConfirmed",true);Set(solution,"Acquired",true);
        var originalCue=(Vector3)Get(first,"cue");Set(first,"cue",origin.position-Vector3.forward*1000);Set(first,"yawVelocity",0f);Set(first,"pitchVelocity",0f);yaw.localRotation=Quaternion.identity;pitch.localRotation=Quaternion.identity;Set(first,"phase",0f);Call(first,"Aim",.05f);var firstYaw=yaw.localRotation;var firstPitch=pitch.localRotation;
        yaw.localRotation=Quaternion.identity;pitch.localRotation=Quaternion.identity;Set(first,"yawVelocity",0f);Set(first,"pitchVelocity",0f);Set(first,"phase",1000f);Call(first,"Aim",.05f);
        Check(Quaternion.Angle(yaw.localRotation,firstYaw)<.001f&&Quaternion.Angle(pitch.localRotation,firstPitch)<.001f,"Visible aircraft tracking has no random/search wobble");
        Check((Vector3)Get(first,"cue")==aircraft.transform.position,"Visible target point updates each frame instead of stepping at cue cadence");
        bool bounded=true;for(int i=0;i<600;i++){var oldYaw=yaw.localRotation;var oldPitch=pitch.localRotation;Call(first,"Aim",.02f);bounded &= Quaternion.Angle(oldYaw,yaw.localRotation)<=.405f&&Quaternion.Angle(oldPitch,pitch.localRotation)<=.305f;}
        Check(bounded&&Vector3.Angle(origin.forward,aircraft.transform.position-origin.position)<.1f,"Damped tracking converges smoothly within yaw/pitch rate limits");
        aircraft.transform.position+=Vector3.right*20;Call(first,"Aim",.02f);Check((Vector3)Get(first,"cue")==aircraft.transform.position,"Locked beam follows moving aircraft without waiting for another radar cue");
        var other=new GameObject("Other radar aircraft");other.transform.SetParent(aircraft.transform.parent,false);var otherUnit=other.AddComponent<Aircraft>();Teams[otherUnit]=hostile;other.transform.position=origin.position+Vector3.forward*2000;radar.detectedTargets=new List<Unit>{otherUnit};Call(first,"RadarCue");
        Check((Unit)Get(first,"attention")==aircraft,"A different radar contact cannot repeatedly steal a visible aircraft lock");radar.detectedTargets=new List<Unit>{aircraft};((List<Radar>)Get(first,"support")).Clear();
        Call(first,"Lose");Call(first,"Focus",aircraft);yaw.localRotation=Quaternion.Euler(0,180,0);pitch.localRotation=Quaternion.identity;Set(solution,"BlindSeconds",2.4f);Tick(first);
        Check((Unit)Get(first,"attention")==aircraft,"Unconfirmed search survives evidence reset while lamp is still traversing");
        Call(first,"Lose");Call(second,"Lose");Call(second,"SetActive",false);
    }
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
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Coastal support fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.position=new Vector3(30000,0,0);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var datumPosition=typeof(Datum).GetField("<originPosition>k__BackingField",S);
        object old=datum.GetValue(null),oldPosition=datumPosition.GetValue(null);var random=UnityEngine.Random.state;
        GameObject blocker=null;Component trail=null;checks=reports=0;dark=local=true;
        try {
            datum.SetValue(null,stage.transform);datumPosition.SetValue(null,Vector3.zero);
            prefix(typeof(Unit).GetProperty("LocalSim").GetGetMethod(true),"Local");
            prefix(typeof(Unit).GetProperty("NetworkHQ").GetGetMethod(true),"Team");
            prefix(typeof(FactionHQ).GetMethod("TryGetKnownPosition",F),"Known");
            prefix(typeof(FactionHQ).GetMethod("GetTargetsWithinRange",F),"KnownTargets");
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
            Check((float)Get(first,"reach")==13000f,"Runtime uses the full 13km beam mesh range");
            var rangeOrigin=(Transform)Get(first,"origin");
            Check((bool)Call(first,"CanReach",rangeOrigin.position+Vector3.forward*13000,1f),"Full-range coastal cue at 13km is reachable");
            Check(!(bool)Call(first,"CanReach",rangeOrigin.position+Vector3.forward*13000.5f,1f),"Coastal cue beyond 13km is rejected");
            Call(first,"ApplyLength",13000f);
            var rangeProperties=new MaterialPropertyBlock();((MeshRenderer)Get(first,"beam")).GetPropertyBlock(rangeProperties);
            Check(rangeProperties.GetFloat("_BeamRange")==13000&&rangeProperties.GetFloat("_VisibleLength")==13000,"Rendered beam and targeting share the 13km range");
            Check(((Light)Get(first,"spot")).range==13000,"Native spot light shares the 13km range");
            var battery=make("Battery").AddComponent<Building>();Teams[battery]=friendly;
            var gun=battery.gameObject.AddComponent<Gun>();gun.attachedUnit=battery;gun.info=AssetDatabase.LoadAssetAtPath<WeaponInfo>("Assets/Blueprinter/Mods/Gunmageddon/203mmEmplacement.asset");
            var muzzle=make("Muzzle").transform;muzzle.position=stage.transform.position+Vector3.up*10;typeof(Gun).GetField("muzzles",F).SetValue(gun,new[]{muzzle});
            var ship=make("Ship").AddComponent<Ship>();Teams[ship]=hostile;
            Vector3 position=((Transform)Get(first,"origin")).position+Vector3.forward*3000-Vector3.up*8f;ship.transform.position=position;
            KnownPositions[ship]=position.ToGlobalPosition();gun.SetTarget(ship);
            stage.SetActive(true);Physics.SyncTransforms();
            var originalYaw=((Transform)Get(first,"yaw")).localRotation;
            // Reproduce a request removed between two observations in the same 0.5s cache window.
            Call(first,"SetActive",false);Call(second,"SetActive",false);
            Static(policy,"Observe",gun,ship);Static(runtime,"ReleaseCoastal",gun);
            Call(first,"SetActive",true);Call(second,"SetActive",true);
            gun.SetTarget(null);Static(runtime,"RefreshBattery",gun);
            Check(gun.GetTarget()==null&&(Unit)Get(first,"attention")==ship,"Faction-known ship can request light before native weapon targeting/aim/fire");
            Check(reports==0,"Independent request does not instantly detect the ship");
            Static(runtime,"ReleaseCoastalIfChanged",gun);
            Check(Static(runtime,"CoastalFor",friendly,ship)!=null,"Native AI clearing a dark gun target must not cancel a known-contact lighting task");gun.SetTarget(ship);
            Check(!(bool)Static(policy,"ShouldFireFlare",gun,ship),"Idle searchlight should reserve before the first flare decision");
            Check((Unit)Get(first,"attention")==ship&&Get(second,"attention")==null,"One friendly lamp per requested ship");
            Check(((Transform)Get(first,"yaw")).localRotation==originalYaw&&reports==0,"Request must not teleport or detect the ship");
            Check(!(bool)Static(policy,"IsIlluminated",ship)&&(bool)Static(policy,"MustIlluminate",ship),"A reservation must not unlock firing");
            var estimate=(Vector3)Get(first,"cue");Check(Vector3.Distance(estimate,position+Vector3.up*8)<.01f,"Cue uses the faction's reported position");
            var nearbyRequest=Static(runtime,"CoastalFor",friendly,ship);var batteryPosition=battery.transform.position;
            battery.transform.position=first.transform.position+Vector3.right*2000f;
            Check((bool)Call(first,"CanSupport",nearbyRequest,0f),"A battery exactly 2km away may request support");
            battery.transform.position=first.transform.position+Vector3.right*2000.5f;
            Check(!(bool)Call(first,"CanSupport",nearbyRequest,0f),"A battery beyond 2km cannot task this lamp");
            Check(!(bool)Call(first,"Enemy",ship),"An ongoing coastal assignment requires an in-range battery");
            Check(!(bool)Static(runtime,"AwaitingCoastal",gun,ship),"An out-of-range lamp cannot defer the battery's flare fallback");
            battery.transform.position=batteryPosition;
            var farBattery=make("Distant battery").AddComponent<Building>();Teams[farBattery]=friendly;farBattery.transform.position=Vector3.right*20000;
            var farGun=farBattery.gameObject.AddComponent<Gun>();farGun.attachedUnit=farBattery;farGun.info=gun.info;
            Static(runtime,"RequestCoastal",farGun,ship,position.ToGlobalPosition(),Vector3.zero);
            var farRequest=((IDictionary)runtime.GetField("CoastalRequests",S).GetValue(null))[farGun];Set(farRequest,"Updated",Time.time+.01f);
            Check(ReferenceEquals(Static(runtime,"CoastalForLamp",friendly,ship,first.transform.position),nearbyRequest),"A distant battery's newer request cannot hide the nearby request");
            Static(runtime,"ReleaseCoastal",farGun);
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position=Vector3.Lerp(((Transform)Get(first,"origin")).position,position+Vector3.up*8,.5f);blocker.transform.localScale=new Vector3(500,500,10);Physics.SyncTransforms();
            Call(first,"Lose");Call(first,"CoastalCue");
            Check((Unit)Get(first,"attention")==ship&&!(bool)Call(first,"Clear",ship),"Blocked sector may cue visible traverse without claiming clear observation");
            Check(!(bool)Call(first,"IlluminatesCoastal",ship),"Accepting a blocked cue cannot illuminate or unlock fire");
            UnityEngine.Object.DestroyImmediate(blocker);blocker=null;
            var baseRotation=first.transform.rotation;
            first.transform.rotation=Quaternion.Euler(-40,25,0);
            var seaPoint=((Transform)Get(first,"origin")).position+Vector3.forward*3000;
            Check((bool)Call(first,"CanReach",seaPoint,1f),"An uphill trailer can accept a sea-level coastal cue");
            Set(first,"cue",seaPoint);for(int i=0;i<600;i++)Call(first,"Aim",.05f);
            Check(Vector3.Angle(((Transform)Get(first,"origin")).forward,seaPoint-((Transform)Get(first,"origin")).position)<1.5f,"Inclined trailer aims at the world horizon instead of retaining its upward bias");
            first.transform.rotation=baseRotation;
            var cliffPoint=((Transform)Get(first,"origin")).position+Vector3.forward*1200-Vector3.up*400;
            Check((bool)Call(first,"CanReach",cliffPoint,1f),"Lamp can look down from a coastal bluff toward the sea");
            Set(first,"cue",cliffPoint);for(int i=0;i<100;i++)Call(first,"Aim",.05f);
            Check(((Transform)Get(first,"origin")).forward.y<-.2f,"Coastal traverse can depress beyond the old five-degree limit");Set(first,"cue",estimate);
            ship.transform.position+=Vector3.right*100;Call(first,"CoastalCue");Check((Vector3)Get(first,"cue")==estimate,"Unseen movement must not become perfect tracking");ship.transform.position=position;
            for(int i=0;i<160;i++)Call(first,"Aim",.05f);
            Check(Mathf.Abs(Mathf.DeltaAngle(((Transform)Get(first,"pitch")).localEulerAngles.x,0))<1f,"Coastal aim can reach the horizon");
            float savedPhase=(float)Get(first,"phase");
            for(int i=0;i<16;i++){Set(first,"phase",i*Mathf.PI/8);for(int j=0;j<100;j++)Call(first,"Aim",.05f);Check((bool)Call(first,"Inside",ship,13000f),"Coastal cue remains inside the acquisition cone across sway phases "+i);}
            Set(first,"phase",savedPhase);
            Align(first,position+Vector3.up*8);Check((bool)Call(first,"Enemy",ship),"Pre-observation coastal request valid="+Call(nearbyRequest,"Valid",Time.time)+", battery="+battery.transform.position+", lamp="+first.transform.position+", near="+Call(nearbyRequest,"InRangeOf",first.transform.position)+", request="+Static(runtime,"CoastalForLamp",friendly,ship,first.transform.position));Tick(first);Check((Unit)Get(first,"attention")==ship,"First tick lost attention; request valid="+Call(nearbyRequest,"Valid",Time.time));Check(reports==0,"One beam sample must not acquire instantly");
            for(int i=0;i<30;i++)Tick(first);
            Check(reports>0&&(bool)Static(policy,"IsIlluminated",ship),"Sustained beam contact illuminates and reports the ship; reports="+reports+", attention="+Get(first,"attention")+", clear="+Call(first,"Clear",ship)+", inside="+Call(first,"Inside",ship,13000f)+", clip="+Call(first,"Clip")+", origin="+((Transform)Get(first,"origin")).position+", forward="+((Transform)Get(first,"origin")).forward+", dark="+dark+", local="+local);
            Check(!(bool)Static(policy,"ShouldFireFlare",gun,ship),"Confirmed stationary coverage conserves flares");
            Check((bool)Call(first,"CoversCoastal",friendly,ship,position,20f),"Stable illumination supports a future coverage forecast");
            Check(!(bool)Call(first,"CoversCoastal",hostile,ship,position,20f),"Opposing batteries do not receive support promises");
            Check(!(bool)Call(first,"CoversCoastal",friendly,ship,position+Vector3.forward*12000,20f),"Ship leaving beam range needs area-illumination fallback");
            var specials=assembly.GetType("GunmageddonAudio.SpecialShells");var obscurants=assembly.GetType("GunmageddonAudio.ShellObscurants");
            var cloudKind=Enum.Parse(assembly.GetType("GunmageddonAudio.SpecialShellKind"),"Smoke");
            Vector3 smokePoint=Vector3.Lerp(((Transform)Get(first,"origin")).position,position+Vector3.up*8,.5f);
            Static(specials,"Cloud",cloudKind,(smokePoint-Vector3.up*14).ToGlobalPosition());
            var smoke=(Component)((IList)obscurants.GetField("Clouds",S).GetValue(null))[0];Set(smoke,"Born",Time.time-10f);
            Check(!(bool)Call(first,"Clear",ship),"Smoke blocks continued observation");
            Check(!(bool)Call(first,"ClearPoint",position+Vector3.up*8,ship),"Smoke blocks coastal candidate and forecast sightlines");
            Tick(first);Check(!(bool)Static(policy,"IsIlluminated",ship),"Smoke immediately revokes coastal illumination");
            UnityEngine.Object.DestroyImmediate(smoke.gameObject);
            for(int i=0;i<30;i++)Tick(first);
            Check((bool)Static(policy,"IsIlluminated",ship),"Illumination recovers after smoke clears");
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
            var airRequest=Static(runtime,"CoastalFor",friendly,ship);
            Check((bool)Call(first,"Focus",aircraft),"Air assignment can be restored");
            Check(!(bool)Call(first,"CanSupport",airRequest,0f),"A lamp tracking aircraft explicitly declines coastal work");Call(first,"Lose");
            Check((bool)Call(first,"Focus",ship),"Idle lamp can resume a nearby coastal request");
            Align(first,position+Vector3.up*8);
            aircraft.transform.position=((Transform)Get(first,"origin")).position+((Transform)Get(first,"origin")).forward*1500;
            ((List<Unit>)Get(first,"nearby")).Add(aircraft);Call(first,"OpticalCue",13000f);
            Check((Unit)Get(first,"attention")==aircraft,"An aircraft entering the beam preempts coastal illumination");
            Check(!(bool)Call(first,"IlluminatesCoastal",ship),"Air priority immediately releases ship illumination");
            Call(second,"SetActive",true);Call(second,"OfferCoastal");
            Check((Unit)Get(second,"attention")==ship,"A free nearby lamp can take the released coastal assignment");Call(second,"Lose");Call(second,"OnDisable");
            aircraft.gameObject.SetActive(false);Tick(first);Call(first,"OfferCoastal");
            Check((Unit)Get(first,"attention")==ship,"Coastal support can resume after the aircraft assignment ends");Call(first,"Lose");
            aircraft.gameObject.SetActive(true);((List<Unit>)Get(first,"nearby")).Clear();
            var radarObject=make("Air-priority radar truck");var truck=radarObject.AddComponent<GroundVehicle>();Teams[truck]=friendly;
            truck.transform.position=first.transform.position+Vector3.right*200;
            var radar=radarObject.AddComponent<Radar>();typeof(TargetDetector).GetField("attachedUnit",F).SetValue(radar,truck);radar.activated=true;radar.detectedTargets=new List<Unit>{aircraft};
            ((List<Radar>)Get(first,"support")).Add(radar);
            Check((bool)Call(first,"Focus",ship),"Coastal assignment established before radar air cue");Call(first,"RadarCue");
            Check((Unit)Get(first,"attention")==aircraft&&(bool)Get(first,"radarCued"),"Radar aircraft cue preempts an existing coastal assignment");Call(first,"Lose");
            Call(first,"OfferCoastal");Check((Unit)Get(first,"attention")==aircraft,"A pending radar air contact wins before an idle lamp accepts coastal work");
            ((List<Radar>)Get(first,"support")).Clear();Call(first,"Lose");
            HearingAndTracking(first,second,aircraft,radar,friendly,hostile);
            var savedKnown=KnownPositions[ship];KnownPositions.Clear();gun.SetTarget(null);
            ((IDictionary)runtime.GetField("BatteryPolls",S).GetValue(null)).Clear();Static(runtime,"RefreshBattery",gun);
            Check(Static(runtime,"CoastalFor",friendly,ship)==null,"Independent battery update does not invent an unknown ship contact");
            KnownPositions[ship]=savedKnown;gun.SetTarget(ship);
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
