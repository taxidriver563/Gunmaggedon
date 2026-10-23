using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class F88RigRegression
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,F).SetValue(obj,value);}
    static object Call(object obj,string name,params object[] args){return obj.GetType().GetMethod(name,F).Invoke(obj,args);}
    public static object Run()
    {
        checks=0;string repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??Path.Combine(repo,"GunmaggedonRuntime/build/1.9.2/Gunmaggedon.Runtime.dll")));
        var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var runtime=a.GetType("GunmageddonAudio.F88Runtime");var motionType=a.GetType("GunmageddonAudio.F88TurretMotion");
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("F88 rig fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        try {
            const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F88_Turret_Rig.prefab");Check(prefab,"Saved F88 prefab exists");
            var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var turret=root.GetComponentInChildren<Turret>(true);var gun=root.GetComponentInChildren<Gun>(true);
            var yaw=turret.transform;var pitch=gun.transform;var recoil=pitch.Find("RecoilPivot");var breech=pitch.Find("BreechSlidePivot");var anchor=pitch.Find("BreechSlideRest");
            var muzzle=recoil.Find("Muzzle");var left=yaw.Find("PitchCrankPivot");var right=yaw.Find("YawCrankPivot");
            Check(!root.GetComponent<Unit>()&&!turret.enabled&&!gun.enabled&&gun.Safety,"Rig-only asset has no mission unit and native combat callbacks are parked");
            Check(yaw.Find("traverse")&&yaw.Find("actualtraverse")&&pitch.Find("gun"),"Actual yaw and elevation meshes are owned by the aiming pivots");
            Check(recoil.localPosition.sqrMagnitude<1e-8&&recoil.Find("barrel")&&breech.Find("breech"),"Zero-rest recoil owns barrel; breech door belongs to pitch pivot");
            Check(left.Find("elevationcrank")&&right.Find("yawcrank"),"Actual crank meshes are attached");
            Check(gun.attachedUnit==null&&gun.info==null,"No borrowed L40 unit or ammunition definition");
            var ts=new SerializedObject(turret);Check(ts.FindProperty("attachedUnit").objectReferenceValue==null&&ts.FindProperty("elevationTransform").objectReferenceValue==pitch,"Native turret points at F88 pitch, without an L40 owner");
            Check(ts.FindProperty("targetDetectors").arraySize==0&&ts.FindProperty("weaponStations").arraySize==0&&ts.FindProperty("criticalParts").arraySize==0,"No source L40 runtime/component lists remain");
            var gs=new SerializedObject(gun);Check(gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue==muzzle&&gs.FindProperty("recoilTransform").objectReferenceValue==recoil,"Native gun recoil and muzzle references are internal");
            Check(!gs.FindProperty("velocityInherit").objectReferenceValue&&gs.FindProperty("ejectionPrefab").objectReferenceValue.name=="F88_SpentCasing"&&gs.FindProperty("fireSounds").arraySize==1&&gs.FindProperty("fireSounds").GetArrayElementAtIndex(0).objectReferenceValue.name=="88mm_Cannon_Fire_Close","No L40 body/casing references; F88 uses its own firing audio");
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BlueprinterSetupBackups/F88/F88_Original.prefab");
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                var authored=original.transform.Find(renderer.name);if(!authored)continue;
                var before=original.transform.worldToLocalMatrix*authored.localToWorldMatrix;var after=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                bool same=true;for(int i=0;i<16;i++)same &= Mathf.Abs(before[i]-after[i])<.0001f;
                Check(same,"Saved original mesh pose retained: "+renderer.name);
            }
            var fixedBase=root.transform.Find("Model/base");var basePose=fixedBase.localToWorldMatrix;
            var barrel=recoil.Find("barrel");var beforeYaw=barrel.position;
            yaw.localRotation=Quaternion.Euler(0,90,0);Check(Vector3.Distance(barrel.position,beforeYaw)>1,"Visible barrel traverses");
            var beforePitch=barrel.position;pitch.localRotation=Quaternion.Euler(-45,0,0);Check(Vector3.Distance(barrel.position,beforePitch)>.5f,"Visible barrel elevates around trunnion");
            Check(fixedBase.localToWorldMatrix==basePose,"Carriage remains fixed while aiming");yaw.localRotation=Quaternion.identity;pitch.localRotation=Quaternion.identity;
            runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});runtime.GetMethod("Initialize",S).Invoke(null,new object[]{turret});
            var motion=root.GetComponentInChildren(motionType,true);Check(motion&&root.GetComponentsInChildren(motionType,true).Length==1,"Native initialization attaches exactly one F88 mechanism controller");Call(motion,"Awake");
            yaw.localRotation=Quaternion.Euler(0,7.5f,0);pitch.localRotation=Quaternion.Euler(-5,0,0);Call(motion,"LateUpdate");
            Check(Mathf.Abs(Mathf.DeltaAngle(right.localEulerAngles.x,45))<.01f&&Mathf.Abs(Mathf.DeltaAngle(left.localEulerAngles.x,60))<.01f,"Both handles follow actual aim changes");
            var leftStill=left.localRotation;var rightStill=right.localRotation;Call(motion,"LateUpdate");Check(left.localRotation==leftStill&&right.localRotation==rightStill,"Handles stop when aiming stops");
            var cycle=motionType.GetField("cycle",F).GetValue(motion);var cycleType=cycle.GetType();
            cycleType.GetMethod("Begin",F).Invoke(cycle,new object[]{0d});
            cycleType.GetMethod("Tick",F).Invoke(cycle,new object[]{.6d});cycleType.GetMethod("Tick",F).Invoke(cycle,new object[]{1d});Call(motion,"ApplyPose",1d);
            Check(Vector3.Distance(breech.localPosition,anchor.localPosition+Vector3.right*.36f)<.0001f,"Reload sequence opens breech right by 36cm before extraction");
            var displacement=breech.position-anchor.position;Check(Vector3.Dot(displacement,pitch.right)>.359f,"Breech slide stays on gun-right at nonzero yaw and elevation");
            foreach(double t in new[]{1.8,3.8,5.4,6,6.2})cycleType.GetMethod("Tick",F).Invoke(cycle,new object[]{t});Call(motion,"ApplyPose",6.2d);
            Check(Vector3.Distance(breech.localPosition,anchor.localPosition)<.0001f,"Breech is closed and secured when reload completes");
            Call(motion,"OnDisable");Check(breech.localPosition==anchor.localPosition,"Disabling controller restores breech rest");Set(gun,"timeUntilReload",0f);
            Set(gun,"sources",new AudioSource[0]);Set(gun,"ticksSinceTriggerPull",100);Set(gun,"queuedBullets",0f);Set(gun,"bulletsLoaded",1);Set(gun,"lastFired",Time.timeSinceLevelLoad);Set(gun,"recoilEnergy",1f);Set(gun,"recoilPosition",0f);
            var barrelRest=barrel.position;var muzzleRest=muzzle.position;var pitchRest=pitch.position;var breechRest=breech.position;Call(gun,"FixedUpdate");
            Check(Vector3.Distance(barrel.position,barrelRest)>.01f&&Vector3.Distance(muzzle.position,muzzleRest)>.01f,"Native recoil moves visible barrel and muzzle");
            Check(Vector3.Distance(breech.position,breechRest)<.0001f,"Breech door stays fixed during native recoil at nonzero aim");
            Check(pitch.position==pitchRest,"Recoil does not displace elevation pivot");Set(gun,"recoilEnergy",0f);for(int i=0;i<160;i++)Call(gun,"FixedUpdate");Check(Vector3.Distance(barrel.position,barrelRest)<.0001f,"Native recoil returns without accumulating barrel offset");
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(dir+"F88_Mechanism_Preview.anim");clip.SampleAnimation(root,4.5f);
            Check(Vector3.Distance(breech.localPosition,anchor.localPosition+Vector3.right*.36f)<.0001f,"Editor preview visibly opens actual breech");clip.SampleAnimation(root,10f);
            Check(yaw.localRotation==Quaternion.identity&&pitch.localRotation==Quaternion.identity&&recoil.localPosition==Vector3.zero&&breech.localPosition==anchor.localPosition,"Editor preview ends in exact rest pose");
            Check(!root.GetComponent<Animation>().playAutomatically,"Editor demo never overrides native aiming automatically");
            Check(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Saved rig has no missing scripts");
            return new{passed=true,checks,details="Saved visible rig, original pose, native muzzle/recoil, geared handles, right-slide native reload, rest recovery and editor preview"};
        }finally{runtime.GetMethod("Shutdown",S).Invoke(null,null);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
