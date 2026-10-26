using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SetupDP127
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object o,string name,object value){var so=new SerializedObject(o);var p=so.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" lacks "+name);
        if(value is string)p.stringValue=(string)value;else if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is Vector3)p.vector3Value=(Vector3)value;else p.objectReferenceValue=value as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();}
    static Transform Node(Transform parent,string name,Vector3 world){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=world;return t;}
    public static object Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stable edit mode required");
        foreach(var name in new[]{"DP127_Emplacement.prefab","DP127_Definition.asset","DP127_Guided.asset"})if(AssetDatabase.LoadMainAssetAtPath(Dir+name))throw new Exception("Already configured: "+name);
        var source=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Gunmaggedon DP127");
        var workspace=(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT before running editor tools or fixtures."));var backup=Path.Combine(workspace,"UnitySetupBackup/DP127/BeforeSetup");Directory.CreateDirectory(backup);
        File.Copy(source.scene.path,Path.Combine(backup,"imported-scene.unity"),false);
        if(!AssetDatabase.IsValidFolder("Assets/BlueprinterSetupBackups"))AssetDatabase.CreateFolder("Assets","BlueprinterSetupBackups");
        PrefabUtility.SaveAsPrefabAsset(source,"Assets/BlueprinterSetupBackups/DP127_Original.prefab");
        var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;
        try{
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"R230_Emplacement.prefab");root=UnityEngine.Object.Instantiate(reference);root.SetActive(false);SceneManager.MoveGameObjectToScene(root,scene);root.name="DP127_Emplacement";
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(lod);
            foreach(var child in root.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            var owner=root.GetComponent<Building>();Set(owner,"wreckage",null);Set(owner,"radar",null);
            var model=UnityEngine.Object.Instantiate(source,root.transform,false);model.name="Model";model.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);model.transform.localScale=source.transform.lossyScale;
            var concrete=model.transform.Find("base");var housing=model.transform.Find("traverse");var cradle=model.transform.Find("gun");var barrel=model.transform.Find("barrel");
            if(!concrete||!housing||!cradle||!barrel)throw new Exception("Expected base/traverse/gun/barrel");
            var yaw=Node(root.transform,"DP127YawPivot",housing.position);var pitch=Node(yaw,"PitchPivot",cradle.position);var recoil=Node(pitch,"RecoilPivot",barrel.position);
            housing.SetParent(yaw,true);cradle.SetParent(pitch,true);barrel.SetParent(recoil,true);
            var muzzle=Node(recoil,"Muzzle",new Vector3(barrel.GetComponent<Renderer>().bounds.center.x,barrel.position.y,barrel.GetComponent<Renderer>().bounds.max.z+.08f));
            var sensor=Node(root.transform,"FireControlSensor",new Vector3(0,5.5f,0));Set(owner,"obstacleTopTransform",sensor);
            var turret=yaw.gameObject.AddComponent<Turret>();EditorUtility.CopySerialized(reference.GetComponentInChildren<Turret>(true),turret);
            var stock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Frigate1_PLACEHOLDER.prefab").GetComponentsInChildren<Gun>(true).Single(g=>g.info&&g.info.name=="Gun127mm_Guided_Naval");
            var gun=pitch.gameObject.AddComponent<Gun>();EditorUtility.CopySerialized(stock,gun);
            foreach(var audio in stock.GetComponents<AudioSource>())EditorUtility.CopySerialized(audio,pitch.gameObject.AddComponent<AudioSource>());
            if(!pitch.GetComponent<AudioSource>()){var audio=pitch.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.maxDistance=30000;}
            var info=UnityEngine.Object.Instantiate(stock.info);info.name="DP127_Guided";info.weaponName="127mm Guided Cannon";info.shortName="127mm Guided";info.description="Guided impact-fuzed surface and proximity-fuzed AA rounds. Advanced FCS;15km engagement envelope.";
            info.useWeaponDoors=false;info.targetRequirements.minRange=300;info.targetRequirements.maxRange=15000;info.targetRequirements.minAltitude=0;info.targetRequirements.maxAltitude=15000;info.targetRequirements.maxSpeed=1200;
            Set(info,"effectiveness.antiAir",.7f);Set(info,"effectiveness.antiSurface",1f);AssetDatabase.CreateAsset(info,Dir+"DP127_Guided.asset");
            Set(gun,"attachedUnit",owner);Set(gun,"info",info);Set(gun,"velocityInherit",null);Set(gun,"recoilTransform",recoil);Set(gun,"recoilTravel",.4f);Set(gun,"recoilImpulse",0f);Set(gun,"spinTransform",null);Set(gun,"ejectionTransform",null);Set(gun,"ejectionPrefab",null);Set(gun,"recoilSound",null);
            Set(gun,"ammo",200);Set(gun,"magazines",40);Set(gun,"magazineCapacity",5);Set(gun,"reloadTime",5f);Set(gun,"fireRate",22f);Set(gun,"bulletSpread",.1f);Set(gun,"ForceServerAuthority",true);Set(gun,"startLoaded",true);Set(gun,"Safety",false);Set(gun,"proximityTimer",false);
            var gs=new SerializedObject(gun);gs.FindProperty("muzzleParticles").arraySize=0;gs.FindProperty("muzzles").arraySize=1;gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;gs.ApplyModifiedPropertiesWithoutUndo();
            // Audio/projectile references use Blueprinter remaps. Muzzle
            // emitters are resolved from the installed naval gun at runtime.
            Set(turret,"attachedUnit",owner);Set(turret,"elevationTransform",pitch);Set(turret,"aimSafetyWeapon",gun);Set(turret,"traverseRate",40f);Set(turret,"elevationRate",35f);Set(turret,"minElevation",-10f);Set(turret,"maxElevation",85f);Set(turret,"aimSolver.artillery",false);
            var ts=new SerializedObject(turret);ts.FindProperty("targetDetectors").arraySize=0;ts.FindProperty("criticalParts").arraySize=0;ts.FindProperty("weaponStations").arraySize=1;
            var station=ts.FindProperty("weaponStations").GetArrayElementAtIndex(0);station.FindPropertyRelative("Weapons").arraySize=1;station.FindPropertyRelative("Weapons").GetArrayElementAtIndex(0).objectReferenceValue=gun;station.FindPropertyRelative("Turrets").arraySize=0;station.FindPropertyRelative("WeaponInfo").objectReferenceValue=info;ts.ApplyModifiedPropertiesWithoutUndo();
            var definition=UnityEngine.Object.Instantiate((BuildingDefinition)owner.definition);definition.name="DP127_Definition";definition.jsonKey="Gunmaggedon_DP127";definition.unitName="DP-127";definition.description="Modern dual-purpose naval gun on a concrete emplacement. Advanced fire control; native guided127mm impact rounds against land/sea and proximity rounds against aircraft out to15km. Native maneuver and interception limits apply.";
            Set(definition,"code","DP-127");Set(definition,"length",12f);Set(definition,"width",6f);Set(definition,"height",5.5f);Set(definition,"mass",600000f);Set(definition,"spawnOffset",Vector3.zero);Set(definition,"roleIdentity.antiAir",.7f);Set(definition,"roleIdentity.antiSurface",1f);Set(definition,"roleIdentity.antiMissile",0f);AssetDatabase.CreateAsset(definition,Dir+"DP127_Definition.asset");Set(owner,"definition",definition);
            foreach(var mesh in new[]{concrete,housing,cradle,barrel}){
                var part=mesh.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(reference.GetComponent<UnitPart>(),part);Set(part,"parentUnit",owner);Set(part,"criticalPart",true);
                Set(part,"armorProperties.pierceArmor",mesh==concrete?300f:70f);Set(part,"armorProperties.blastArmor",mesh==concrete?200f:50f);Set(part,"armorProperties.pierceTolerance",mesh==concrete?6f:2f);Set(part,"armorProperties.blastTolerance",mesh==concrete?5f:2f);Set(part,"structuralThreshold",-1000000f);
                var box=mesh.gameObject.AddComponent<BoxCollider>();box.center=mesh.GetComponent<MeshFilter>().sharedMesh.bounds.center;box.size=mesh.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            }
            Set(root.GetComponent<UnitPart>(),"parentUnit",owner);Set(root.GetComponent<Mirage.NetworkIdentity>(),"_prefabHash",0);
            SetupUnitLODs.ConfigureCarriage(root);var group=root.GetComponent<LODGroup>();group.localReferencePoint=Vector3.up;group.size=28;
            var triangles=group.GetLODs().Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();if(!(triangles[0]>triangles[1]&&triangles[1]>triangles[2]))throw new Exception("No meaningful LOD reduction");
            root.SetActive(true);var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+"DP127_Emplacement.prefab");Set(definition,"unitPrefab",prefab);AssetDatabase.SaveAssets();
            return new{prefab=AssetDatabase.GetAssetPath(prefab),triangles,muzzle=muzzle.position.ToString(),sensor=sensor.position.ToString(),sceneChanged=false};
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
