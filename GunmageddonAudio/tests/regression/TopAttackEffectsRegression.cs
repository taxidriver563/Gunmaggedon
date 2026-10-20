using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TopAttackEffectsRegression
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static object Run()
    {
        var a = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "GunmageddonAudio/build/1.5.4/GunmageddonAudio.dll")));
        var feature = a.GetType("GunmageddonAudio.TopAttackEffects");
        var root = new GameObject("Top attack effect test origin");
        var origin = typeof(Datum).GetField("<origin>k__BackingField", Static);
        var old = origin.GetValue(null); origin.SetValue(null, root.transform);
        try
        {
            var bomb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/CoastalTopAttackBomb.prefab");
            var reference = bomb.transform.Find("SlugEffectsReference");
            var audio = reference.GetComponentsInChildren<AudioSource>(true);
            if (audio.Length != 4 || audio.Any(s => !s.clip || s.playOnAwake || s.spatialBlend != 1)) throw new Exception("Discharge placeholder audio setup incorrect");
            if (audio.Select(s => s.clip).Distinct().Count() != 4) throw new Exception("Missing discharge variations");
            feature.GetMethod("Configure", Static).Invoke(null, new object[] { reference });
            for (int kind = 0; kind < 3; kind++)
                for (int i = 0; i < 20; i++) feature.GetMethod("Impact", Static).Invoke(null, new object[] { new Vector3(i * 20, 0, kind * 100), Vector3.up, kind == 0, kind == 2 });
            var pools = (IDictionary)feature.GetField("Pools", Static).GetValue(null);
            if (pools.Count != 3) throw new Exception("Surface-specific pools missing");
            foreach (DictionaryEntry entry in pools)
            {
                var pool = (IList)entry.Value;
                if (pool.Count != 16) throw new Exception("Impact pool exceeded budget");
                foreach (Component effect in pool)
                {
                    if (effect.GetComponentsInChildren<AudioSource>(true).Length != 0) throw new Exception("Particles duplicate sound");
                    var systems = effect.GetComponentsInChildren<ParticleSystem>(true);
                    if (systems.Length == 0 || systems.Any(ps => !ps.isPlaying || ps.time > .01f)) throw new Exception("Repeated impact failed to reset particles");
                    if (Quaternion.Angle(effect.transform.rotation, ((GameObject)entry.Key).transform.rotation) > .01f) throw new Exception("Native particle orientation lost");
                }
            }
            return "Passed: four distinct spatial placeholder discharge clips; armor, dirt and water native effects; 60 repeated impacts restart correctly; bounded pools and no duplicated audio; native particle orientation retained.";
        }
        finally { feature.GetMethod("Shutdown", Static).Invoke(null, null); origin.SetValue(null, old); UnityEngine.Object.DestroyImmediate(root); }
    }
}
