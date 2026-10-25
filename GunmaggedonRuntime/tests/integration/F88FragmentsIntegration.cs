using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class F88FragmentsIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks,calls;static bool server=true,local=true;
    static readonly Dictionary<Unit,double> Damage=new Dictionary<Unit,double>();
    public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=server;return false;}
    public static bool Local(ref bool __result){__result=local;return false;}
    public static bool Rpc(Unit __instance,byte __0,DamageInfo __1)
    {
        double amount=NetworkFloatHelper.Decompress(__1.pierceDamage)+NetworkFloatHelper.Decompress(__1.blastDamage)+NetworkFloatHelper.Decompress(__1.fireDamage)+NetworkFloatHelper.Decompress(__1.impactDamage);
        double old;Damage.TryGetValue(__instance,out old);Damage[__instance]=old+amount;calls++;return false;
    }
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Set(object target,string name,object value){typeof(UnitPart).GetField(name,F).SetValue(target,value);}
    static Unit Plane(Transform parent,string name,Vector3 position,bool aircraft=true)
    {
        var go=new GameObject(name,typeof(Mirage.NetworkIdentity));go.transform.SetParent(parent,false);go.transform.position=position;var unit=aircraft?(Unit)go.AddComponent<Aircraft>():go.AddComponent<GroundVehicle>();
        for(int i=0;i<6;i++){
            var partGo=new GameObject("Test airframe part "+i);partGo.transform.SetParent(go.transform,false);partGo.transform.localPosition=new Vector3(0,(i-2.5f)*4,0);
            var part=partGo.AddComponent<AeroPart>();part.parentUnit=unit;
            Set(part,"armorProperties",new ArmorProperties{pierceArmor=20,pierceTolerance=5});Set(part,"hitPoints",100f);Set(part,"criticalPart",false);Set(part,"id",(byte)i);
            partGo.AddComponent<BoxCollider>().size=Vector3.one*2;
        }
        return unit;
    }
    static double Amount(Unit unit){double value;return Damage.TryGetValue(unit,out value)?value:0;}
    public static object Run(){return RunProfile(false);}
    public static object RunHeavy(){return RunProfile(true);}
    static object RunProfile(bool heavy)
    {
        checks=calls=0;server=local=true;Damage.Clear();
        var loaded=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));
        var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);var fragments=a.GetType("GunmageddonAudio.F88Fragments");
        var h=new Harmony("gunmaggedon.f88.fragments.fixture");h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(F88FragmentsIntegration).GetMethod("Server")));h.Patch(typeof(Unit).GetProperty("LocalSim").GetMethod,prefix:new HarmonyMethod(typeof(F88FragmentsIntegration).GetMethod("Local")));h.Patch(typeof(Unit).GetMethod("RpcDamage",F),prefix:new HarmonyMethod(typeof(F88FragmentsIntegration).GetMethod("Rpc")));
        var aircraftRpc=typeof(Aircraft).GetMethod("RpcDamage",F);if(aircraftRpc.DeclaringType!=typeof(Unit))h.Patch(aircraftRpc,prefix:new HarmonyMethod(typeof(F88FragmentsIntegration).GetMethod("Rpc")));
        // Preview scenes are excluded from default Physics overlap queries.
        // Use a temporary additive scene, restored/closed without saving.
        var previousScene=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(previousScene);var root=new GameObject("Fragment damage fixture");SceneManager.MoveGameObjectToScene(root,scene);
        // Isolate from scene assets while retaining real Unity overlap/line casts.
        root.transform.position=new Vector3(30000,30000,30000);Vector3 origin=root.transform.position;
        Action burst=()=>{Physics.SyncTransforms();fragments.GetMethod(heavy?"HeavyBurst":"Burst",BindingFlags.Static|F).Invoke(null,new object[]{origin,PersistentID.None});};
        try{
            var near=Plane(root.transform,"Near aircraft",origin+Vector3.right*50);
            var far=Plane(root.transform,"Outer aircraft",origin+new Vector3(heavy?180:100,30,0));
            var outside=Plane(root.transform,"Outside aircraft",origin+Vector3.right*(heavy?255:155));
            var ground=Plane(root.transform,"Ground unit",origin+new Vector3(50,60,0),false);
            burst();double first=Amount(near),outer=Amount(far);
            Check(first>0&&first<=(heavy?22.15:12.15),"Native armor and RPC damage applies gradual damage to a near miss; hp="+first+", callbacks="+calls);
            Check(outer>0&&outer<first,"Native outer-envelope damage falls off with distance");
            Check(Amount(outside)==0&&Amount(ground)==0,"No fragment damage outside envelope or to ground units");Check(calls<=(heavy?8:6),"Per-aircraft part-hit limit despite six exposed parts");
            burst();Check(Math.Abs(Amount(near)-2*first)<.01,"Repeated real bursts accumulate damage rather than requiring direct hits");
            Check(Math.Abs(Amount(far)-2*outer)<.01,"Repeated outer bursts also accumulate");
            Damage.Clear();calls=0;
            var firstPart=near.GetComponentsInChildren<AeroPart>()[0];for(int i=0;i<70;i++)firstPart.gameObject.AddComponent<BoxCollider>().size=Vector3.one*2;
            burst();Check(Math.Abs(Amount(near)-first)<.15&&calls<=(heavy?8:6),"Saturated first buffer grows and duplicate colliders do not multiply damage");
            Damage.Clear();calls=0;
            var wall=new GameObject("Solid fragment barrier");wall.transform.SetParent(root.transform,false);wall.transform.localPosition=new Vector3(25,0,0);wall.AddComponent<BoxCollider>().size=new Vector3(2,100,100);
            burst();Check(Amount(near)==0&&Amount(far)==0,"Solid geometry blocks fragment paths");UnityEngine.Object.DestroyImmediate(wall);
            foreach(var part in near.GetComponentsInChildren<AeroPart>())Set(part,"armorProperties",new ArmorProperties{pierceArmor=200,pierceTolerance=5});
            burst();Check(Amount(near)==0,"Armor above fragment penetration limit resists damage");
            Damage.Clear();calls=0;local=false;burst();Check(calls==0,"Remote-only aircraft cannot receive authoritative damage");local=true;server=false;burst();Check(calls==0,"Non-server aircraft cannot receive damage");
            return new{passed=true,checks,nearNativeHp=first,outerNativeHp=outer,details="Real physics queries/obstruction, native UnitPart.TakeDamage armor and compressed RPC values, falloff, per-aircraft caps, duplicate colliders and authority; RPC transport intercepted"};
        }finally{fragments.GetMethod("Clear",BindingFlags.Static|F).Invoke(null,null);h.UnpatchSelf();EditorSceneManager.CloseScene(scene,true);Damage.Clear();server=local=true;}
    }
}
