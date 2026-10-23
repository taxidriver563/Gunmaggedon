using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupF88Effects
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static void Backup(string path,string folder)
    {if(File.Exists(path)&&!File.Exists(Path.Combine(folder,Path.GetFileName(path))))File.Copy(path,Path.Combine(folder,Path.GetFileName(path)));}
    public static object Run()
    {
        string repo=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(repo))throw new Exception("Set GUNMAGGEDON_ROOT.");
        var shot=AssetDatabase.LoadAssetAtPath<AudioClip>(Dir+"88mm_Cannon_Fire_Close.mp3");
        var burstSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Dir+"Flak_Explosion.mp3");
        if(!shot||!burstSound)throw new Exception("F88 custom audio missing.");
        var backup=Path.Combine(repo,"UnitySetupBackup/F88/BeforeEffects");Directory.CreateDirectory(backup);
        string rigPath=Dir+"F88_Turret_Rig.prefab",effectPath=Dir+"F88_Flak_Airburst.prefab";
        Backup(rigPath,backup);Backup(effectPath,backup);
        const string nativePath="Assets/Blueprinter/_donotship/GameObject/airburst_57mm_PLACEHOLDER.prefab";
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(effectPath)&&!AssetDatabase.CopyAsset(nativePath,effectPath))throw new Exception("Failed copying native airburst.");
        var native=AssetDatabase.LoadAssetAtPath<GameObject>(nativePath);
        var effect=PrefabUtility.LoadPrefabContents(effectPath);
        try {
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true)) {
                bool smoke=ps.transform==effect.transform||ps.name=="persistentSmoke";
                string path=AnimationUtility.CalculateTransformPath(ps.transform,effect.transform);
                var original=string.IsNullOrEmpty(path)?native.GetComponent<ParticleSystem>():native.transform.Find(path).GetComponent<ParticleSystem>();
                var main=ps.main;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
                // Derive from stock every time; rerunning cannot multiply again.
                var lifetime=original.main.startLifetime;
                if(smoke) {lifetime.constantMin*=1.25f;lifetime.constantMax*=1.25f;}
                main.startLifetime=lifetime;
            }
            var sound=effect.GetComponent<AudioSource>();if(!sound)sound=effect.AddComponent<AudioSource>();
            sound.clip=burstSound;sound.loop=false;sound.playOnAwake=false;sound.mute=false;sound.spatialBlend=1;sound.dopplerLevel=0;sound.maxDistance=10000;
            PrefabUtility.SaveAsPrefabAsset(effect,effectPath);
        }finally{PrefabUtility.UnloadPrefabContents(effect);}
        var rig=PrefabUtility.LoadPrefabContents(rigPath);
        try {
            var gun=rig.GetComponentInChildren<Gun>(true);var gs=new SerializedObject(gun);
            var muzzle=(Transform)gs.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue;
            var reference=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"L40_AA_Trailer.prefab").GetComponentInChildren<Gun>(true);
            var source=reference.GetComponentsInChildren<ParticleSystem>(true);
            var emitters=new ParticleSystem[2];
            string[] originalNames={"muzzleFlashR","muzzle_R"},names={"F88MuzzleFlash","F88MuzzleSmoke"};
            for(int i=0;i<names.Length;i++) {
                var existing=muzzle.Find(names[i]);if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
                var obj=UnityEngine.Object.Instantiate(source.Single(p=>p.name==originalNames[i]).gameObject,muzzle,false);obj.name=names[i];obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.identity;
                emitters[i]=obj.GetComponent<ParticleSystem>();var main=emitters[i].main;main.loop=false;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
                if(i==0)main.startSizeMultiplier*=2f;
            }
            var audio=gun.GetComponents<AudioSource>();
            if(audio.Length==0)audio=new[]{gun.gameObject.AddComponent<AudioSource>()};
            for(int i=1;i<audio.Length;i++)UnityEngine.Object.DestroyImmediate(audio[i]);
            EditorUtility.CopySerialized(reference.GetComponent<AudioSource>(),audio[0]);
            audio[0].playOnAwake=false;audio[0].loop=false;audio[0].mute=false;audio[0].enabled=true;audio[0].dopplerLevel=0;audio[0].spatialBlend=1;audio[0].maxDistance=10000;
            gs.Update();gs.FindProperty("fireSounds").arraySize=1;gs.FindProperty("fireSounds").GetArrayElementAtIndex(0).objectReferenceValue=shot;
            gs.FindProperty("muzzleParticles").arraySize=2;for(int i=0;i<2;i++)gs.FindProperty("muzzleParticles").GetArrayElementAtIndex(i).objectReferenceValue=emitters[i];
            foreach(string name in new[]{"fireStart","fireSustained","fireEnd"})gs.FindProperty(name).objectReferenceValue=null;
            gs.FindProperty("volume").floatValue=1;gs.FindProperty("pitch").floatValue=1;gs.FindProperty("pitchVariation").floatValue=.025f;gs.FindProperty("modifyPitch").boolValue=false;
            gs.FindProperty("selfDestructEffect").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(effectPath);gs.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(rig,rigPath);
        }finally{PrefabUtility.UnloadPrefabContents(rig);}
        AssetDatabase.SaveAssets();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{muzzleParticles=2,shot=shot.name,explosion=burstSound.name,effect=effectPath,initialSmokeSeconds="6.25-12.5",persistentSmokeSeconds="12.5-31.25",combatStillStaged=true};
    }
}
