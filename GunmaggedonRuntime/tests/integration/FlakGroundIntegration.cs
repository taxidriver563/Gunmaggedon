using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
public static partial class F88FlakIntegration
{
    static readonly Dictionary<byte,UnitPart> groundParts=new Dictionary<byte,UnitPart>();
    static int groundSpawns;static bool groundServer=true;static UnitPart groundDamagePart;static int groundDamageCalls;
    public static bool GroundServer(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=groundServer;return false;}
    [HarmonyPriority(Priority.Last)]
    public static bool GroundCapture(float __2,float __3){groundSpawns++;Check(Math.Abs(__2-(groundHeavy?.10f:.18f))<.001&&__3==30,"Native SpawnBullet reaches AP launch with correct spread and safe expiry");return false;}
    static GameAssets groundAssets;
    public static bool GroundAssets(ref GameAssets __result){__result=groundAssets;return false;}
    static bool groundHeavy;static NuclearOption.Networking.NetworkManagerNuclearOption groundManager;
    public static bool GroundManager(ref NuclearOption.Networking.NetworkManagerNuclearOption __result){__result=groundManager;return false;}
    public static bool GroundRpc(byte __0,DamageInfo __1){groundDamageCalls++;UnitPart part;if(groundParts.TryGetValue(__0,out part))((IDamageable)part).ApplyDamage(NetworkFloatHelper.Decompress(__1.pierceDamage),NetworkFloatHelper.Decompress(__1.blastDamage),NetworkFloatHelper.Decompress(__1.fireDamage),NetworkFloatHelper.Decompress(__1.impactDamage));return false;}
    public static bool GroundKill(){return false;}
    public static bool GroundVisibility(ref Cysharp.Threading.Tasks.UniTask __result){__result=default(Cysharp.Threading.Tasks.UniTask);return false;}
    public static object RunGround(){return RunProfile(false,false,true);}
    public static object RunHeavyGround(){return RunProfile(true,false,true);}
    static void GroundAlign(object c,Turret turret,Gun gun){var direction=(Vector3)Get(c,"direction");var local=turret.transform.parent.InverseTransformDirection(direction);turret.transform.localRotation=Quaternion.Euler(0,Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,0);gun.transform.localRotation=Quaternion.Euler(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg,0,0);}
    static object RunGroundScenario(Assembly a,Component c,GroundVehicle owner,Gun gun,Turret turret,GameObject stage,FactionHQ hostile,FactionHQ friendly,Harmony h,bool heavy)
    {
        groundParts.Clear();groundSpawns=groundDamageCalls=0;groundServer=true;groundHeavy=heavy;groundDamagePart=null;
        var managerObject=new GameObject("Ground fixture network facade");managerObject.SetActive(false);managerObject.transform.SetParent(stage.transform,false);groundManager=managerObject.AddComponent<NuclearOption.Networking.NetworkManagerNuclearOption>();groundManager.Server=managerObject.AddComponent<Mirage.NetworkServer>();
        h.Patch(typeof(NuclearOption.Networking.NetworkManagerNuclearOption).GetProperty("i",S|BindingFlags.FlattenHierarchy).GetMethod,prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundManager")));
        groundAssets=ScriptableObject.CreateInstance<GameAssets>();groundAssets.WaterMaterial=new PhysicMaterial("Water fixture material");groundAssets.terrainMaterial=new PhysicMaterial("Terrain fixture material");
        h.Patch(typeof(GameAssets).GetProperty("i",S).GetMethod,prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundAssets")));
        var definitions=new List<UnityEngine.Object>();var flak=a.GetType("GunmageddonAudio.F88FlakRuntime");var policy=a.GetType("GunmageddonAudio.FlakGroundPolicy");
        var shared=a.GetType("GunmageddonAudio.SharedWorldQueries");var network=a.GetType("GunmageddonAudio.F88FlakNetwork");
        h.Patch(typeof(Unit).GetMethod("SingleRemoteFire",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundKill")));
        h.Patch(typeof(Weapon).GetMethod("TrackFiringVisibility",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundVisibility")));
        h.Patch(typeof(Gun).GetMethod("ShotSound",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundKill")));
        h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundServer")));
        h.Patch(a.GetType("GunmageddonAudio.CoastalIllumination").GetMethod("IsLowLight",S),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("Dark")));
        h.Patch(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc",S),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("Grid")));
        Func<string,Vector3,FactionHQ,GroundVehicle> vehicle=(name,position,team)=>{
            var go=new GameObject(name);go.transform.SetParent(stage.transform,false);go.transform.position=position;
            var u=go.AddComponent<GroundVehicle>();var d=UnityEngine.Object.Instantiate(owner.definition);d.jsonKey=name;d.height=2;((Unit)u).definition=d;definitions.Add(d);Teams[u]=team;Set(u,"disabled",false);Set(u,"visibility",6000f);return u;
        };
        Action choose=()=>{Static(shared,"Clear");Call(c,"ChooseContact",Time.time);};
        try{
            var land=vehicle("Ground enemy",new Vector3(0,0,2000),hostile);var ally=vehicle("Ground friendly",new Vector3(0,0,1500),friendly);
            stage.SetActive(true);dark=false;OpticalCandidates.Add(ally);OpticalCandidates.Add(land);
            Check((bool)Call(c,"GroundVisible",land),"Native daytime ground LOS supplies optical evidence");
            choose();Check((Unit)Get(c,"target")==land&&(bool)c.GetType().GetProperty("GroundMode",F).GetValue(c),"Hostile ground vehicle selected without faction-contact prerequisite; friendly ignored");
            Check(!(bool)Call(c,"Eligible",ally),"Friendly ground vehicle cannot become an AP target");
            dark=true;Check(!(bool)Call(c,"GroundVisible",land),"Dark unilluminated ground target cannot supply optical evidence");dark=false;
            var original=land.transform.position;land.transform.position=new Vector3(0,0,heavy?6100:4100);Check(!(bool)Call(c,"GroundVisible",land),"Ground range remains shorter than aerial envelope");land.transform.position=new Vector3(0,0,99);Check(!(bool)Call(c,"GroundVisible",land),"Ground target inside100m safety minimum excluded");land.transform.position=original;
            var wall=new GameObject("Direct-fire obstruction");wall.transform.SetParent(stage.transform,false);wall.layer=6;wall.transform.position=new Vector3(0,0,1000);wall.AddComponent<BoxCollider>().size=new Vector3(500,100,20);Physics.SyncTransforms();
            Check(!(bool)Call(c,"GroundVisible",land),"Real static obstruction blocks ground optical LOS");UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
            Call(c,"Observe",Time.time-.8f);land.transform.position+=Vector3.right*8;Call(c,"Observe",Time.time);
            Set(c,"nextAim",0f);for(int i=0;i<3;i++){Set(c,"nextAim",0f);Call(c,"StepGround",Time.time);GroundAlign(c,turret,gun);}
            Check((bool)Get(c,"planned")&&(float)c.GetType().GetProperty("Fuse",F).GetValue(c)==30,"Ground observations reach ballistic AP plan rather than timed suppression");
            Check((bool)c.GetType().GetProperty("ReadyToFire",F).GetValue(c),"Fresh visible ground plan reaches native fire gate");
            groundServer=false;Check(!(bool)c.GetType().GetProperty("ReadyToFire",F).GetValue(c),"Remote nonserver controller cannot authorize ground damage");groundServer=true;
            Set(gun,"timeUntilReload",0f);Set(gun,"bulletsLoaded",1);Set(gun,"ticksSinceTriggerPull",100);
            Call(c,"StepGround",Time.time);Check((int)Get(gun,"ticksSinceTriggerPull")==0&&(bool)c.GetType().GetProperty("APQueued",F).GetValue(c),"Native Fire queues the AP round through mechanical/ammunition gate");
            var simObject=new GameObject("AP bullet backend");simObject.transform.SetParent(stage.transform,false);var sim=simObject.AddComponent<BulletSim>();Set(sim,"gun",gun);Set(gun,"bulletSim",sim);Call(gun,"Awake");
            h.Patch(typeof(BulletSim).GetMethod("AddBullet",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundCapture")));
            Call(gun,"SpawnBullet",0f);Check(groundSpawns==1,"Native spawn independently verified, not inferred from effects");
            h.Unpatch(typeof(BulletSim).GetMethod("AddBullet",F),HarmonyPatchType.Prefix,h.Id);
            // Re-arm only the test's mechanics after the real shot, then test
            // immutable native trace profiles independently of the renderer.
            Call(c,"CueRound");Set(sim,"visualOnly",false);
            object[] add={sim,0f,24f,true,land,null};Static(flak,"Add",add);var pending=add[5];
            var bullet=new BulletSim.Bullet(Vector3.forward*gun.info.muzzleVelocity,null,30,false,false,null){active=true,position=new Vector3(15000,10000,15000).ToGlobalPosition(),velocity=Vector3.forward*gun.info.muzzleVelocity,destructTimer=30,explosive=true,proximityFuse=true,target=land};
            var water=(ParticleEffectManager.PrefabEffect)FormatterServices.GetUninitializedObject(typeof(ParticleEffectManager.PrefabEffect));var impacts=new ParticleEffectManager.PrefabEffect[4];impacts[2]=water;
            object[] trace={bullet,gun.info,owner,impacts,false};Static(flak,"TraceStart",trace);var profile=(WeaponInfo)trace[1];var routed=(ParticleEffectManager.PrefabEffect[])trace[3];
            Check(profile!=gun.info&&profile.pierceDamage==(heavy?4200:2400)&&profile.blastDamage==0&&!bullet.explosive&&!bullet.proximityFuse&&bullet.target==null,"AP trace owns kinetic native profile and clears explosive/proximity state");
            Check(profile.muzzleVelocity==gun.info.muzzleVelocity&&profile.dragCoef==gun.info.dragCoef&&profile.gravMult==gun.info.gravMult,"AP preserves native gravity/drag/speed");
            Check(routed[0]!=null&&ReferenceEquals(routed[0],routed[1])&&ReferenceEquals(routed[2],water),"Compact impact routing preserves native water splash only");
            Static(flak,"Added",null,pending);
            trace=new object[]{bullet,gun.info,null,impacts,false};Static(flak,"TraceStart",trace);Check(!(bool)trace[4]&&ReferenceEquals(trace[1],profile),"Launched authoritative AP retains damage profile after source owner disappears");
            // A new aircraft has immediate priority without mutating this AP.
            var aircraft=new GameObject("Priority aircraft").AddComponent<Aircraft>();aircraft.transform.SetParent(stage.transform,false);aircraft.transform.position=new Vector3(0,1600,2500);Teams[aircraft]=hostile;Set(aircraft,"visibility",6000f);Set(aircraft,"disabled",false);OpticalCandidates.Add(aircraft);
            choose();Check((Unit)Get(c,"target")==aircraft&&!(bool)c.GetType().GetProperty("GroundMode",F).GetValue(c),"Detected air threat preempts ground engagement");
            trace=new object[]{bullet,gun.info,owner,impacts,false};Static(flak,"TraceStart",trace);Check(ReferenceEquals(trace[1],profile)&&profile.pierceDamage==(heavy?4200:2400),"Already-launched AP remains AP after switching to aerial fire");
            // Expiry must never produce native flak damage or puff.
            int before=damage;bullet.active=false;bullet.impacted=false;bullet.destructTimer=0;Static(flak,"TraceEnd",bullet);Check(damage==before&&!profile,"AP expiry has no area fragmentation and releases profile");
            Set(c,"target",land);Call(c,"CueRound");add=new object[]{sim,0f,24f,true,land,null};Static(flak,"Add",add);var flight=Get(add[5],"Flight");var second=(WeaponInfo)Get(flight,"APInfo");Check(second,"Another AP launch has its own profile");Static(flak,"Added",new Exception("isolated launch failure"),add[5]);Check(!second,"Failed launch finalizer releases unbound AP profile");
            groundServer=false;add=new object[]{sim,0f,24f,true,land,null};Static(flak,"Add",add);Check(Get(Get(add[5],"Flight"),"APInfo")==null,"Nonserver cannot create damaging AP profile");Static(flak,"Added",null,add[5]);groundServer=true;
            // Actual native penetration damage against a real unit part.
            var victim=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer.prefab"),stage.transform,false);victim.transform.position=new Vector3(15000,10000,15020);var victimUnit=victim.GetComponent<Unit>();Teams[victimUnit]=hostile;
            var collider=victim.GetComponentsInChildren<Collider>(true).First(x=>!x.isTrigger&&x.GetComponent<UnitPart>());groundDamagePart=collider.GetComponent<UnitPart>();
            byte id=0;foreach(var part in victim.GetComponentsInChildren<UnitPart>(true)){
                part.id=id++;groundParts[part.id]=part;part.parentUnit=victimUnit;typeof(UnitPart).GetProperty("xform",F).SetValue(part,part.transform);Set(part,"rb",victim.GetComponent<Rigidbody>());Set(part,"hitPoints",100f);
                var serialized=new SerializedObject(part);serialized.FindProperty("damageEffects").arraySize=0;serialized.ApplyModifiedPropertiesWithoutUndo();
            }Set(victimUnit,"disabled",false);
            h.Patch(typeof(Unit).GetMethod("RpcDamage",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundRpc")));h.Patch(typeof(Unit).GetMethod("ReportKilled",F),prefix:new HarmonyMethod(typeof(F88FlakIntegration).GetMethod("GroundKill")));
            Set(c,"target",land);Call(c,"CueRound");add=new object[]{sim,0f,24f,true,land,null};Static(flak,"Add",add);var hitProfile=(WeaponInfo)Get(Get(add[5],"Flight"),"APInfo");
            Physics.SyncTransforms();var hit=new BulletSim.Bullet(Vector3.forward*gun.info.muzzleVelocity,null,30,false,false,null){active=true,position=(collider.bounds.center-Vector3.forward*(collider.bounds.extents.z+2)).ToGlobalPosition(),velocity=Vector3.forward*gun.info.muzzleVelocity,destructTimer=30};
            hit.TrajectoryTrace(null,gun.info,owner,new ParticleEffectManager.PrefabEffect[4],1,false,.02f,false,default(GlobalPosition),Vector3.zero,1,false,default(GlobalPosition));Static(flak,"Added",null,add[5]);
            Check(hit.impacted&&groundDamageCalls>0&&groundParts.Values.Any(p=>(float)Get(p,"hitPoints")<100),"Native AP projectile impact penetrates and damages actual vehicle part");Static(flak,"Remove",hit);Check(!hitProfile,"Hit/retirement releases AP profile");
            var messageType=a.GetType("GunmageddonAudio.FlakAPImpact");var message=Activator.CreateInstance(messageType);Set(message,"Point",new GlobalPosition(1,2,3));Set(message,"Velocity",Vector3.forward*800);Set(message,"Source",123u);Set(message,"Time",123d);
            var bytes=(byte[])typeof(Mirage.Serialization.MessagePacker).GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1).MakeGenericMethod(messageType).Invoke(null,new[]{message});var copy=typeof(Mirage.Serialization.MessagePacker).GetMethod("Unpack").MakeGenericMethod(messageType).Invoke(null,new object[]{bytes,null});
            Check((uint)Get(copy,"Source")==123&&((Vector3)Get(copy,"Velocity")).z==800&&(double)Get(copy,"Time")==123d,"Native Mirage AP impact codec round-trip");
            Static(network,"ReceiveAPImpact",null,message);Check(groundDamageCalls>0&&damage==before,"Unconnected receiver cannot introduce AP damage");
            var buildingObject=new GameObject("Ground structure");buildingObject.transform.SetParent(stage.transform,false);var building=buildingObject.AddComponent<Building>();
            var buildingDefinition=ScriptableObject.CreateInstance<BuildingDefinition>();buildingDefinition.jsonKey="GroundStructureFixture";buildingDefinition.height=4;((Unit)building).definition=buildingDefinition;definitions.Add(buildingDefinition);Teams[building]=hostile;Set(building,"visibility",6000f);Set(building,"disabled",false);building.transform.position=new Vector3(100,0,2000);
            Check((bool)Call(c,"Eligible",building)&&(bool)Call(c,"GroundVisible",building),"Visible hostile structure is a valid direct-fire AP target");
            var special=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer Special.prefab"),stage.transform,false);special.SetActive(false);
            var specialTurret=special.GetComponentInChildren<Turret>(true);var specialController=specialTurret.gameObject.AddComponent(c.GetType());Call(specialController,"Awake");
            Check(!(bool)c.GetType().GetProperty("GroundEnabled",F).GetValue(specialController),"Special F120 incendiary variant cannot acquire AP capability");
            Set(c,"target",land);Call(c,"CueRound");add=new object[]{sim,0f,24f,true,land,null};Static(flak,"Add",add);
            var leased=new BulletSim.Bullet(Vector3.forward*800,null,30,false,false,null){active=true,position=new GlobalPosition(15000,10000,15000),velocity=Vector3.forward*800,destructTimer=30};
            trace=new object[]{leased,gun.info,owner,impacts,false};Static(flak,"TraceStart",trace);var retained=(WeaponInfo)trace[1];Static(flak,"Added",null,add[5]);
            Static(flak,"ClearFlights");Check(!retained&&((IDictionary)flak.GetField("Flights",S).GetValue(null)).Count==0,"Mission cleanup releases active AP profiles and native flights");
            return new{passed=true,checks,profile=heavy?"F120 AP":"F88 AP",groundSpawns,groundDamageCalls,details="Ground selection/air priority, native optical obstruction, ballistic plan, native Fire/SpawnBullet, immutable AP and actual native vehicle damage; mission/MP timing pending"};
        }finally{if(groundAssets){UnityEngine.Object.DestroyImmediate(groundAssets.WaterMaterial);UnityEngine.Object.DestroyImmediate(groundAssets.terrainMaterial);UnityEngine.Object.DestroyImmediate(groundAssets);}groundAssets=null;groundParts.Clear();groundManager=null;groundDamagePart=null;groundServer=true;foreach(var d in definitions)UnityEngine.Object.DestroyImmediate(d);}
    }
}
public static class F88GroundIntegration{public static object Run(){return F88FlakIntegration.RunGround();}}
public static class F120GroundIntegration{public static object Run(){return F88FlakIntegration.RunHeavyGround();}}
