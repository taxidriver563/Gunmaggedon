using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class UnitLODIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static object Call(object target,string method,params object[] args){return target.GetType().GetMethod(method,F).Invoke(target,args);}
    public static bool ProductionVisibility(object __instance,ref bool __result){__result=(bool)Call(__instance,"ShouldAnimate",true);return false;}
    public static async Task<object> Run()
    {
        checks=0;const string dir="Assets/Blueprinter/Mods/Gunmageddon/";
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));
        var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var detailType=a.GetType("GunmageddonAudio.ModelDetail");
        var h=new Harmony("gunmaggedon.lod.fixture");
        h.Patch(detailType.GetProperty("Animate",F).GetGetMethod(true),prefix:new HarmonyMethod(typeof(UnitLODIntegration).GetMethod("ProductionVisibility")));
        var scene=EditorSceneManager.NewPreviewScene();var rt=new RenderTexture(320,240,24);rt.Create();
        try{
            var co=new GameObject("LOD validation camera",typeof(Camera));SceneManager.MoveGameObjectToScene(co,scene);var camera=co.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.targetTexture=rt;camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=1000;
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{dir}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>{var g=AssetDatabase.LoadAssetAtPath<GameObject>(p);return g.GetComponent<Unit>()||p.EndsWith("_Turret_Rig.prefab");}).ToArray();
            Check(paths.Length==9,"All seven units and both staged rigs covered");
            foreach(var path in paths){
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var root=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                foreach(var group in root.GetComponentsInChildren<LODGroup>(true)){
                    var levels=group.GetLODs();Check(group.enabled&&levels.Length==3,path+" has three enabled LOD levels");Check(group.size>0&&group.size<100,path+" finite physical-model bounds exclude beam");
                    int previous=int.MaxValue;float height=1;
                    foreach(var level in levels){Check(level.screenRelativeTransitionHeight>0&&level.screenRelativeTransitionHeight<height,"Descending LOD thresholds");height=level.screenRelativeTransitionHeight;
                        Check(level.renderers.Length>0&&level.renderers.All(r=>r&&r.transform.IsChildOf(group.transform)),"Valid renderer references stay inside their group");
                        int count=level.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);Check(count<previous,"Every subsequent level actually reduces geometry");previous=count;
                        foreach(var r in level.renderers){var mesh=r.GetComponent<MeshFilter>().sharedMesh;Check(mesh.subMeshCount<=r.sharedMaterials.Length&&r.sharedMaterials.All(m=>m),"All mesh sections retain materials");Check(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)),"Finite generated vertex positions");}
                    }
                    Check(!levels.Skip(1).SelectMany(l=>l.renderers).Any(r=>r.transform.parent.name.Contains("crank")||r.transform.parent.name.StartsWith("shell_")),"Distant levels omit small loading and crank parts");
                }
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
                    bool assigned=root.GetComponentsInChildren<LODGroup>(true).SelectMany(g=>g.GetLODs()).Any(l=>l.renderers.Contains(renderer));
                    Check(assigned||(renderer.name=="SearchlightBeam"||renderer.name=="LensGlow"),"Every model renderer is assigned; only beam and glare exempt");
                    if(renderer.name=="GM_LOD1"||renderer.name=="GM_LOD2")Check(renderer.transform.localPosition==Vector3.zero&&renderer.transform.localRotation==Quaternion.identity&&renderer.transform.localScale==Vector3.one,"LOD copies follow original articulated transform exactly");
                }
                root.SetActive(true);var lod=root.GetComponent<LODGroup>();var bounds=new Bounds(root.transform.TransformPoint(lod.localReferencePoint),Vector3.one*lod.size);camera.transform.position=bounds.center+new Vector3(1,.6f,1).normalized*lod.size*2;camera.transform.LookAt(bounds.center);
                var detail=Activator.CreateInstance(detailType,F,null,new object[]{root.transform},null);
                // Unity retains visibility for the rest of a render frame. Test
                // far first, then near, rather than expecting a same-frame clear.
                lod.ForceLOD(2);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check(!(bool)Call(detail,"ShouldAnimate",true),path+" distant camera render suspends cosmetic updates");
                if(path.EndsWith("L25_AA_Trailer.prefab")){
                    var yaw=root.transform.Find("L25YawPivot");var motion=yaw.gameObject.AddComponent(a.GetType("GunmageddonAudio.L25TurretMotion"));Call(motion,"Awake");
                    var gun=root.GetComponentsInChildren<Gun>(true)[0];var barrel=yaw.Find("PitchPivot/BarrelLeft");var rest=barrel.localPosition;var handle=yaw.Find("YawCrankPivot");var old=handle.localRotation;
                    Call(motion,"ShotFired",gun);yaw.localRotation=Quaternion.Euler(0,20,0);Call(motion,"LateUpdate");
                    Check(barrel.localPosition==rest&&handle.localRotation==old,"Distant L25 skips recoil and crank animation");
                    Check((int)motion.GetType().GetField("tracerPhase",F).GetValue(motion)==1,"Distant shot still advances tracer phase");
                    lod.ForceLOD(0);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Call(motion,"ShotFired",gun);Check(barrel.localPosition.z<rest.z-.1f,"Near L25 resumes barrel recoil");
                }
                if(path.EndsWith("F88_AA_Trailer.prefab")){
                    var yaw=root.transform.Find("F88YawPivot");var motion=yaw.gameObject.AddComponent(a.GetType("GunmageddonAudio.F88TurretMotion"));Call(motion,"Awake");Call(motion,"BeginReload");
                    var cycle=motion.GetType().GetField("cycle",F).GetValue(motion);double now=Time.time;
                    foreach(double offset in new[]{.6,1,1.8,3.8,5.4,6,6.2}){
                        Call(cycle,"Tick",now+offset);Call(motion,"ApplyPose",now+offset);
                        if(offset<6.2)Check(!(bool)motion.GetType().GetProperty("Ready",F).GetValue(motion),"Distant F88 cannot become ready before secured dwell");
                    }
                    Check((bool)motion.GetType().GetProperty("Ready",F).GetValue(motion),"Distant F88 completes full reload clock");
                    var visuals=yaw.Find("PitchPivot/ReloadVisuals");Check(!visuals.Find("ExtractingCasing").gameObject.activeSelf&&!visuals.Find("LoadingShell").gameObject.activeSelf,"Distant F88 loading props remain hidden");
                    Call(motion,"BeginReload");Call(cycle,"Tick",now+.6);Call(cycle,"Tick",now+1);Call(cycle,"Tick",now+1.8);Call(cycle,"Tick",now+3.8);
                    lod.ForceLOD(0);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Call(motion,"ApplyPose",now+4.6);Check(visuals.Find("LoadingShell").gameObject.activeSelf,"Returning near mid-reload restores current loading pose");
                }
                lod.ForceLOD(0);EditorApplication.QueuePlayerLoopUpdate();await Task.Delay(150);camera.Render();Check((bool)Call(detail,"ShouldAnimate",true),path+" full-detail camera render enables cosmetic updates");
                lod.ForceLOD(-1);UnityEngine.Object.DestroyImmediate(root);
            }
            return new {passed=true,checks,details="All unit/rig LOD coverage, cheaper meshes, material references, rendered near/far cosmetic gating and distance-independent F88 reload"};
        }finally{h.UnpatchSelf();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
