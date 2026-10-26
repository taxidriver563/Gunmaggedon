using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HarmonyLib;
public static class R230PlayerAlertIntegration
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static Aircraft local;static int checks;
 static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 public static bool Player(ref Aircraft __0,ref bool __result){__0=local;__result=local;return false;}
 public static bool Own(ref bool __result){__result=true;return false;}
 static object Field(Type type,string name){return type.GetField(name,S).GetValue(null);}
 public static object Run(){
  checks=0;var a=Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
  var alert=a.GetTypes().Single(t=>t.Name=="R230PlayerAlert");var network=a.GetTypes().Single(t=>t.Name=="F88FlakNetwork");var controllerType=a.GetTypes().Single(t=>t.Name=="R230Controller");var state=a.GetTypes().Single(t=>t.Name=="RailgunAlertState");
  var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
  SceneManager.SetActiveScene(scene);var stage=new GameObject("Railgun player warning fixture");stage.SetActive(false);
  var origin=typeof(Datum).GetField("<origin>k__BackingField",S);var old=origin.GetValue(null);var h=new Harmony("gunmaggedon.railgun.playeralert.fixture");
  RenderTexture rt=null;Texture2D tex=null;var oldRT=RenderTexture.active;
  try{
   origin.SetValue(null,stage.transform);
   h.Patch(typeof(GameManager).GetMethod("GetLocalAircraft",S),prefix:new HarmonyMethod(typeof(R230PlayerAlertIntegration).GetMethod("Player")));
   h.Patch(typeof(Unit).GetProperty("LocalSim").GetMethod,prefix:new HarmonyMethod(typeof(R230PlayerAlertIntegration).GetMethod("Own")));
   h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(R230PlayerAlertIntegration).GetMethod("Own")));
   var craft=new GameObject("Local aircraft");local=craft.AddComponent<Aircraft>();var other=new GameObject("Other aircraft").AddComponent<Aircraft>();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230_Emplacement.prefab");var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var source=root.GetComponent<Unit>();
   var c=root.GetComponentInChildren<Turret>(true).gameObject.AddComponent(controllerType);controllerType.GetField("owner",F).SetValue(c,source);controllerType.GetField("target",F).SetValue(c,local);
   var cue=controllerType.GetMethod("Alert",F);var report=alert.GetMethod("Report",S);var tick=alert.GetMethod("Tick",S);
   alert.GetMethod("Enable",S).Invoke(null,null);
   Func<int,object> value=i=>Enum.ToObject(state,i);
   Action<int,float> publish=(s,p)=>cue.Invoke(c,new[]{value(s),(object)p});
   publish(1,0);tick.Invoke(null,null);
   Check((byte)Convert.ToByte(Field(alert,"current"))==1,"Authoritative controller reports tracking to local aircraft");
   var tones=(AudioClip[])Field(alert,"tones");Check(tones.All(t=>t&&t.length>0&&t.length<=.401),"All three provided MP3s yield short audible pulse/loop clips");
   var samples=new float[tones[0].samples*tones[0].channels];Check(tones[0].GetData(samples,0)&&samples.Any(x=>Mathf.Abs(x)>.01),"Derived tracking clip contains real custom audio samples");
   var voice=(AudioSource)Field(alert,"voice");Check(voice&&voice.spatialBlend==0&&!voice.playOnAwake&&voice.priority==64,"One local nonspatial slow tracking voice");
   alert.GetField("nextPulse",S).SetValue(null,Time.unscaledTime-1);tick.Invoke(null,null);Check(voice.clip==tones[0]&&!voice.loop&&Math.Abs(voice.pitch-.92f)<.001,"Tracking first note");
   alert.GetField("secondPulse",S).SetValue(null,Time.unscaledTime-1);tick.Invoke(null,null);Check(Math.Abs(voice.pitch-1.12f)<.001,"Tracking second note has distinct pitch");
   publish(2,.3f);tick.Invoke(null,null);Check(voice.clip==tones[1]&&!voice.loop&&voice.priority==32,"Charging uses separate pulsed custom tone");
   publish(3,.9f);tick.Invoke(null,null);Check(voice.clip==tones[2]&&voice.loop&&voice.priority==8,"Imminent uses continuous high-priority third tone");
   var canvasObject=new GameObject("Fixture HUD",typeof(Canvas));var canvas=canvasObject.GetComponent<Canvas>();
   var panelObject=new GameObject("Threat panel",typeof(RectTransform),typeof(VerticalLayoutGroup));panelObject.transform.SetParent(canvas.transform,false);var panel=(RectTransform)panelObject.transform;panel.anchorMin=panel.anchorMax=new Vector2(0,1);panel.pivot=new Vector2(0,1);panel.anchoredPosition=new Vector2(24,-180);panel.sizeDelta=new Vector2(300,100);
   var nativeSample=new GameObject("Native-style sample").AddComponent<Text>();nativeSample.font=(Font)alert.GetMethod("FallbackFont",S).Invoke(null,null);nativeSample.fontSize=16;nativeSample.fontStyle=FontStyle.Normal;nativeSample.alignment=TextAnchor.MiddleLeft;
   var label=(Text)alert.GetMethod("BuildThreatRow",S).Invoke(null,new object[]{panel,nativeSample});
   Check(label.font==nativeSample.font&&label.fontSize==nativeSample.fontSize&&label.alignment==nativeSample.alignment,"Threat row inherits supplied warning typography");
   Check(label.GetComponentInParent<LayoutElement>(true)&&label.GetComponent<Outline>()==null,"Compact layout row without banner outline");
   var background=label.transform.parent.Find("Background").GetComponent<Image>();Check(background&&background.color.r<.12f&&background.color.a>.5f&&!background.raycastTarget,"Dark warning backing with no input interception");
   Check(label.rectTransform.offsetMin.x>=8&&label.rectTransform.offsetMax.x<=-8,"Warning text padded inside backing");
   UnityEngine.Object.DestroyImmediate(nativeSample.gameObject);alert.GetField("text",S).SetValue(null,label);tick.Invoke(null,null);
   foreach(var caption in new[]{"Railgun [TRACK]","Railgun [CHARGE]","Railgun [FIRE]  x12"}) {
    label.text=caption;alert.GetMethod("FitRow",S).Invoke(null,new object[]{label});
    var backing=(RectTransform)background.transform;var row=(RectTransform)label.transform.parent;
    row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,80);
    Check(backing.rect.width>=label.preferredWidth+15&&Math.Abs(backing.rect.width-label.rectTransform.rect.width-16)<.1f,"Backing fits complete caption even when native wrapper width clamps: "+caption);
    Check(label.rectTransform.anchoredPosition==new Vector2(8,-3),"Matching label/backing padding");
   }
   alert.GetField("displayCount",S).SetValue(null,-1);tick.Invoke(null,null);
   Check(label.text.Contains("Railgun [FIRE]")&&label.gameObject.activeInHierarchy&&!label.raycastTarget,"Custom HUD caption is exclusive and does not intercept input");
   var second=UnityEngine.Object.Instantiate(prefab,stage.transform,false).GetComponent<Unit>();report.Invoke(null,new object[]{second,local,value(2),.2f,1u,0f});tick.Invoke(null,null);
   Check(label.text.Contains("x2")&&voice.loop&&((GameObject)Field(alert,"audioRoot")).GetComponents<AudioSource>().Length==1,"Two guns aggregate into one highest-priority alert");
   publish(0,0);tick.Invoke(null,null);Check((byte)Convert.ToByte(Field(alert,"current"))==2,"Discharge/cancel removes only its own gun alert");
   report.Invoke(null,new object[]{source,local,value(3),.9f,1u,0f});tick.Invoke(null,null);Check((byte)Convert.ToByte(Field(alert,"current"))==2,"Delayed earlier source sequence cannot resurrect alarm");
   report.Invoke(null,new object[]{second,other,value(1),0f,2u,0f});tick.Invoke(null,null);Check((byte)Convert.ToByte(Field(alert,"current"))==0&&!voice.isPlaying,"Gun target switch silences former player alert");
   report.Invoke(null,new object[]{source,local,value(1),0f,100u,1f});tick.Invoke(null,null);Check((byte)Convert.ToByte(Field(alert,"current"))==0,"Expired delayed cue cannot extend warning lifetime");
   // Restore a fresh lease for rendering and verify real UI output.
   report.Invoke(null,new object[]{source,local,value(3),.9f,101u,0f});tick.Invoke(null,null);
   var camera=new GameObject("HUD render camera").AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.25f,.25f);camera.nearClipPlane=.1f;camera.farClipPlane=100;
   camera.cullingMask=1<<31;foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;rt=new RenderTexture(960,640,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();label.color=(Color)alert.GetMethod("StateColor",S).Invoke(null,new object[]{value(3),0f});camera.Render();RenderTexture.active=rt;tex=new Texture2D(960,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,640),0,0);tex.Apply();int bright=tex.GetPixels().Count(x=>x.r>.2f&&x.r>x.g*1.5f);
   Check(tex.GetPixel(26,455).r<tex.GetPixel(0,0).r*.8f,"Warning backing visibly darkens the scene behind its padded row");

   var output=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"GunmaggedonRuntime/build/railgun-alert/player-warning-preview.png");File.WriteAllBytes(output,tex.EncodeToPNG());
   Check(bright>100,"Custom red HUD warning renders in controlled native Canvas: pixels="+bright+", label="+label.rectTransform.rect+", row="+label.transform.parent.GetComponent<RectTransform>().rect);
   // Actual Mirage codec, without any Missile or native MWS event.
   network.GetMethod("Install",S).Invoke(null,null);var type=a.GetTypes().Single(t=>t.Name=="R230PlayerAlertCue");var message=Activator.CreateInstance(type);
   foreach(var pair in new[]{new[]{"Source","123"},new[]{"Target","456"},new[]{"Sequence","789"}})type.GetField(pair[0],F).SetValue(message,uint.Parse(pair[1]));type.GetField("State",F).SetValue(message,(byte)3);type.GetField("Progress",F).SetValue(message,.9f);type.GetField("Time",F).SetValue(message,10d);
   var packer=typeof(Mirage.Serialization.MessagePacker);var bytes=(byte[])packer.GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1).MakeGenericMethod(type).Invoke(null,new[]{message});var copy=packer.GetMethod("Unpack").MakeGenericMethod(type).Invoke(null,new object[]{bytes,null});
   Check((uint)type.GetField("Source",F).GetValue(copy)==123&&(uint)type.GetField("Target",F).GetValue(copy)==456&&(byte)type.GetField("State",F).GetValue(copy)==3&&(uint)type.GetField("Sequence",F).GetValue(copy)==789,"Real targeted warning packet round-trip");
   Check((byte)network.GetField("ProtocolVersion",S).GetRawConstantValue()==10,"Matching client protocol required");
   local=other;tick.Invoke(null,null);Check((byte)Convert.ToByte(Field(alert,"current"))==0&&!label.transform.parent.gameObject.activeSelf,"Changing player's aircraft clears stale warning");
   alert.GetMethod("Clear",S).Invoke(null,null);Check(Field(alert,"voice")==null&&((AudioClip[])Field(alert,"tones")).All(t=>!t),"Mission cleanup destroys owned audio clips and presentation");
   return new{passed=true,checks,brightPixels=bright,details="Real custom PCM, native AudioSource settings/pulse states, controlled HUD render and native Mirage codec. Audible mission mix and live multiplayer not certified."};
  }finally{
   alert.GetMethod("Shutdown",S).Invoke(null,null);network.GetMethod("Shutdown",S).Invoke(null,null);h.UnpatchSelf();local=null;
   a.GetTypes().Single(t=>t.Name=="OptimizationRuntime").GetMethod("Clear",S).Invoke(null,null);origin.SetValue(null,old);
   RenderTexture.active=oldRT;if(tex)UnityEngine.Object.DestroyImmediate(tex);if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);
  }
 }
}
