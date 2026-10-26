using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
// Initial staging only. No Unit, combat definition, runtime installation or export.
public static class SetupC127Rigs
{
    const string Dir="Assets/GunmaggedonStaging/C127/";
    const float Duration=5.17f;
    static void Set(UnityEngine.Object o,string n,object value){var s=new SerializedObject(o);var p=s.FindProperty(n);if(p==null)throw new Exception("Missing "+n);if(value is float)p.floatValue=(float)value;else if(value is int)p.intValue=(int)value;else if(value is bool)p.boolValue=(bool)value;else if(value is string)p.stringValue=(string)value;else p.objectReferenceValue=value as UnityEngine.Object;s.ApplyModifiedPropertiesWithoutUndo();}
    static Transform Find(GameObject root,string name){return root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);}
    static Transform Pivot(string name,Transform parent,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=position;t.rotation=Quaternion.identity;return t;}
    static float Smooth(float t,float a,float b){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));}
    static AnimationCurve Samples(Func<float,float> evaluate,float seconds=Duration){var times=Enumerable.Range(0,307).Select(i=>i/306f).Concat(new[]{.045f,.05f,.09f,.13f,.21f,.23f,.24f,.3f,.34f,.475f,.48f,.55f,.7f,.77f,.79f,.81f,.89f,.90f,.95f,.98f}).Distinct().OrderBy(t=>t);var keys=times.Select(phase=>new Keyframe(phase*seconds,evaluate(phase))).ToArray();var curve=new AnimationCurve(keys);for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}return curve;}
    static void Curve(AnimationClip clip,GameObject root,Transform target,string property,Func<float,float> evaluate,float seconds=Duration){clip.SetCurve(AnimationUtility.CalculateTransformPath(target,root.transform),typeof(Transform),property,Samples(evaluate,seconds));}
    static void Visibility(AnimationClip clip,GameObject root,Transform target,float on,float off){var keys=new[]{new Keyframe(0,0),new Keyframe(on*Duration,1),new Keyframe(off*Duration,0),new Keyframe(Duration,0)};var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Constant);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Constant);}clip.SetCurve(AnimationUtility.CalculateTransformPath(target,root.transform),typeof(GameObject),"m_IsActive",curve);}
    static void AttachPreview(GameObject root,AnimationClip clip){var a=root.GetComponent<Animation>();if(!a)a=root.AddComponent<Animation>();a.AddClip(clip,clip.name);if(!a.clip)a.clip=clip;a.playAutomatically=false;a.wrapMode=WrapMode.ClampForever;}
    static AnimationClip SaveClip(AnimationClip clip){var path=Dir+clip.name+".anim";var prior=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(prior){EditorUtility.CopySerialized(clip,prior);UnityEngine.Object.DestroyImmediate(clip);EditorUtility.SetDirty(prior);return prior;}AssetDatabase.CreateAsset(clip,path);return clip;}
    static void Reload(GameObject root,Transform recoil,Transform elevation)
    {
        var block=Find(root,"C127_Exposed_BreechBlock");var lever=Find(root,"C127_Exposed_OperatingLever");var rammer=Find(root,"C127_Exposed_Rammer");var lift=Find(root,"C127_Exposed_RammerLift");var round=Find(root,"C127_Exposed_LoadingRound");var casing=Find(root,"C127_Exposed_EjectedCase");
        // The FBX has a hidden end-of-cycle pose, including zero scale on BOTH
        // ammunition parents and meshes. Restore physical scales on this clone.
        foreach(var name in new[]{"C127_Exposed_LoadingRound","C127_Exposed_Projectile","C127_Exposed_PowderCase","C127_Exposed_EjectedCase","C127_Exposed_SpentCase"})Find(root,name).localScale=Vector3.one;
        round.gameObject.SetActive(false);casing.gameObject.SetActive(false);
        round.localPosition=new Vector3(0,.016f,0);casing.localPosition=Vector3.zero;
        var baseBlock=block.localPosition;var baseLever=lever.localEulerAngles;var baseRammer=rammer.localPosition;var baseLift=lift.localPosition;
        var clip=new AnimationClip{name="C127_Exposed_Reload_Preview",legacy=true,frameRate=60,wrapMode=WrapMode.ClampForever};
        Func<float,float> open=t=>Smooth(t,.13f,.24f)*(1-Smooth(t,.79f,.89f));
        Curve(clip,root,recoil,"localPosition.z",t=>-.24f*Smooth(t,.05f,.09f)*(1-Smooth(t,.09f,.23f)));
        Curve(clip,root,block,"localPosition.y",t=>baseBlock.y+.00018f*open(t));Curve(clip,root,block,"localPosition.z",t=>baseBlock.z-.0042f*open(t));
        Curve(clip,root,lever,"localEulerAnglesRaw.x",t=>baseLever.x-55*open(t));
        Curve(clip,root,casing,"localPosition.y",t=>.0165f*Smooth(t,.21f,.34f));Curve(clip,root,casing,"localPosition.x",t=>.0055f*Smooth(t,.34f,.475f));Curve(clip,root,casing,"localPosition.z",t=>-.0065f*Smooth(t,.34f,.475f));
        Visibility(clip,root,casing,.21f,.475f);Visibility(clip,root,round,.48f,.79f);
        Curve(clip,root,round,"localPosition.y",t=>.016f*(1-Smooth(t,.55f,.77f)));
        Curve(clip,root,rammer,"localPosition.y",t=>baseRammer.y-.016f*Smooth(t,.55f,.77f)*(1-Smooth(t,.81f,.95f)));
        Curve(clip,root,lift,"localPosition.x",t=>baseLift.x);Curve(clip,root,lift,"localPosition.y",t=>baseLift.y);
        Curve(clip,root,lift,"localPosition.z",t=>baseLift.z*(1-Smooth(t,.48f,.55f)*(1-Smooth(t,.79f,.89f))));
        clip=SaveClip(clip);AttachPreview(root,clip);
        // Geometry-only handoff marker. No physics case is spawned in this pass.
        var caseMesh=Find(root,"C127_Exposed_SpentCase").GetComponent<MeshFilter>().sharedMesh;
        Pivot("CaseHandoff",elevation,elevation.position+new Vector3(.55f,-.65f,-(1.65f+caseMesh.bounds.center.y*100)));
    }
    static void Effects(Gun gun,Transform muzzle)
    {
        var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/F88_AA_Trailer.prefab").GetComponentInChildren<Gun>(true);var original=new SerializedObject(reference).FindProperty("muzzleParticles");var s=new SerializedObject(gun);var particles=s.FindProperty("muzzleParticles");particles.arraySize=original.arraySize;
        for(int i=0;i<original.arraySize;i++){var ps=original.GetArrayElementAtIndex(i).objectReferenceValue as ParticleSystem;if(!ps)throw new Exception("Working muzzle reference missing");var fx=UnityEngine.Object.Instantiate(ps.gameObject,muzzle,false);fx.name=i==0?"C127MuzzleFlash":"C127MuzzleSmoke";fx.transform.localPosition=Vector3.zero;fx.transform.localRotation=Quaternion.identity;var own=fx.GetComponent<ParticleSystem>();var main=own.main;main.playOnAwake=false;main.loop=false;main.stopAction=ParticleSystemStopAction.None;own.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particles.GetArrayElementAtIndex(i).objectReferenceValue=own;}
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    static Gun GunSetup(GameObject root,Transform pitch,Transform recoil,string barrelName,WeaponInfo info,Gun stock)
    {
        var barrel=Find(root,barrelName);var bounds=barrel.GetComponent<Renderer>().bounds;var muzzle=Pivot("Muzzle",recoil,new Vector3(recoil.position.x,recoil.position.y,bounds.max.z+.025f));
        var gun=pitch.gameObject.AddComponent<Gun>();EditorUtility.CopySerialized(stock,gun);Set(gun,"info",info);Set(gun,"attachedUnit",null);Set(gun,"velocityInherit",null);Set(gun,"guidedProjectile",null);Set(gun,"recoilTransform",recoil);Set(gun,"recoilTravel",.24f);Set(gun,"recoilImpulse",0f);Set(gun,"recoilSound",null);Set(gun,"spinTransform",null);Set(gun,"ejectionTransform",null);Set(gun,"ejectionPrefab",null);
        Set(gun,"ammo",0);Set(gun,"magazines",0);Set(gun,"magazineCapacity",1);Set(gun,"startLoaded",false);Set(gun,"Safety",true);Set(gun,"ForceServerAuthority",true);Set(gun,"proximityTimer",false);Set(gun,"reloadTime",Duration);Set(gun,"fireRate",60/Duration);
        var s=new SerializedObject(gun);s.FindProperty("muzzles").arraySize=1;s.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue=muzzle;s.ApplyModifiedPropertiesWithoutUndo();Effects(gun,muzzle);
        foreach(var audio in stock.GetComponents<AudioSource>()){var own=pitch.gameObject.AddComponent<AudioSource>();EditorUtility.CopySerialized(audio,own);own.playOnAwake=false;own.loop=false;}
        if(!pitch.GetComponent<AudioSource>()){var audio=pitch.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;}
        gun.enabled=false;return gun;
    }
    static void AimPreview(GameObject root,Transform yaw,List<Transform> pitches,Transform pitchWheel,Transform yawWheel)
    {
        var clip=new AnimationClip{name=root.name.Replace("_Turret_Rig","")+"_Aim_Preview",legacy=true,frameRate=60,wrapMode=WrapMode.ClampForever};Func<float,float> move=t=>Smooth(t,0,.3f)*(1-Smooth(t,.7f,1));
        Curve(clip,root,yaw,"localEulerAnglesRaw.y",t=>45*move(t),8);foreach(var pitch in pitches)Curve(clip,root,pitch,"localEulerAnglesRaw.x",t=>-25*move(t),8);
        if(pitchWheel)Curve(clip,root,pitchWheel,"localEulerAnglesRaw.x",t=>450*move(t),8);if(yawWheel)Curve(clip,root,yawWheel,"localEulerAnglesRaw.x",t=>540*move(t),8);
        clip=SaveClip(clip);AttachPreview(root,clip);
    }
    static bool Small(Transform t){return t.name.Contains("LoadingRound")||t.name.Contains("EjectedCase")||t.name.Contains("Projectile")||t.name.Contains("PowderCase")||t.name.Contains("SpentCase")||t.name.Contains("Rammer")||t.name.Contains("OperatingLever")||t.name.Contains("Wheel");}
    static void Lods(GameObject root)
    {
        SetupC127LODs.ConfigureCarriage(root);var group=root.GetComponent<LODGroup>();var levels=group.GetLODs();
        for(int i=1;i<levels.Length;i++){var remove=levels[i].renderers.Where(r=>Small(r.transform.parent)).ToArray();levels[i].renderers=levels[i].renderers.Except(remove).ToArray();foreach(var r in remove)UnityEngine.Object.DestroyImmediate(r.gameObject);}
        group.SetLODs(levels);group.size=16;group.localReferencePoint=Vector3.up;levels[0].screenRelativeTransitionHeight=.055f;levels[1].screenRelativeTransitionHeight=.014f;levels[2].screenRelativeTransitionHeight=.0008f;group.SetLODs(levels);group.size=16;group.localReferencePoint=Vector3.up;
        var tris=levels.Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();if(!(tris[0]>tris[1]&&tris[1]>tris[2]))throw new Exception("LOD reduction missing");
    }
    static GameObject Build(Transform source,bool twin,Scene scene,WeaponInfo info,Gun stock)
    {
        var root=new GameObject(twin?"C127_Twin_Turret_Rig":"C127_Exposed_Turret_Rig");SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=source.position;root.SetActive(false);var model=UnityEngine.Object.Instantiate(source.gameObject,root.transform,false);model.name="ImportedModel";model.transform.SetPositionAndRotation(source.position,source.rotation);model.transform.localScale=source.lossyScale;
        var original=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!Small(r.transform)||r.transform.localScale!=Vector3.zero).ToDictionary(r=>r.name,r=>r.localToWorldMatrix);
        var oldYaw=Find(root,twin?"C127_Twin_Traverse":"C127_Exposed_Traverse");var yaw=Pivot("C127YawPivot",root.transform,source.position);oldYaw.SetParent(yaw,true);if(!twin)Find(root,"C127_Exposed_TraverseMesh").SetParent(yaw,true);var pitches=new List<Transform>();var guns=new List<Gun>();Transform pitchWheel=null,yawWheel=null;
        foreach(var label in twin?new[]{"Left","Right"}:new[]{"Exposed"}){
            var oldPitch=Find(root,twin?"C127_Twin_Elevation_"+label:"C127_Exposed_Elevation");var pitch=Pivot(twin?"Pitch"+label:"PitchPivot",yaw,oldPitch.position);oldPitch.SetParent(pitch,true);pitches.Add(pitch);
            var oldRecoil=Find(root,twin?"C127_Twin_Recoil_"+label:"C127_Exposed_Recoil");var recoil=Pivot("RecoilPivot",pitch,oldRecoil.position);oldRecoil.SetParent(recoil,true);
            guns.Add(GunSetup(root,pitch,recoil,twin?"C127_Twin_"+label+"_Barrel":"C127_Exposed_Barrel",info,stock));if(!twin)Reload(root,recoil,pitch);
        }
        if(!twin){foreach(var entry in new[]{new[]{"C127_Exposed_ElevationWheel","PitchCrankPivot"},new[]{"C127_Exposed_TraverseWheel","YawCrankPivot"}}){var mesh=Find(root,entry[0]);var pivot=Pivot(entry[1],yaw,mesh.GetComponent<Renderer>().bounds.center);mesh.SetParent(pivot,true);if(entry[1]=="PitchCrankPivot")pitchWheel=pivot;else yawWheel=pivot;}}
        var turret=yaw.gameObject.AddComponent<Turret>();EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab").GetComponentInChildren<Turret>(true),turret);Set(turret,"attachedUnit",null);Set(turret,"fireControl",null);Set(turret,"elevationTransform",pitches[0]);Set(turret,"aimSafetyWeapon",guns[0]);Set(turret,"traverseRate",12f);Set(turret,"elevationRate",8f);Set(turret,"minElevation",-10f);Set(turret,"maxElevation",twin?85f:45f);
        var ts=new SerializedObject(turret);foreach(var n in new[]{"weaponStations","criticalParts","targetDetectors"})ts.FindProperty(n).arraySize=0;ts.ApplyModifiedPropertiesWithoutUndo();turret.enabled=false;
        // Housed barrels have independent gun/recoil references. No hidden
        // breech, loading rounds or rammer animation is built for this variant.
        if(twin){var clip=new AnimationClip{name="C127_Twin_Recoil_Preview",legacy=true,frameRate=60,wrapMode=WrapMode.ClampForever};foreach(var pitch in pitches){var recoil=pitch.Find("RecoilPivot");Curve(clip,root,recoil,"localPosition.z",t=>-.24f*Smooth(t,.05f,.09f)*(1-Smooth(t,.09f,.23f)));}clip=SaveClip(clip);AttachPreview(root,clip);}
        AimPreview(root,yaw,pitches,pitchWheel,yawWheel);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(original.ContainsKey(r.name)){var before=original[r.name];for(int i=0;i<16;i++)if(Math.Abs(before[i]-r.localToWorldMatrix[i])>.0001)throw new Exception("Visible import moved: "+r.name);}
        Lods(root);root.transform.position=Vector3.zero;root.SetActive(true);var prefab=PrefabUtility.SaveAsPrefabAsset(root,Dir+root.name+".prefab");return prefab;
    }
    public static object RefreshPreviews()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        foreach(var variant in new[]{"Exposed","Twin"}){var root=PrefabUtility.LoadPrefabContents(Dir+"C127_"+variant+"_Turret_Rig.prefab");try{var yaw=Find(root,"C127YawPivot");var pitches=root.GetComponentsInChildren<Gun>(true).Select(g=>g.transform).ToList();if(variant=="Exposed")Reload(root,Find(root,"RecoilPivot"),pitches[0]);else{var clip=new AnimationClip{name="C127_Twin_Recoil_Preview",legacy=true,frameRate=60,wrapMode=WrapMode.ClampForever};foreach(var pitch in pitches)Curve(clip,root,pitch.Find("RecoilPivot"),"localPosition.z",t=>-.24f*Smooth(t,.05f,.09f)*(1-Smooth(t,.09f,.23f)));SaveClip(clip);}AimPreview(root,yaw,pitches,variant=="Exposed"?Find(root,"PitchCrankPivot"):null,variant=="Exposed"?Find(root,"YawCrankPivot"):null);}finally{PrefabUtility.UnloadPrefabContents(root);}}AssetDatabase.SaveAssets();return "Updated owned preview clips; existing prefab/source scene poses unchanged";
    }
    public static object Run()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stable edit mode required");
        var userScene=SceneManager.GetActiveScene();var original=userScene.GetRootGameObjects().Single(g=>g.name=="Gunmaggedon 127mms");var exposed=original.transform.Find("C127_Exposed_Root");var twin=original.transform.Find("C127_Twin_Root");if(!exposed||!twin)throw new Exception("Both models required");
        foreach(var name in new[]{"C127_Exposed_Turret_Rig.prefab","C127_Twin_Turret_Rig.prefab","C127_Exposed_Staged_Weapon.asset","C127_Twin_Staged_Weapon.asset"})if(AssetDatabase.LoadMainAssetAtPath(Dir+name))throw new Exception("Existing staging asset; initial helper refuses overwrite: "+name);
        Directory.CreateDirectory("Assets/GunmaggedonStaging/C127");Directory.CreateDirectory("Assets/BlueprinterSetupBackups/C127");AssetDatabase.Refresh();
        var dirty=userScene.isDirty;EditorSceneManager.SaveScene(userScene,"Assets/BlueprinterSetupBackups/C127/SceneBeforeRig.unity",true);if(dirty)EditorSceneManager.MarkSceneDirty(userScene);PrefabUtility.SaveAsPrefabAsset(original,"Assets/BlueprinterSetupBackups/C127/ImportedModels.prefab");
        var stock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Frigate1_PLACEHOLDER.prefab").GetComponentsInChildren<Gun>(true).Single(g=>g.info&&g.info.name=="Gun127mm_Guided_Naval");Func<bool,WeaponInfo> makeInfo=isTwin=>{var info=UnityEngine.Object.Instantiate(stock.info);info.name=isTwin?"C127_Twin_Staged_Weapon":"C127_Exposed_Staged_Weapon";info.weaponName=isTwin?"C-127 Twin (staging)":"C-127 Exposed (staging)";info.shortName="127mm";info.description=isTwin?"Old Guard dual-purpose twin, primarily anti-surface. Rig preview only; combat integration pending.":"Old Guard anti-surface cannon only. Ground/naval targets; no antiaircraft role. Rig/reload preview only; combat integration pending.";info.useWeaponDoors=false;info.targetRequirements.maxRange=0;Set(info,"effectiveness.antiAir",isTwin?.25f:0f);Set(info,"effectiveness.antiSurface",1f);AssetDatabase.CreateAsset(info,Dir+info.name+".asset");return info;};var exposedInfo=makeInfo(false);var twinInfo=makeInfo(true);
        var scene=EditorSceneManager.NewPreviewScene();GameObject first=null,second=null;
        try{first=Build(exposed,false,scene,exposedInfo,stock);second=Build(twin,true,scene,twinInfo,stock);AssetDatabase.SaveAssets();}finally{EditorSceneManager.ClosePreviewScene(scene);}
        var group=new GameObject("C127 Staging Rigs");Undo.RegisterCreatedObjectUndo(group,"Set up C127 staged rigs");foreach(var pair in new[]{new{prefab=first,position=exposed.position},new{prefab=second,position=twin.position}}){var instance=(GameObject)PrefabUtility.InstantiatePrefab(pair.prefab,userScene);Undo.RegisterCreatedObjectUndo(instance,"Set up C127 staged rigs");instance.transform.position=pair.position;instance.transform.SetParent(group.transform,true);}
        Undo.RecordObject(original,"Preserve imported C127 models");original.SetActive(false);EditorSceneManager.MarkSceneDirty(userScene);Selection.activeGameObject=group;
        return new{folder=Dir,exposed=first.name,twin=second.name,spawnable=false,installed=false,userSceneSaved=false,backup="Assets/BlueprinterSetupBackups/C127/SceneBeforeRig.unity"};
    }
}
