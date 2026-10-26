using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HarmonyLib;
public static class R230Integration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly Dictionary<Unit,FactionHQ> Teams=new Dictionary<Unit,FactionHQ>();static readonly HashSet<Unit> Blocked=new HashSet<Unit>();static int checks,spawnRequests;static bool local=true,server=true;
    static NuclearOption.Networking.NetworkManagerNuclearOption manager;static UnitPart damaged;
    public static bool Manager(ref NuclearOption.Networking.NetworkManagerNuclearOption __result){__result=manager;return false;}
    public static bool Skip(){return false;}
    public static bool Visibility(ref Cysharp.Threading.Tasks.UniTask __result){__result=default(Cysharp.Threading.Tasks.UniTask);return false;}
    public static bool AddBullet(Vector3 __1,float __2,float __3){spawnRequests++;Check(Mathf.Abs(__1.magnitude-15000)<1&&Mathf.Abs(__2-.003f)<.00001f&&__3==16,"Native SpawnBullet passes velocity, dispersion and life to BulletSim");return false;}
    public static bool DamageRpc(byte __0,DamageInfo __1){if(damaged)((IDamageable)damaged).ApplyDamage(NetworkFloatHelper.Decompress(__1.pierceDamage),NetworkFloatHelper.Decompress(__1.blastDamage),NetworkFloatHelper.Decompress(__1.fireDamage),NetworkFloatHelper.Decompress(__1.impactDamage));return false;}
    public static bool Team(Unit __instance,ref FactionHQ __result){return !Teams.TryGetValue(__instance,out __result);}
    public static bool Local(ref bool __result){__result=local;return false;}
    public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=server;return false;}
    public static bool Sight(Unit __instance,Vector3 __0,float __1,ref bool __result){__result=!Blocked.Contains(__instance)&&Vector3.Distance(__0,__instance.transform.position)<=5000*__1;return false;}
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static object Call(object o,string n,params object[] args){return o.GetType().GetMethod(n,F).Invoke(o,args);}
    public static object Run()
    {
        checks=spawnRequests=0;local=server=true;Teams.Clear();Blocked.Clear();var defs=new List<UnityEngine.Object>();
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);var runtime=a.GetType("GunmageddonAudio.R230Runtime");
        var h=new Harmony("gunmaggedon.r230.fixture");h.Patch(typeof(Unit).GetProperty("NetworkHQ").GetMethod,prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Team")));h.Patch(typeof(Unit).GetProperty("LocalSim").GetMethod,prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Local")));h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Server")));h.Patch(typeof(Unit).GetMethod("LineOfSight"),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Sight")));
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("R230 isolated validation");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var oldDatum=datum.GetValue(null);var datumPos=typeof(Datum).GetField("<originPosition>k__BackingField",S);var oldPos=datumPos.GetValue(null);
        try{
            datum.SetValue(null,stage.transform);datumPos.SetValue(null,Vector3.zero);runtime.GetMethod("Install",S).Invoke(null,new object[]{h});
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230_Emplacement.prefab");Check(asset,"Saved R230 building exists");var root=UnityEngine.Object.Instantiate(asset,stage.transform,false);var unit=root.GetComponent<Building>();var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);
            Check(unit.definition.jsonKey=="Gunmaggedon_R230"&&unit.definition.unitName=="R-230"&&gun.info.name=="R230_Railgun","Unique building/weapon identities and designation");
            Check(gun.info.targetRequirements.maxRange==65000&&gun.info.dragCoef==0&&gun.info.muzzleVelocity==15000,"Native 65km railgun parameters");
            Check(root.GetComponentsInChildren<Collider>(true).All(c=>c.GetComponent<IDamageable>()!=null),"Every damage collision has native receiver");
            Check(root.GetComponentsInChildren<Transform>(true).All(t=>!t.name.ToLower().Contains("piston")),"Removed pistons are not reconstructed");
            var team=new GameObject("Friendly HQ").AddComponent<FactionHQ>();team.transform.SetParent(stage.transform,false);var enemy=new GameObject("Hostile HQ").AddComponent<FactionHQ>();enemy.transform.SetParent(stage.transform,false);Teams[unit]=team;
            runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});var c=turret.GetComponent(a.GetType("GunmageddonAudio.R230Controller"));Check(c,"Native initialize attaches controller");Call(c,"Awake");
            Func<string,bool,Vector3,Unit> entity=(key,missile,pos)=>{var go=new GameObject(key,typeof(Mirage.NetworkIdentity));go.transform.SetParent(stage.transform,false);go.transform.position=pos;go.AddComponent<Rigidbody>().useGravity=false;var u=missile?(Unit)go.AddComponent<Missile>():go.AddComponent<Aircraft>();var d=missile?(UnitDefinition)ScriptableObject.CreateInstance<MissileDefinition>():ScriptableObject.CreateInstance<AircraftDefinition>();d.jsonKey=key;d.value=100;u.definition=d;typeof(Unit).GetField("<rb>k__BackingField",F).SetValue(u,go.GetComponent<Rigidbody>());defs.Add(d);Teams[u]=enemy;return u;};
            var fighter=entity("FighterFixture",false,new Vector3(0,3000,6000));var dark=entity("Darkreach",false,new Vector3(0,3000,60000));var alkyon=entity("FastBomber1",false,new Vector3(0,3000,50000));var missile=entity("ballisticMissile1",true,new Vector3(0,15000,50000));var terminal=entity("ballisticMissile1_tacNuke",true,new Vector3(0,10000,55000));
            stage.SetActive(true);terminal.rb.velocity=new Vector3(0,-1000,-2000);missile.rb.velocity=Vector3.up*1000;
            Func<List<Unit>,Unit> select=list=>(Unit)Call(c,"Select",list);
            Check(select(new List<Unit>{fighter,dark})==dark,"Distant strategic bomber overrides close fighter");Check(select(new List<Unit>{fighter,alkyon})==alkyon,"Alkyon override");
            Check(select(new List<Unit>{dark,missile})==dark,"Climbing Piledriver yields to incoming bomber");Check(select(new List<Unit>{terminal,missile,dark,fighter})==terminal,"Terminal Piledriver is first priority");
            Blocked.Add(terminal);Check(select(new List<Unit>{terminal,missile})==missile,"Blocked LOS cannot establish priority");Blocked.Clear();
            Teams[terminal]=team;Check(select(new List<Unit>{terminal,missile})==missile,"Friendly Piledriver rejected");Teams[terminal]=enemy;
            terminal.transform.position=new Vector3(0,0,65020);Check(select(new List<Unit>{terminal,fighter})==fighter,"Out-of-range override rejected");terminal.transform.position=new Vector3(0,10000,55000);
            terminal.transform.position=new Vector3(0,50000,20000);Check(select(new List<Unit>{terminal,missile,dark,fighter})==terminal,"Descending TBM at 50km receives terminal override within 65km LOS envelope");
            var velocity=terminal.rb.velocity;Call(c,"Acceleration",terminal,velocity,100f);
            Vector3 estimated=Vector3.zero;for(int i=1;i<=30;i++)estimated=(Vector3)Call(c,"Acceleration",terminal,velocity+Physics.gravity*(i*.02f),100f+i*.02f);
            Check(Vector3.Distance(estimated,Physics.gravity)<.1f,"Measured terminal acceleration converges from velocity samples");
            Check((Vector3)Call(c,"Acceleration",fighter,Vector3.zero,101f)==Vector3.zero,"Aircraft retain constant-velocity lead");
            Call(c,"Acceleration",missile,Vector3.up*1000,101.02f);Check(((Vector3)Get(c,"targetAcceleration")).sqrMagnitude<.001f,"Target switch resets previous missile acceleration");
            terminal.transform.position=new Vector3(0,10000,55000);
            // Compare the lead to real PhysX descent and native Bullet trajectory,
            // without putting test colliders into the user's/default physics scene.
            var falling=new GameObject("Isolated 50km ballistic descent");SceneManager.MoveGameObjectToScene(falling,scene);var body=falling.AddComponent<Rigidbody>();body.useGravity=true;body.drag=0;
            var start=new Vector3(20000,20000,20000);var relative=new Vector3(10000,50000,10000);var initialVelocity=new Vector3(-300,-1800,-600);body.position=start+relative;body.velocity=initialVelocity;
            var vectorType=a.GetType("GunmageddonAudio.OpticalVector");Func<Vector3,object> vector=x=>Activator.CreateInstance(vectorType,new object[]{(double)x.x,(double)x.y,(double)x.z});
            float step=Time.fixedDeltaTime;object[] arguments={vector(relative),vector(initialVelocity),vector(Physics.gravity),15000d,9.81d,(double)step,null,0d};
            Check((bool)a.GetType("GunmageddonAudio.R230Solution").GetMethod("InterceptAccelerated").Invoke(null,arguments),"50km ballistic lead exists");
            var aim=arguments[6];var launch=new Vector3((float)(double)vectorType.GetField("X").GetValue(aim),(float)(double)vectorType.GetField("Y").GetValue(aim),(float)(double)vectorType.GetField("Z").GetValue(aim));
            var bullet=new BulletSim.Bullet(launch*15000,null,1,true,false,null){active=true,position=start.ToGlobalPosition(),velocity=launch*15000,destructTimer=16,explosive=false};
            float closest=float.MaxValue;var physics=scene.GetPhysicsScene();
            for(int i=0;i*step<(double)arguments[7]+step;i++){
                Vector3 before=bullet.position.ToLocalPosition()-body.position;physics.Simulate(step);
                bullet.TrajectoryTrace(null,gun.info,unit,new ParticleEffectManager.PrefabEffect[4],1,false,step,false,default(GlobalPosition),Vector3.zero,1,true,bullet.position);
                Vector3 after=bullet.position.ToLocalPosition()-body.position,change=after-before;float fraction=change.sqrMagnitude>0?Mathf.Clamp01(-Vector3.Dot(before,change)/change.sqrMagnitude):0;closest=Mathf.Min(closest,(before+change*fraction).magnitude);
            }
            Check(closest<.15f,"Native bullet and PhysX ballistic descent meet within 15cm; miss="+closest);bullet.Remove();
            fighter.transform.position=new Vector3(0,1000,3000);Check(select(new List<Unit>{fighter})==null,"Targets inside 5km are not acquired");fighter.transform.position=new Vector3(0,3000,8000);Set(c,"target",fighter);Set(c,"nextScan",float.MaxValue);var originalPose=turret.transform.localRotation;Call(c,"FixedUpdate");Check(!(bool)Get(c,"solution")&&turret.transform.localRotation==originalPose&&Get(c,"warning")==null,"New visible target cannot trigger immediate aiming or warning");Call(Get(c,"reaction"),"Track",fighter.GetInstanceID(),true,(double)Time.time+5.01);Call(c,"FixedUpdate");Check((bool)Get(c,"solution"),"Finite native aim solution from latest aircraft state");
            var dir=(Vector3)Get(c,"direction");var yaw=turret.transform;var pitch=gun.transform;var v=yaw.parent.InverseTransformDirection(dir);yaw.localRotation=Quaternion.Euler(0,Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg,0);pitch.localRotation=Quaternion.Euler(-Mathf.Atan2(v.y,new Vector2(v.x,v.z).magnitude)*Mathf.Rad2Deg,0,0);
            // Allow the solver to refresh after the fixture applies the aim pose.
            Call(c,"FixedUpdate");Check(!(bool)c.GetType().GetProperty("CanFire",F).GetValue(c),"Alignment alone cannot bypass new-target acquisition");Call(Get(c,"acquisition"),"Track",fighter.GetInstanceID(),true,(double)Time.time+.76);typeof(Gun).GetField("bulletsLoaded",F).SetValue(gun,1);typeof(Gun).GetField("timeUntilReload",F).SetValue(gun,0f);gun.enabled=false;Call(Get(c,"capacitor"),"Tick",fighter.GetInstanceID(),true,(double)Time.time-2.01);Check((bool)c.GetType().GetProperty("CanFire",F).GetValue(c),"Loaded idle disabled gun remains ready for native Fire to enable it");
            local=false;Check(!(bool)c.GetType().GetProperty("CanFire",F).GetValue(c),"Remote client cannot authorize damage");local=true;server=false;Check(!(bool)c.GetType().GetProperty("CanFire",F).GetValue(c),"Non-server cannot authorize firing");server=true;
            var loadedField=typeof(Gun).GetField("bulletsLoaded",F);var reload=typeof(Gun).GetField("timeUntilReload",F);Check((int)loadedField.GetValue(gun)<=1,"Single-round native magazine");
            var sounds=new SerializedObject(gun).FindProperty("fireSounds");Check(sounds.arraySize==4&&sounds.GetArrayElementAtIndex(0).objectReferenceValue,"Four stock railgun sound variations assigned");Check(new SerializedObject(gun).FindProperty("muzzleParticles").arraySize>=2,"Muzzle presentation assigned");
            Check(gun.GetComponents<AudioSource>().Any(s=>s.spatialBlend==1&&!s.playOnAwake),"Native spatial firing AudioSource present");
            var managerGo=new GameObject("Isolated network facade");managerGo.SetActive(false);managerGo.transform.SetParent(stage.transform,false);manager=managerGo.AddComponent<NuclearOption.Networking.NetworkManagerNuclearOption>();manager.Server=managerGo.AddComponent<Mirage.NetworkServer>();
            h.Patch(typeof(NuclearOption.Networking.NetworkManagerNuclearOption).GetProperty("i",S|BindingFlags.FlattenHierarchy).GetMethod,prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Manager")));
            h.Patch(typeof(Weapon).GetMethod("TrackFiringVisibility",F),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Visibility")));
            h.Patch(typeof(Unit).GetMethod("SingleRemoteFire",F),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Skip")));
            h.Patch(typeof(Gun).GetMethod("ShotSound",F),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("Skip")));
            h.Patch(typeof(BulletSim).GetMethod("AddBullet",F),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("AddBullet")));
            var simGo=new GameObject("Isolated railgun bullet backend");simGo.transform.SetParent(stage.transform,false);var sim=simGo.AddComponent<BulletSim>();typeof(Gun).GetField("bulletSim",F).SetValue(gun,sim);Call(gun,"Awake");gun.enabled=true;
            loadedField.SetValue(gun,1);reload.SetValue(gun,0f);gun.enabled=false;typeof(Gun).GetField("ticksSinceTriggerPull",F).SetValue(gun,100);Call(c,"FixedUpdate");Check(gun.enabled&&(int)typeof(Gun).GetField("ticksSinceTriggerPull",F).GetValue(gun)==0,"Native Fire accepts current precision solution");
            Call(gun,"SpawnBullet",0f);Check(spawnRequests==1,"Native shot reaches bullet simulation, independently of audio/flash");
            Check(Get(gun,"recoilTransform")==null&&(float)Get(gun,"recoilTravel")==0f&&(float)Get(gun,"recoilImpulse")==0f,"R230 native recoil disconnected without disabling gun or muzzle");
            Check((int)Get(c,"capacitor").GetType().GetProperty("Remaining").GetValue(Get(c,"capacitor"))==2,"Native shot debits capacitor once");
            var warning=(Component)Get(c,"warning");Check(warning,"Charge warning presentation created");var laser=(LineRenderer)Get(warning,"beam");Check(laser&&!laser.useWorldSpace&&laser.positionCount==2&&laser.transform.parent.name=="Muzzle","Two-point warning beam emitted by actual muzzle");
            Check(!laser.enabled&&!((LineRenderer)Get(warning,"beamHalo")).enabled&&!((MeshRenderer)Get(warning,"glow")).enabled&&!((MeshRenderer)Get(warning,"auraGlow")).enabled,"Native discharge clears both beam layers and both aim-point layers");
            Check((bool)Call(warning,"TryBindNative"),"Installed native laser renderer/material/texture must resolve for visual certification");
            var nativeBeam=(MeshRenderer)Get(warning,"nativeBeam");var nativeMaterial=(Material)Get(warning,"nativeMaterial");var template=(Material)warning.GetType().GetField("nativeTemplate",S).GetValue(null);
            const string laserTexture="_Triplanar_e8f423b0745f4324b5f1c2df6a5d2320_Texture_1_Texture2D";
            Check(nativeMaterial!=template&&nativeMaterial.shader==template.shader&&nativeMaterial.shader.name=="Shader Graphs/LaserCone","Private copy uses native laser shader");
            Check(nativeMaterial.GetTexture(laserTexture)&&nativeMaterial.GetTexture(laserTexture)==template.GetTexture(laserTexture),"Native beam texture is borrowed unchanged");
            Check(Mathf.Approximately(nativeMaterial.GetFloat("_Emissive"),template.GetFloat("_Emissive")*1.10f)&&!nativeBeam.enabled,"Native emission with only ten-percent boost; inactive warning stays hidden");
            Check(nativeMaterial.GetColor("_Color")==template.GetColor("_Color"),"Native HDR tint preserved without additional brightness multiplier");
            Check(laser.GetComponentsInChildren<Collider>(true).Length==0,"Danger orb and laser are nonphysical presentation only");
            h.Patch(typeof(Building).GetMethod("RpcDamage",F),prefix:new HarmonyMethod(typeof(R230Integration).GetMethod("DamageRpc")));
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)){damaged=collider.GetComponent<UnitPart>();typeof(UnitPart).GetProperty("xform",F).SetValue(damaged,damaged.transform);typeof(UnitPart).GetField("hitPoints",F).SetValue(damaged,100f);typeof(Unit).GetField("disabled",F).SetValue(unit,false);((IDamageable)damaged).TakeDamage(5000,0,1,0,0,PersistentID.None);Check(unit.Networkdisabled,"Every actual hitbox can disable the emplacement");}damaged=null;
            var lod=root.GetComponent<LODGroup>();var levels=lod.GetLODs();Check(lod.enabled&&levels.Length==3,"Three enabled model levels");int previous=int.MaxValue;
            foreach(var level in levels){int tris=level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);Check(tris<previous,"Reduced distant geometry");previous=tris;}
            Check(root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.transform.IsChildOf(laser.transform)).All(r=>levels.Any(l=>l.renderers.Contains(r))),"All model renderers accounted for; charge-only warning effects have separate lifetime");
            foreach(float angle in new[]{0,90,180,270})foreach(float elevation in new[]{-5,45,90}){yaw.localRotation=Quaternion.Euler(0,angle,0);pitch.localRotation=Quaternion.Euler(-elevation,0,0);foreach(var r in levels[0].renderers){float extent=(r.bounds.center-lod.transform.TransformPoint(lod.localReferencePoint)).magnitude+r.bounds.extents.magnitude;Check(extent<lod.size*.5f,"Aim pose remains within LOD bounds");}}
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BlueprinterSetupBackups/R230_Original.prefab");Check(original&&original.GetComponentsInChildren<MeshRenderer>(true).Length==3,"Original imported three-part model backed up");
            return new{passed=true,checks,spawnRequests,ballisticMiss=closest,triangles=levels.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray(),details="Native Fire/SpawnBullet reaches intercepted BulletSim backend, strict priority/range/faction/LOS/authority, damage, audio/effects and LOD bounds; live combat/transport unverified"};
        }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);foreach(var d in defs)UnityEngine.Object.DestroyImmediate(d);Teams.Clear();Blocked.Clear();datum.SetValue(null,oldDatum);datumPos.SetValue(null,oldPos);local=server=true;manager=null;damaged=null;}
    }
}
