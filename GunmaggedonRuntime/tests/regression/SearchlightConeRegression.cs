using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

// Run RenderSearchlightVisual first. Checks actual GPU-rendered views rather than
// inferring visibility or illumination from material and Light properties alone.
public static class SearchlightConeRegression
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/Visuals/Searchlight/";
  string images=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/Searchlight/Preview/");
  var mat=AssetDatabase.LoadAssetAtPath<Material>(dir+"SearchlightBeam.mat");
  Check(ShaderUtil.GetShaderMessages(mat.shader).Length==0,"Shader compiler errors/warnings");
  Check(mat.GetFloat("_BeamRange")==10000&&mat.GetFloat("_Length")==13000,"Existing beam reach retained");
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(dir+"SearchlightFrustum.asset");
  Check(mesh.vertexCount==129&&mesh.triangles.Length/3==192,"Hollow skin and transparent far raster cap");
  var unlit=new Texture2D(2,2);var lit=new Texture2D(2,2);var inside=new Texture2D(2,2);
  try {
   unlit.LoadImage(System.IO.File.ReadAllBytes(images+"Searchlight_AircraftUnlit.png"));
   lit.LoadImage(System.IO.File.ReadAllBytes(images+"Searchlight_AircraftSpotOnly.png"));
   inside.LoadImage(System.IO.File.ReadAllBytes(images+"Searchlight_Inside.png"));
   var a=unlit.GetPixels();var b=lit.GetPixels();var c=inside.GetPixels();double dark=0,bright=0,interior=0;int surfaces=0,background=0;
   for(int i=0;i<a.Length;i++) {
    if(a[i].grayscale>.025f){dark+=a[i].grayscale;bright+=b[i].grayscale;surfaces++;}
    else {interior+=c[i].grayscale;background++;}
   }
   double gain=bright/Math.Max(.0001,dark),fog=interior/Math.Max(1,background);
   Check(surfaces>1000&&gain>1.05,"Native spotlight visibly lights aircraft proxy at 3km");
   Check(fog>.01&&fog<.4,"Interior cone visible with bounded brightness");
   foreach(var name in new[]{"Searchlight_Visual.prefab","Searchlight_Trailer.prefab"}) {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/"+name);
    Check(prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"Missing scripts");
   }
   return new{passed=true,aircraftSurfaceBrightnessRatio=gain,insideBackgroundBrightness=fog,checks="compiled shader, existing reach, two-sided skin, actual interior rendering, isolated aircraft lighting at 3km, prefab script integrity"};
  }finally{UnityEngine.Object.DestroyImmediate(unlit);UnityEngine.Object.DestroyImmediate(lit);UnityEngine.Object.DestroyImmediate(inside);}
 }
}
