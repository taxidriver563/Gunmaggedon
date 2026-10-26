using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GunmageddonAudio
{
    // Presentation follows native bullets, including visual-only remote bullets.
    // One particle renderer for all shots; no per-ring objects, lights or damage.
    internal static class R230PressureRings
    {
        internal const float Interval=.14f,Lifetime=1.5f;
        sealed class Cursor { internal float Until; }
        static readonly Dictionary<BulletSim.Bullet,Cursor> flights=new Dictionary<BulletSim.Bullet,Cursor>();
        static ParticleSystem particles;static Mesh mesh;static Material material;
        static bool Ensure()
        {
            if(particles)return true;if(!Datum.origin)return false;
            var shader=Shader.Find("Universal Render Pipeline/Particles/Lit");
            if(!shader)shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if(!shader)shader=Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if(!shader)shader=Shader.Find("Sprites/Default");if(!shader)return false;
            material=new Material(shader){name="R230N glassy pressure rings"};
            if(material.HasProperty("_TintColor"))material.SetColor("_TintColor",Color.white);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",Color.white);
            if(material.HasProperty("_Surface")){
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
                material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
            }
            if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",.12f);
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.98f);
            if(material.HasProperty("_EmissionColor")){material.SetColor("_EmissionColor",new Color(.3f,.36f,.42f));material.EnableKeyword("_EMISSION");}
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",Texture2D.whiteTexture);
            // Rounded translucent torus: real curved normals and specular highlights,
            // rather than a flat billboard. No scene-copy/refraction pass.
            const int segments=96,tube=8;var vertices=new Vector3[(segments+1)*(tube+1)];var normals=new Vector3[vertices.Length];var colors=new Color[vertices.Length];var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*tube*6];int t=0;
            for(int i=0;i<=segments;i++){
                float angle=i*Mathf.PI*2/segments;var axis=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                for(int j=0;j<=tube;j++){
                    float cross=j*Mathf.PI*2/tube;int at=i*(tube+1)+j;
                    normals[at]=axis*Mathf.Cos(cross)+Vector3.forward*Mathf.Sin(cross);
                    vertices[at]=axis*.43f+normals[at]*.07f;uv[at]=new Vector2((float)i/segments,(float)j/tube);
                    // Clearer face with strong inner/outer reflective rims.
                    colors[at]=new Color(1,1,1,.18f+.82f*Mathf.Abs(Mathf.Cos(cross)));
                    if(i==segments||j==tube)continue;
                    int next=at+tube+1;triangles[t++]=at;triangles[t++]=at+1;triangles[t++]=next;
                    triangles[t++]=at+1;triangles[t++]=next+1;triangles[t++]=next;
                }
            }
            mesh=new Mesh{name="R230N rounded glass pressure torus"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateTangents();mesh.RecalculateBounds();
            var go=new GameObject("R230N flight pressure rings");go.SetActive(false);go.transform.SetParent(Datum.origin,false);
            particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.startLifetime=Lifetime;main.startSpeed=0;
            main.startSize=840;main.startColor=new Color(.9f,.96f,1,.8f);main.startRotation3D=true;
            main.simulationSpace=ParticleSystemSimulationSpace.Custom;main.customSimulationSpace=Datum.origin;
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.maxParticles=128;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.12f,1,1));
            var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.85f,.45f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;
            renderer.alignment=ParticleSystemRenderSpace.Local;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            renderer.lightProbeUsage=LightProbeUsage.Off;
            go.SetActive(true);particles.Play(false);return true;
        }
        static void Emit(Vector3 point,Vector3 direction,bool launch)
        {
            if(!Ensure())return;var main=particles.main;
            if(particles.particleCount+1>=main.maxParticles)main.maxParticles=Mathf.NextPowerOfTwo(main.maxParticles+1);
            var rotation=Quaternion.LookRotation(Datum.origin.InverseTransformDirection(direction.normalized));
            particles.Emit(new ParticleSystem.EmitParams{position=Datum.origin.InverseTransformPoint(point),velocity=Vector3.zero,
                rotation3D=rotation.eulerAngles,startLifetime=Lifetime,startSize=launch?1120:840,
                startColor=new Color(.9f,.96f,1,launch?.95f:.8f)},1);
            OptimizationMetrics.Count("R230PressureRings");
        }
        internal static void Begin(BulletSim.Bullet bullet,WeaponInfo info,Unit owner)
        {
            if(!R230Runtime.NuclearShot(info,owner)||flights.ContainsKey(bullet))return;
            OptimizationRuntime.EnsureMission();flights[bullet]=new Cursor{Until=Interval};
            if(bullet.velocity.sqrMagnitude>0)Emit(bullet.position.ToLocalPosition(),bullet.velocity,true);
        }
        internal static void Advance(BulletSim.Bullet bullet,GlobalPosition start,float deltaTime)
        {
            Cursor cursor;if(!flights.TryGetValue(bullet,out cursor))return;
            var from=start.ToLocalPosition();var to=bullet.position.ToLocalPosition();var direction=to-from;
            if(deltaTime>0&&direction.sqrMagnitude>.001f){float at=cursor.Until;
                while(at<=deltaTime){Emit(Vector3.Lerp(from,to,at/deltaTime),direction,false);at+=Interval;}cursor.Until=at-deltaTime;}
            if(!bullet.active)flights.Remove(bullet);
        }
        internal static void Remove(BulletSim.Bullet bullet){flights.Remove(bullet);}
        internal static void Clear()
        {
            flights.Clear();if(particles)RuntimePool<ParticleSystem>.Remove(particles.gameObject);particles=null;
            if(material)RuntimePool<ParticleSystem>.Remove(material);if(mesh)RuntimePool<ParticleSystem>.Remove(mesh);material=null;mesh=null;
        }
    }
}
