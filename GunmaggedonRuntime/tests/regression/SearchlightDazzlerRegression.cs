using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class SearchlightDazzlerRegression
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static Color[] Load(string name)
 {
  var t=new Texture2D(2,2);
  try{t.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT to the repository folder before running editor tools or fixtures."), "UnitySetupBackup/Searchlight/Preview/Searchlight_")+name+".png"));return t.GetPixels();}
  finally{UnityEngine.Object.DestroyImmediate(t);}
 }
 static double White(Color[] pixels){return pixels.Count(p=>p.r>.95&&p.g>.95&&p.b>.9)/(double)pixels.Length;}
 public static object Run()
 {
  var near=Load("DazzleNear");var far=Load("DazzleFar");var off=Load("DazzleOff");var axis=Load("DazzleOffAxis");
  var blocked=Load("DazzleBlocked");var blockedOff=Load("DazzleBlockedOff");
  double a=White(near),b=White(far);Check(a>.05&&b>.05,"Head-on dazzle covers a large part of the screen at 50m and 1km");
  Check(White(off)<.001&&White(axis)<.001,"Disabled and off-axis views do not dazzle");
  double difference=0;for(int i=0;i<blocked.Length;i++)difference=Math.Max(difference,Math.Abs(blocked[i].grayscale-blockedOff[i].grayscale));
  Check(difference<.01,"Opaque obstruction hides the entire halo, not just its centre");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Blueprinter/Mods/Gunmageddon/Visuals/Searchlight/SearchlightDazzler.shader");
  Check(ShaderUtil.GetShaderMessages(shader).Length==0,"Dazzler shader compiler messages");
  foreach(var name in new[]{"Searchlight_Visual.prefab","Searchlight_Trailer.prefab"}) {
   var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/"+name);
   var lens=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="LensGlow");
   Check(lens.sharedMaterials.Length==2&&lens.sharedMaterials[1].shader==shader,"Dazzler shares the night-gated lens renderer");
   Check(lens.GetComponent<MeshFilter>().sharedMesh.subMeshCount==2,"Both lens passes present");
  }
  return new{passed=true,whiteScreenFractionNear=a,whiteScreenFraction1km=b,occludedImageDifference=difference,checks="large head-on glare, distance persistence, off-axis fade, inactive lens, whole-flare occlusion, compiler and prefab gating"};
 }
}
