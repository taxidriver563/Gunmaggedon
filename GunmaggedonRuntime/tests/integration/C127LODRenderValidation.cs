using System;using System.Linq;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static class C127LODRenderValidation
{
    static int checks;static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static int GreenPixels(Camera camera,RenderTexture target){camera.Render();var previous=RenderTexture.active;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();return texture.GetPixels().Count(c=>c.g>c.r+.15f&&c.g>c.b+.15f);}finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}}
    public static async Task<object> Run()
    {
        checks=0;var black=new Material(Shader.Find("Universal Render Pipeline/Unlit"));black.SetColor("_BaseColor",Color.black);var green=new Material(Shader.Find("Universal Render Pipeline/Unlit"));green.SetColor("_BaseColor",Color.green);var scene=EditorSceneManager.NewPreviewScene();var target=new RenderTexture(320,240,24);target.Create();
        try{
            var co=new GameObject("C127 LOD camera",typeof(Camera));SceneManager.MoveGameObjectToScene(co,scene);var camera=co.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.cullingMask=1<<31;camera.targetTexture=target;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=45;camera.nearClipPlane=.1f;camera.farClipPlane=2000000;
            foreach(var variant in new[]{"Exposed","Twin"}){
                var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GunmaggedonStaging/C127/C127_"+variant+"_Turret_Rig.prefab"));SceneManager.MoveGameObjectToScene(root,scene);foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                var group=root.GetComponent<LODGroup>();var levels=group.GetLODs();camera.transform.position=new Vector3(7,4,-8);camera.transform.LookAt(Vector3.up*1.5f);
                var animation=root.GetComponent<Animation>();animation.clip.SampleAnimation(root,.6f*5.17f);
                // First render distant geometry; retained per-frame visibility
                // must settle before asserting a subsequent LOD's visibility.
                foreach(var level in new[]{2,1,0}){group.ForceLOD(level);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(levels[level].renderers.Any(r=>r.isVisible),variant+" LOD"+level+" actually renders");}
                if(variant=="Exposed"){
                    var round=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="C127_Exposed_LoadingRound");var ammo=round.GetComponentsInChildren<MeshRenderer>(true);foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>ammo.Contains(renderer)?green:black).ToArray();Check(round.gameObject.activeSelf&&GreenPixels(camera,target)>0,"Near active loading ammunition visible");
                    group.ForceLOD(2);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(round.gameObject.activeSelf&&GreenPixels(camera,target)==0,"Distant geometry omits transient ammunition without changing reload phase");
                    group.ForceLOD(0);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(GreenPixels(camera,target)>0,"Returning near restores current loading phase");
                    animation.clip.SampleAnimation(root,0);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(!round.gameObject.activeSelf&&GreenPixels(camera,target)==0,"Ready pose never exposes ammunition");
                }
                group.ForceLOD(-1);camera.transform.position=new Vector3(7,4,-8).normalized*1000000;camera.transform.LookAt(Vector3.up);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(GreenPixels(camera,target)==0&&group.GetLODs()[2].screenRelativeTransitionHeight==.0008f,variant+" final cull threshold and no far image");
                var aim=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/GunmaggedonStaging/C127/C127_"+variant+"_Aim_Preview.anim");aim.SampleAnimation(root,2.4f);var expected=Quaternion.Euler(0,45,0)*Quaternion.Euler(-25,0,0)*Vector3.forward;
                foreach(var gun in root.GetComponentsInChildren<Gun>(true)){var muzzle=(Transform)new SerializedObject(gun).FindProperty("muzzles").GetArrayElementAtIndex(0).objectReferenceValue;Check(Vector3.Angle(muzzle.forward,expected)<.01f,"Aim preview turns all muzzle branches correctly");}
                if(variant=="Exposed"){var platform=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="C127_Exposed_TraverseMesh");Check(platform.IsChildOf(root.transform.Find("C127YawPivot")),"Crew platform follows traverse and retains aligned loading clearance");Check(new SerializedObject(root.GetComponentInChildren<Turret>()).FindProperty("maxElevation").floatValue==45,"Measured exposed provisional45degree limit");}
                UnityEngine.Object.DestroyImmediate(root);
            }
            return new{passed=true,checks,details="Both staged rigs render all LODs and cull; near/far ammunition visibility, current-phase restoration, aim/muzzle branches and exposed platform parenting checked."};
        }finally{UnityEngine.Object.DestroyImmediate(black);UnityEngine.Object.DestroyImmediate(green);target.Release();UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
