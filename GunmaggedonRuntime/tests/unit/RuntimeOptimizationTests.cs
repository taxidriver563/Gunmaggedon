using System;
using System.Collections.Generic;
using System.Linq;
using GunmageddonAudio;
using UnityEngine;

// Deterministic transport doubles. Production pooling/query/trail/audio code is
// compiled unchanged; these tests do not certify Unity rendering or acoustics.
namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;static int ids;readonly int id=++ids;
        public int GetInstanceID(){return id;}
        public static implicit operator bool(Object o){return !ReferenceEquals(o,null)&&!o.Dead;}
        public static void Destroy(Object o){if(!ReferenceEquals(o,null))o.Dead=true;}
        public static void DestroyImmediate(Object o){Destroy(o);}
    }
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform {get{return gameObject.transform;}}
        public T GetComponent<T>() where T:Component{return gameObject.GetComponent<T>();}
        public void GetComponentsInChildren<T>(bool include,List<T> output) where T:Component{gameObject.GetComponentsInChildren(include,output);}
    }
    public class MonoBehaviour:Component { }
    public class Transform:Component
    {
        public Vector3 position,localScale;public Quaternion localRotation;public Transform parent;internal readonly List<Transform> children=new List<Transform>();
        public void SetParent(Transform p,bool world){if(parent)parent.children.Remove(this);parent=p;if(p)p.children.Add(this);}
    }
    public class GameObject:Object
    {
        public string name;public bool activeSelf=true;public bool activeInHierarchy {get{return activeSelf&&(!transform.parent||transform.parent.gameObject.activeInHierarchy);}}
        public Transform transform;readonly List<Component> components=new List<Component>();
        public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};components.Add(transform);}
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
        public T GetComponent<T>() where T:Component{return components.OfType<T>().FirstOrDefault(c=>c);}
        public void GetComponentsInChildren<T>(bool include,List<T> output) where T:Component{output.Clear();Gather(include,output);}
        void Gather<T>(bool include,List<T> output) where T:Component{if(include||activeInHierarchy){foreach(var c in components)if(c is T&&c)output.Add((T)c);foreach(var t in transform.children)t.gameObject.Gather(include,output);}}
        public void SetActive(bool value){activeSelf=value;}
    }
    public static class Application {public static bool isPlaying;}
    public static class Time {public static float time,fixedTime;public static int frameCount;}
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero {get{return new Vector3();}}public static Vector3 one {get{return new Vector3(1,1,1);}}public static Vector3 up {get{return new Vector3(0,1,0);}}
        public float sqrMagnitude {get{return x*x+y*y+z*z;}}
        public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator *(Vector3 a,float n){return new Vector3(a.x*n,a.y*n,a.z*n);}
        public static bool operator ==(Vector3 a,Vector3 b){return a.x==b.x&&a.y==b.y&&a.z==b.z;}public static bool operator !=(Vector3 a,Vector3 b){return !(a==b);}
        public override bool Equals(object o){return o is Vector3&&this==(Vector3)o;}public override int GetHashCode(){return x.GetHashCode()^y.GetHashCode()^z.GetHashCode();}
        public static float Distance(Vector3 a,Vector3 b){return (float)Math.Sqrt((a-b).sqrMagnitude);}
        public static Vector3 Lerp(Vector3 a,Vector3 b,float n){return a+(b-a)*n;}
    }
    public struct Quaternion {public static Quaternion identity {get{return new Quaternion();}}}
    public struct Bounds
    {
        public Vector3 center,size;public Bounds(Vector3 center,Vector3 size){this.center=center;this.size=size;}
        public void Encapsulate(Bounds b){var min=new Vector3(Math.Min(center.x-size.x/2,b.center.x-b.size.x/2),Math.Min(center.y-size.y/2,b.center.y-b.size.y/2),Math.Min(center.z-size.z/2,b.center.z-b.size.z/2));var max=new Vector3(Math.Max(center.x+size.x/2,b.center.x+b.size.x/2),Math.Max(center.y+size.y/2,b.center.y+b.size.y/2),Math.Max(center.z+size.z/2,b.center.z+b.size.z/2));center=(min+max)*.5f;size=max-min;}
    }
    public class Collider:Component {public bool isTrigger,enabled=true;public static int Reads;public Vector3 Size=new Vector3(2,2,2);public Bounds bounds {get{Reads++;return new Bounds(transform.position,Size);}}}
    public static class Mathf
    {
        public static int FloorToInt(float x){return (int)Math.Floor(x);}public static int CeilToInt(float x){return (int)Math.Ceiling(x);}
        public static int NextPowerOfTwo(int n){int p=1;while(p<n)p*=2;return p;}
        public static float Max(float a,float b){return Math.Max(a,b);}public static float Clamp01(float x){return Math.Max(0,Math.Min(1,x));}public static float Sqrt(float x){return (float)Math.Sqrt(x);}
    }
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white {get{return new Color(1,1,1);}}}
    public class Material:Object { }
    public enum LineAlignment {View}
    public class LineRenderer:Component
    {
        public bool useWorldSpace,enabled,receiveShadows;public int positionCount,numCornerVertices,numCapVertices;public float startWidth,endWidth;public Color startColor,endColor;public Material sharedMaterial;public LineAlignment alignment;public Rendering.ShadowCastingMode shadowCastingMode;public int Uploads;public Vector3[] Points;
        public void SetPropertyBlock(object o){ }
        public void SetPositions(Vector3[] points){Uploads++;if(Points==null||Points.Length<positionCount)Points=new Vector3[points.Length];Array.Copy(points,Points,positionCount);}
    }
    public class Camera:Object {public static event Action<Camera> onPreCull;public static void Cull(){if(onPreCull!=null)onPreCull(null);}}
    public class AudioClip:Object {public float length;}
    public class AudioSource:Component {public bool playOnAwake,Played;public AudioClip clip;public float volume=1,pitch=1;public void Play(){Played=true;}public void Stop(){Played=false;}}
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode {Off}
    public struct ScriptableRenderContext { }
    public static class RenderPipelineManager {public static event Action<ScriptableRenderContext,Camera> beginCameraRendering;public static void Cull(){if(beginCameraRendering!=null)beginCameraRendering(default(ScriptableRenderContext),null);}}
}
namespace UnityEngine.Profiling {public static class Profiler {public static void BeginSample(string name){}public static void EndSample(){}}}
public struct GlobalPosition {public float x,y,z;public GlobalPosition(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
public static class Datum {public static Transform origin;public static Vector3 Offset;}
public static class GlobalPositionExtensions
{
    public static GlobalPosition ToGlobalPosition(this Vector3 p){p+=Datum.Offset;return new GlobalPosition(p.x,p.y,p.z);}
    public static Vector3 ToLocalPosition(this GlobalPosition p){return new Vector3(p.x,p.y,p.z)-Datum.Offset;}
}
public class Definition:UnityEngine.Object {public float width=4,height=3,length=20;}
public class Unit:Component {public Definition definition;public GlobalPosition GlobalPosition(){return transform.position.ToGlobalPosition();}}
public class Radar:Component {public Unit Owner;public Unit GetAttachedUnit(){return Owner;}}
public class TrackingInfo {public Unit Unit;}
public class FactionHQ:Component {public readonly List<TrackingInfo> Known=new List<TrackingInfo>();public int Queries;public void GetTargetsWithinRange(List<TrackingInfo> list,Transform observer,float radius,bool flag){Queries++;list.AddRange(Known);}}
public static class BattlefieldGrid
{
    public static readonly List<Unit> Units=new List<Unit>();public static int Calls;
    public static void GetUnitsInRangeNonAlloc(GlobalPosition center,float radius,List<Unit> output){Calls++;foreach(var u in Units)if(u&&Vector3.Distance(u.transform.position,center.ToLocalPosition())<=radius)output.Add(u);}
}
namespace GunmageddonAudio
{
    internal static class OptimizationRuntime {internal static void EnsureMission(){}}
    internal static class Plugin
    {
        internal static Vector3 Listener;internal static Vector3 ListenerPosition(){return Listener;}
        internal static float TravelSeconds(float d){return Math.Max(0,d)/343;}internal static float DistantMix(float d){return Mathf.Clamp01((d-400)/1100);}
        internal static AudioSource CopyVoice(AudioSource voice,AudioSource source){if(!source)return null;voice.clip=source.clip;voice.volume=source.volume;voice.pitch=source.pitch;return voice;}
    }
}
public static class RuntimeOptimizationTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static Unit AddUnit(Vector3 p){var u=new GameObject("Unit").AddComponent<Unit>();u.transform.position=p;BattlefieldGrid.Units.Add(u);return u;}
    static object Measure(Action action)
    {
        for(int i=0;i<1000;i++)action();var samples=new double[100];long bytes=0;
        for(int batch=0;batch<samples.Length;batch++){long memory=GC.GetAllocatedBytesForCurrentThread(),started=System.Diagnostics.Stopwatch.GetTimestamp();for(int i=0;i<1000;i++)action();samples[batch]=(System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency;bytes+=GC.GetAllocatedBytesForCurrentThread()-memory;}
        Array.Sort(samples);return new{medianMsPer1000=samples[50],p95MsPer1000=samples[94],allocatedBytes=bytes};
    }
    public static void Main()
    {
        int created=0,reset=0;var pool=new RuntimePool<Component>(2,()=>{created++;return new GameObject("Pooled").AddComponent<Component>();},c=>reset++);
        var leases=new List<RuntimePool<Component>.Lease>();for(int i=0;i<10;i++)leases.Add(pool.Rent());Check(pool.Count==10,"Active capacity is not capped");foreach(var l in leases)Check(pool.Release(l.Value,l.Id),"Release valid lease");Check(pool.IdleCount==2&&reset==10,"Only idle retention is bounded");
        var old=pool.Rent();pool.Release(old.Value,old.Id);var reused=pool.Rent();Check(ReferenceEquals(old.Value,reused.Value)&&old.Id!=reused.Id,"Reuse changes generation");Check(!pool.Release(old.Value,old.Id)&&pool.Count==1,"Stale lease cannot retire current effect");pool.Release(reused.Value,reused.Id);
        int before=created;for(int i=0;i<10000;i++){var l=pool.Rent();pool.Release(l.Value,l.Id);}Check(created==before,"Warm pool creates no objects");pool.Clear();Check(pool.Count==0&&pool.IdleCount==0,"Teardown clears active and idle leases");
        SharedWorldQueries.Clear();BattlefieldGrid.Units.Clear();var random=new Random(44);for(int i=0;i<300;i++)AddUnit(new Vector3(random.Next(-600,600),random.Next(-300,300),random.Next(-600,600)));
        var found=new List<Unit>();for(int i=0;i<300;i++){var p=new Vector3(random.Next(-500,500),random.Next(-200,200),random.Next(-500,500));float radius=i%2==0?300:130;SharedWorldQueries.Units(p.ToGlobalPosition(),radius,found);var expected=BattlefieldGrid.Units.Where(u=>Vector3.Distance(u.transform.position,p)<=radius).ToList();Check(found.Count==expected.Count&&expected.All(found.Contains),"Expanded cell query matches direct sphere");}
        SharedWorldQueries.Clear();Time.frameCount++;int calls=BattlefieldGrid.Calls;SharedWorldQueries.Units(new GlobalPosition(1,1,1),300,found);SharedWorldQueries.Units(new GlobalPosition(249,249,249),300,found);Check(BattlefieldGrid.Calls==calls+1,"Same-cell consumers share broad query");Time.fixedTime+=.02f;SharedWorldQueries.Units(new GlobalPosition(1,1,1),300,found);Check(BattlefieldGrid.Calls==calls+2,"Physics-step change invalidates cache");Time.frameCount++;SharedWorldQueries.Units(new GlobalPosition(1,1,1),300,found);Check(BattlefieldGrid.Calls==calls+3,"Frame change invalidates cache");
        BattlefieldGrid.Units.Clear();SharedWorldQueries.Clear();var truck=AddUnit(new Vector3(300,0,0));var radar=truck.gameObject.AddComponent<Radar>();radar.Owner=truck;var radars=new List<Radar>();SharedWorldQueries.Radars(new GlobalPosition(),300,radars);Check(radars.Count==1,"Radar inclusive boundary");truck.transform.position=new Vector3(300.01f,0,0);SharedWorldQueries.Radars(new GlobalPosition(),300,radars);Check(radars.Count==0,"Live distance revalidated within snapshot");truck.transform.position=new Vector3(300,0,0);UnityEngine.Object.Destroy(radar);SharedWorldQueries.Radars(new GlobalPosition(),300,radars);Check(radars.Count==0,"Destroyed radar removed from evidence candidates");
        var team=new GameObject("HQ").AddComponent<FactionHQ>();var other=new GameObject("Other HQ").AddComponent<FactionHQ>();team.Known.Add(new TrackingInfo{Unit=truck});var contacts=new List<TrackingInfo>();var observer=new GameObject("Observer").transform;SharedWorldQueries.KnownContacts(team,observer,12000,contacts);SharedWorldQueries.KnownContacts(team,observer,12000,contacts);Check(contacts.Count==1&&team.Queries==1,"Known contacts shared within faction");SharedWorldQueries.KnownContacts(other,observer,12000,contacts);Check(contacts.Count==0,"No faction knowledge leak");
        SharedShipBounds.Clear();var ship=new GameObject("Ship").AddComponent<Unit>();ship.gameObject.AddComponent<Collider>();Collider.Reads=0;var b=SharedShipBounds.Get(ship);SharedShipBounds.Get(ship);Check(Collider.Reads==1,"One bounds read per ship per physics step");Time.fixedTime+=.02f;ship.transform.position=new Vector3(50,0,0);b=SharedShipBounds.Get(ship);Check(b.center.x==50&&Collider.Reads==2,"Moving ship bounds update each step");Datum.Offset=new Vector3(1000,0,0);ship.transform.position=new Vector3(-950,0,0);b=SharedShipBounds.Get(ship);Check(b.center.x==-950,"Cached bounds rebase without another physics step");Datum.Offset=Vector3.zero;Time.fixedTime+=.02f;ship.GetComponent<Collider>().enabled=false;ship.definition=new Definition();Check(SharedShipBounds.Get(ship).size.z==20,"Disabled colliders use definition fallback");
        var material=new Material();Time.time=0;var trail=TrailService.Rent(new Vector3(10,0,0),Color.white,4,material);trail.Sample(new GlobalPosition(11,0,0),.02f);trail.Refresh(.02f);var line=trail.GetComponent<LineRenderer>();Check(line.positionCount==2&&line.enabled,"Slow moving launch retains head and anchor");Check(line.Uploads==1,"Single bulk upload");trail.Refresh(.02f);Check(line.Uploads==1,"Unchanged refresh does not reupload");trail.MaximumLength=.5f;trail.Refresh(.02f);Check(Vector3.Distance(line.Points[0],line.Points[1])<=.501f,"Maximum-length clipping preserved");Datum.Offset=new Vector3(1000,0,0);trail.Refresh(.02f);Check(line.Points[0].x==-989,"Camera-time rebase refresh preserved");Datum.Offset=Vector3.zero;trail.Freeze();trail.Refresh(4.1f);Check(trail.Expired&&!line.enabled,"Frozen trail fully fades");TrailService.Register(trail);Time.time=4.1f;TrailService.Tick();var fresh=TrailService.Rent(new Vector3(20,0,0),new Color(2,1,0),4,material);Check(ReferenceEquals(trail,fresh)&&float.IsPositiveInfinity(fresh.MaximumLength)&&fresh.MinimumSampleDistance==8,"Reused trail resets clipping and sampling");Check(fresh.GetComponent<LineRenderer>().startWidth==1.5f,"Reused trail resets width");fresh.Sample(new GlobalPosition(21,0,0),4.12f);fresh.Refresh(4.12f);Check(fresh.GetComponent<LineRenderer>().enabled,"Reused trail is unfrozen");TrailService.Clear();
        TravellingAudio.Clear();Time.time=0;Plugin.Listener=Vector3.zero;var template=new GameObject("Template").AddComponent<AudioSource>();template.clip=new AudioClip{length=2};TravellingAudio.Schedule(new Vector3(343,0,0),template,template,1000);var voicePool=(RuntimePool<TravellingVoice>)typeof(TravellingAudio).GetField("pool",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);var voice=voicePool.At(0).Value;Check(!voice.Near.Played,"Sound waits for propagation");Time.time=1;TravellingAudio.Tick();Check(voice.Near.Played&&voice.Far.Played,"Both sound variants play after sound arrival");Time.time=4.01f;TravellingAudio.Tick();Check(voicePool.Count==0&&voice.Near.clip==null,"Finished voice releases clip references");TravellingAudio.Schedule(new Vector3(343,0,0),template,null,1000);Check(!voice.Near.Played&&!voice.Far.Played,"Pooled emitter resets playback");Plugin.Listener=new Vector3(343,0,0);TravellingAudio.Tick();Check(voice.Near.Played&&!voice.Far.Played,"Live listener movement and absent far source respected");TravellingAudio.Clear();
        // Test stale releases after Clear, destroyed objects and callback failure.
        var stale=pool.Rent();pool.Clear();Check(!pool.Release(stale.Value,stale.Id),"Mission clear invalidates outstanding leases");
        var doomed=pool.Rent();UnityEngine.Object.Destroy(doomed.Value);Check(pool.Release(doomed.Value,doomed.Id),"Destroyed active object can be removed");
        var broken=new RuntimePool<Component>(1,()=>new GameObject("Failure").AddComponent<Component>(),c=>{throw new InvalidOperationException("reset failure");});var failed=broken.Rent();try{broken.Release(failed.Value,failed.Id);}catch(InvalidOperationException){}Check(broken.Count==0&&broken.IdleCount==0&&!failed.Value.gameObject,"Failed reset destroys object instead of leaking a live lease");
        var stress=TrailService.Rent(Vector3.zero,Color.white,4,material);for(int i=0;i<1200;i++){float t=i*.02f;stress.Sample(new GlobalPosition(i*30,0,0),t);stress.Refresh(t);}Check(stress.GetComponent<LineRenderer>().positionCount<=202,"Ring buffer prunes and wraps without shifting live samples");stress.Freeze();stress.Refresh(29);Check(stress.Expired,"Wrapped history expires completely");TrailService.Clear();
        var benchmarkPool=new RuntimePool<Component>(2,()=>new GameObject("Pooled benchmark").AddComponent<Component>(),c=>{});
        var baseline=Measure(()=>{var c=new GameObject("Baseline transient").AddComponent<Component>();UnityEngine.Object.DestroyImmediate(c.gameObject);});
        var candidate=Measure(()=>{var l=benchmarkPool.Rent();benchmarkPool.Release(l.Value,l.Id);});benchmarkPool.Clear();
        var report=new{kind="Synthetic .NET transport benchmark; not Unity/native rendering or mission FPS",baseline,candidate};System.IO.Directory.CreateDirectory("GunmaggedonRuntime/build/optimization");System.IO.File.WriteAllText("GunmaggedonRuntime/build/optimization/synthetic-benchmark.json",System.Text.Json.JsonSerializer.Serialize(report));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
        Console.WriteLine("RuntimeOptimizationTests: "+checks+" production-logic assertions passed using deterministic Unity transport doubles.");
    }
}
