using System.Linq;
using UnityEngine;
using UnityEditor;

public static class SetupSearchlightDazzler
{
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/",visual=dir+"Visuals/Searchlight/";
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(visual+"SearchlightDazzler.shader");
  if(!shader)throw new System.Exception("Dazzler shader missing");
  var material=AssetDatabase.LoadAssetAtPath<Material>(visual+"SearchlightDazzler.mat");
  if(!material){material=new Material(shader){name="SearchlightDazzler"};AssetDatabase.CreateAsset(material,visual+"SearchlightDazzler.mat");}
  material.SetFloat("_Intensity",4);material.SetFloat("_Size",.7f);material.SetFloat("_ApertureRadius",.58f);material.SetFloat("_InnerAngle",1);material.SetFloat("_OuterAngle",8);material.SetFloat("_DistanceFade",20000);EditorUtility.SetDirty(material);
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(visual+"SearchlightLensDisc.asset");
  var triangles=mesh.GetTriangles(0);mesh.subMeshCount=2;mesh.SetTriangles(triangles,1);EditorUtility.SetDirty(mesh);
  var lensMaterial=AssetDatabase.LoadAssetAtPath<Material>(visual+"SearchlightLens.mat");
  foreach(var name in new[]{"Searchlight_Visual.prefab","Searchlight_Trailer.prefab"}) {
   var path=dir+name;var root=PrefabUtility.LoadPrefabContents(path);
   try{var lens=root.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name=="LensGlow");lens.sharedMaterials=new[]{lensMaterial,material};PrefabUtility.SaveAsPrefabAsset(root,path);}
   finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  AssetDatabase.SaveAssets();return new{shaderMessages=ShaderUtil.GetShaderMessages(shader).Select(m=>m.message).ToArray(),submeshes=mesh.subMeshCount,onAxisDiameterScreenHeights=1.4};
 }
}
