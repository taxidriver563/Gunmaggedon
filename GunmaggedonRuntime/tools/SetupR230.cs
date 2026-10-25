using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SetupR230
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    public static void Set(UnityEngine.Object o,string name,object v){var s=new SerializedObject(o);var p=s.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" lacks "+name);if(v is float)p.floatValue=(float)v;else if(v is int)p.intValue=(int)v;else if(v is bool)p.boolValue=(bool)v;else if(v is string)p.stringValue=(string)v;else if(v is Vector3)p.vector3Value=(Vector3)v;else p.objectReferenceValue=v as UnityEngine.Object;s.ApplyModifiedPropertiesWithoutUndo();}
    static Transform Node(Transform parent,string name,Vector3 world){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=world;return t;}
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        const string prefabPath=Dir+"R230_Emplacement.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))throw new Exception("R230 already exists; use a migration");
        var source=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name.Contains("R230"));
        if(source.scene.isDirty)throw new Exception("Save the imported scene before setup");
        var workspace=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");var backup=Path.Combine(workspace,"UnitySetupBackup/R230/BeforeSetup");Directory.CreateDirectory(backup);
        File.Copy(source.scene.path,Path.Combine(backup,"mounted-scene.unity"),false);
        if(!AssetDatabase.IsValidFolder("Assets/BlueprinterSetupBackups"))AssetDatabase.CreateFolder("Assets","BlueprinterSetupBackups");
        PrefabUtility.SaveAsPrefabAsset(source,"Assets/BlueprinterSetupBackups/R230_Original.prefab");
        var preview=EditorSceneManager.NewPreviewScene();GameObject result;
        try{
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Emplacement_203mmCG.prefab");
            var root=UnityEngine.Object.Instantiate(reference);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,preview);root.name="R230_Emplacement";
            foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            foreach(var l in root.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(l);
            foreach(var child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            var building=root.GetComponent<Building>();Set(building,"wreckage",null);
            var model=UnityEngine.Object.Instantiate(source,root.transform,false);model.name="Model";model.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);model.transform.localScale=source.transform.lossyScale;
            var baseMesh=model.transform.Find("base");var traverse=model.transform.Find("traverse");var barrel=model.transform.Find("gun");
            if(!baseMesh||!traverse||!barrel)throw new Exception("Expected base/traverse/gun model");
            var yaw=Node(root.transform,"R230YawPivot",traverse.position);var pitch=Node(yaw,"PitchPivot",barrel.position);var recoil=Node(pitch,"RecoilPivot",barrel.position);
            traverse.SetParent(yaw,true);barrel.SetParent(recoil,true);
            var muzzle=Node(recoil,"Muzzle",new Vector3(barrel.GetComponent<Renderer>().bounds.center.x,barrel.position.y,barrel.GetComponent<Renderer>().bounds.max.z+.05f));
            var oldTurret=reference.GetComponentInChildren<Turret>(true);var turret=yaw.gameObject.AddComponent<Turret>();EditorUtility.CopySerialized(oldTurret,turret);
            var gun=pitch.gameObject.AddComponent<Gun>();
            var stock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/destroyer1_PLACEHOLDER.prefab").GetComponentsInChildren<Gun>(true).First(g=>g.info&&g.info.name=="Railgun1");EditorUtility.CopySerialized(stock,gun);
            foreach(var sound in stock.GetComponents<AudioSource>())EditorUtility.CopySerialized(sound,pitch.gameObject.AddComponent<AudioSource>());
            var info=UnityEngine.Object.Instantiate(stock.info);info.name="R230_Railgun";AssetDatabase.CreateAsset(info,Dir+"R230_Railgun.asset");
            Set(info,"weaponName","230mm Railgun");Set(info,"shortName","230mm Railgun");Set(info,"description","Long-range precision kinetic air and terminal ballistic-missile defense.");
            Set(info,"muzzleVelocity",15000f);Set(info,"dragCoef",0f);Set(info,"gravMult",1f);Set(info,"pierceDamage",15000f);Set(info,"blastDamage",25f);Set(info,"fireInterval",8f);Set(info,"massPerRound",80f);Set(info,"useWeaponDoors",false);
            Set(info,"targetRequirements.minRange",0f);Set(info,"targetRequirements.maxRange",65000f);Set(info,"targetRequirements.minAltitude",0f);Set(info,"targetRequirements.maxAltitude",100000f);Set(info,"targetRequirements.maxSpeed",10000f);Set(info,"effectiveness.antiAir",1f);Set(info,"effectiveness.antiMissile",1f);Set(info,"effectiveness.antiSurface",0f);
            Set(gun,"attachedUnit",building);Set(gun,"info",info);Set(gun,"velocityInherit",null);Set(gun,"recoilTransform",null);Set(gun,"spinTransform",null);Set(gun,"ejectionTransform",null);Set(gun,"ejectionPrefab",null);Set(gun,"recoilSound",null);
            Set(gun,"ammo",60);Set(gun,"magazines",60);Set(gun,"magazineCapacity",1);Set(gun,"reloadTime",8f);Set(gun,"fireRate",7.5f);Set(gun,"bulletSpread",.003f);Set(gun,"bulletSelfDestruct",16f);Set(gun,"tracerRatio",0);Set(gun,"tracerSize",5f);Set(gun,"proximityTimer",false);Set(gun,"ForceServerAuthority",true);Set(gun,"startLoaded",true);Set(gun,"Safety",false);Set(gun,"recoilTravel",0f);
            var gs=new SerializedObject(gun);gs.FindProperty("muzzles").arraySize=1;gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;
            // Copy only presentation particles from the working native railgun.
            var fx=new System.Collections.Generic.List<ParticleSystem>();var ps=new SerializedObject(stock).FindProperty("muzzleParticles");
            for(int i=0;i<ps.arraySize;i++){var original=ps.GetArrayElementAtIndex(i).objectReferenceValue as ParticleSystem;if(!original)continue;var go=UnityEngine.Object.Instantiate(original.gameObject,muzzle,false);go.name="R230MuzzleFX"+i;go.transform.localPosition=Vector3.zero;foreach(var mesh in go.GetComponentsInChildren<MeshFilter>(true))UnityEngine.Object.DestroyImmediate(mesh.gameObject);var p=go.GetComponent<ParticleSystem>();if(p)fx.Add(p);}
            gs.FindProperty("muzzleParticles").arraySize=fx.Count;for(int i=0;i<fx.Count;i++)gs.FindProperty("muzzleParticles").GetArrayElementAtIndex(i).objectReferenceValue=fx[i];gs.ApplyModifiedPropertiesWithoutUndo();
            Set(turret,"attachedUnit",building);Set(turret,"elevationTransform",pitch);Set(turret,"aimSafetyWeapon",gun);Set(turret,"traverseRate",36f);Set(turret,"elevationRate",30f);Set(turret,"minElevation",-5f);Set(turret,"maxElevation",90f);Set(turret,"aimSolver.artillery",false);
            var ts=new SerializedObject(turret);ts.FindProperty("targetDetectors").arraySize=0;ts.FindProperty("criticalParts").arraySize=1;ts.FindProperty("criticalParts").GetArrayElementAtIndex(0).objectReferenceValue=root.GetComponent<UnitPart>();ts.FindProperty("weaponStations").arraySize=1;
            var station=ts.FindProperty("weaponStations").GetArrayElementAtIndex(0);station.FindPropertyRelative("Weapons").arraySize=1;station.FindPropertyRelative("Weapons").GetArrayElementAtIndex(0).objectReferenceValue=gun;station.FindPropertyRelative("Turrets").arraySize=0;station.FindPropertyRelative("WeaponInfo").objectReferenceValue=info;ts.ApplyModifiedPropertiesWithoutUndo();
            var def=UnityEngine.Object.Instantiate((BuildingDefinition)building.definition);def.name="R230_Definition";AssetDatabase.CreateAsset(def,Dir+"R230_Definition.asset");
            Set(def,"jsonKey","Gunmaggedon_R230");Set(def,"unitName","R-230");Set(def,"code","R-230");Set(def,"description","Stratopenetrator building railgun. 65km LOS envelope; terminal Piledrivers, other Piledrivers and strategic bombers override ordinary scored aircraft threats.");Set(def,"length",20f);Set(def,"width",10f);Set(def,"height",12f);Set(def,"spawnOffset",Vector3.zero);Set(def,"roleIdentity.antiAir",1f);Set(def,"roleIdentity.antiMissile",1f);Set(def,"roleIdentity.antiSurface",0f);Set(building,"definition",def);
            foreach(var mesh in new[]{baseMesh,traverse,barrel}){
                var part=mesh.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(reference.GetComponent<UnitPart>(),part);Set(part,"parentUnit",building);Set(part,"armorProperties.pierceArmor",mesh==baseMesh?300f:150f);Set(part,"armorProperties.blastArmor",100f);Set(part,"armorProperties.pierceTolerance",mesh==baseMesh?6f:3f);Set(part,"armorProperties.blastTolerance",3f);Set(part,"structuralThreshold",-1000000f);
                var box=mesh.gameObject.AddComponent<BoxCollider>();box.center=mesh.GetComponent<MeshFilter>().sharedMesh.bounds.center;box.size=mesh.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            }
            Set(root.GetComponent<UnitPart>(),"parentUnit",building);
            var binding=new SerializedObject(root.GetComponent<Mirage.NetworkIdentity>());binding.FindProperty("_prefabHash").intValue=0;binding.ApplyModifiedPropertiesWithoutUndo();
            SetupUnitLODs.ConfigureCarriage(root);var lod=root.GetComponent<LODGroup>();lod.localReferencePoint=new Vector3(0,-1,0);lod.size=52;
            root.SetActive(true);result=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Set(def,"unitPrefab",result);AssetDatabase.SaveAssets();
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
        var scene=source.scene;var instance=(GameObject)PrefabUtility.InstantiatePrefab(result,scene);Undo.RegisterCreatedObjectUndo(instance,"Set up R230");Undo.DestroyObjectImmediate(source);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Created R-230 emplacement, native railgun effects/audio and three model LODs; pistons omitted.";
    }
}
