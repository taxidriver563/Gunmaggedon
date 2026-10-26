using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using NuclearOption.UIStyleSystem;
namespace GunmageddonAudio
{
    // Local instrument presentation only. Never creates a missile or alters an AI.
    internal static class R230PlayerAlert
    {
        static readonly R230AlertLedger ledger = new R230AlertLedger();
        static readonly AudioClip[] tones = new AudioClip[3];
        static AudioSource voice; static GameObject audioRoot; static Text text;
        static bool suppressed;
        static int displayCount=-1;static RailgunAlertState displayState;
        static Aircraft player; static RailgunAlertState current;
        static float nextPulse, secondPulse = float.PositiveInfinity, stopAt = float.PositiveInfinity, nextBind;
        static readonly System.Reflection.FieldInfo AlarmTypes = AccessTools.Field(typeof(ThreatList), "alarmTypes");
        internal static void Report(Unit source, Aircraft target, RailgunAlertState state, float progress, uint sequence, float age = 0)
        {
            if(suppressed || !Datum.origin || !source || !source.definition || source.definition.jsonKey != R230Runtime.Key && source.definition.jsonKey != R230Runtime.NuclearKey && source.definition.jsonKey != R230Runtime.LegacyKey) return;
            OptimizationRuntime.EnsureMission();
            Aircraft local; if(!GameManager.GetLocalAircraft(out local) || !local || local.Networkdisabled) return;
            if(player != local) { Stop(); ledger.Clear(); player = local; }
            if(state != RailgunAlertState.Off && target != local) {
                // A source switching to another aircraft must release our alert too.
                ledger.Receive(source.GetInstanceID(), 0, RailgunAlertState.Off, 0, sequence, Time.unscaledTime); return;
            }
            if(!ledger.Receive(source.GetInstanceID(), target ? target.GetInstanceID() : 0, state, progress, sequence, Time.unscaledTime-age)) return;
            if(state != RailgunAlertState.Off && !tones[0] && Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + 1; Bind(source); }
        }
        static bool Bind(Unit source)
        {
            var bank = source.transform.Find("R230PlayerAlertAudio"); if(!bank) return false;
            var names = new[] { "RG_Tracking", "RG_Charging", "RG_Firing" };
            var built = new AudioClip[3];
            for(int i=0;i<3;i++) {
                var child = bank.Find(names[i]); var template = child ? child.GetComponent<AudioSource>() : null;
                built[i] = template && template.clip ? MakeTone(template.clip, i == 2 ? .40f : .18f, i == 2) : null;
                if(!built[i]) { foreach(var clip in built) if(clip) RuntimePool<Transform>.Remove(clip); return false; }
            }
            for(int i=0;i<3;i++) tones[i] = built[i]; return true;
        }
        static AudioClip MakeTone(AudioClip original, float seconds, bool loop)
        {
            original.LoadAudioData(); var data = new float[original.samples * original.channels]; if(!original.GetData(data, 0)) return null;
            int window = Math.Max(1, original.frequency / 50), peakFrame = 0; double peak = 0;
            for(int frame=0;frame+window<original.samples;frame+=window) {
                double energy=0; for(int j=frame*original.channels;j<(frame+window)*original.channels;j++) energy+=data[j]*data[j];
                if(energy>peak) { peak=energy; peakFrame=frame; }
            }
            if(peak < .000001) return null;
            int frames=Math.Min((int)(seconds*original.frequency), original.samples-peakFrame); if(frames<64) return null;
            var samples=new float[frames*original.channels]; double sum=0;
            Array.Copy(data,peakFrame*original.channels,samples,0,samples.Length); foreach(float value in samples)sum+=value*value;
            float gain=Mathf.Min(8,.10f/(float)Math.Sqrt(sum/samples.Length)); int fade=Math.Min(frames/4,original.frequency/125);
            for(int frame=0;frame<frames;frame++) {
                float envelope=Mathf.Min(1,Mathf.Min((float)frame/fade,(float)(frames-1-frame)/fade));
                for(int ch=0;ch<original.channels;ch++) samples[frame*original.channels+ch]=Mathf.Clamp(samples[frame*original.channels+ch]*gain*envelope,-.95f,.95f);
            }
            // Short fades make repeated pulse and sustained-loop seams click-free.
            var clip=AudioClip.Create("Gunmaggedon railgun "+(loop?"sustained tone":"pulse"),frames,original.channels,original.frequency,false);clip.SetData(samples,0);return clip;
        }
        static void EnsureVoice()
        {
            if(voice) return;
            audioRoot=new GameObject("Gunmaggedon local railgun warning");
            if(Plugin.Instance) audioRoot.transform.SetParent(Plugin.Instance.transform,false);
            voice=audioRoot.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.dopplerLevel=0;
            voice.ignoreListenerPause=false;voice.priority=64;
            // Use the native threat-warning mixer when its UI has initialized.
            foreach(var list in UnityEngine.Object.FindObjectsOfType<ThreatList>()) {
                var alarms=AlarmTypes.GetValue(list) as Array; if(alarms==null) continue;
                foreach(var alarm in alarms) {
                    if(alarm==null)continue;
                    var source=AccessTools.Field(alarm.GetType(),"alarmSource").GetValue(alarm) as AudioSource;
                    if(source && source.outputAudioMixerGroup) {voice.outputAudioMixerGroup=source.outputAudioMixerGroup;return;}
                }
            }
        }
        internal static Font FallbackFont()
        {
            try{return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");}
            catch(ArgumentException){return Resources.GetBuiltinResource<Font>("Arial.ttf");}
        }
        internal static Text BuildText(Canvas canvas) { return BuildThreatRow(canvas.transform, null); }
        internal static Text BuildThreatRow(Transform parent, Text native)
        {
            var go=new GameObject("Gunmaggedon railgun threat row",typeof(RectTransform));go.transform.SetParent(parent,false);
            var backing=new GameObject("Background",typeof(RectTransform));backing.transform.SetParent(go.transform,false);
            var background=backing.AddComponent<Image>();background.raycastTarget=false;background.color=new Color(0,0,0,.72f);
            var nativeBacking=native?native.GetComponentInParent<Image>():null;
            if(nativeBacking&&nativeBacking.color.r<.12f&&nativeBacking.color.g<.12f&&nativeBacking.color.b<.12f&&nativeBacking.color.a>.1f) {
                background.sprite=nativeBacking.sprite;background.type=nativeBacking.type;background.color=nativeBacking.color;
            }
            var content=new GameObject("Label",typeof(RectTransform));content.transform.SetParent(go.transform,false);
            var label=content.AddComponent<Text>();
            label.font=native&&native.font?native.font:FallbackFont();
            if(native)label.material=native.material;
            label.fontSize=native?native.fontSize:18;label.fontStyle=native?native.fontStyle:FontStyle.Normal;
            label.lineSpacing=native?native.lineSpacing:1;label.supportRichText=native?native.supportRichText:true;
            label.alignment=native?native.alignment:TextAnchor.MiddleLeft;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;
            label.raycastTarget=false;
            var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            float height=Mathf.Max(20,label.fontSize*(native?native.lineSpacing:1)*1.3f)+6;
            float width=Mathf.Max(220,native?native.rectTransform.rect.width:260);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(width,height);
            var layout=go.AddComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=height;layout.preferredWidth=width;layout.flexibleWidth=1;
            var contentRect=label.rectTransform;contentRect.anchorMin=Vector2.zero;contentRect.anchorMax=Vector2.one;
            contentRect.offsetMin=new Vector2(8,3);contentRect.offsetMax=new Vector2(-8,-3);
            go.SetActive(false);return label;
        }
        internal static void FitRow(Text label)
        {
            float width=Mathf.Ceil(label.preferredWidth)+16;
            float height=Mathf.Max(20,Mathf.Ceil(label.preferredHeight))+6;
            var row=(RectTransform)label.transform.parent;var layout=row.GetComponent<LayoutElement>();
            layout.minWidth=layout.preferredWidth=width;layout.flexibleWidth=0;
            layout.minHeight=layout.preferredHeight=height;
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            // Native list layouts may constrain the wrapper width. The backing
            // and label retain matching content sizes independently of that clamp.
            var background=(RectTransform)row.Find("Background");
            background.anchorMin=background.anchorMax=background.pivot=new Vector2(0,1);
            background.anchoredPosition=Vector2.zero;background.sizeDelta=new Vector2(width,height);
            var content=label.rectTransform;content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1);
            content.anchoredPosition=new Vector2(8,-3);content.sizeDelta=new Vector2(width-16,height-6);
        }
        static GameObject RowObject(Text label) {return label.transform.parent.gameObject;}
        static void EnsureText()
        {
            if(text || !CombatHUD.i) return;
            var list=AccessTools.Field(typeof(CombatHUD),"threatList").GetValue(CombatHUD.i) as ThreatList;
            if(!list) return;
            var prefab=AccessTools.Field(typeof(ThreatList),"threatItemPrefab").GetValue(list) as GameObject;
            var item=prefab?prefab.GetComponent<ThreatItem>():null;
            var sample=item?AccessTools.Field(typeof(ThreatItem),"text").GetValue(item) as Text:prefab?prefab.GetComponentInChildren<Text>(true):null;
            // Borrow only presentation properties. Do not clone ThreatItem scripts,
            // register a fake missile, or modify the native warning dictionary.
            text=BuildThreatRow(list.transform,sample);
        }
        internal static Color StateColor(RailgunAlertState state,float now)
        {
            var theme=ThemeManager.Active;var colors=theme?theme.ColorTheme:null;
            Color warning=colors?colors.Warning:new Color(1,.8f,.25f);
            Color alert=colors?colors.Alert:new Color(1,.25f,.2f);
            Color clear=colors?colors.AllClear:Color.white;
            if(state==RailgunAlertState.Tracking)return warning;
            if(state==RailgunAlertState.Charging)return alert;
            // Same alert/all-clear pulse convention as native ThreatItem.
            return alert+clear*Mathf.Sin(now*20);
        }
        static void Pulse(float pitch, float now, AudioClip clip)
        { if(!clip)return;voice.Stop();voice.loop=false;voice.clip=clip;voice.pitch=pitch;voice.Play();stopAt=now+clip.length/Mathf.Max(.1f,pitch); }
        internal static void Tick()
        {
            Aircraft local;
            if(!Datum.origin || !GameManager.GetLocalAircraft(out local) || !local || local.Networkdisabled || !local.gameObject.activeInHierarchy) { Stop();ledger.Clear();player=null;return; }
            if(player!=local) {Stop();ledger.Clear();player=local;return;}
            float now=Time.unscaledTime;ledger.Prune(now);double progress;int count;
            var state=ledger.Select(local.GetInstanceID(),now,out progress,out count);
            if(state==RailgunAlertState.Off) {Stop();return;}
            EnsureVoice();EnsureText();
            if(current!=state) {
                voice.Stop();current=state;secondPulse=stopAt=float.PositiveInfinity;nextPulse=now+(state==RailgunAlertState.Tracking?.35f:0);
            }
            voice.priority=state==RailgunAlertState.Imminent?8:state==RailgunAlertState.Charging?32:64;
            voice.volume=state==RailgunAlertState.Imminent?.75f:state==RailgunAlertState.Charging?.60f:.45f;
            if(state==RailgunAlertState.Imminent) {
                if(tones[2] && (!voice.isPlaying || voice.clip!=tones[2])) {voice.Stop();voice.clip=tones[2];voice.pitch=1;voice.loop=true;voice.Play();}
            } else {
                if(now>=stopAt) {voice.Stop();stopAt=float.PositiveInfinity;}
                if(state==RailgunAlertState.Tracking && now>=secondPulse) {Pulse(1.12f,now,tones[0]);secondPulse=float.PositiveInfinity;}
                if(now>=nextPulse) {
                    Pulse(state==RailgunAlertState.Tracking?.92f:1,now,tones[state==RailgunAlertState.Tracking?0:1]);
                    nextPulse=now+(float)R230AlertPolicy.PulseInterval(state,progress);
                    if(state==RailgunAlertState.Tracking) secondPulse=now+.28f;
                }
            }
            if(text) {
                if(displayState!=state||displayCount!=count||string.IsNullOrEmpty(text.text)) {
                    text.text=state==RailgunAlertState.Tracking?"Railgun [TRACK]":state==RailgunAlertState.Charging?"Railgun [CHARGE]":"Railgun [FIRE]";
                    if(count>1)text.text+="  x"+count;
                    displayState=state;displayCount=count;FitRow(text);
                }
                text.color=StateColor(state,Time.realtimeSinceStartup);
                RowObject(text).SetActive(true);
            }
        }
        static void Stop()
        {if(voice)voice.Stop();if(text)RowObject(text).SetActive(false);current=RailgunAlertState.Off;displayCount=-1;secondPulse=stopAt=float.PositiveInfinity;}
        internal static void Enable() { suppressed=false; }
        internal static void Shutdown() { suppressed=true; Clear(); }
        internal static void Clear()
        {
            Stop();ledger.Clear();player=null;nextBind=nextPulse=0;
            if(audioRoot)RuntimePool<Transform>.Remove(audioRoot);audioRoot=null;voice=null;
            if(text)RuntimePool<Transform>.Remove(RowObject(text));text=null;
            for(int i=0;i<tones.Length;i++){if(tones[i])RuntimePool<Transform>.Remove(tones[i]);tones[i]=null;}
        }
    }
}
