using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using GunmageddonAudio;

public static class RevampCarriageIntegration
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    const string PathName=Dir+"Revamp_Carriage_Rig.prefab";
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static string Workspace {get{return Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT before running editor tools or fixtures."));}}
    static RevampCarriageMotion Create(GameObject asset,Scene scene,Vector3 position)
    {
        var holder=new GameObject("Inactive fixture staging");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
        var rig=UnityEngine.Object.Instantiate(asset,holder.transform,false);rig.transform.position=position;rig.transform.SetParent(null,true);UnityEngine.Object.DestroyImmediate(holder);
        var motion=rig.AddComponent<RevampCarriageMotion>();motion.Initialize();return motion;
    }
    static void Tick(PhysicsScene physics,RevampCarriageMotion motion,int steps)
    {for(int i=0;i<steps;i++){motion.Advance(.02f);physics.Simulate(.02f);motion.ApplyVisuals();}}
    public static object Run()
    {
        checks=0;var asset=AssetDatabase.LoadAssetAtPath<GameObject>(PathName);Check(asset,"Saved rig exists");
        var body=asset.GetComponent<Rigidbody>();Check(body&&!body.isKinematic&&body.useGravity&&body.constraints==RigidbodyConstraints.None,"Free dynamic chassis, gravity, no frozen-axis workaround");
        Check(asset.GetComponentsInChildren<Rigidbody>(true).Length==1,"Single chassis rigidbody avoids internal joint/self-collision explosions");
        Check(asset.GetComponentsInChildren<WheelCollider>(true).Length==4,"Four independent suspension contacts");
        Check(asset.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"No editor/runtime script serialized into exported prefab");
        Check(asset.transform.Find("TowHook")&&asset.transform.Find("GunMount")&&asset.transform.Find("RearTowHook"),"Reusable attachment points");
        var levels=asset.GetComponent<LODGroup>().GetLODs();Check(levels.Length==3,"Three model detail levels");
        int previous=int.MaxValue;foreach(var level in levels){int triangles=level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);Check(triangles<previous,"Distant LOD actually reduces triangles");previous=triangles;foreach(var r in level.renderers)Check(r&&r.sharedMaterials.All(m=>m),"LOD preserves renderer/material ownership");}
        foreach(var r in asset.GetComponentsInChildren<MeshRenderer>(true))Check(levels.Any(l=>l.renderers.Contains(r)),"All carriage renderers managed by LOD");
        foreach(var wheel in asset.GetComponentsInChildren<WheelCollider>(true))Check(Mathf.Abs(wheel.radius-.875f)<.001f&&wheel.transform.lossyScale==Vector3.one,"Model-sized wheel radius at unit scale");
        bool auto=Physics.autoSimulation;var scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();Check(!physics.Equals(Physics.defaultPhysicsScene),"Only isolated preview physics is simulated");
        Physics.autoSimulation=false;
        try{
            var ground=new GameObject("Carriage test ground");SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(0,-.5f,0);ground.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
            var motion=Create(asset,scene,new Vector3(0,.03f,0));var rb=motion.GetComponent<Rigidbody>();Physics.SyncTransforms();
            float maximumY=rb.position.y;
            for(int i=0;i<300;i++){Tick(physics,motion,1);maximumY=Mathf.Max(maximumY,rb.position.y);}
            Check(maximumY<.2f,"Deployed spawn must not launch: max height "+maximumY);
            Check(Mathf.Abs(rb.position.y)<.12f&&rb.velocity.magnitude<.2f,"Deployed carriage settles: y="+rb.position.y+", speed="+rb.velocity.magnitude);
            Check(Mathf.Abs(motion.transform.Find("FrontPiston").GetComponent<BoxCollider>().bounds.min.y)<.025f,"Deployed feet contact the ground rather than floating on wheel springs");
            float settled=rb.position.y;var front=motion.transform.Find("FrontPiston");var deployed=front.localPosition;
            motion.SetTowed(true);Tick(physics,motion,70);
            Check(!motion.transform.Find("StationarySupports").gameObject.activeSelf,"Both side supports and side pistons disappear while towed");
            Check(motion.ReadyToTow,"Tow-ready gate waits for piston clearance");
            Check(Mathf.Abs(front.localPosition.y-deployed.y-.85f)<.001f,"Front piston retracts upward 0.85m");
            Check(Mathf.Abs(motion.transform.Find("RearPiston").localPosition.y-front.localPosition.y)<.001f,"Rear piston retracts with front");
            Check(!front.GetComponent<BoxCollider>().enabled&&!motion.transform.Find("RearPiston").GetComponent<BoxCollider>().enabled,"Retracted feet cannot scrape terrain");
            Check(motion.GetComponentsInChildren<WheelCollider>().All(w=>w.brakeTorque==0),"Towed wheels roll freely");
            var target=new GameObject("Tow direction fixture");SceneManager.MoveGameObjectToScene(target,scene);target.transform.position=motion.transform.TransformPoint(new Vector3(5,1.3f,10));motion.SetTowed(true,target.transform);motion.Advance(0);
            var wheels=motion.GetComponentsInChildren<WheelCollider>().OrderBy(w=>w.transform.parent.name).ToArray();
            Check(wheels.Where(w=>w.transform.parent.name.StartsWith("F")).All(w=>w.steerAngle>0&&w.steerAngle<=28.001f),"Front wheels steer toward tow direction with bounded lock: "+string.Join(",",wheels.Select(w=>w.transform.parent.name+"="+w.steerAngle)));
            Check(wheels.Where(w=>w.transform.parent.name.StartsWith("B")).All(w=>w.steerAngle==0),"Rear wheels remain unsteered");
            motion.SetTowed(true);Tick(physics,motion,100);var idlePosition=rb.position;Tick(physics,motion,100);Check(Vector3.Distance(idlePosition,rb.position)<.03f,"Free-rolling wake torque must not propel the unpowered carriage");var spin=motion.transform.Find("Suspension/FL/WheelSpin");var rotation=spin.rotation;
            for(int i=0;i<150;i++){rb.AddForce(motion.transform.forward*2200,ForceMode.Force);Tick(physics,motion,1);}
            Check(rb.position.z>1,"Native wheel physics permits physical towing/pulling: position="+rb.position+", velocity="+rb.velocity+", wheels="+string.Join(";",wheels.Select(w=>w.transform.parent.name+" rpm="+w.rpm+" brake="+w.brakeTorque+" grounded="+w.isGrounded))+", contacts="+string.Join(";",motion.GetComponentsInChildren<Collider>().Where(c=>c.enabled).Select(c=>c.name+" bottom="+c.bounds.min.y)));
            Check(wheels.Any(w=>Mathf.Abs(w.rpm)>1)&&Quaternion.Angle(rotation,spin.rotation)>1,"Visible wheels roll from actual native wheel RPM");
            Check(Mathf.Abs(rb.position.y)<.2f,"Rolling ride height stays near authored ground plane: "+rb.position.y);
            motion.SetTowed(false);Tick(physics,motion,80);
            Check(motion.Deployed&&motion.transform.Find("StationarySupports").gameObject.activeSelf,"Unhitch/deploy restores side supports");
            Check(Vector3.Distance(front.localPosition,deployed)<.001f&&front.GetComponent<BoxCollider>().enabled,"Piston returns exactly to deployed pose and collision");
            Check(motion.GetComponentsInChildren<WheelCollider>().All(w=>w.brakeTorque>0),"Deployed parking brakes restored");
            for(int cycle=0;cycle<4;cycle++){motion.SetTowed(true);motion.Advance(2);motion.SetTowed(false);motion.Advance(2);Check(front.localPosition==deployed,"Repeated stow/deploy has no transform drift");}
            UnityEngine.Object.DestroyImmediate(motion.gameObject);UnityEngine.Object.DestroyImmediate(target);
            // A local bump must produce different suspension travel at each wheel.
            var bump=new GameObject("Single-wheel bump");SceneManager.MoveGameObjectToScene(bump,scene);bump.transform.position=new Vector3(-1.744f,.06f,3.487f);bump.AddComponent<BoxCollider>().size=new Vector3(1,.12f,1.5f);
            motion=Create(asset,scene,new Vector3(0,.15f,0));motion.SetTowed(true);Physics.SyncTransforms();Tick(physics,motion,250);
            var fl=motion.transform.Find("Suspension/FL/WheelSpin");var fr=motion.transform.Find("Suspension/FR/WheelSpin");
            float difference=Mathf.Abs(fl.localPosition.y-fr.localPosition.y);Check(difference>.02f,"Independent suspension responds to one-wheel bump: "+difference);
            Check(Quaternion.Angle(motion.transform.Find("Suspension/FL/UpperArm").localRotation,Quaternion.identity)>1,"Suspension links visibly articulate");
            // Hiding a mechanical group must hide its lower-detail children too.
            motion.GetComponent<LODGroup>().ForceLOD(2);Check(motion.transform.Find("StationarySupports").GetComponentsInChildren<Renderer>(true).All(r=>!r.gameObject.activeInHierarchy),"LOD does not resurrect stowed parts");
            Render(asset,scene);
            var result=new{passed=true,checks,maximumSpawnHeight=maximumY,settledHeight=settled,independentWheelTravelDifference=difference,preview=Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/preview.png")};
            File.WriteAllText(Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/physics-validation.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result));return result;
        }finally{Physics.autoSimulation=auto;EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Render(GameObject asset,Scene scene)
    {
        var rt=new RenderTexture(640,400,24);rt.Create();var tile=new Texture2D(640,400,TextureFormat.RGB24,false);var sheet=new Texture2D(1280,400,TextureFormat.RGB24,false);var old=RenderTexture.active;
        var cameraObject=new GameObject("Rig render camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.cullingMask=1<<31;camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.19f);camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.fieldOfView=36;
        var lampObject=new GameObject("Rig preview sun",typeof(Light));SceneManager.MoveGameObjectToScene(lampObject,scene);var light=lampObject.GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<31;lampObject.transform.rotation=Quaternion.Euler(45,-35,0);
        var motion=Create(asset,scene,new Vector3(20,.03f,0));foreach(var t in motion.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        try{for(int mode=0;mode<2;mode++){motion.SetTowed(mode==1);Tick(scene.GetPhysicsScene(),motion,200);motion.GetComponent<LODGroup>().ForceLOD(0);camera.transform.position=new Vector3(32,10,14);camera.transform.LookAt(new Vector3(20,1,0));camera.Render();RenderTexture.active=rt;tile.ReadPixels(new Rect(0,0,640,400),0,0);tile.Apply();sheet.SetPixels(mode*640,0,640,400,tile.GetPixels());}sheet.Apply();File.WriteAllBytes(Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/preview.png"),sheet.EncodeToPNG());}
        finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(motion.gameObject);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(lampObject);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tile);UnityEngine.Object.DestroyImmediate(sheet);}
    }
}
