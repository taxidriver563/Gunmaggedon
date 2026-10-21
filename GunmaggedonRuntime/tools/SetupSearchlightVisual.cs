using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SetupSearchlightVisual
{
 static Mesh Frustum(float length,float radius,float endRadius)
 {
  // Hollow two-sided skin with a transparent far raster cap so interior views
  // also cover rays that leave through the cone's far end. No opaque end disc.
  const int n=64;var v=new Vector3[n*2+1];var t=new int[n*9];int k=0;
  for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);v[i]=d*radius;v[i+n]=d*endRadius+Vector3.forward*length;}
  v[n*2]=Vector3.forward*length;
  for(int i=0;i<n;i++){int j=(i+1)%n;foreach(int x in new[]{i,j,i+n,j,j+n,i+n,n*2,i+n,j+n})t[k++]=x;}
  var m=new Mesh{name="SearchlightFrustum"};m.vertices=v;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static Mesh Disc(float radius)
 {
  const int n=64;var v=new Vector3[n+1];var t=new int[n*3];
  for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v[i+1]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);t[i*3]=0;t[i*3+1]=i+1;t[i*3+2]=(i+1)%n+1;}
  var m=new Mesh{name="SearchlightLensDisc"};m.vertices=v;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static Transform Pivot(string name,Transform parent,Vector3 point){var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.position=point;return g.transform;}
 static void SaveMesh(Mesh m,string path){var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);}
 public static object Run()
 {
  const string dir="Assets/Blueprinter/Mods/Gunmageddon/Visuals/Searchlight/";
  System.IO.Directory.CreateDirectory(dir);AssetDatabase.Refresh();
  var root=GameObject.Find("Gunmaggedon Air Defense Searchlight");if(!root)throw new Exception("Searchlight model not found");
  if(root.transform.Find("SearchlightYaw"))throw new Exception("Visual already set up; refusing duplicate pivots");
  System.IO.Directory.CreateDirectory("Assets/BlueprinterSetupBackups");AssetDatabase.Refresh();
  PrefabUtility.SaveAsPrefabAsset(root,"Assets/BlueprinterSetupBackups/Searchlight_Original.prefab");
  if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  var yaw=Pivot("SearchlightYaw",root.transform,root.transform.TransformPoint(new Vector3(0,.39145464f,0)));
  var pitch=Pivot("LampPitch",yaw,root.transform.TransformPoint(new Vector3(0,1.8f,0)));
  foreach(var n in new[]{"Cube","traverse.001"})root.transform.Find(n).SetParent(yaw,true);
  foreach(var n in new[]{"Cylinder","Cylinder.001"})root.transform.Find(n).SetParent(pitch,true);
  var origin=Pivot("BeamOrigin",pitch,root.transform.TransformPoint(new Vector3(0,1.8f,.79f)));
  const float length=13000,reach=10000,radius=.58f;float slope=Mathf.Tan(1.1f*Mathf.Deg2Rad);
  SaveMesh(Frustum(length,radius,radius+slope*length),dir+"SearchlightFrustum.asset");SaveMesh(Disc(radius),dir+"SearchlightLensDisc.asset");
  var shader=AssetDatabase.LoadAssetAtPath<Shader>(dir+"SearchlightBeam.shader");if(!shader)throw new Exception("Beam shader missing");
  var material=new Material(shader){name="SearchlightBeam"};material.SetFloat("_Length",length);material.SetFloat("_BeamRange",reach);material.SetFloat("_VisibleLength",reach);material.SetFloat("_DistanceFade",20000);material.SetFloat("_StartRadius",radius);material.SetFloat("_Slope",slope);material.SetFloat("_Opacity",.065f);material.SetFloat("_Intensity",1.8f);material.SetColor("_BeamColor",new Color(1,.94f,.82f,1));
  AssetDatabase.CreateAsset(material,dir+"SearchlightBeam.mat");
  var beam=new GameObject("SearchlightBeam",typeof(MeshFilter),typeof(MeshRenderer));beam.transform.SetParent(origin,false);
  beam.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(dir+"SearchlightFrustum.asset");var renderer=beam.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
  var lensMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="SearchlightLens"};lensMaterial.SetColor("_BaseColor",new Color(8,7.52f,6.56f,1));AssetDatabase.CreateAsset(lensMaterial,dir+"SearchlightLens.mat");
  var lens=new GameObject("LensGlow",typeof(MeshFilter),typeof(MeshRenderer));lens.transform.SetParent(origin,false);lens.transform.localPosition=Vector3.forward*.003f;lens.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(dir+"SearchlightLensDisc.asset");lens.GetComponent<MeshRenderer>().sharedMaterial=lensMaterial;lens.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
  var lightObject=new GameObject("SearchlightSpot",typeof(Light));lightObject.transform.SetParent(origin,false);var spot=lightObject.GetComponent<Light>();spot.type=LightType.Spot;spot.spotAngle=6f;spot.innerSpotAngle=2.2f;spot.range=reach;spot.intensity=1800000;spot.color=new Color(1,.94f,.82f);spot.shadows=LightShadows.None;spot.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Low;spot.shadowBias=.05f;spot.shadowNormalBias=.1f;spot.shadowNearPlane=.3f;
  pitch.localRotation=Quaternion.Euler(-35,0,0);
  var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/Blueprinter/Mods/Gunmageddon/Searchlight_Visual.prefab",InteractionMode.AutomatedAction);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
  if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(pitch.position,Quaternion.Euler(10,-35,0),8);
  return new{prefab=AssetDatabase.GetAssetPath(prefab),beamLength=reach,maximumLength=length,visualFullAngle=2.2f,nativeLightOuterAngle=6f,origin=origin.position,meshes=renderer.sharedMeshName()};
 }
 static string sharedMeshName(this MeshRenderer r){return r.GetComponent<MeshFilter>().sharedMesh.name;}
}
