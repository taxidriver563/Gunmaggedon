using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Keep the original plugin ID so BepInEx retains existing configuration.
    [BepInPlugin("gunmageddon.203mm.audio", "Gunmaggedon Runtime", "1.10.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        private Harmony harmony;
        private readonly Dictionary<string, AudioClip> lastClips = new Dictionary<string, AudioClip>();
        private const string Marker = "GmAudio_";
        private sealed class AudioBank
        {
            internal AudioSource[] Near, Far;
            internal float Limit;
            internal bool Empty;
        }
        private readonly Dictionary<GameObject, AudioBank> markedBanks = new Dictionary<GameObject, AudioBank>();
        private readonly Dictionary<GameObject, AudioBank> placeholderBanks = new Dictionary<GameObject, AudioBank>();
        private readonly List<GameObject> expiredBanks = new List<GameObject>();
        private float nextBankCleanup;
        private static Transform listenerTransform;
        private static int listenerFrame = -1;
        internal void ResetAudioBanks(){markedBanks.Clear();placeholderBanks.Clear();expiredBanks.Clear();lastClips.Clear();listenerTransform=null;listenerFrame=-1;nextBankCleanup=0;}

        private void Awake()
        {
            Instance = this;
            harmony = new Harmony("gunmageddon.203mm.audio");
            try
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("Gun"), "ShotSound"),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(GunSound)));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ParticleEffectManager+PrefabEffect"), "Play"),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(ImpactSound)));
                CoastalWaterImpacts.Install(harmony);
                NativeTopAttackBomb.Install(harmony);
                CoastalArtillery.Install(harmony, Config);
                SpecialShells.Install(harmony, Config, Logger);
                SpecialPayloadNetwork.Install();
                CoastalRanging.Install(harmony, Config, Logger);
                L40Runtime.Install(Logger);
                L25Runtime.Install(Logger);
                F88Runtime.Install(Logger);
                SearchlightRuntime.Install(Logger);
                Logger.LogInfo("203mm firing and impact audio: travel delay and distance variants enabled.");
            }
            catch (Exception e) { harmony.UnpatchSelf(); Logger.LogError(e); }
        }

        private void OnDestroy()
        {
            OptimizationRuntime.Clear();
            SpecialPayloadNetwork.Shutdown();
            SearchlightRuntime.Shutdown();
            F88Runtime.Shutdown();
            L25Runtime.Shutdown();
            L40Runtime.Shutdown();
            harmony?.UnpatchSelf();
            CoastalRanging.Shutdown();
            SpecialShells.Shutdown();
            CoastalArtillery.Shutdown();
            CoastalWaterImpacts.Shutdown();
            markedBanks.Clear(); placeholderBanks.Clear(); expiredBanks.Clear(); lastClips.Clear();
            listenerTransform = null; listenerFrame = -1;
            if (Instance == this) Instance = null;
        }

        private void Update() { OptimizationRuntime.Tick(); SpecialPayloadNetwork.Tick(); F88FlakNetwork.Tick(); }
        private void LateUpdate() { TrailService.Tick(); }

        private static bool GunSound(Component __instance)
        {
            if (!Instance || !__instance || !CoastalArtillery.IsSupported(__instance as Gun)) return true;
            var sources = Instance.Bank(__instance.gameObject, true);
            if (sources.Empty) return true;
            Instance.Queue(__instance.transform, sources, "fire");
            return false;
        }

        private static void ImpactSound(ParticleEffectManager.PrefabEffect __instance)
        {
            if (!Instance) return;
            var effect = __instance.gameObject;
            if (!effect || !effect.name.StartsWith("203mm_impactEffect", StringComparison.Ordinal)) return;
            var sources = Instance.Bank(effect, true);
            if (!sources.Empty) Instance.Queue(effect.transform, sources, effect.name.Replace("(Clone)", ""));
        }

        private AudioBank Bank(GameObject owner, bool marked)
        {
            var cache = marked ? markedBanks : placeholderBanks;
            AudioBank bank;
            if (cache.TryGetValue(owner, out bank)) return bank;
            if (Time.time >= nextBankCleanup)
            {
                nextBankCleanup = Time.time + 5f;
                foreach (var banks in new[] { markedBanks, placeholderBanks })
                {
                    expiredBanks.Clear();
                    foreach (var entry in banks) if (!entry.Key) expiredBanks.Add(entry.Key);
                    foreach (var key in expiredBanks) banks.Remove(key);
                }
                expiredBanks.Clear();
            }
            var all = owner.GetComponentsInChildren<AudioSource>(true)
                .Where(s => s && s.clip && (!marked || s.gameObject.name.StartsWith(Marker, StringComparison.Ordinal))).ToArray();
            var near = all.Where(s => s.name.EndsWith("Close", StringComparison.Ordinal)).ToArray();
            var far = all.Where(s => s.name.EndsWith("Distant", StringComparison.Ordinal)).ToArray();
            if (near.Length == 0 && far.Length == 0) near = far = all;
            bank = new AudioBank { Near = near, Far = far, Empty = all.Length == 0,
                Limit = all.Length == 0 ? 0f : all.Max(s => s.maxDistance) };
            cache.Add(owner, bank); return bank;
        }

        private void Queue(Transform origin, AudioBank templates, string key)
        {
            OptimizationRuntime.EnsureMission();
            // A separate emitter survives effect pooling, movement and deactivation.
            var voice=TravellingAudio.Rent(origin.position,templates.Limit);
            CopyVoice(voice.Near,Choose(templates.Near,key+"/near"));
            CopyVoice(voice.Far,Choose(templates.Far,key+"/far"));
            TravellingAudio.Commit(voice);
        }

        internal static void PlaceholderSound(Vector3 position, GameObject source, string key)
        {
            if (!Instance || !source) return;
            OptimizationRuntime.EnsureMission();
            var templates = Instance.Bank(source, false);
            if (templates.Empty) return;
            var voice=TravellingAudio.Rent(position,templates.Limit);
            CopyVoice(voice.Near,Instance.Choose(templates.Near,key+"/near"));
            CopyVoice(voice.Far,Instance.Choose(templates.Far,key+"/far"));
            TravellingAudio.Commit(voice);
        }

        internal static Vector3 ListenerPosition()
        {
            if (listenerFrame != Time.frameCount)
            {
                listenerFrame = Time.frameCount;
                var camera = Camera.main;
                if (camera) listenerTransform = camera.transform;
                else
                {
                    var listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
                    listenerTransform = listener ? listener.transform : null;
                }
            }
            // Read the live transform: a floating-origin shift may occur within a frame.
            return listenerTransform ? listenerTransform.position : Vector3.zero;
        }

        public static float TravelSeconds(float distance) { return Mathf.Max(0f, distance) / 343f; }
        public static float DistantMix(float distance) { return Mathf.InverseLerp(400f, 1500f, distance); }

        private AudioSource Choose(AudioSource[] options, string key)
        {
            if (options.Length == 0) return null;
            AudioClip previous;
            lastClips.TryGetValue(key, out previous);
            int eligible = 0;
            foreach (var option in options) if (option && option.clip && option.clip != previous) eligible++;
            bool repeat = eligible == 0;
            if (repeat) foreach (var option in options) if (option && option.clip) eligible++;
            if (eligible == 0) return null;
            int index = UnityEngine.Random.Range(0, eligible);
            AudioSource chosen = null;
            foreach (var option in options)
                if (option && option.clip && (repeat || option.clip != previous) && index-- == 0) { chosen = option; break; }
            lastClips[key] = chosen.clip;
            return chosen;
        }

        internal static AudioSource CopyVoice(AudioSource voice, AudioSource template)
        {
            if (!template) return null;
            voice.playOnAwake = false;
            voice.loop = false;
            voice.clip = template.clip;
            voice.outputAudioMixerGroup = template.outputAudioMixerGroup;
            voice.spatialBlend = 1f;
            voice.dopplerLevel = 0f;
            voice.rolloffMode = template.rolloffMode;
            voice.minDistance = template.minDistance;
            voice.maxDistance = template.maxDistance;
            if (template.rolloffMode == AudioRolloffMode.Custom)
                voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff, template.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            voice.volume = template.volume * UnityEngine.Random.Range(.95f, 1f);
            voice.pitch = Mathf.Clamp(template.pitch * UnityEngine.Random.Range(.95f, 1.05f), .5f, 2f);
            voice.priority = template.priority;
            voice.reverbZoneMix = template.reverbZoneMix;
            return voice;
        }
    }
}
