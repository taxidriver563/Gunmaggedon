using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RenderSearchlightVisual
{
 public static object Run()
 {
  var scene=EditorSceneManager.NewPreviewScene();var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGBHalf);
  var image=new Texture2D(1280,800,TextureFormat.RGBAFloat,false);var encoded=new Texture2D(1280,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  string output=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/Searchlight/Preview");
  System.IO.Directory.CreateDirectory(output);
  try {
   var stage=new GameObject("Searchlight rendering fixture");SceneManager.MoveGameObjectToScene(stage,scene);stage.transform.position=new Vector3(10000,0,0);
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/Searchlight_Visual.prefab");
   var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);
   foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
   root.GetComponentInChildren<Light>().cullingMask=1<<31;
   var moon=new GameObject("Preview moon",typeof(Light));moon.transform.SetParent(stage.transform,false);moon.transform.rotation=Quaternion.Euler(35,-40,0);var ml=moon.GetComponent<Light>();ml.type=LightType.Directional;ml.intensity=.45f;ml.color=new Color(.5f,.65f,1);ml.cullingMask=1<<31;
   var camObject=new GameObject("Preview camera",typeof(Camera));camObject.transform.SetParent(stage.transform,false);var camera=camObject.GetComponent<Camera>();
   camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);rt.Create();camera.targetTexture=rt;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.006f,.009f,.018f);camera.cullingMask=1<<31;camera.nearClipPlane=.05f;camera.farClipPlane=22000;camera.fieldOfView=50;camera.allowHDR=true;
   var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthOption=CameraOverrideOption.On;
   var ground=new GameObject("Preview ground",typeof(MeshFilter),typeof(MeshRenderer));ground.transform.SetParent(stage.transform,false);ground.layer=31;
   var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-100,-.94f,-100),new Vector3(-100,-.94f,100),new Vector3(100,-.94f,100),new Vector3(100,-.94f,-100)};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();ground.GetComponent<MeshFilter>().sharedMesh=mesh;
   var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.035f,.045f,.04f));ground.GetComponent<MeshRenderer>().sharedMaterial=mat;
   var beam=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="SearchlightBeam");
   var origin=beam.transform.parent;
   var aircraft=new GameObject("Aircraft lighting proxy");aircraft.transform.SetParent(stage.transform,false);aircraft.transform.position=origin.position+origin.forward*3000;aircraft.transform.rotation=Quaternion.Euler(0,25,0);
   var aircraftMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));aircraftMaterial.SetColor("_BaseColor",new Color(.3f,.33f,.35f));aircraftMaterial.SetFloat("_Smoothness",.25f);
   foreach(var part in new[]{"Fuselage","Wings","Tail"}) {
    var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=part;g.transform.SetParent(aircraft.transform,false);g.layer=31;
    g.transform.localScale=part=="Fuselage"?new Vector3(2,2,15):part=="Wings"?new Vector3(18,.25f,4):new Vector3(.3f,4,3);
    if(part=="Tail")g.transform.localPosition=new Vector3(0,1,-5);g.GetComponent<MeshRenderer>().sharedMaterial=aircraftMaterial;
   }
   var lens=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="LensGlow");
   var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Dazzler occlusion test";blocker.transform.SetParent(stage.transform,false);blocker.layer=31;blocker.transform.position=origin.position+origin.forward*600;blocker.transform.localScale=Vector3.one*50;blocker.GetComponent<MeshRenderer>().sharedMaterial=mat;blocker.SetActive(false);
   foreach(var view in new[]{"Near","Wide","LongRange","Inside","AircraftUnlit","AircraftSpotOnly","AircraftOutside","DazzleNear","DazzleFar","DazzleOffAxis","DazzleBlocked","DazzleBlockedOff","DazzleOff"}) {
    camera.transform.position=stage.transform.position+(view=="Near"?new Vector3(10,6,14):view=="Wide"?new Vector3(55,30,90):new Vector3(11000,4500,6500));
    camera.transform.LookAt(stage.transform.position+(view=="Near"?new Vector3(0,3,3):view=="Wide"?new Vector3(0,26,38):new Vector3(0,2869,4096)));
    bool interior=view=="Inside"||view=="AircraftUnlit"||view=="AircraftSpotOnly";
    if(interior){camera.transform.position=aircraft.transform.position-origin.forward*40+origin.right*12+Vector3.up*4;camera.transform.LookAt(aircraft.transform.position);}
    if(view=="AircraftOutside"){camera.transform.position=aircraft.transform.position-origin.forward*60+origin.right*100+Vector3.up*20;camera.transform.LookAt(aircraft.transform.position);}
    root.GetComponentInChildren<Light>().enabled=view!="AircraftUnlit";beam.enabled=view!="AircraftUnlit"&&view!="AircraftSpotOnly";if(view=="AircraftSpotOnly")root.GetComponentInChildren<Light>().shadows=LightShadows.None;
    bool dazzle=view.StartsWith("Dazzle");blocker.SetActive(view=="DazzleBlocked"||view=="DazzleBlockedOff");lens.enabled=view!="DazzleOff"&&view!="DazzleBlockedOff";
    if(dazzle){float distance=view=="DazzleNear"?50:1000;var ray=view=="DazzleOffAxis"?Quaternion.AngleAxis(15,origin.up)*origin.forward:origin.forward;camera.transform.position=origin.position+ray*distance;camera.transform.LookAt(origin.position);}
    camera.Render();
    RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);
    var pixels=image.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;encoded.SetPixels(pixels);
    encoded.Apply();System.IO.File.WriteAllBytes(output+"/Searchlight_"+view+".png",encoded.EncodeToPNG());
   }
   var messages=ShaderUtil.GetShaderMessages(beam.sharedMaterial.shader).Select(m=>m.severity+": "+m.message).ToArray();
   UnityEngine.Object.DestroyImmediate(aircraftMaterial);UnityEngine.Object.DestroyImmediate(mat);UnityEngine.Object.DestroyImmediate(mesh);
   return new{images=System.IO.Directory.GetFiles(output,"*.png"),shaderMessages=messages};
  }finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(encoded);EditorSceneManager.ClosePreviewScene(scene);}
 }
}
