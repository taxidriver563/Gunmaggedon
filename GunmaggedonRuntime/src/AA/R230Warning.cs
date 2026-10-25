using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GunmageddonAudio
{
    // Presentation only. Shared pre-camera refresh handles rebasing and multiple cameras.
    public sealed class R230Warning:MonoBehaviour
    {
        static readonly List<R230Warning> Active=new List<R230Warning>();
        static bool subscribed;
        static readonly System.Reflection.FieldInfo NativeRenderer=HarmonyLib.AccessTools.Field(typeof(Laser),"beamRenderer");
        static Material nativeTemplate;static Mesh nativeMesh;static float nextNativeLookup;static int presenters;
        MeshRenderer nativeBeam;Material nativeMaterial;Bounds nativeBounds;float nativeEmission;
        internal const string NativeTexture="_Triplanar_e8f423b0745f4324b5f1c2df6a5d2320_Texture_1_Texture2D";
        MaterialPropertyBlock nativeBlock;
        Transform muzzle,orb,aura;LineRenderer beam,beamHalo;MeshRenderer glow,auraGlow;Mesh mesh;GlobalPosition endpoint;float progress,expires;
        Material laserMaterial,orbMaterial;Texture2D laserTexture,orbTexture;
        readonly Vector3[] points=new Vector3[2];
        MaterialPropertyBlock block;bool visible,initialized;
        MaterialPropertyBlock auraBlock;
        float warningScale=1;
        internal void SetNuclear(bool nuclear){warningScale=nuclear?4f:1f;}
        static LineRenderer Line(GameObject go,Material material)
        {var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.sharedMaterial=material;line.alignment=LineAlignment.View;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;}
        static Material MaterialFor(Texture texture,string name)
        {var shader=Shader.Find("Legacy Shaders/Particles/Additive");if(!shader)shader=Shader.Find("Sprites/Default");if(!shader)shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(!shader)throw new System.InvalidOperationException("No R230 warning shader available");var material=new Material(shader){name=name,mainTexture=texture};if(material.HasProperty("_TintColor"))material.SetColor("_TintColor",Color.white);return material;}
        internal void Initialize(Transform origin)
        {
            muzzle=origin;presenters++;initialized=true;
            nativeBlock=new MaterialPropertyBlock();block=new MaterialPropertyBlock();auraBlock=new MaterialPropertyBlock();
            // Uniform along the beam's length, soft across its width. The old
            // radial particle texture also faded the beam along its whole length.
            laserTexture=new Texture2D(1,64,TextureFormat.RGBA32,false){name="R230 laser cross-section",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++){float r=(y-31.5f)/31.5f;laserTexture.SetPixel(0,y,new Color(1,1,1,Mathf.Exp(-r*r*6)));}laserTexture.Apply();
            orbTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="R230 danger aura",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;orbTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2)));}orbTexture.Apply();
            laserMaterial=MaterialFor(laserTexture,"R230 luminous warning beam");orbMaterial=MaterialFor(orbTexture,"R230 luminous danger point");
            var go=new GameObject("R230 barrel warning laser");go.transform.SetParent(origin,false);
            beam=Line(go,laserMaterial);var haloGo=new GameObject("R230 beam halo");haloGo.transform.SetParent(go.transform,false);beamHalo=Line(haloGo,laserMaterial);
            var orbGo=new GameObject("R230 danger orb");orbGo.transform.SetParent(go.transform,false);orb=orbGo.transform;
            mesh=new Mesh{name="R230 warning billboard"};mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};mesh.RecalculateBounds();orbGo.AddComponent<MeshFilter>().sharedMesh=mesh;
            glow=orbGo.AddComponent<MeshRenderer>();glow.sharedMaterial=orbMaterial;glow.shadowCastingMode=ShadowCastingMode.Off;glow.receiveShadows=false;glow.enabled=false;
            var auraGo=new GameObject("R230 aim-point aura");auraGo.transform.SetParent(orb,false);aura=auraGo.transform;aura.localScale=Vector3.one*3.5f;auraGo.AddComponent<MeshFilter>().sharedMesh=mesh;
            auraGlow=auraGo.AddComponent<MeshRenderer>();auraGlow.sharedMaterial=orbMaterial;auraGlow.shadowCastingMode=ShadowCastingMode.Off;auraGlow.receiveShadows=false;auraGlow.enabled=false;
            TryBindNative();
        }
        // Borrow the installed game's assets. Never instantiate a Laser weapon,
        // alter its shared material, or export its texture/mesh into the mod pack.
        internal bool TryBindNative()
        {
            if(nativeBeam)return true;
            if(!nativeTemplate||!nativeMesh)
            {
                if(Time.unscaledTime<nextNativeLookup)return false;nextNativeLookup=Time.unscaledTime+1;
                nativeTemplate=null;nativeMesh=null;
                var lasers=Resources.FindObjectsOfTypeAll<Laser>();
                // Prefer the prefab's authored material over a live weapon's
                // intensity-adjusted material. Fall back if only live lasers exist.
                for(int pass=0;pass<2&&!nativeTemplate;pass++)foreach(var laser in lasers)
                {
                    if(!laser||(pass==0&&laser.gameObject.scene.IsValid()))continue;
                    var renderer=laser?NativeRenderer.GetValue(laser) as Renderer:null;
                    var material=renderer?renderer.sharedMaterial:null;var filter=renderer?renderer.GetComponent<MeshFilter>():null;
                    if(!material||!filter||!filter.sharedMesh||!material.shader||material.shader.name!="Shader Graphs/LaserCone"||!material.HasProperty("_Emissive")||!material.HasProperty(NativeTexture)||!material.GetTexture(NativeTexture))continue;
                    nativeTemplate=material;nativeMesh=filter.sharedMesh;
                    Debug.Log("[Gunmaggedon] R230 warning resolved native laser material "+material.name+", texture "+material.GetTexture(NativeTexture).name+"; private emissive copy enabled.");break;
                }
            }
            if(!nativeTemplate||!nativeMesh)return false;
            nativeMaterial=new Material(nativeTemplate){name="R230 native emissive warning laser"};nativeBounds=nativeMesh.bounds;
            // Keep native HDR tint and all other emission controls intact. A
            // single ten-percent scalar boost avoids multiplying several gains.
            nativeEmission=nativeMaterial.GetFloat("_Emissive");
            nativeMaterial.SetFloat("_Emissive",nativeEmission*1.10f);
            var go=new GameObject("R230 native laser beam");go.transform.SetParent(beam.transform,false);go.AddComponent<MeshFilter>().sharedMesh=nativeMesh;
            nativeBeam=go.AddComponent<MeshRenderer>();nativeBeam.sharedMaterial=nativeMaterial;nativeBeam.shadowCastingMode=ShadowCastingMode.Off;nativeBeam.receiveShadows=false;nativeBeam.enabled=false;
            return true;
        }
        internal void Show(GlobalPosition point,float charge,float ttl=.4f)
        {
            endpoint=point;progress=Mathf.Clamp01(charge);expires=Time.unscaledTime+ttl;visible=true;
            if(!Active.Contains(this))Active.Add(this);
            if(!subscribed){Camera.onPreCull+=RenderAll;RenderPipelineManager.beginCameraRendering+=RenderSrp;subscribed=true;}
        }
        void HideRenderers(){if(beam)beam.enabled=false;if(beamHalo)beamHalo.enabled=false;if(nativeBeam)nativeBeam.enabled=false;if(glow)glow.enabled=false;if(auraGlow)auraGlow.enabled=false;}
        internal void Hide(){if(!visible)return;visible=false;HideRenderers();Active.Remove(this);UnsubscribeIfEmpty();}
        static void UnsubscribeIfEmpty(){if(Active.Count==0&&subscribed){Camera.onPreCull-=RenderAll;RenderPipelineManager.beginCameraRendering-=RenderSrp;subscribed=false;}}
        static void RenderSrp(ScriptableRenderContext context,Camera camera){RenderAll(camera);}
        static void RenderAll(Camera camera){for(int i=Active.Count-1;i>=0;i--){var p=Active[i];if(p)p.Render(camera);}}
        void Render(Camera camera)
        {
            if(!muzzle||!camera||Time.unscaledTime>expires||!Datum.origin){HideRenderers();return;}
            var end=endpoint.ToLocalPosition();points[0]=Vector3.zero;points[1]=beam.transform.InverseTransformPoint(end);beam.SetPositions(points);beamHalo.SetPositions(points);
            float pulse=1+.12f*Mathf.Sin(Time.unscaledTime*18);var color=new Color(8+progress*10,1.2f,.9f,.95f);
            var segment=end-muzzle.position;float along=segment.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(camera.transform.position-muzzle.position,segment)/segment.sqrMagnitude):0;
            float viewDistance=Vector3.Distance(camera.transform.position,muzzle.position+segment*along);
            float metersPerPixel=camera.orthographic?2*camera.orthographicSize/Mathf.Max(1,camera.pixelHeight):2*viewDistance*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,camera.pixelHeight);
            float width=Mathf.Clamp(metersPerPixel*1.35f,.5f,80)*Mathf.Lerp(.8f,1,progress)*(warningScale>1?1.5f:1f);
            beam.startColor=beam.endColor=color;beam.startWidth=beam.endWidth=width;beam.enabled=true;
            beamHalo.startColor=beamHalo.endColor=new Color(5+progress*5,.03f,.02f,.28f);beamHalo.startWidth=beamHalo.endWidth=width*4;beamHalo.enabled=true;
            if(nativeBeam&&segment.sqrMagnitude>.0001f)
            {
                // The stock shader uses a Z-axis beam mesh, world-space texture
                // projection and _Emissive, not merely LineRenderer vertex tint.
                var bounds=nativeBounds;
                var scale=new Vector3(width*2/Mathf.Max(.001f,bounds.size.x),width*2/Mathf.Max(.001f,bounds.size.y),segment.magnitude/Mathf.Max(.001f,bounds.size.z));
                var rotation=Quaternion.LookRotation(beam.transform.InverseTransformDirection(segment));
                nativeBeam.transform.localRotation=rotation;nativeBeam.transform.localScale=scale;
                nativeBeam.transform.localPosition=-(rotation*Vector3.Scale(new Vector3(bounds.center.x,bounds.center.y,bounds.min.z),scale));
                nativeBlock.SetFloat("_Emissive",nativeEmission*Mathf.Lerp(1f,1.10f,progress));
                nativeBlock.SetVector("_WorldOffset",Datum.originPosition);
                nativeBeam.SetPropertyBlock(nativeBlock);nativeBeam.enabled=true;beam.enabled=beamHalo.enabled=false;
            }
            else if(nativeBeam)nativeBeam.enabled=false;
            orb.position=end;orb.rotation=camera.transform.rotation;float distance=Vector3.Distance(camera.transform.position,end);orb.localScale=Vector3.one*Mathf.Clamp(distance*.003f,3,150)*Mathf.Lerp(.65f,1,progress)*pulse*warningScale;
            // Cached blocks tint both billboard layers without per-frame material instances.
            block.SetColor("_Color",color);block.SetColor("_TintColor",color);glow.SetPropertyBlock(block);glow.enabled=true;
            var auraColor=new Color(7+progress*5,.06f,.025f,.35f*pulse);auraBlock.SetColor("_Color",auraColor);auraBlock.SetColor("_TintColor",auraColor);auraGlow.SetPropertyBlock(auraBlock);auraGlow.enabled=true;
        }
        void Update(){if(Time.unscaledTime>expires)Hide();else if(visible&&!nativeBeam)TryBindNative();}
        void OnDisable(){Hide();}
        static void Release(UnityEngine.Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){Hide();if(beam)Release(beam.gameObject);Release(mesh);Release(laserMaterial);Release(orbMaterial);Release(nativeMaterial);Release(laserTexture);Release(orbTexture);if(initialized){initialized=false;if(--presenters<=0){presenters=0;nativeTemplate=null;nativeMesh=null;nextNativeLookup=0;}}}
    }
}
