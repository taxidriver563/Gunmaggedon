using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
public static class F120RigRegression
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    static object Call(object o,string method,params object[] args){return o.GetType().GetMethod(method,F).Invoke(o,args);}
    public static object Run() { return RunVariant(false); }
    public static object RunSpecial() { return RunVariant(true); }
    static object RunVariant(bool special)
    {
        checks=0;var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));
        var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var motionType=assembly.GetType("GunmageddonAudio.F88TurretMotion");var runtime=assembly.GetType("GunmageddonAudio.F88Runtime");
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>((special?"Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer Special.prefab":"Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer.prefab"));Check(asset,"Saved F120 unit exists");
        var scene=EditorSceneManager.NewPreviewScene();var holder=new GameObject("Inactive F120 validation");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
        try{
            var root=UnityEngine.Object.Instantiate(asset,holder.transform,false);var yaw=root.transform.Find("F120YawPivot");var pitch=yaw.Find("PitchPivot");var gun=pitch.GetComponent<Gun>();
            Check(root.transform.localScale==Vector3.one&&root.GetComponentsInChildren<Rigidbody>(true).Length==1,"One unit-scale body; no nested imported body");
            Check(root.GetComponentsInChildren<WheelCollider>(true).Length==0,"Native GroundVehicle owns suspension without a competing WheelCollider solver");
            Check(root.GetComponent<GroundVehicle>().definition.jsonKey==(special?"Gunmaggedon_F120S_AA":"Gunmaggedon_F120_AA")&&gun.info.name==(special?"F120S_AA_Weapon":"F120_AA_Weapon"),"Distinct unit and ammunition keys");
            Check(yaw.GetComponent<TargetDetector>()&&new SerializedObject(yaw.GetComponent<TargetDetector>()).FindProperty("visualRange").floatValue==10000,"Visual rangefinder uses an improved finite optical range");
            Check(gun.info.targetRequirements.maxRange==20000&&gun.info.targetRequirements.maxAltitude==20000&&gun.info.targetRequirements.minAltitude==250,"Heavy AA engagement envelope");
            var motion=yaw.gameObject.AddComponent(motionType);Call(motion,"Awake");var door=pitch.Find("BreechSlidePivot");var rest=pitch.Find("BreechSlideRest");
            var visuals=pitch.Find("ReloadVisuals");var casing=visuals.Find("ExtractingCasing");var live=visuals.Find("LoadingShell");
            Check(!casing.gameObject.activeSelf&&!live.gameObject.activeSelf,"Only loading phases show temporary shells");
            Call(motion,"BeginReload");var cycle=motionType.GetField("cycle",F).GetValue(motion);double now=Time.time;
            Action<double> tick=t=>{Call(cycle,"Tick",t);Call(motion,"ApplyPose",t);};
            tick(now+.6);tick(now+.8);Check(door.localPosition.y>rest.localPosition.y&&Mathf.Abs(door.localPosition.x-rest.localPosition.x)<.0001f,"Breech opens UP, not sideways or backward");
            tick(now+1);Check(casing.gameObject.activeSelf&&!live.gameObject.activeSelf,"Open door precedes extraction");
            tick(now+1.8);Check(!casing.gameObject.activeSelf&&!live.gameObject.activeSelf,"Extraction ends before clearance pause");
            tick(now+3.799);Check(!live.gameObject.activeSelf,"Full two-second clearance before insertion");
            tick(now+3.8);Check(live.gameObject.activeSelf&&Mathf.Abs(door.localPosition.y-rest.localPosition.y-.38f)<.001f,"Door remains raised during insertion");
            tick(now+5.4);Check(!live.gameObject.activeSelf&&!(bool)motionType.GetProperty("Ready",F).GetValue(motion),"Seated shell alone cannot authorize fire");
            tick(now+6);Check(!(bool)motionType.GetProperty("Ready",F).GetValue(motion),"Closed door requires secured dwell");
            tick(now+6.2);Check((bool)motionType.GetProperty("Ready",F).GetValue(motion),"Secured breech enables readiness");
            var recoil=pitch.Find("RecoilPivot");var muzzle=recoil.Find("Muzzle");Check(new SerializedObject(gun).FindProperty("recoilTransform").objectReferenceValue==recoil,"Native recoil drives the actual barrel and muzzle");
            var doorBefore=door.position;var muzzleBefore=muzzle.position;recoil.localPosition=Vector3.back*.65f;Check(Vector3.Distance(door.position,doorBefore)<.0001f&&Vector3.Distance(muzzle.position,muzzleBefore)>.64f,"Door is independent of barrel recoil");recoil.localPosition=Vector3.zero;
            var gs=new SerializedObject(gun);Check(gs.FindProperty("muzzleParticles").arraySize==2&&gs.FindProperty("fireSounds").arraySize>0,"Flash/smoke and placeholder firing audio assigned");
            var spent=gs.FindProperty("ejectionPrefab").objectReferenceValue as GameObject;Check(spent&&spent.GetComponent<Rigidbody>()&&!spent.GetComponent<Unit>(),"Casing is a harmless independent physics prop");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BlueprinterSetupBackups/F120_Original.prefab");
            foreach(var renderer in original.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!r.name.StartsWith("GM_LOD")&&r.name!="shell_casing"&&r.name!="shell_loaded")){
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var current=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>!m.name.StartsWith("GM_LOD")&&m.sharedMesh==mesh&&m.GetComponent<MeshRenderer>()&&(special||m.GetComponent<MeshRenderer>().sharedMaterial==renderer.sharedMaterial));
                var before=renderer.localToWorldMatrix;var after=current.transform.localToWorldMatrix;for(int i=0;i<16;i++)Check(Mathf.Abs(before[i]-after[i])<.001f,"Authored mesh pose preserved: "+renderer.name);
            }
            var lod=root.GetComponent<LODGroup>();var levels=lod.GetLODs();Check(lod.enabled&&levels.Length>=3,"Enabled model LOD levels");
            int previous=int.MaxValue;foreach(var level in levels){int triangles=level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);Check(triangles<previous,"Strictly reduced distant geometry");previous=triangles;}
            Check(root.GetComponentsInChildren<MeshRenderer>(true).All(r=>levels.Any(l=>l.renderers.Contains(r))),"Every model renderer belongs to a LOD");
            lod.ForceLOD(2);Check(!casing.gameObject.activeSelf&&!live.gameObject.activeSelf,"Distant LOD cannot resurrect inactive loading shells");lod.ForceLOD(-1);
            foreach(float yawAngle in new[]{0,90,180,270})foreach(float elevation in new[]{0,45,85}){
                yaw.localRotation=Quaternion.Euler(0,yawAngle,0);pitch.localRotation=Quaternion.Euler(-elevation,0,0);
                foreach(var renderer in levels[0].renderers){var b=renderer.bounds;float extent=(b.center-lod.transform.TransformPoint(lod.localReferencePoint)).magnitude+b.extents.magnitude;Check(extent<=lod.size*.5f+.01f,"LOD bounds cover aimed silhouette: "+renderer.name+" needs "+extent+", radius "+lod.size*.5f);}
            }
            yaw.localRotation=Quaternion.identity;pitch.localRotation=Quaternion.identity;((Behaviour)motion).enabled=false;
            root.transform.SetParent(null,true);root.transform.position=Vector3.up*.04f;root.SetActive(true);
            var floor=new GameObject("Isolated F120 ground");SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=Vector3.down*.5f;floor.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
            var physics=scene.GetPhysicsScene();Check(!physics.Equals(Physics.defaultPhysicsScene),"Only isolated passive physics simulated");Physics.SyncTransforms();
            var rb=root.GetComponent<Rigidbody>();float maxHeight=rb.position.y;
            for(int i=0;i<300;i++){physics.Simulate(.02f);maxHeight=Mathf.Max(maxHeight,rb.position.y);}
            Check(maxHeight<.15f&&Mathf.Abs(rb.position.y)<.12f&&rb.velocity.magnitude<.2f,"Passive deployed body settles without launching: max="+maxHeight+", final="+rb.position.y);
            return new{passed=true,checks,doorTravel=.38f,passiveMaxHeight=maxHeight,passiveSettledHeight=rb.position.y,placeholderSound="88mm_Cannon_Fire_Close",triangles=levels.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray()};
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
public static class F120FlakIntegration {public static object Run(){return F88FlakIntegration.RunHeavy();}}
public static class F120FragmentsIntegration {public static object Run(){return F88FragmentsIntegration.RunHeavy();}}
public static class F120PresentationIntegration
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static object Run()
    {
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));
        var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var cases=a.GetType("GunmageddonAudio.F88Casings");var effects=a.GetType("GunmageddonAudio.F88FlakEffects");
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Heavy flak presentation validation");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var oldDatum=datum.GetValue(null);
        try{
            datum.SetValue(null,stage.transform);const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
            var small=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F88_SpentCasing.prefab");var large=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F120_SpentCasing.prefab");
            cases.GetMethod("Eject",S).Invoke(null,new object[]{small,stage.transform,null});var pool=(System.Collections.IList)cases.GetField("Pool",S).GetValue(null);var first=(Component)pool[0];first.gameObject.SetActive(false);
            cases.GetMethod("Eject",S).Invoke(null,new object[]{large,stage.transform,null});Check(pool.Count==2,"F120 cannot reuse an F88 mesh template");
            var second=(Component)pool[1];Check(second.GetComponentInChildren<MeshFilter>(true).sharedMesh==large.GetComponentInChildren<MeshFilter>(true).sharedMesh,"F120 ejected model matches authored casing");
            cases.GetMethod("Eject",S).Invoke(null,new object[]{small,stage.transform,null});Check(pool.Count==2&&first.gameObject.activeSelf,"F88 reuses its own casing while F120 remains active");
            Check(second.GetComponent<Rigidbody>().velocity.magnitude<1&&!second.GetComponent<Unit>(),"Heavy casing stays gentle and noncombat");
            var e88=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F88_Flak_Airburst.prefab");var e120=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F120_Flak_Airburst.prefab");
            effects.GetMethod("Play",S).Invoke(null,new object[]{e88,new GlobalPosition(0,1000,0),0f});effects.GetMethod("Play",S).Invoke(null,new object[]{e120,new GlobalPosition(100,1000,0),0f});
            var banks=(System.Collections.IDictionary)effects.GetField("Banks",S).GetValue(null);Check(banks.Count==2&&banks[e88]!=banks[e120],"F88 and F120 retain independent puff templates");
            var bank=banks[e120];var puffs=(System.Collections.IList)bank.GetType().GetField("Pool",F).GetValue(bank);var puff=(Component)puffs[0];
            var native= e120.GetComponentsInChildren<ParticleSystem>(true).First();var actual=puff.GetComponentsInChildren<ParticleSystem>(true).First();Check(Mathf.Approximately(actual.main.startSize.constantMax,native.main.startSize.constantMax),"F120 puff keeps its larger template settings");
            return new{passed=true,checks=6,details="Mixed F88/F120 casing and puff reuse retains each model/material/template; gentle independent physical casings"};
        }finally{try{cases.GetMethod("Shutdown",S).Invoke(null,null);effects.GetMethod("Shutdown",S).Invoke(null,null);}finally{datum.SetValue(null,oldDatum);EditorSceneManager.ClosePreviewScene(scene);}}
    }
}
