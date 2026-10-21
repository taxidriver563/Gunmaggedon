using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SearchlightCloudRenderRegression
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static object Run()
    {
        string root=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??throw new Exception("Set GUNMAGGEDON_ROOT.");
        string output=Path.Combine(root,"UnitySetupBackup/Searchlight/CloudFadePreview");Directory.CreateDirectory(output);
        var scene=EditorSceneManager.NewPreviewScene();var previous=RenderTexture.active;
        var rt=new RenderTexture(1280,800,24,RenderTextureFormat.ARGBHalf);var image=new Texture2D(1280,800,TextureFormat.RGBAFloat,false,true);
        var encoded=new Texture2D(1280,800,TextureFormat.RGB24,false);var map=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        try {
            var stage=new GameObject("Cloud fade rendering fixture");SceneManager.MoveGameObjectToScene(stage,scene);
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/Searchlight_Visual.prefab"),stage.transform,false);
            foreach(var t in model.GetComponentsInChildren<Transform>(true)){
                t.gameObject.layer=31;
                if(t.name=="LampPitch")t.localRotation=Quaternion.Euler(-45,0,0);
                if(t.name=="SearchlightYaw")t.localRotation=Quaternion.identity;
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.enabled=renderer.name=="SearchlightBeam";
            foreach(var light in model.GetComponentsInChildren<Light>())light.enabled=false;
            var beam=model.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="SearchlightBeam");var origin=beam.transform.parent;
            var cameraObject=new GameObject("Cloud fade camera",typeof(Camera));cameraObject.transform.SetParent(stage.transform,false);
            var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position=new Vector3(4500,2800,-6000);camera.transform.LookAt(origin.position+origin.forward*5000);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.002f,.003f,.005f,1);camera.cullingMask=1<<31;
            camera.nearClipPlane=.1f;camera.farClipPlane=22000;camera.fieldOfView=55;camera.allowHDR=true;rt.Create();camera.targetTexture=rt;
            var properties=new MaterialPropertyBlock();properties.SetFloat("_UseSceneDepth",0);properties.SetTexture("_CloudMap",map);properties.SetVector("_CloudUV",new Vector4(0,0,.0001f,.0001f));
            var data=new Vector4(3000-origin.position.y,400,.006f,1);
            Func<float,Color[],double> band=(distance,pixels)=>{
                var screen=camera.WorldToViewportPoint(origin.position+origin.forward*distance);int x=(int)(screen.x*1280),y=(int)(screen.y*800);double sum=0;
                for(int dy=-8;dy<=8;dy++)for(int dx=-8;dx<=8;dx++){
                    int px=x+dx,py=y+dy;if(px<0||px>=1280||py<0||py>=800)continue;
                    var c=pixels[py*1280+px];sum+=Math.Max(0,c.r-.002f)+Math.Max(0,c.g-.003f)+Math.Max(0,c.b-.005f);
                }return sum;
            };
            Color[] clear=null,dense=null,thin=null,gap=null;
            foreach(var view in new[]{"Clear","Dense","Thin","Gap"}) {
                map.SetPixels(Enumerable.Repeat(view=="Gap"?Color.black:view=="Thin"?new Color(.25f,.25f,.25f,1):Color.white,4).ToArray());map.Apply();
                properties.SetVector("_CloudData",view=="Clear"?Vector4.zero:data);beam.SetPropertyBlock(properties);camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var pixels=image.GetPixels();
                if(view=="Clear")clear=pixels;else if(view=="Dense")dense=pixels;else if(view=="Thin")thin=pixels;else gap=pixels;
                encoded.SetPixels(pixels.Select(c=>c.gamma).ToArray());encoded.Apply();File.WriteAllBytes(Path.Combine(output,"CloudFade_"+view+".png"),encoded.EncodeToPNG());
            }
            double below=band(2000,dense)/Math.Max(.00001,band(2000,clear));double above=band(6500,dense)/Math.Max(.00001,band(6500,clear));
            double gapDifference=clear.Zip(gap,(a,b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b)).Max();
            double thinAbove=band(6500,thin)/Math.Max(.00001,band(6500,clear));
            Check(band(2000,clear)>.01&&band(6500,clear)>.01,"Clear control must have visible beam pixels");
            Check(below>.97&&below<1.03,"Cloud fade changed the beam below the cloud: "+below);
            Check(above<.0001,"Dense-cloud cutoff left a visible outgoing beam: "+above);
            Check(thinAbove<.0001,"Thin cloud left the faint continuation visible: "+thinAbove);
            Check(gapDifference<.001,"Empty cloud-map gaps should preserve clear beam pixels");
            Check(ShaderUtil.GetShaderMessages(beam.sharedMaterial.shader).Length==0,"Cloud shader compilation warnings/errors");
            return new{passed=true,belowCloudRatio=below,aboveCloudRatio=above,thinCloudAboveRatio=thinAbove,gapMaximumDifference=gapDifference,previewFolder=output};
        } finally {
            RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(encoded);UnityEngine.Object.DestroyImmediate(map);EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
