using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Creates a separate F120 from the user's mounted scene model and the native
// component layout of the F88. Never applies overrides to the shared carriage.
public static class SetupF120
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    public const string Prefab=Dir+"F120_AAA_Trailer.prefab";
    static void Set(UnityEngine.Object o,string name,object value){var s=new SerializedObject(o);var p=s.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" missing "+name);
        if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is string)p.stringValue=(string)value;else if(value is Vector3)p.vector3Value=(Vector3)value;else p.objectReferenceValue=value as UnityEngine.Object;s.ApplyModifiedPropertiesWithoutUndo();}
    static Transform Point(Transform parent,string name,Vector3 local){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=local;return t;}
    static void CenterMesh(Transform mesh,Transform wrapper){var center=mesh.GetComponent<Renderer>().bounds.center;wrapper.position=center;mesh.SetParent(wrapper,true);}
    static void Move(Transform mesh,Transform pivot){mesh.SetParent(pivot,true);}
    static void Curve(AnimationClip clip,string path,string member,float start,float open,float close,float end){clip.SetCurve(path,typeof(Transform),member,new AnimationCurve(new Keyframe(0,start),new Keyframe(3.4f,start),new Keyframe(3.8f,open),new Keyframe(8.2f,close),new Keyframe(8.8f,end),new Keyframe(10,end)));}
    static void PositionCurve(AnimationClip clip,string path,Vector3 start,Vector3 end,float begin,float finish){foreach(var axis in new[]{"x","y","z"}){int i=axis=="x"?0:axis=="y"?1:2;clip.SetCurve(path,typeof(Transform),"localPosition."+axis,new AnimationCurve(new Keyframe(0,start[i]),new Keyframe(begin,start[i]),new Keyframe(finish,end[i]),new Keyframe(10,end[i])));}}
    public static object Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new Exception("F120 already exists; inspect/migrate instead of replacing it.");
        var source=GameObject.Find("F120_AAA_Trailer");if(!source)throw new Exception("Mounted F120 scene root missing.");
        var workspace=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(workspace))throw new Exception("Set GUNMAGGEDON_ROOT.");
        var backup=Path.Combine(workspace,"UnitySetupBackup/F120/BeforeSetup");Directory.CreateDirectory(backup);
        File.Copy(source.scene.path,Path.Combine(backup,"mounted-scene.unity"),false);
        const string originalPath="Assets/BlueprinterSetupBackups/F120_Original.prefab";
        PrefabUtility.SaveAsPrefabAsset(source,originalPath);File.Copy(originalPath,Path.Combine(backup,"F120_Original.prefab"),false);
        // Capture only user-authored near meshes; generated LOD children are rebuilt.
        var before=source.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name!="GM_LOD1"&&r.name!="GM_LOD2").ToDictionary(r=>AnimationUtility.CalculateTransformPath(r.transform,source.transform),r=>r.localToWorldMatrix);
        var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;GameObject result=null;
        try{
            root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"F88_AA_Trailer.prefab"));root.SetActive(false);SceneManager.MoveGameObjectToScene(root,preview);root.name="F120_AAA_Trailer";
            var vehicle=root.GetComponent<GroundVehicle>();var body=root.GetComponent<Rigidbody>();
            var yaw=root.GetComponentInChildren<Turret>(true).transform;var turret=yaw.GetComponent<Turret>();var pitch=yaw.Find("PitchPivot");var gun=pitch.GetComponent<Gun>();
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true).ToArray())if(mesh)UnityEngine.Object.DestroyImmediate(mesh.gameObject);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(lod);
            var carriage=UnityEngine.Object.Instantiate(source,root.transform,false);carriage.name="Carriage";
            carriage.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);carriage.transform.localScale=source.transform.lossyScale;
            foreach(var rb in carriage.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(rb);
            foreach(var w in carriage.GetComponentsInChildren<WheelCollider>(true))UnityEngine.Object.DestroyImmediate(w);
            foreach(var lod in carriage.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(lod);
            // The mounted F120 model meshes are direct children of the scene root.
            Func<string,Transform> part=name=>carriage.transform.Find(name)??throw new Exception("Missing F120 part: "+name);
            var traverse=part("traverse");var barrel=part("barrel");var breech=part("breech");var caseMesh=part("shell_casing");var liveMesh=part("shell_loaded");
            Vector3 muzzlePosition=barrel.GetComponent<Renderer>().bounds.center;muzzlePosition.z=barrel.GetComponent<Renderer>().bounds.max.z+.04f;
            var casingBounds=caseMesh.GetComponent<Renderer>().bounds;var loadedBounds=liveMesh.GetComponent<Renderer>().bounds;var doorBounds=breech.GetComponent<Renderer>().bounds;
            yaw.name="F120YawPivot";yaw.position=traverse.position;pitch.position=part("gun").position;
            var recoil=pitch.Find("RecoilPivot");recoil.position=pitch.position;recoil.localRotation=Quaternion.identity;
            Move(traverse,yaw);Move(part("gun"),pitch);Move(barrel,recoil);
            var left=yaw.Find("PitchCrankPivot");left.position=part("elevation_crank").position;Move(part("elevation_crank"),left);
            var right=yaw.Find("YawCrankPivot");right.position=part("travese_crank").position;Move(part("travese_crank"),right);
            var slide=pitch.Find("BreechSlidePivot");var rest=pitch.Find("BreechSlideRest");slide.position=breech.position;rest.position=slide.position;Move(breech,slide);
            var visuals=pitch.Find("ReloadVisuals");var extracting=visuals.Find("ExtractingCasing");var loading=visuals.Find("LoadingShell");
            CenterMesh(caseMesh,extracting);CenterMesh(liveMesh,loading);
            Vector3 caseSeat=pitch.InverseTransformPoint(new Vector3(casingBounds.center.x,casingBounds.center.y,doorBounds.max.z+.06f+casingBounds.size.z*.5f));
            Vector3 loadSeat=pitch.InverseTransformPoint(new Vector3(loadedBounds.center.x,loadedBounds.center.y,doorBounds.max.z+.06f+loadedBounds.size.z*.5f));
            var caseClear=pitch.InverseTransformPoint(casingBounds.center);var loadStart=pitch.InverseTransformPoint(loadedBounds.center);
            visuals.Find("CasingSeated").localPosition=caseSeat;visuals.Find("CasingClear").localPosition=caseClear;visuals.Find("LoadingStart").localPosition=loadStart;visuals.Find("LoadingSeated").localPosition=loadSeat;
            extracting.localPosition=caseSeat;loading.localPosition=loadStart;extracting.gameObject.SetActive(false);loading.gameObject.SetActive(false);
            var muzzle=recoil.Find("Muzzle");muzzle.SetPositionAndRotation(muzzlePosition,Quaternion.identity);
            foreach(var p in muzzle.GetComponentsInChildren<ParticleSystem>(true)){var main=p.main;main.startSizeMultiplier*=1.25f;}
            var optics=yaw.GetComponentsInChildren<TargetDetector>(true).First();
            var opticsTransform=new SerializedObject(optics).FindProperty("scanner").objectReferenceValue as Transform;
            if(opticsTransform){opticsTransform.name="F120OpticalRangefinder";opticsTransform.position=new Vector3(1.45f,4.3f,-.1f);}
            Set(optics,"visualRange",10000f);Set(turret,"traverseRate",20f);Set(turret,"elevationRate",16f);
            if(!AssetDatabase.CopyAsset(Dir+"F88_AA_Weapon.asset",Dir+"F120_AA_Weapon.asset"))throw new Exception("Weapon copy failed.");
            var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir+"F120_AA_Weapon.asset");
            Set(info,"weaponName","120mm Timed Heavy Flak");Set(info,"shortName","120mm Flak");Set(info,"description","Postwar optical fire control, short committed predictions and a broad 240m fragmentation envelope. Timed shells, not proximity or homing rounds.");
            Set(info,"muzzleVelocity",950f);Set(info,"dragCoef",.035f);Set(info,"massPerRound",22f);Set(info,"pierceDamage",1200f);Set(info,"blastDamage",1.5f);Set(info,"fireInterval",6.2f);
            Set(info,"targetRequirements.minRange",600f);Set(info,"targetRequirements.maxRange",20000f);Set(info,"targetRequirements.minAltitude",250f);Set(info,"targetRequirements.maxAltitude",20000f);
            Set(gun,"info",info);Set(gun,"ammo",80);Set(gun,"magazines",80);Set(gun,"magazineCapacity",1);Set(gun,"reloadTime",6.2f);Set(gun,"recoilTravel",.65f);Set(gun,"bulletSpread",.45f);Set(gun,"ForceServerAuthority",true);
            var gs=new SerializedObject(gun);gs.FindProperty("muzzles").arraySize=1;gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;gs.ApplyModifiedPropertiesWithoutUndo();
            var ts=new SerializedObject(turret);ts.FindProperty("weaponStations").GetArrayElementAtIndex(0).FindPropertyRelative("WeaponInfo").objectReferenceValue=info;ts.ApplyModifiedPropertiesWithoutUndo();
            // Distinct puff template, sharing only installed-game remaps and audio.
            var puff=UnityEngine.Object.Instantiate(new SerializedObject(gun).FindProperty("selfDestructEffect").objectReferenceValue as GameObject);SceneManager.MoveGameObjectToScene(puff,preview);puff.name="F120_Flak_Airburst";
            foreach(var p in puff.GetComponentsInChildren<ParticleSystem>(true)){var main=p.main;main.startSizeMultiplier*=1.45f;}
            var effect=PrefabUtility.SaveAsPrefabAsset(puff,Dir+"F120_Flak_Airburst.prefab");Set(gun,"selfDestructEffect",effect);UnityEngine.Object.DestroyImmediate(puff);
            var spent=new GameObject("F120_SpentCasing");SceneManager.MoveGameObjectToScene(spent,preview);
            var spentMesh=UnityEngine.Object.Instantiate(caseMesh.gameObject,spent.transform,false);spentMesh.transform.localPosition=caseMesh.localPosition;spentMesh.transform.localRotation=caseMesh.localRotation;spentMesh.transform.localScale=caseMesh.localScale;
            spent.AddComponent<Rigidbody>().mass=7;spent.AddComponent<BoxCollider>().size=casingBounds.size;
            var casingPrefab=PrefabUtility.SaveAsPrefabAsset(spent,Dir+"F120_SpentCasing.prefab");Set(gun,"ejectionPrefab",casingPrefab);Set(gun,"ejectionTransform",null);UnityEngine.Object.DestroyImmediate(spent);
            if(!AssetDatabase.CopyAsset(Dir+"F88_AA_Definition.asset",Dir+"F120_AA_Definition.asset"))throw new Exception("Definition copy failed.");
            var definition=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(Dir+"F120_AA_Definition.asset");
            Set(definition,"jsonKey","Gunmaggedon_F120_AA");Set(definition,"unitName","F120 Stratohammer Heavy Flak Trailer");Set(definition,"description","Postwar Old Guard 120mm heavy AA. Improved visual fire control revises estimated aerial firing regions quickly; nearby radar improves knowledge. Large timed-fragment bursts remain inaccurate against evasive aircraft.");
            Set(definition,"mass",12000f);Set(definition,"length",14f);Set(definition,"width",10.3f);Set(definition,"height",5.3f);Set(definition,"spawnOffset",new Vector3(0,.04f,0));Set(vehicle,"definition",definition);
            body.mass=12000;body.centerOfMass=new Vector3(0,1.35f,0);Set(root.GetComponent<UnitPart>(),"mass",12000f);Set(vehicle,"springRate",450000f);Set(vehicle,"dampingRate",65000f);Set(vehicle,"suspensionTravel",.04f+12000*9.81f/450000f);Set(vehicle,"wheelRadius",1.05f);
            var wheels=new SerializedObject(vehicle);var array=wheels.FindProperty("wheels");array.arraySize=4;var ids=new[]{"FL","FR","BL","BR"};
            for(int i=0;i<4;i++){var contact=root.transform.Find("WheelContact"+(i+1));contact.position=carriage.transform.Find("Suspension/"+ids[i]+"/WheelSpin").position;contact.gameObject.AddComponent<SphereCollider>().radius=1.05f;array.GetArrayElementAtIndex(i).objectReferenceValue=contact;}wheels.ApplyModifiedPropertiesWithoutUndo();
            var barrelCollider=recoil.gameObject.AddComponent<BoxCollider>();var bounds=barrel.GetComponent<Renderer>().bounds;barrelCollider.center=recoil.InverseTransformPoint(bounds.center);barrelCollider.size=bounds.size;
            var gunCollider=pitch.gameObject.AddComponent<BoxCollider>();bounds=pitch.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="gun").bounds;gunCollider.center=pitch.InverseTransformPoint(bounds.center);gunCollider.size=bounds.size;
            // Native bullet/blast queries resolve IDamageable on the hit object,
            // not its ancestors. Every physical shape must carry a receiver.
            foreach(var c in root.GetComponentsInChildren<Collider>(true))if(!c.isTrigger&&c.GetComponent<IDamageable>()==null){
                var parent=c.transform.IsChildOf(yaw)?yaw.GetComponent<UnitPart>():root.GetComponent<UnitPart>();
                var part=c.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(parent,part);
                Set(part,"parentUnit",vehicle);Set(part,"rb",body);Set(part,"mass",0f);Set(part,"structuralThreshold",-1000000f);Set(part,"integrityThreshold",-1000000f);
                var ps=new SerializedObject(part);ps.FindProperty("disintegrationEffects").arraySize=0;ps.FindProperty("disintegrateObjects").arraySize=0;ps.ApplyModifiedPropertiesWithoutUndo();
            }
            var sourceClip=root.GetComponent<Animation>().clip;var clip=new AnimationClip{name="F120_Mechanism_Preview",legacy=true,frameRate=60};
            foreach(var binding in AnimationUtility.GetCurveBindings(sourceClip)){var b=binding;b.path=b.path.Replace("F88YawPivot","F120YawPivot");AnimationUtility.SetEditorCurve(clip,b,AnimationUtility.GetEditorCurve(sourceClip,binding));}
            const string prefix="F120YawPivot/PitchPivot/";
            Curve(clip,prefix+"BreechSlidePivot","localPosition.x",rest.localPosition.x,rest.localPosition.x,rest.localPosition.x,rest.localPosition.x);
            Curve(clip,prefix+"BreechSlidePivot","localPosition.y",rest.localPosition.y,rest.localPosition.y+.38f,rest.localPosition.y+.38f,rest.localPosition.y);
            Curve(clip,prefix+"BreechSlidePivot","localPosition.z",rest.localPosition.z,rest.localPosition.z,rest.localPosition.z,rest.localPosition.z);
            PositionCurve(clip,prefix+"ReloadVisuals/ExtractingCasing",caseSeat,caseClear,3.8f,4.6f);PositionCurve(clip,prefix+"ReloadVisuals/LoadingShell",loadStart,loadSeat,6.6f,8.2f);
            AssetDatabase.CreateAsset(clip,Dir+"F120_Mechanism_Preview.anim");var animation=root.GetComponent<Animation>();animation.RemoveClip(sourceClip);animation.AddClip(clip,clip.name);animation.clip=clip;animation.playAutomatically=false;
            Set(root.GetComponent<Mirage.NetworkIdentity>(),"_prefabHash",0);
            SetupUnitLODs.ConfigureCarriage(root);
            // Rest-pose bounds alone miss the long barrel at high elevation/yaw.
            var lodGroup=root.GetComponent<LODGroup>();lodGroup.localReferencePoint=new Vector3(0,5.4f,0);lodGroup.size=26;
            root.SetActive(true);result=PrefabUtility.SaveAsPrefabAsset(root,Prefab);Set(definition,"unitPrefab",result);AssetDatabase.SaveAssets();
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
        var scene=source.scene;var instance=(GameObject)PrefabUtility.InstantiatePrefab(result,scene);Undo.RegisterCreatedObjectUndo(instance,"Set up F120");Undo.DestroyObjectImmediate(source);Selection.activeGameObject=instance;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{prefab=Prefab,unitKey="Gunmaggedon_F120_AA",fragmentRadius=240,range=20000,partsBefore=before.Count,breechDirection="up",existingCarriagePrefabUnchanged=true};
    }
}
