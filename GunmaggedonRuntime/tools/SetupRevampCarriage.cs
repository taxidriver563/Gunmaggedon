using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupRevampCarriage
{
    public const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    public const string Prefab=Dir+"Revamp_Carriage_Rig.prefab";
    static string Workspace {get{return Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??"C:/Users/keena/OneDrive/Documents/Gunmaggedon";}}
    static Transform Part(Transform root,string name){return root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);}
    static Transform Pivot(string name,Transform parent,Vector3 local){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=local;return go.transform;}
    static Transform Wrap(string name,Transform parent,Transform part,Vector3 local){var pivot=Pivot(name,parent,local);part.SetParent(pivot,true);return pivot;}
    static void Box(Transform target,Renderer renderer,Vector3 shrink)
    {
        // All wrappers are unit scale; do not put primitives on 100x FBX parts.
        var box=target.gameObject.AddComponent<BoxCollider>();box.center=target.InverseTransformPoint(renderer.bounds.center);
        box.size=Vector3.Scale(renderer.bounds.size,shrink);
    }
    public static object Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode before setup");
        var scene=EditorSceneManager.GetActiveScene();var source=scene.GetRootGameObjects().SingleOrDefault(g=>g.name=="Gunmaggedon Revamp Carriage");
        if(!source)throw new Exception("Select the scene containing the untouched imported carriage");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new Exception("Existing rig requires an explicit migration; setup will not overwrite it");
        var backup=Path.Combine(Workspace,"UnitySetupBackup/RevampCarriage/BeforeRig");Directory.CreateDirectory(backup);
        PrefabUtility.SaveAsPrefabAsset(source,"Assets/BlueprinterSetupBackups/RevampCarriage_Original.prefab");
        if(!string.IsNullOrEmpty(scene.path))File.Copy(scene.path,Path.Combine(backup,"scene-before.unity"),true);
        File.Copy(Dir+"Gunmaggedon Revamp Carriage.fbx.meta",Path.Combine(backup,"carriage.fbx.meta"),true);
        var original=source.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r=>r.name,r=>r.localToWorldMatrix);
        var root=new GameObject("Gunmaggedon Revamp Carriage Rig");root.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        if(PrefabUtility.IsPartOfPrefabInstance(source))PrefabUtility.UnpackPrefabInstance(source,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        source.name="Model";source.transform.SetParent(root.transform,true);
        var body=root.AddComponent<Rigidbody>();body.mass=2000;body.useGravity=true;body.isKinematic=false;body.drag=.05f;body.angularDrag=.8f;
        body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.centerOfMass=new Vector3(0,1.05f,0);
        body.solverIterations=12;body.solverVelocityIterations=4;
        var collision=Pivot("ChassisCollision",root.transform,Vector3.zero);
        Box(collision,Part(source.transform,"back_support").GetComponent<Renderer>(),new Vector3(.94f,.85f,.98f));
        var pedestal=Pivot("PedestalContact",collision,Vector3.zero);var p=pedestal.gameObject.AddComponent<BoxCollider>();p.center=new Vector3(0,1.34f,0);p.size=new Vector3(1.78f,.85f,1.88f);
        // Compound collision follows the cross shape instead of filling its AABB.
        var sides=Pivot("StationarySupports",root.transform,Vector3.zero);
        var side=Part(source.transform,"side_support");side.SetParent(sides,true);Box(sides,side.GetComponent<Renderer>(),new Vector3(.98f,.85f,.94f));
        foreach(var name in new[]{"l_sidepiston","r_sidepiston"}){var t=Part(source.transform,name);var wrapper=Wrap(name+"Contact",sides,t,root.transform.InverseTransformPoint(t.position));Box(wrapper,t.GetComponent<Renderer>(),new Vector3(.8f,1,.8f));}
        foreach(var entry in new[]{new[]{"f_piston","FrontPiston"},new[]{"b_piston","RearPiston"}}){var t=Part(source.transform,entry[0]);var wrapper=Wrap(entry[1],root.transform,t,root.transform.InverseTransformPoint(t.position));Box(wrapper,t.GetComponent<Renderer>(),new Vector3(.8f,1,.8f));}
        var suspension=Pivot("Suspension",root.transform,Vector3.zero);var ids=new[]{"FL","FR","BL","BR"};
        foreach(var id in ids){
            string prefix=id.ToLowerInvariant();var corner=Pivot(id,suspension,Vector3.zero);
            var housing=Part(source.transform,prefix+"_suspension1");housing.SetParent(corner,true);
            foreach(var entry in new[]{new[]{"_suspension2a","UpperArm"},new[]{"_suspension2b","LowerArm"},new[]{"_suspension3","Upright"}}){var t=Part(source.transform,prefix+entry[0]);Wrap(entry[1],corner,t,root.transform.InverseTransformPoint(t.position));}
            var wheelMesh=Part(source.transform,prefix+"_wheel");var center=root.transform.InverseTransformPoint(wheelMesh.GetComponent<Renderer>().bounds.center);
            Wrap("WheelSpin",corner,wheelMesh,center);
            var contact=Pivot("WheelContact",corner,center+Vector3.up*.13f);var wheel=contact.gameObject.AddComponent<WheelCollider>();
            wheel.radius=.875f;wheel.mass=45;wheel.suspensionDistance=.24f;wheel.sprungMass=500;wheel.wheelDampingRate=.15f;wheel.forceAppPointDistance=.25f;
            wheel.suspensionSpring=new JointSpring{spring=49050,damper=7000,targetPosition=.5f};wheel.brakeTorque=20000;
            var forward=wheel.forwardFriction;forward.stiffness=1.1f;wheel.forwardFriction=forward;var sideways=wheel.sidewaysFriction;sideways.stiffness=1.25f;wheel.sidewaysFriction=sideways;
            wheel.ConfigureVehicleSubsteps(5,12,15);
        }
        var mount=Pivot("GunMount",root.transform,new Vector3(0,1.8f,0));Part(source.transform,"traverse").SetParent(mount,true);
        Pivot("TowHook",root.transform,new Vector3(0,1.3f,4.43f));Pivot("RearTowHook",root.transform,new Vector3(0,1.3f,-4.43f));
        Pivot("GroundReference",root.transform,Vector3.zero);Pivot("CenterOfMass",root.transform,body.centerOfMass);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)){
            var before=original[r.name];var after=r.localToWorldMatrix;for(int i=0;i<16;i++)if(Mathf.Abs(before[i]-after[i])>.001f)throw new Exception("Original mesh moved: "+r.name);
        }
        SetupUnitLODs.ConfigureCarriage(root);
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,Prefab,InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=root;
        var request=JsonUtility.FromJson<BuildRequest>(File.ReadAllText(Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/request.json")));
        EditorPrefs.SetString("Gunmaggedon.CarriagePreview.Assembly",request.assembly);
        File.Copy(Prefab,Path.Combine(backup,"rig-created.prefab"),true);
        return new{prefab=Prefab,mass=body.mass,wheels=4,radius=.875f,frontSteeringDegrees=28,pistonRetraction=.85f,modelPosesPreserved=original.Count,
            lods=prefab.GetComponent<LODGroup>().GetLODs().Select(l=>new{threshold=l.screenRelativeTransitionHeight,triangles=l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)}).ToArray()};
    }
    [Serializable] sealed class BuildRequest {public string assembly;}
    public static object FinalizeRig()
    {
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{var refs=root.transform.Find("RigReferences")??Pivot("RigReferences",root.transform,Vector3.zero);
            foreach(var entry in new[]{new[]{"FrontPiston","FrontPistonRest"},new[]{"RearPiston","RearPistonRest"}})
                if(!refs.Find(entry[1]))Pivot(entry[1],refs,root.transform.Find(entry[0]).localPosition);
            foreach(var wheel in root.GetComponentsInChildren<WheelCollider>(true))wheel.transform.localPosition=wheel.transform.parent.Find("WheelSpin").localPosition+Vector3.up*.13f;
            foreach(var name in new[]{"FrontPiston","RearPiston","StationarySupports/l_sidepistonContact","StationarySupports/r_sidepistonContact"}){
                var contact=root.transform.Find(name);var renderer=contact.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name!="GM_LOD1"&&r.name!="GM_LOD2");var box=contact.GetComponent<BoxCollider>();box.size=new Vector3(box.size.x,renderer.bounds.size.y,box.size.z);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        var request=JsonUtility.FromJson<BuildRequest>(File.ReadAllText(Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/request.json")));
        EditorPrefs.SetString("Gunmaggedon.CarriagePreview.Assembly",request.assembly);
        AssetDatabase.SaveAssets();var scene=EditorSceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{prefab=Prefab,restReferences=true,previewAssemblyConfigured=true};
    }
    public static object FinishEditorSetup()
    {
        var request=JsonUtility.FromJson<BuildRequest>(File.ReadAllText(Path.Combine(Workspace,"GunmaggedonRuntime/build/carriage/request.json")));
        EditorPrefs.SetString("Gunmaggedon.CarriagePreview.Assembly",request.assembly);
        const string bridge="Assets/Editor/GunmaggedonCarriageEditorBridge.cs";
        if(File.Exists(bridge)){
            if(!File.ReadAllText(bridge).Contains("Temporary editor-only carriage setup bridge"))throw new Exception("Unexpected bridge contents; preserving it");
            AssetDatabase.DeleteAsset(bridge);
        }
        AssetDatabase.ImportAsset("Assets/Editor/RevampCarriageEditorPreview.cs",ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();return new{previewMenus="Gunmaggedon/Revamp carriage",temporaryBridgeRemoved=true};
    }
}
