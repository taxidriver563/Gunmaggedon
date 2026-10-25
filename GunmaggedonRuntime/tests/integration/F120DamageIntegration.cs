using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HarmonyLib;

public static class F120DamageIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static bool server=true;static int reports,rpcs,checks;static UnitPart current;
    static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
    public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=server;return false;}
    public static bool Report(){reports++;return false;}
    public static bool Rpc(byte __0,DamageInfo __1)
    {
        if(__0!=current.id)throw new Exception("Native damage ID did not match collider part");rpcs++;
        ((IDamageable)current).ApplyDamage(NetworkFloatHelper.Decompress(__1.pierceDamage),NetworkFloatHelper.Decompress(__1.blastDamage),NetworkFloatHelper.Decompress(__1.fireDamage),NetworkFloatHelper.Decompress(__1.impactDamage));
        return false;
    }
    public static object Run()
    {
        checks=reports=rpcs=0;server=true;
        var h=new Harmony("gunmaggedon.f120.damage.fixture");
        h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(F120DamageIntegration).GetMethod("Server")));
        h.Patch(typeof(Unit).GetMethod("ReportKilled",F),prefix:new HarmonyMethod(typeof(F120DamageIntegration).GetMethod("Report")));
        h.Patch(typeof(Unit).GetMethod("RpcDamage",F),prefix:new HarmonyMethod(typeof(F120DamageIntegration).GetMethod("Rpc")));
        var scene=EditorSceneManager.NewPreviewScene();var holder=new GameObject("F120 damage validation");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
        try{
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/F120_AAA_Trailer.prefab");
            var root=UnityEngine.Object.Instantiate(asset,holder.transform,false);var owner=root.GetComponent<GroundVehicle>();var rb=root.GetComponent<Rigidbody>();
            var hp=typeof(UnitPart).GetField("hitPoints",F);var disabled=typeof(Unit).GetField("disabled",F);
            var xform=typeof(UnitPart).GetProperty("xform",F);int id=0;
            var all=root.GetComponentsInChildren<UnitPart>(true);
            foreach(var part in all){part.id=(byte)id++;part.parentUnit=owner;typeof(UnitPart).GetField("rb",F).SetValue(part,rb);xform.SetValue(part,part.transform);var so=new SerializedObject(part);so.FindProperty("damageEffects").arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();}
            var colliders=root.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger).ToArray();
            Check(colliders.Length==13,"All physical F120 hitboxes accounted for");
            foreach(var collider in colliders){
                var damageable=collider.GetComponent<IDamageable>();Check(damageable!=null,"Native same-object damage lookup: "+collider.name);
                current=damageable as UnitPart;Check(current&&current.parentUnit==owner,"Hitbox belongs to this unit");
                Check(new SerializedObject(current).FindProperty("criticalPart").boolValue,"Destroyed hitbox disables the unit");
                hp.SetValue(current,100f);disabled.SetValue(owner,false);int before=rpcs;
                server=false;damageable.TakeDamage(0,0,1,0,10,PersistentID.None);Check((float)hp.GetValue(current)==100&&rpcs==before,"Nonserver cannot apply damage");
                server=true;damageable.TakeDamage(0,0,1,0,10,PersistentID.None);Check(Mathf.Abs((float)hp.GetValue(current)-90)<.1f&&!owner.Networkdisabled,"Sublethal damage reduces native health");
                int killed=reports;damageable.TakeDamage(2000,0,1,0,0,PersistentID.None);
                Check((float)hp.GetValue(current)<0&&owner.Networkdisabled&&reports==killed+1,"Lethal native penetration disables/reports trailer: "+collider.name);
            }
            var lod=root.GetComponent<LODGroup>();lod.ForceLOD(2);Check(colliders.All(c=>c.enabled&&c.GetComponent<IDamageable>()!=null),"Distant render detail retains all damage receivers");
            return new{passed=true,checks,hitboxes=colliders.Length,kills=reports,nativeDamageCalls=rpcs,details="Native same-object collider lookup, armor/health, lethal disable/kill reporting, server guard and LOD independence; RPC delivery and kill-report transport intercepted"};
        }finally{current=null;server=true;h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);}
    }
}
