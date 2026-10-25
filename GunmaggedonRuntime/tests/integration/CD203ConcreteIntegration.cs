using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HarmonyLib;
public static class CD203ConcreteIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static UnitPart current;static bool server=true;static int checks,calls;
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=server;return false;}
    public static bool Rpc(byte __0,DamageInfo __1){calls++;((IDamageable)current).ApplyDamage(NetworkFloatHelper.Decompress(__1.pierceDamage),NetworkFloatHelper.Decompress(__1.blastDamage),NetworkFloatHelper.Decompress(__1.fireDamage),NetworkFloatHelper.Decompress(__1.impactDamage));return false;}
    public static object Run()
    {
        checks=calls=0;server=true;var h=new Harmony("gunmaggedon.concrete.fixture");
        h.Patch(typeof(Unit).GetProperty("IsServer").GetMethod,prefix:new HarmonyMethod(typeof(CD203ConcreteIntegration).GetMethod("Server")));
        h.Patch(typeof(Building).GetMethod("RpcDamage",F),prefix:new HarmonyMethod(typeof(CD203ConcreteIntegration).GetMethod("Rpc")));
        var scene=EditorSceneManager.NewPreviewScene();var holder=new GameObject("Concrete validation");holder.SetActive(false);SceneManager.MoveGameObjectToScene(holder,scene);
        var hp=typeof(UnitPart).GetField("hitPoints",F);var disabled=typeof(Unit).GetField("disabled",F);
        try{
            foreach(var name in new[]{"Emplacement_203mmCG","Emplacement_203mmCG_SPECIAL"}){
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/"+name+".prefab");var root=UnityEngine.Object.Instantiate(asset,holder.transform,false);var unit=root.GetComponent<Building>();
                Check(!root.GetComponent<Collider>(),"No oversized box shielding the exposed weapon");
                var concrete=root.transform.Find("concrete");var collider=concrete.GetComponent<MeshCollider>();
                Check(collider&&!collider.convex&&collider.sharedMesh==concrete.GetComponent<MeshFilter>().sharedMesh,"Concrete collision follows actual ring/pit geometry");
                var cs=root.GetComponentsInChildren<Collider>(true);Check(cs.Length==4,"Concrete plus three gun hitboxes");
                foreach(var c in cs){
                    current=c.GetComponent<UnitPart>();Check(current&&current.parentUnit==unit,"Native same-object damage receiver and correct owner");
                    typeof(UnitPart).GetProperty("xform",F).SetValue(current,current.transform);var so=new SerializedObject(current);so.FindProperty("damageEffects").arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();
                    bool fortified=c.gameObject==concrete.gameObject;var armor=((IDamageable)current).GetArmorProperties();
                    Check(armor.pierceArmor==(fortified?300:0)&&armor.blastTolerance==(fortified?4:1),"Only concrete has increased protection");
                    hp.SetValue(current,100f);disabled.SetValue(unit,false);int before=calls;server=false;((IDamageable)current).TakeDamage(0,100,1,0,0,PersistentID.None);Check((float)hp.GetValue(current)==100&&calls==before,"Server authority preserved");server=true;
                    ((IDamageable)current).TakeDamage(0,100,1,0,0,PersistentID.None);float loss=100-(float)hp.GetValue(current);Check(Mathf.Abs(loss-(fortified?25:100))<.2f,"Native blast damage is quartered only on concrete");
                    hp.SetValue(current,100f);disabled.SetValue(unit,false);((IDamageable)current).TakeDamage(50,0,1,0,0,PersistentID.None);Check(Mathf.Abs((float)hp.GetValue(current)-(fortified?100:50))<.2f,"Concrete resists light penetration; exposed gun retains original vulnerability");
                    hp.SetValue(current,100f);disabled.SetValue(unit,false);((IDamageable)current).TakeDamage(0,0,1,50,0,PersistentID.None);Check(Mathf.Abs((float)hp.GetValue(current)-(fortified?100:50))<.2f,"Fire resistance belongs only to concrete");
                    hp.SetValue(current,100f);disabled.SetValue(unit,false);((IDamageable)current).TakeDamage(2000,0,1,0,0,PersistentID.None);Check(unit.Networkdisabled&&(float)hp.GetValue(current)<0,"Heavy direct penetration can still destroy the emplacement");
                }
                var lod=root.GetComponent<LODGroup>();var levels=lod.GetLODs();Check(lod.enabled&&levels.Length==3,"Three enabled model LOD levels");int previous=int.MaxValue;
                foreach(var l in levels){int tris=l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);Check(tris<previous,"Actual distant geometry reduction");previous=tris;}
                var template=root.transform.Find("TopAttackSubmunitionTemplate");
                Check(root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!template||!r.transform.IsChildOf(template)).All(r=>levels.Any(l=>l.renderers.Contains(r))),"Physical model renderer coverage retained");
                lod.ForceLOD(2);Check(cs.All(c=>c.enabled&&c.GetComponent<IDamageable>()!=null),"LOD does not disable damage collision");
            }
            return new{passed=true,checks,nativeDamageCalls=calls,details="Both variants: native armor/HP/critical destruction, blast/pierce/fire balance, server guard, same-object routing and LODs; RPC delivery replayed locally, live combat unverified"};
        }finally{current=null;h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);}
    }
}
