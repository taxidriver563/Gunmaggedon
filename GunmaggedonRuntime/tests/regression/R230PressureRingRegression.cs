using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class R230PressureRingRegression
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static object Run()
    {
        checks=0;var assembly=Assembly.LoadFrom(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER"));
        var type=assembly.GetTypes().Single(t=>t.Name=="R230PressureRings");var nuclear=assembly.GetTypes().Single(t=>t.Name=="R230NuclearRuntime");
        var optimization=assembly.GetTypes().Single(t=>t.Name=="OptimizationRuntime");
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("Pressure ring test");SceneManager.MoveGameObjectToScene(stage,scene);
        var origin=typeof(Datum).GetField("<origin>k__BackingField",S);var prior=origin.GetValue(null);
        var originPosition=typeof(Datum).GetField("<originPosition>k__BackingField",S);var priorPosition=originPosition.GetValue(null);
        RenderTexture target=null;Texture2D image=null;var oldTarget=RenderTexture.active;
        try
        {
            origin.SetValue(null,stage.transform);originPosition.SetValue(null,Vector3.zero);optimization.GetMethod("EnsureMission",S).Invoke(null,null);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230N_Emplacement.prefab");
            var gun=prefab.GetComponentInChildren<Gun>(true);var owner=prefab.GetComponent<Unit>();
            var normal=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230_Emplacement.prefab").GetComponentInChildren<Gun>(true);
            var group=prefab.GetComponent<LODGroup>();var triangles=group.GetLODs().Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();
            Check(group.enabled&&triangles.SequenceEqual(new[]{1060,610,288}),"Existing three reduced model LODs preserved");
            var bullet=new BulletSim.Bullet(Vector3.forward*10000,null,1,true,false,null){position=Vector3.zero.ToGlobalPosition(),velocity=Vector3.forward*10000,active=true,destructTimer=10};
            Action begin=()=>type.GetMethod("Begin",S).Invoke(null,new object[]{bullet,gun.info,owner});
            Func<ParticleSystem> system=()=>type.GetField("particles",S).GetValue(null) as ParticleSystem;
            var cursors=(IDictionary)type.GetField("flights",S).GetValue(null);
            type.GetMethod("Begin",S).Invoke(null,new object[]{bullet,normal.info,normal.attachedUnit});Check(cursors.Count==0&&!system(),"Ordinary kinetic round emits no rings");
            // No LocalSim/IsServer stubs: presentation must work for remote flight.
            begin();var ps=system();Check(ps&&cursors.Count==1&&ps.particleCount==1,"Visual-only/remote flight can emit launch ring without damage authority");
            begin();Check(ps.particleCount==1,"Duplicate trajectory callback does not duplicate launch");
            var start=bullet.position;bullet.position=new Vector3(0,0,4200).ToGlobalPosition();
            type.GetMethod("Advance",S).Invoke(null,new object[]{bullet,start,.421f});
            Check(ps.particleCount==4,"Delayed step emits every interval along swept trajectory");
            var particles=new ParticleSystem.Particle[512];int count=ps.GetParticles(particles);
            Check(particles.Take(count).All(p=>p.velocity.sqrMagnitude<.001f),"Detached rings stay behind rather than following projectile");
            Check(particles[0].position.z==0&&particles[1].position.z>1300&&particles[3].position.z>4100,"Ring centers interpolate actual native path");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            Check(renderer.renderMode==ParticleSystemRenderMode.Mesh&&renderer.mesh.vertexCount==873,"Single renderer uses rounded glassy torus with curved surface normals");
            Check(renderer.sharedMaterial.shader.name=="Universal Render Pipeline/Particles/Lit"&&renderer.sharedMaterial.GetFloat("_Smoothness")>=.95f,"Glassy material uses lit curved normals and high smoothness");
            Check(particles[0].startSize==1120&&particles[1].startSize==840,"Launch and flight rings doubled in diameter");
            Check(!ps.emission.enabled&&!ps.shape.enabled&&ps.main.customSimulationSpace==stage.transform,"Manual emission with floating-origin simulation space");
            Check(!ps.GetComponentsInChildren<Light>().Any()&&!ps.GetComponentsInChildren<Collider>().Any(),"Presentation adds no lights or colliders");
            ps.Simulate(.3f,true,false);count=ps.GetParticles(particles);
            Check(particles[0].GetCurrentSize(ps)>particles[0].startSize*.12f,"Pressure rings expand over lifetime");
            // Test shifted origin and two camera angles without rotating particles.
            var before=particles[1].position;stage.transform.position=new Vector3(-9000,70,8000);ps.GetParticles(particles);
            Check(particles[1].position==before,"Origin movement does not alter stored ring positions");stage.transform.position=Vector3.zero;
            var cameraObject=new GameObject("Ring preview camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.transform.position=new Vector3(420,220,-600);camera.transform.LookAt(new Vector3(0,0,900));camera.farClipPlane=6000;camera.fieldOfView=60;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.12f,.17f);camera.allowHDR=false;
            target=new RenderTexture(768,512,24);target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(768,512,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,768,512),0,0);image.Apply();
            int bright=image.GetPixels().Count(c=>c.r>.14f&&c.g>.18f&&c.b>.22f);
            var directory=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_REPO")??(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT before running editor tools or fixtures.")),"GunmaggedonRuntime/build/r230n-balance");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"preview.png"),image.EncodeToPNG());Check(bright>2000,"Actual GPU render contains visible translucent rings (pixels="+bright+")");
            type.GetMethod("Remove",S).Invoke(null,new object[]{bullet});Check(cursors.Count==0&&ps.particleCount>0,"Projectile retirement preserves fading rings");
            ps.Simulate(1.6f,true,false);Check(ps.particleCount==0,"All rings expire after projectile removal");
            // Exercise on-demand capacity and reuse of the same renderer.
            for(int i=0;i<180;i++)type.GetMethod("Emit",S).Invoke(null,new object[]{new Vector3(i,0,0),Vector3.forward,false});
            Check(system()==ps&&ps.particleCount==180&&ps.main.maxParticles>=180,"Simultaneous rings grow capacity without dropped particles or new renderers");
            type.GetMethod("Clear",S).Invoke(null,null);Check(!system()&&cursors.Count==0&&!renderer,"Teardown removes particles, cursors and renderer");
            begin();Check(system()&&system()!=ps&&system().particleCount==1,"Mission restart creates a clean ring system");
            // Production hook integration: visual-only path has no damage Flight,
            // yet Before/After must still forward its exact trajectory.
            type.GetMethod("Clear",S).Invoke(null,null);var args=new object[]{bullet,gun.info,owner,null};
            nuclear.GetMethod("Before",S).Invoke(null,args);Check(system().particleCount==1,"Native Before hook starts remote presentation");
            bullet.position=new Vector3(0,0,6200).ToGlobalPosition();nuclear.GetMethod("After",S).Invoke(null,new object[]{bullet,.2f,args[3]});
            Check(system().particleCount==2,"Native After hook advances remote presentation independently of nuclear authority");
            return new{passed=true,checks,triangles,brightPixels=bright,details="Native particle simulation/render, remote trajectory hooks, lifetime, growth, origin storage, cleanup; live mission appearance/transport pending"};
        }
        finally
        {
            type.GetMethod("Clear",S).Invoke(null,null);optimization.GetMethod("Clear",S).Invoke(null,null);
            RenderTexture.active=oldTarget;if(image)UnityEngine.Object.DestroyImmediate(image);if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            EditorSceneManager.ClosePreviewScene(scene);origin.SetValue(null,prior);originPosition.SetValue(null,priorPosition);
        }
    }
}
