using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class F88EffectsRegression
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks,shotCalls,burstCalls;static AudioClip shotClip,burstClip;
    public static bool Shot(AudioClip clip){shotCalls++;shotClip=clip;return false;}
    public static bool Burst(AudioSource __instance){burstCalls++;burstClip=__instance.clip;return false;}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static object Run()
    {
        const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
        checks=shotCalls=burstCalls=0;shotClip=burstClip=null;
        var h=new Harmony("gunmaggedon.f88.effects.fixture");
        h.Patch(typeof(AudioSource).GetMethod("PlayOneShot",new[]{typeof(AudioClip)}),new HarmonyMethod(typeof(F88EffectsRegression).GetMethod("Shot")));
        h.Patch(typeof(AudioSource).GetMethod("Play",Type.EmptyTypes),new HarmonyMethod(typeof(F88EffectsRegression).GetMethod("Burst")));
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("F88 effects fixture");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var old=datum.GetValue(null);ParticleEffectManager.PrefabEffect effect=null;
        try {
            datum.SetValue(null,stage.transform);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F88_Turret_Rig.prefab");var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var gun=root.GetComponentInChildren<Gun>(true);var so=new SerializedObject(gun);
            var muzzle=(Transform)so.FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue;
            var particles=so.FindProperty("muzzleParticles");Check(particles.arraySize==2,"Native gun has both muzzle flash and smoke emitters assigned");
            for(int i=0;i<particles.arraySize;i++) {
                var ps=(ParticleSystem)particles.GetArrayElementAtIndex(i).objectReferenceValue;
                Check(ps&&ps.transform.parent==muzzle&&ps.transform.localPosition==Vector3.zero,"Actual muzzle emitter is aligned with recoiling muzzle");
                Check(Vector3.Dot(ps.transform.forward,muzzle.forward)>.999f&&!ps.main.loop&&!ps.main.playOnAwake,"Muzzle emitter points down barrel and only bursts on command");
            }
            var flash=muzzle.Find("F88MuzzleFlash").GetComponent<ParticleSystem>();Check(flash.main.startLifetime.constantMax<=.1f,"Muzzle flash remains brief");
            var before=muzzle.position;var recoil=muzzle.parent;recoil.localPosition=Vector3.back*.55f;
            Check(Vector3.Distance(flash.transform.position,before)>.5f&&flash.transform.position==muzzle.position,"Flash follows recoil without lagging behind muzzle");recoil.localPosition=Vector3.zero;
            var owner=new GameObject("Audio fixture unit").AddComponent<Building>();owner.transform.SetParent(stage.transform,false);gun.attachedUnit=owner;typeof(Unit).GetField("displayDetail",F).SetValue(owner,1f);
            var sources=gun.GetComponents<AudioSource>();Check(sources.Length==1&&!sources[0].loop&&!sources[0].mute&&sources[0].enabled,"Single enabled one-shot source avoids rotary audio routing");
            typeof(Gun).GetField("sources",F).SetValue(gun,sources);typeof(Gun).GetMethod("ShotSound",F).Invoke(gun,null);
            Check(shotCalls==1&&shotClip&&shotClip.name=="88mm_Cannon_Fire_Close","Native firing callback plays the custom 88mm clip");
            Check(new[]{"fireStart","fireSustained","fireEnd"}.All(n=>!so.FindProperty(n).objectReferenceValue),"No looping rotary audio layers");
            var airburst=AssetDatabase.LoadAssetAtPath<GameObject>(dir+"F88_Flak_Airburst.prefab");Check(so.FindProperty("selfDestructEffect").objectReferenceValue==airburst,"Native airburst effect uses the F88 prefab");
            var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/airburst_57mm_PLACEHOLDER.prefab");
            foreach(var ps in airburst.GetComponentsInChildren<ParticleSystem>(true)) {
                var path=AnimationUtility.CalculateTransformPath(ps.transform,airburst.transform);
                var stock=string.IsNullOrEmpty(path)?native.GetComponent<ParticleSystem>():native.transform.Find(path).GetComponent<ParticleSystem>();
                float factor=(string.IsNullOrEmpty(path)||ps.name=="persistentSmoke")?1.25f:1f;
                Check(Mathf.Approximately(ps.main.startLifetime.constantMax,stock.main.startLifetime.constantMax*factor),"Only smoke lifetime extended: "+ps.name);
                Check(ps.emission.burstCount==stock.emission.burstCount&&Mathf.Approximately(ps.emission.rateOverTime.constantMax,stock.emission.rateOverTime.constantMax)&&!ps.main.loop,"Emission density/looping unchanged: "+ps.name);
            }
            stage.SetActive(true);effect=new ParticleEffectManager.PrefabEffect(airburst,null);effect.Play(Vector3.zero,Quaternion.identity);
            Check(burstCalls==1&&burstClip&&burstClip.name=="Flak_Explosion","Native effect callback plays the custom flak explosion clip");
            foreach(var clip in new[]{shotClip,burstClip}) {
                clip.LoadAudioData();var samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0)&&samples.Any(v=>Mathf.Abs(v)>.001f),"Custom clip decodes non-silent audio: "+clip.name);
            }
            foreach(var ps in effect.systems){ps.useAutoRandomSeed=false;ps.randomSeed=17;ps.Simulate(11,false,true,false);}
            Check(effect.systems.Single(p=>p.name=="persistentSmoke").particleCount>0,"Flak puff is still present eleven seconds after burst");
            Check(effect.systems.Single(p=>p.name=="flash").particleCount==0,"Burst flash does not linger with the puff");
            foreach(var ps in effect.systems)ps.Simulate(34,false,true,false);
            Check(effect.systems.All(p=>p.particleCount==0),"Extended effect still finishes without immortal particles");
            Check(!prefab.GetComponentInChildren<Gun>(true).enabled&&!prefab.GetComponentInChildren<Turret>(true).enabled,"Adding effects does not enable unfinished combat integration");
            return new{passed=true,checks,shot=shotClip.name,burst=burstClip.name,smokeLifetimeMultiplier=1.25f};
        }finally{h.UnpatchSelf();if(effect!=null&&effect.gameObject)UnityEngine.Object.DestroyImmediate(effect.gameObject);datum.SetValue(null,old);EditorSceneManager.ClosePreviewScene(scene);shotClip=burstClip=null;}
    }
}
