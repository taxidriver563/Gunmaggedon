using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class RuntimeOptimizationIntegration
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks,gridCalls;static readonly List<Unit> units=new List<Unit>();
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static object Call(Type t,string n,params object[] args){return t.GetMethod(n,S).Invoke(null,args);}
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object value){o.GetType().GetField(n,F).SetValue(o,value);}
    public static bool Grid(GlobalPosition __0,float __1,List<Unit> __2){gridCalls++;foreach(var u in units)if(u&&Vector3.Distance(u.transform.position,__0.ToLocalPosition())<=__1)__2.Add(u);return false;}
    static Unit AddUnit(Transform parent,Vector3 p){var g=new GameObject("Query fixture unit");g.transform.SetParent(parent,false);g.transform.position=p;var u=g.AddComponent<GroundVehicle>();units.Add(u);return u;}
    static object Measure(Action action)
    {
        var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);var allocated=method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
        if(allocated!=null){long probe=allocated();GC.KeepAlive(new byte[4096]);if(allocated()<=probe)allocated=null;}
        for(int i=0;i<20;i++)action();var samples=new double[50];long bytes=0;
        for(int j=0;j<50;j++){long memory=allocated==null?0:allocated(),start=Stopwatch.GetTimestamp();for(int i=0;i<100;i++)action();samples[j]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;if(allocated!=null)bytes+=allocated()-memory;}
        Array.Sort(samples);return new{medianMsPer100=samples[25],p95MsPer100=samples[47],allocatedBytes=allocated==null?(long?)null:bytes,allocationMethod=allocated==null?"Unavailable: no working thread-allocation counter; capture Unity Profiler GC.Alloc":"Thread allocation counter"};
    }
    public static object Run()
    {
        checks=gridCalls=0;units.Clear();var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        var core=a.GetType("GunmageddonAudio.OptimizationRuntime");var trails=a.GetType("GunmageddonAudio.TrailService");var query=a.GetType("GunmageddonAudio.SharedWorldQueries");var bounds=a.GetType("GunmageddonAudio.SharedShipBounds");
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Optimization validation");SceneManager.MoveGameObjectToScene(root,scene);var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var originPosition=typeof(Datum).GetField("<originPosition>k__BackingField",S);var old=datum.GetValue(null);var oldPosition=originPosition.GetValue(null);
        var h=new Harmony("gunmaggedon.optimization.fixture");var resources=new List<UnityEngine.Object>();
        try{
            datum.SetValue(null,root.transform);originPosition.SetValue(null,Vector3.zero);Call(core,"EnsureMission");
            h.Patch(typeof(BattlefieldGrid).GetMethod("GetUnitsInRangeNonAlloc",S),prefix:new HarmonyMethod(typeof(RuntimeOptimizationIntegration).GetMethod("Grid")));
            var point=new GlobalPosition(0,0,0);var expected=AddUnit(root.transform,new Vector3(300,0,0));AddUnit(root.transform,new Vector3(300.01f,0,0));var found=new List<Unit>();
            Call(query,"Units",point,300f,found);Check(found.Count==1&&found[0]==expected,"Native-vector inclusive range filter");Call(query,"Units",new GlobalPosition(1,0,0),300f,found);Check(gridCalls==1&&found.Count==2,"Shared expanded broad query retains boundary candidates");
            var radar=expected.gameObject.AddComponent<Radar>();typeof(Radar).GetField("attachedUnit",F).SetValue(radar,expected);var radars=new List<Radar>();Call(query,"Radars",point,300f,radars);Check(radars.Count==1&&radars[0]==radar,"Radar component discovered from candidate unit");expected.transform.position=new Vector3(300.1f,0,0);Call(query,"Radars",point,300f,radars);Check(radars.Count==0,"Radar live distance revalidated");
            var shader=Shader.Find("Sprites/Default")??Shader.Find("Universal Render Pipeline/Particles/Unlit");var material=new Material(shader);resources.Add(material);
            var trail=(Component)Call(trails,"Rent",new Vector3(100,100,100),Color.cyan,4f,material);var tt=trail.GetType();Action<GlobalPosition,float> sample=(Action<GlobalPosition,float>)Delegate.CreateDelegate(typeof(Action<GlobalPosition,float>),trail,tt.GetMethod("Sample"));Action<float> refresh=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),trail,tt.GetMethod("Refresh"));float now=Time.time;
            sample(new GlobalPosition(101,100,100),now+.02f);refresh(now+.02f);var line=trail.GetComponent<LineRenderer>();Check(line.positionCount==2&&line.enabled,"Actual LineRenderer bulk upload preserves slow head/anchor");Check(Vector3.Distance(line.GetPosition(0),new Vector3(101,100,100))<.001f,"No unused upload-buffer points rendered");
            tt.GetProperty("MaximumLength").SetValue(trail,.5f);refresh(now+.03f);Check(Vector3.Distance(line.GetPosition(0),line.GetPosition(1))<=.501f,"Length clipping preserved in actual renderer");
            root.transform.position=new Vector3(-1000,0,0);originPosition.SetValue(null,new Vector3(-1000,0,0));tt.GetMethod("BeforeCameraCull",F).Invoke(trail,new object[]{null});var rebasedHead=new GlobalPosition(101,100,100).ToLocalPosition();Check(Vector3.Distance(line.GetPosition(0),rebasedHead)<.01f,"Camera-time origin rebase preserved: actual "+line.GetPosition(0)+", expected "+rebasedHead);root.transform.position=Vector3.zero;originPosition.SetValue(null,Vector3.zero);
            tt.GetMethod("Freeze").Invoke(trail,null);refresh(now+5);Call(trails,"Tick");var trailPool=trails.GetField("pool",S).GetValue(null);var poolType=trailPool.GetType();var idleBefore=poolType.GetProperty("IdleCount",F).GetValue(trailPool);var activeBefore=poolType.GetProperty("Count",F).GetValue(trailPool);var reused=(Component)Call(trails,"Rent",Vector3.zero,Color.magenta,4f,material);Check(reused==trail&&Mathf.Abs(line.startWidth-1.5f)<.001f&&float.IsPositiveInfinity((float)tt.GetProperty("MaximumLength").GetValue(reused)),"Trail lease resets native renderer and clipping: same="+(reused==trail)+", width="+line.startWidth+", length="+tt.GetProperty("MaximumLength").GetValue(reused)+", idle="+idleBefore+", active="+activeBefore+", originalActive="+trail.gameObject.activeSelf+", originalExpired="+tt.GetProperty("Expired",F).GetValue(trail));
            // More than 64 samples exercises ring growth, expiry and bulk count.
            for(int i=0;i<300;i++){sample(new GlobalPosition(i*30,100,0),now+i*.02f);refresh(now+i*.02f);}Check(line.positionCount<=202&&line.positionCount>=190,"Ring history and native upload agree after expiry");
            tt.GetMethod("Freeze").Invoke(trail,null);refresh(now+12);Call(trails,"Tick");
            var visuals=a.GetType("GunmageddonAudio.ClusterVisualPools");var headPool=visuals.GetField("Heads",S).GetValue(null);var pt=headPool.GetType();var leases=new List<object>();
            for(int i=0;i<520;i++)leases.Add(pt.GetMethod("Rent",F).Invoke(headPool,null));Check((int)pt.GetProperty("Count",F).GetValue(headPool)==520,"No active head-effect cap");foreach(var lease in leases)pt.GetMethod("Release",F).Invoke(headPool,new[]{Get(lease,"Value"),Get(lease,"Id")});Check((int)pt.GetProperty("IdleCount",F).GetValue(headPool)==512,"Idle head retention capped");
            var stale=pt.GetMethod("Rent",F).Invoke(headPool,null);pt.GetMethod("Release",F).Invoke(headPool,new[]{Get(stale,"Value"),Get(stale,"Id")});var current=pt.GetMethod("Rent",F).Invoke(headPool,null);Check(!(bool)pt.GetMethod("Release",F).Invoke(headPool,new[]{Get(stale,"Value"),Get(stale,"Id")}),"Stale head lease cannot release new effect");pt.GetMethod("Release",F).Invoke(headPool,new[]{Get(current,"Value"),Get(current,"Id")});
            var ship=AddUnit(root.transform,Vector3.zero);ship.gameObject.AddComponent<BoxCollider>().size=new Vector3(10,5,40);var first=(Bounds)Call(bounds,"Get",ship);var second=(Bounds)Call(bounds,"Get",ship);Check(first.size==second.size&&first.size.z>39,"Shared actual collider bounds retained");
