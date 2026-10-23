using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Stage the mechanical rig only. Unit definitions and heavy-flak fire control
// are intentionally deferred until their behavior is designed.
public static class SetupF88
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Set(UnityEngine.Object obj,string name,object value)
    {
        var so=new SerializedObject(obj);var p=so.FindProperty(name);
        if(p==null)throw new Exception(obj.GetType()+" missing "+name);
        if(value is float)p.floatValue=(float)value;
        else if(value is int)p.intValue=(int)value;
        else if(value is bool)p.boolValue=(bool)value;
        else if(value is Vector3)p.vector3Value=(Vector3)value;
        else p.objectReferenceValue=value as UnityEngine.Object;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Transform Pivot(string name,Transform parent,Vector3 position)
    {
        var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=position;t.rotation=parent.rotation;return t;
    }
    static void Curve(AnimationClip clip,string path,string property,params float[] values)
    {
        var keys=new Keyframe[values.Length/2];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(values[i*2],values[i*2+1]);
        clip.SetCurve(path,typeof(Transform),property,new AnimationCurve(keys));
    }
    public static object Run()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"F88_Turret_Rig.prefab"))throw new Exception("F88 rig already exists; refusing duplicate setup.");
        var model=GameObject.Find("Gunmaggedon F88 AAA");if(!model)throw new Exception("F88 model missing.");
        var repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(repo))throw new Exception("Set GUNMAGGEDON_ROOT.");
        var backup=Path.Combine(repo,"UnitySetupBackup/F88/BeforeRig");Directory.CreateDirectory(backup);
        const string backupAssets="Assets/BlueprinterSetupBackups/F88";
        Directory.CreateDirectory(backupAssets);AssetDatabase.Refresh();
        PrefabUtility.SaveAsPrefabAsset(model,backupAssets+"/F88_Original.prefab");
        File.Copy(model.scene.path,Path.Combine(backup,"anvil-before-F88.unity"),false);
        File.Copy(backupAssets+"/F88_Original.prefab",Path.Combine(backup,"F88_Original.prefab"),false);
        var original=model.GetComponentsInChildren<MeshRenderer>(true).ToDictionary(r=>r.name,r=>r.transform.localToWorldMatrix);
        if(PrefabUtility.IsPartOfPrefabInstance(model))PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var root=new GameObject("Gunmaggedon F88 AAA");root.transform.SetPositionAndRotation(model.transform.position,model.transform.rotation);
        model.name="Model";model.transform.SetParent(root.transform,true);
        var yaw=Pivot("F88YawPivot",root.transform,model.transform.Find("actualtraverse").position);
        var pitch=Pivot("PitchPivot",yaw,model.transform.Find("gun").position);
        var recoil=Pivot("RecoilPivot",pitch,pitch.position);
        model.transform.Find("traverse").SetParent(yaw,true);model.transform.Find("actualtraverse").SetParent(yaw,true);
        model.transform.Find("gun").SetParent(pitch,true);var barrel=model.transform.Find("barrel");barrel.SetParent(recoil,true);
        var breech=model.transform.Find("breech");var slide=Pivot("BreechSlidePivot",pitch,breech.position);breech.SetParent(slide,true);
        // The zero-rest wrapper owns the translation, preserving the FBX's
        // arbitrary mesh origin and its axis conversion.
        var slideRest=slide.localPosition;Pivot("BreechSlideRest",pitch,slide.position);
        foreach(var names in new[]{new[]{"elevationcrank","PitchCrankPivot"},new[]{"yawcrank","YawCrankPivot"}}) {
            var part=model.transform.Find(names[0]);var crank=Pivot(names[1],yaw,part.position);part.SetParent(crank,true);
        }

        var muzzle=Pivot("Muzzle",recoil,root.transform.TransformPoint(new Vector3(0,2.985f,8.25f)));
        // Copy native component settings, not the L40's meshes or host unit.
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L40_AA_Trailer.prefab");
        var turret=yaw.gameObject.AddComponent<Turret>();EditorUtility.CopySerialized(source.GetComponentInChildren<Turret>(true),turret);
        var gun=pitch.gameObject.AddComponent<Gun>();EditorUtility.CopySerialized(source.GetComponentInChildren<Gun>(true),gun);
        Set(turret,"attachedUnit",null);Set(turret,"fireControl",null);Set(turret,"aimSafetyWeapon",gun);Set(turret,"elevationTransform",pitch);
        Set(turret,"traverseRate",12f);Set(turret,"elevationRate",10f);Set(turret,"minElevation",-5f);Set(turret,"maxElevation",85f);
        var ts=new SerializedObject(turret);ts.FindProperty("targetDetectors").arraySize=0;ts.FindProperty("criticalParts").arraySize=0;ts.FindProperty("weaponStations").arraySize=0;ts.ApplyModifiedPropertiesWithoutUndo();
        Set(gun,"attachedUnit",null);Set(gun,"info",null);Set(gun,"velocityInherit",null);Set(gun,"ammo",0);Set(gun,"magazines",0);
        Set(gun,"Safety",true);Set(gun,"magazineCapacity",1);Set(gun,"reloadTime",6.2f);Set(gun,"fireRate",15f);Set(gun,"proximityTimer",false);
        Set(gun,"recoilTransform",recoil);Set(gun,"recoilTravel",.55f);Set(gun,"recoilRate",12f);Set(gun,"recoilReturnRate",2.2f);Set(gun,"recoilImpulse",0f);
        Set(gun,"ejectionTransform",null);Set(gun,"ejectionPrefab",null);Set(gun,"reloadSound",null);Set(gun,"recoilSound",null);
        Set(gun,"fireStart",null);Set(gun,"fireSustained",null);Set(gun,"fireEnd",null);
        var gs=new SerializedObject(gun);gs.FindProperty("fireSounds").arraySize=0;gs.FindProperty("muzzleParticles").arraySize=0;
        gs.FindProperty("muzzles").arraySize=1;gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;gs.ApplyModifiedPropertiesWithoutUndo();
        turret.enabled=false;gun.enabled=false;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true)) {
            var before=original[r.name];var after=r.transform.localToWorldMatrix;
            for(int i=0;i<16;i++)if(Mathf.Abs(before[i]-after[i])>.0001f)throw new Exception("Authored pose changed: "+r.name);
        }
        var clip=new AnimationClip{name="F88_Mechanism_Preview",legacy=true,frameRate=60};
        Curve(clip,"F88YawPivot","localEulerAnglesRaw.y",0,0,2,45,4,45,8,0);
        Curve(clip,"F88YawPivot/PitchPivot","localEulerAnglesRaw.x",0,0,2,-45,6,-45,8,0);
        Curve(clip,"F88YawPivot/YawCrankPivot","localEulerAnglesRaw.x",0,0,2,270,4,270,8,0);
        Curve(clip,"F88YawPivot/PitchCrankPivot","localEulerAnglesRaw.x",0,0,2,540,6,540,8,0);
        Curve(clip,"F88YawPivot/PitchPivot/RecoilPivot","localPosition.z",0,0,2.8f,0,2.9f,-.55f,3.7f,0,8,0);
        string slidePath="F88YawPivot/PitchPivot/BreechSlidePivot";
        Curve(clip,slidePath,"localPosition.x",0,slideRest.x,3.4f,slideRest.x,4f,slideRest.x+.36f,5.6f,slideRest.x+.36f,6.4f,slideRest.x,8,slideRest.x);
        // Unity groups position curves and otherwise inserts zero for the
        // other axes; explicitly preserve the authored breech rest offset.
        Curve(clip,slidePath,"localPosition.y",0,slideRest.y,8,slideRest.y);
        Curve(clip,slidePath,"localPosition.z",0,slideRest.z,8,slideRest.z);
        AssetDatabase.CreateAsset(clip,Dir+"F88_Mechanism_Preview.anim");
        var animation=root.AddComponent<Animation>();animation.clip=clip;animation.AddClip(clip,clip.name);animation.playAutomatically=false;
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,Dir+"F88_Turret_Rig.prefab",InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
        return new{prefab=AssetDatabase.GetAssetPath(prefab),preview=AssetDatabase.GetAssetPath(clip),muzzle=muzzle.position,trunnion=pitch.position,breechTravel=.36f,parts=original.Count};
    }
}
