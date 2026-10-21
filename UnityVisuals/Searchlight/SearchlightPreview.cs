// Editor preview only. No custom script component is serialized into the visual prefab.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GunmaggedonSearchlightPreview
{
 static readonly List<MeshRenderer> Beams=new List<MeshRenderer>();
 static readonly RaycastHit[] Hits=new RaycastHit[64];
 static readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();
 static double nextTick;static bool dirty=true;
 static GunmaggedonSearchlightPreview()
 {
  EditorApplication.hierarchyChanged+=()=>dirty=true;
  EditorApplication.update+=Tick;
 }
 public static void Rebuild(){dirty=true;nextTick=0;Tick();}
 static void Tick()
 {
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.timeSinceStartup<nextTick)return;
  nextTick=EditorApplication.timeSinceStartup+.1;
  if(dirty) {
   dirty=false;Beams.Clear();
   for(int s=0;s<SceneManager.sceneCount;s++) {
    var scene=SceneManager.GetSceneAt(s);if(!scene.isLoaded||EditorSceneManager.IsPreviewScene(scene))continue;
    foreach(var root in scene.GetRootGameObjects())
     if(root.name.IndexOf("Gunmaggedon",StringComparison.OrdinalIgnoreCase)>=0&&root.name.IndexOf("Searchlight",StringComparison.OrdinalIgnoreCase)>=0)
      foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(r.name=="SearchlightBeam")Beams.Add(r);
   }
  }
  if(Beams.Count==0)return;Physics.SyncTransforms();
  foreach(var beam in Beams) {
   if(!beam||!beam.sharedMaterial)continue;
   float maximum=Mathf.Clamp(beam.sharedMaterial.GetFloat("_BeamRange"),0,beam.sharedMaterial.GetFloat("_Length"));float visible=maximum;
   // Ignore own trailer and moving units: terrain/static structures truncate the beam.
   // A central ray keeps this cheap while depth testing handles the visible cone edges.
   int count=Physics.RaycastNonAlloc(beam.transform.position,beam.transform.forward,Hits,maximum,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
   for(int i=0;i<count;i++)if(Hits[i].collider&&!Hits[i].collider.attachedRigidbody&&
     !Hits[i].collider.transform.IsChildOf(beam.transform.root))visible=Mathf.Min(visible,Mathf.Max(.05f,Hits[i].distance-.03f));
   beam.GetPropertyBlock(Block);Block.SetFloat("_VisibleLength",visible);beam.SetPropertyBlock(Block);
   float radius=beam.sharedMaterial.GetFloat("_StartRadius")+beam.sharedMaterial.GetFloat("_Slope")*visible;
   beam.localBounds=new Bounds(new Vector3(0,0,visible*.5f),new Vector3(radius*2,radius*2,visible));
   var lamp=beam.transform.parent.GetComponentInChildren<Light>(true);if(lamp)lamp.range=visible;
  }
  SceneView.RepaintAll();
 }
}
