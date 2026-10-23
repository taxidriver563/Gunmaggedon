using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupF88Combat
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object o,string name,object value)
    {
        var s=new SerializedObject(o);var p=s.FindProperty(name);if(p==null)throw new Exception(o.GetType()+" missing "+name);
        if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is string)p.stringValue=(string)value;else if(value is Vector3)p.vector3Value=(Vector3)value;else p.objectReferenceValue=value as UnityEngine.Object;
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    public static object Run()
    {
        string repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(repo))throw new Exception("Set GUNMAGGEDON_ROOT.");
        string path=Dir+"F88_AA_Trailer.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path))throw new Exception("F88 combat prefab exists; refusing duplicate setup.");
        var backup=Path.Combine(repo,"UnitySetupBackup/F88/BeforeCombat");Directory.CreateDirectory(backup);
        File.Copy(Dir+"F88_Turret_Rig.prefab",Path.Combine(backup,"F88_Turret_Rig.prefab"),true);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L40_AA_Trailer.prefab");
        // Work only on an independent clone in a temporary preview scene.
        // Never load editable L40 prefab contents or replace the user's scene.
        var preview=EditorSceneManager.NewPreviewScene();
        var root=UnityEngine.Object.Instantiate(source);root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
        try {
            root.name="F88 Heavy Flak Trailer";
            var oldYaw=root.GetComponentInChildren<Turret>(true).gameObject;UnityEngine.Object.DestroyImmediate(oldYaw);UnityEngine.Object.DestroyImmediate(root.transform.Find("Model").gameObject);
            foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"F88_Turret_Rig.prefab"),root.transform,false);
            foreach(var child in rig.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==rig.transform).ToArray())child.SetParent(root.transform,false);
            var animation=root.AddComponent<Animation>();EditorUtility.CopySerialized(rig.GetComponent<Animation>(),animation);UnityEngine.Object.DestroyImmediate(rig);
            var vehicle=root.GetComponent<GroundVehicle>();var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);
            var part=turret.gameObject.AddComponent<UnitPart>();EditorUtility.CopySerialized(source.GetComponentInChildren<Turret>(true).GetComponent<UnitPart>(),part);
            Set(part,"parentUnit",vehicle);Set(part,"rb",root.GetComponent<Rigidbody>());
            var ps=new SerializedObject(part);ps.FindProperty("disintegrateObjects").arraySize=1;ps.FindProperty("disintegrateObjects").GetArrayElementAtIndex(0).objectReferenceValue=turret.gameObject;ps.ApplyModifiedPropertiesWithoutUndo();
            var detector=turret.gameObject.AddComponent<TargetDetector>();EditorUtility.CopySerialized(source.GetComponentInChildren<Turret>(true).GetComponent<TargetDetector>(),detector);
            var optics=new GameObject("F88Optics").transform;optics.SetParent(turret.transform,false);optics.localPosition=new Vector3(-.8f,1.1f,-1.3f);
            Set(detector,"attachedUnit",vehicle);Set(detector,"scanner",optics);Set(detector,"part",part);Set(detector,"visualRange",8000f);
            Set(turret,"attachedUnit",vehicle);Set(turret,"elevationTransform",gun.transform);Set(turret,"aimSafetyWeapon",gun);
            var ts=new SerializedObject(turret);ts.FindProperty("targetDetectors").arraySize=1;ts.FindProperty("targetDetectors").GetArrayElementAtIndex(0).objectReferenceValue=detector;
            ts.FindProperty("criticalParts").arraySize=1;ts.FindProperty("criticalParts").GetArrayElementAtIndex(0).objectReferenceValue=part;
            ts.FindProperty("weaponStations").arraySize=1;
            var ws=ts.FindProperty("weaponStations").GetArrayElementAtIndex(0);ws.FindPropertyRelative("Weapons").arraySize=1;ws.FindPropertyRelative("Weapons").GetArrayElementAtIndex(0).objectReferenceValue=gun;ws.FindPropertyRelative("Turrets").arraySize=0;ts.ApplyModifiedPropertiesWithoutUndo();
            if(!AssetDatabase.CopyAsset(Dir+"L40_AA_Weapon.asset",Dir+"F88_AA_Weapon.asset"))throw new Exception("Cannot create F88 weapon.");
            var info=AssetDatabase.LoadAssetAtPath<WeaponInfo>(Dir+"F88_AA_Weapon.asset");
            Set(info,"weaponName","88mm Timed Flak");Set(info,"shortName","88mm Flak");Set(info,"description","Timed fragmentation barrages aimed into estimated aerial regions; nearby radar improves contact knowledge only.");
            Set(info,"muzzleVelocity",820f);Set(info,"dragCoef",.04f);Set(info,"gravMult",1f);Set(info,"pierceDamage",900f);Set(info,"blastDamage",1f);Set(info,"fireInterval",4f);Set(info,"useWeaponDoors",false);
            Set(info,"targetRequirements.minRange",800f);Set(info,"targetRequirements.maxRange",12000f);Set(info,"targetRequirements.minAltitude",350f);Set(info,"targetRequirements.maxAltitude",9000f);
            Set(info,"effectiveness.antiSurface",0f);Set(info,"effectiveness.antiAir",1f);Set(info,"effectiveness.antiMissile",0f);Set(info,"massPerRound",9.4f);Set(info,"armorTierEffectiveness",1f);
            var stationData=new SerializedObject(turret);stationData.FindProperty("weaponStations").GetArrayElementAtIndex(0).FindPropertyRelative("WeaponInfo").objectReferenceValue=info;stationData.ApplyModifiedPropertiesWithoutUndo();
            Set(gun,"attachedUnit",vehicle);Set(gun,"info",info);Set(gun,"velocityInherit",root.GetComponent<Rigidbody>());Set(gun,"Safety",false);Set(gun,"ammo",120);Set(gun,"magazines",120);Set(gun,"magazineCapacity",1);Set(gun,"reloadTime",6.2f);Set(gun,"fireRate",15f);Set(gun,"bulletSpread",.65f);Set(gun,"bulletSelfDestruct",24f);Set(gun,"proximityTimer",false);Set(gun,"ForceServerAuthority",true);Set(gun,"startLoaded",true);
            gun.enabled=true;turret.enabled=true;
            if(!AssetDatabase.CopyAsset(Dir+"L40_AA_Definition.asset",Dir+"F88_AA_Definition.asset"))throw new Exception("Cannot create F88 definition.");
            var def=AssetDatabase.LoadAssetAtPath<VehicleDefinition>(Dir+"F88_AA_Definition.asset");
            Set(def,"jsonKey","Gunmaggedon_F88_AA");Set(def,"unitName","F88 Heavy Flak Trailer");Set(def,"description","88mm area-suppression flak. Fires committed timed barrages along estimated aircraft routes; friendly radar within 300m improves observations, not gun dispersion or fuze precision.");
            Set(def,"mass",4500f);Set(def,"spawnOffset",new Vector3(0,.04f,0));Set(def,"length",13.2f);Set(def,"width",8.3f);Set(def,"height",4f);Set(def,"roleIdentity.antiAir",1f);Set(def,"roleIdentity.antiMissile",0f);Set(def,"roleIdentity.antiSurface",0f);
            Set(vehicle,"definition",def);Set(vehicle,"mobile",false);Set(vehicle,"wreckage",null);Set(vehicle,"springRate",200000f);Set(vehicle,"dampingRate",40000f);Set(vehicle,"suspensionTravel",.04f+4500f*9.81f/200000f);Set(vehicle,"wheelRadius",.98f);
            Set(root.GetComponent<UnitPart>(),"mass",4500f);var body=root.GetComponent<Rigidbody>();body.mass=4500;body.angularDrag=.5f;
            var chassis=root.AddComponent<BoxCollider>();chassis.center=new Vector3(0,1.05f,0);chassis.size=new Vector3(1.7f,.8f,7.6f);
            var mount=turret.gameObject.AddComponent<CapsuleCollider>();mount.center=new Vector3(0,.85f,0);mount.height=2;mount.radius=.8f;
            var barrel=gun.gameObject.AddComponent<BoxCollider>();barrel.center=new Vector3(0,.1f,3.75f);barrel.size=new Vector3(1.1f,.9f,12);
            int index=1;foreach(float x in new[]{-2.65f,2.65f})foreach(float z in new[]{-4.25f,4.25f}) {var wheel=root.transform.Find("WheelContact"+index++);wheel.localPosition=new Vector3(x,.96f,z);wheel.gameObject.AddComponent<SphereCollider>().radius=.98f;}
            var hook=root.transform.Find("TowHook");if(hook)hook.localPosition=new Vector3(0,1,4.95f);
            var identity=new SerializedObject(root.GetComponent<Mirage.NetworkIdentity>());identity.FindProperty("_prefabHash").intValue=0;identity.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Set(def,"unitPrefab",prefab);AssetDatabase.SaveAssets();
        }finally{EditorSceneManager.ClosePreviewScene(preview);}
        // New assets are reviewable in the Project window; existing scene
        // objects and the L40 assets are left untouched.
        Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return new{prefab=path,definition=Dir+"F88_AA_Definition.asset",weapon=Dir+"F88_AA_Weapon.asset",mass=4500,radarSupportMetres=300,rounds=120};
    }
}
