using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirage.Serialization;
using UnityEngine;

public static class StaticReviewRegression
{
    const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static Assembly assembly;
    static Type top, native, submunition;
    static int calls, failAt, checks;
    static bool nullSpawn;
    static bool remoteDamage;
    static int remoteImpacts;
    static readonly List<GameObject> transferred=new List<GameObject>();
    static object Get(object o,string n){return o.GetType().GetField(n,F).GetValue(o);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static object Call(Type t,string n,params object[] args){return t.GetMethod(n,S).Invoke(null,args);}
    static void Check(bool v,string message){checks++;if(!v)throw new Exception(message);}
    public static bool Parachute(ref Parachute __result){__result=parachute;return false;}
    static Parachute parachute;
    public static bool Impact(bool __3){remoteImpacts++;remoteDamage|=__3;return false;}
    public static bool Spawn(Gun __1,ref GameObject __result)
    {
        if(calls++==failAt)throw new InvalidOperationException("Injected native spawn failure");
        if(nullSpawn){__result=null;return false;}
        var go=new GameObject("Transferred slot fixture");go.SetActive(false);
        var component=go.AddComponent(submunition);Set(component,"gun",__1);
        submunition.GetMethod("TakeReservation",F).Invoke(component,null);
        transferred.Add(go);__result=go;return false;
    }
    static object RoundTrip(Type t,object value)
    {
        var bytes=(byte[])typeof(MessagePacker).GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1).MakeGenericMethod(t).Invoke(null,new[]{value});
        return typeof(MessagePacker).GetMethod("Unpack").MakeGenericMethod(t).Invoke(null,new object[]{bytes,null});
    }
    public static object Run()
    {
        string helper=Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")??throw new Exception("Set GUNMAGGEDON_TEST_HELPER to the freshly built runtime.");
        var loaded=Assembly.Load(File.ReadAllBytes(helper));assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);
        top=assembly.GetType("GunmageddonAudio.TopAttackMunitions");native=assembly.GetType("GunmageddonAudio.NativeTopAttackBomb");submunition=assembly.GetType("GunmageddonAudio.TopAttackSubmunition");
        var network=assembly.GetType("GunmageddonAudio.SpecialPayloadNetwork");var special=assembly.GetType("GunmageddonAudio.SpecialShells");var obscurants=assembly.GetType("GunmageddonAudio.ShellObscurants");
        var kind=assembly.GetType("GunmageddonAudio.SpecialShellKind");var datum=typeof(Datum).GetField("<origin>k__BackingField",S);var datumPosition=typeof(Datum).GetField("<originPosition>k__BackingField",S);
        object old=datum.GetValue(null),oldPosition=datumPosition.GetValue(null);var random=UnityEngine.Random.state;
        var root=new GameObject("Current review fixture origin");root.SetActive(false);var geometry=new GameObject("Dense slug fixture geometry");
        var harmony=new Harmony("gunmaggedon.current.static-review");checks=0;
        var registry=(IDictionary)typeof(MessagePacker).GetProperty("MessageTypes").GetValue(null);
        var oldMessages=new Dictionary<int,object>();
        foreach(var name in new[]{"PayloadHello","PayloadRelease","PayloadClouds"}) {
            int id=MessagePacker.GetId(assembly.GetType("GunmageddonAudio."+name));
            if(registry.Contains(id)){oldMessages[id]=registry[id];registry.Remove(id);}
        }
        try {
            datum.SetValue(null,root.transform);datumPosition.SetValue(null,Vector3.zero);Call(special,"EnsureMaterials");Call(network,"Install");
            var slugType=assembly.GetType("GunmageddonAudio.TopAttackSlug");
            foreach(int total in new[]{90,600}) {
                foreach(Transform c in geometry.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(c.gameObject);
                Vector3 from=new Vector3(50000,50000,50000);
                for(int i=0;i<total;i++) {
                    var go=new GameObject("Ray blocker "+i);go.transform.SetParent(geometry.transform,false);go.transform.position=from+Vector3.forward*(1+i*.04f);
                    var box=go.AddComponent<BoxCollider>();box.size=new Vector3(1,1,.01f);
                }
                Physics.SyncTransforms();
                Check(Physics.RaycastAll(from,Vector3.forward,30f).Length==total,"Dense fixture contains all swept colliders");
                slugType.GetField("Hits",S).SetValue(null,new RaycastHit[64]);
                var projectile=new GameObject("Dense slug");projectile.SetActive(false);var slug=projectile.AddComponent(slugType);
                slugType.GetMethod("Initialize",F).Invoke(slug,new object[]{null,from,Vector3.forward*1500,15000f});
                slugType.GetMethod("FixedUpdate",F).Invoke(slug,null);
                Check(Mathf.Abs(projectile.transform.position.z-from.z-.995f)<.02f,"Slug stops at nearest collider with "+total+" hits");
                Check(((RaycastHit[])slugType.GetField("Hits",S).GetValue(null)).Length==(total>512?512:128),"Saturated query expands before processing");
                Check(((RaycastHit[])slugType.GetField("Hits",S).GetValue(null)).All(h=>!h.collider),"Query scratch does not retain colliders");
                UnityEngine.Object.DestroyImmediate(projectile);
            }
            UnityEngine.Object.DestroyImmediate(geometry);geometry=null;
            var p=new GameObject("Dummy native parachute");p.transform.SetParent(root.transform,false);parachute=p.AddComponent<Parachute>();
            harmony.Patch(assembly.GetType("GunmageddonAudio.CoastalIllumination").GetMethod("GetNativeParachute",S),new HarmonyMethod(typeof(StaticReviewRegression),nameof(Parachute)));
            harmony.Patch(native.GetMethod("Spawn",S),new HarmonyMethod(typeof(StaticReviewRegression),nameof(Spawn)));
            var gun=root.AddComponent<Gun>();var definition=ScriptableObject.CreateInstance<MissileDefinition>();top.GetField("bombDefinition",S).SetValue(null,definition);
            var slots=(IDictionary)top.GetField("Slots",S).GetValue(null);var carrierType=special.GetNestedType("Carrier",BindingFlags.NonPublic);
            foreach(int failure in new[]{0,1,2}) {
                calls=0;failAt=failure;nullSpawn=failure==2;slots[gun]=2;top.GetField("active",S).SetValue(null,2);
                var carrier=Activator.CreateInstance(carrierType,true);Set(carrier,"Gun",gun);Set(carrier,"TopAttackReserved",true);
                bool threw=false;try{Call(top,"Release",carrier,new Vector3(0,1000,0).ToGlobalPosition(),Vector3.forward*200);}catch(TargetInvocationException){threw=true;}
                Check(threw==(failure<2),"Injected spawn failure propagated");
                int expected=failure==1?1:0;
                Check((int)top.GetField("active",S).GetValue(null)==expected,"Only transferred reservations remain after failure "+failure);
                Check(!((bool)Get(carrier,"TopAttackReserved")),"Carrier reservation flag cleared");Call(special,"Cancel",carrier);
                foreach(var go in transferred){submunition.GetMethod("OnDestroy",F).Invoke(go.GetComponent(submunition),null);UnityEngine.Object.DestroyImmediate(go);}transferred.Clear();
                Check((int)top.GetField("active",S).GetValue(null)==0&&slots.Count==0,"Transferred objects return exactly their own slots");
            }
            UnityEngine.Object.DestroyImmediate(definition);
            var releaseType=assembly.GetType("GunmageddonAudio.PayloadRelease");var release=Activator.CreateInstance(releaseType);
            Set(release,"Kind",Enum.Parse(kind,"Cluster"));Set(release,"Position",new GlobalPosition{x=12345,y=600,z=-7654});Set(release,"Gravity",19.62f);Set(release,"ReleasedAt",1234.5d);Set(release,"Velocities",new[]{new Vector3(12,-50,200),new Vector3(-7,-49,201)});
            var decoded=RoundTrip(releaseType,release);Check(((Vector3[])Get(decoded,"Velocities"))[1]==new Vector3(-7,-49,201),"Remote cluster velocities match authoritative dispersal");
            Check(((GlobalPosition)Get(decoded,"Position")).x==12345,"Payload release uses global coordinates");
            Check((double)Get(decoded,"ReleasedAt")==1234.5d,"Release time retained for network catch-up");
            var flightType=assembly.GetType("GunmageddonAudio.SpecialPayloadFlight");
            harmony.Patch(special.GetMethod("Detonate",S),new HarmonyMethod(typeof(StaticReviewRegression),nameof(Impact)));
            remoteDamage=false;remoteImpacts=0;
            var cloudList=(IList)obscurants.GetField("Clouds",S).GetValue(null);int beforeClouds=cloudList.Count;
            foreach(var payload in new[]{"Cluster","Smoke","Chaff"}) {
                var go=new GameObject("Remote payload authority fixture");go.SetActive(false);go.transform.SetParent(root.transform,false);
                var flight=go.AddComponent(flightType);
                flightType.GetMethod("Initialize",F).Invoke(flight,new object[]{Enum.Parse(kind,payload),null,new Vector3(0,Datum.LocalSeaY+.4f,0).ToGlobalPosition(),Vector3.zero,1,9.81f,new[]{Vector3.down*1000}});
                flightType.GetMethod("FixedUpdate",F).Invoke(flight,null);
                Check(cloudList.Count==beforeClouds,"Remote "+payload+" cannot create authoritative clouds");
                UnityEngine.Object.DestroyImmediate(go);
            }
            Check(remoteImpacts>0&&!remoteDamage,"Remote cluster impacts are presentation-only");
            var cloudType=assembly.GetType("GunmageddonAudio.CloudState");var snapshotType=assembly.GetType("GunmageddonAudio.PayloadClouds");var states=Array.CreateInstance(cloudType,2);
            for(int i=0;i<2;i++) {var state=Activator.CreateInstance(cloudType);Set(state,"Id",(uint)(i+1));Set(state,"Kind",Enum.Parse(kind,i==0?"Smoke":"Chaff"));Set(state,"Position",new GlobalPosition{x=0,y=100,z=i*1000});Set(state,"Age",12f);Set(state,"Lifetime",70f);states.SetValue(state,i);}
            var snapshot=Activator.CreateInstance(snapshotType);Set(snapshot,"States",states);snapshot=RoundTrip(snapshotType,snapshot);Call(network,"ApplyClouds",snapshot);
            var clouds=(IList)obscurants.GetField("Clouds",S).GetValue(null);foreach(Component cloud in clouds)cloud.transform.SetParent(null,true);Check(clouds.Count==2,"Late join receives existing smoke and chaff");
            Check((bool)Call(obscurants,"SmokeBlocks",new Vector3(-100,100,0),new Vector3(100,100,0)),"Replicated smoke blocks client optics");
            Check((float)Call(obscurants,"RadarFactor",new Vector3(-1000,100,1000),new Vector3(1000,100,1000))<.4f,"Replicated chaff affects client radar");
            Check(Mathf.Abs(Time.time-(float)Get(clouds[0],"Born")-12f)<.01f,"Cloud age retained across different local clocks");
            Call(network,"ApplyClouds",snapshot);Check(clouds.Count==2,"Repeated snapshot does not duplicate clouds");
            root.transform.position=Vector3.right*2000;Call(network,"ApplyClouds",snapshot);
            Check(Vector3.Distance(((Component)clouds[0]).transform.position,((GlobalPosition)Get(clouds[0],"Position")).ToLocalPosition())<.01f,"Cloud snapshots survive floating-origin moves");
            Set(snapshot,"States",Array.CreateInstance(cloudType,0));Call(network,"ApplyClouds",snapshot);Check(clouds.Count==0,"Authoritative removal clears remote obscuration");
            return new{passed=true,checks,details="90/600 collider penetration, spawn failures before/between transfers, null spawn rollback, serializer round trips, late-join smoke/chaff, optics/radar, age, duplicate/removal and origin rebasing"};
        } finally {
            harmony.UnpatchSelf();foreach(var go in transferred)if(go)UnityEngine.Object.DestroyImmediate(go);transferred.Clear();
            foreach(var name in new[]{"PayloadHello","PayloadRelease","PayloadClouds"})registry.Remove(MessagePacker.GetId(assembly.GetType("GunmageddonAudio."+name)));
            foreach(var pair in oldMessages)registry[pair.Key]=pair.Value;
            Call(network,"Shutdown");Call(top,"Shutdown");
            var effects=(IList)special.GetField("Effects",S).GetValue(null);foreach(GameObject go in effects)if(go)UnityEngine.Object.DestroyImmediate(go);effects.Clear();
            foreach(var name in new[]{"SmokeMaterial","ChaffMaterial","SparkMaterial","puff"}){var f=special.GetField(name,S);var value=f.GetValue(null) as UnityEngine.Object;if(value)UnityEngine.Object.DestroyImmediate(value);f.SetValue(null,null);}
            if(geometry)UnityEngine.Object.DestroyImmediate(geometry);UnityEngine.Object.DestroyImmediate(root);datum.SetValue(null,old);datumPosition.SetValue(null,oldPosition);UnityEngine.Random.state=random;
        }
    }
}