var baselinePath=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"GunmaggedonRuntime/build/optimization/baseline/Gunmaggedon.Runtime.dll");var baselineLoaded=Assembly.LoadFrom(baselinePath);var baseline=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==baselineLoaded.ManifestModule.ModuleVersionId);var beforeBuilder=baseline.GetTypes().Single(t=>t.Name=="CoastalRanging").GetMethod("BuildTrail",S);var afterBuilder=a.GetType("GunmageddonAudio.CoastalRanging").GetMethod("BuildTrail",S);
            var rentBefore=(Func<Vector3,Color,float,Component>)Delegate.CreateDelegate(typeof(Func<Vector3,Color,float,Component>),beforeBuilder);var rentAfter=(Func<Vector3,Color,float,Component>)Delegate.CreateDelegate(typeof(Func<Vector3,Color,float,Component>),afterBuilder);
            var poolBefore=Measure(()=>{var c=rentBefore(Vector3.zero,Color.cyan,4);UnityEngine.Object.DestroyImmediate(c.gameObject);});
            var poolAfter=Measure(()=>{var c=rentAfter(Vector3.zero,Color.cyan,4);c.GetType().GetMethod("Freeze").Invoke(c,null);c.GetType().GetMethod("Refresh").Invoke(c,new object[]{Time.time+5});Call(trails,"Tick");});
            var nativeBench=new{poolBefore,poolAfter,note="Unity editor native object/trail churn; candidate includes reflection retirement overhead. Not mission FPS or a full combat benchmark."};
            // Teardown must invalidate leases even while native visuals are active.
            for(int restart=0;restart<3;restart++){var activeTrail=(Component)Call(trails,"Rent",Vector3.zero,Color.cyan,4f,material);var activeHead=pt.GetMethod("Rent",F).Invoke(headPool,null);Call(core,"Clear");Check(!activeTrail&&!(UnityEngine.Object)Get(activeHead,"Value"),"Mission clear destroys active trail/head native objects");Check((int)pt.GetProperty("Count",F).GetValue(headPool)==0&&(int)pt.GetProperty("IdleCount",F).GetValue(headPool)==0,"Mission clear empties active and idle head leases");Check(!(bool)pt.GetMethod("Release",F).Invoke(headPool,new[]{Get(activeHead,"Value"),Get(activeHead,"Id")}),"Pre-teardown lease rejected after restart");}
            var report=new{passed=true,checks,benchmark=nativeBench};var json=Newtonsoft.Json.JsonConvert.SerializeObject(report);var output=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"GunmaggedonRuntime/build/optimization/unity-optimization.json");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json);return report;
        }finally{h.UnpatchSelf();Call(core,"Clear");foreach(var o in resources)if(o)UnityEngine.Object.DestroyImmediate(o);datum.SetValue(null,old);originPosition.SetValue(null,oldPosition);EditorSceneManager.ClosePreviewScene(scene);units.Clear();}
    }
}
