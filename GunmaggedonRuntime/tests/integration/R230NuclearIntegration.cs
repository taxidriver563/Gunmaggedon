using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class R230NuclearIntegration
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static bool authority=true;static int checks;
    public static bool Local(ref bool __result){__result=authority;return false;}
    public static bool Server(Mirage.NetworkBehaviour __instance,ref bool __result){if(!(__instance is Unit))return true;__result=authority;return false;}
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static object Get(object o,string name){return o.GetType().GetField(name,F).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,F).SetValue(o,value);}
    public static object Run()
    {
        checks=0;authority=true;var a=Assembly.Load(File.ReadAllBytes(Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER")));var type=a.GetType("GunmageddonAudio.R230NuclearRuntime");var runtime=a.GetType("GunmageddonAudio.R230Runtime");
        var scene=EditorSceneManager.NewPreviewScene();var stage=new GameObject("R230N isolated validation");stage.SetActive(false);SceneManager.MoveGameObjectToScene(stage,scene);
        var origin=typeof(Datum).GetField("<origin>k__BackingField",S);var old=origin.GetValue(null);var position=typeof(Datum).GetField("<originPosition>k__BackingField",S);var oldPosition=position.GetValue(null);var h=new Harmony("gunmaggedon.r230n."+Guid.NewGuid());
        try
        {
            origin.SetValue(null,stage.transform);position.SetValue(null,Vector3.zero);
            var stock=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/nuclearBomb1_PLACEHOLDER.prefab");Check(stock,"Native nuclear reference loaded");
            Check((bool)type.GetMethod("ResolveTemplate",S).Invoke(null,null),"Native nuclear airburst effect resolves");
            var nativeBomb=stock.GetComponent<Missile>();var nativeWarhead=Get(nativeBomb,"warhead");
            Check((GameObject)type.GetField("template",S).GetValue(null)==(GameObject)Get(nativeWarhead,"airEffect"),"Uses stock nuclearBomb1 effect, never the first arbitrary nuclear/UFO effect");
            var decoyObject=new GameObject("Wrong nuclear effect source");decoyObject.transform.SetParent(stage.transform,false);var decoy=decoyObject.AddComponent<Missile>();var decoyDefinition=ScriptableObject.CreateInstance<MissileDefinition>();
            try{decoyDefinition.jsonKey="UFO";decoy.definition=decoyDefinition;Set(decoy,"warhead",nativeWarhead);Set(decoy,"blastYield",1500000f);Check(!(bool)type.GetMethod("BindTemplate",S).Invoke(null,new object[]{decoy}),"Wrong source rejected even with matching yield and nuclear components");}finally{UnityEngine.Object.DestroyImmediate(decoyObject);UnityEngine.Object.DestroyImmediate(decoyDefinition);}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230N_Emplacement.prefab");Check(prefab,"Separate variant prefab exists");
            var root=UnityEngine.Object.Instantiate(prefab,stage.transform,false);var owner=root.GetComponent<Building>();var gun=root.GetComponentInChildren<Gun>(true);
            Check(owner.definition.jsonKey=="Gunmaggedon_R230N"&&owner.definition.unitName=="R-230N"&&gun.info.name=="R230N_Railgun","Separate variant definition and weapon");
            Check(root.GetComponentsInChildren<Collider>(true).All(c=>c.GetComponent<UnitPart>()&&(Unit)Get(c.GetComponent<UnitPart>(),"parentUnit")==owner),"Every copied damage hitbox points at the variant owner");
            Check(gun.info.blastDamage==0&&gun.info.pierceDamage==0,"Native shockwave is the sole nuclear damage path");
            Check((bool)runtime.GetMethod("Nuclear",S).Invoke(null,new object[]{gun}),"Variant recognized");
            var ordinary=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/R230_Emplacement.prefab").GetComponentInChildren<Gun>(true);
            Check(!(bool)runtime.GetMethod("Nuclear",S).Invoke(null,new object[]{ordinary})&&ordinary.info.pierceDamage==15000,"Ordinary R230 remains kinetic");
            var group=root.GetComponent<LODGroup>();var triangles=group.GetLODs().Select(l=>l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)).ToArray();Check(group.enabled&&triangles.SequenceEqual(new[]{1060,610,288}),"Three inherited decreasing model LODs");
            var targetObject=new GameObject("Fast crossing target");targetObject.transform.SetParent(stage.transform,false);targetObject.AddComponent<Mirage.NetworkIdentity>();var body=targetObject.AddComponent<Rigidbody>();body.useGravity=false;var target=targetObject.AddComponent<Aircraft>();typeof(Unit).GetField("disabled",F).SetValue(target,false);typeof(Unit).GetField("<rb>k__BackingField",F).SetValue(target,body);targetObject.transform.position=new Vector3(500,50,0);body.velocity=new Vector3(0,1000,0);
            var controller=root.GetComponentInChildren<Turret>(true).gameObject.AddComponent(a.GetType("GunmageddonAudio.R230Controller"));Set(controller,"target",target);
            var sim=stage.AddComponent<BulletSim>();typeof(BulletSim).GetField("gun",F).SetValue(sim,gun);
            h.Patch(typeof(Unit).GetProperty("LocalSim").GetGetMethod(),prefix:new HarmonyMethod(typeof(R230NuclearIntegration).GetMethod("Local")));
            h.Patch(typeof(Mirage.NetworkBehaviour).GetProperty("IsServer").GetGetMethod(),prefix:new HarmonyMethod(typeof(R230NuclearIntegration).GetMethod("Server")));
            authority=false;var add=new object[]{sim,null};type.GetMethod("Add",S).Invoke(null,add);Check(add[1]==null,"Remote simulation cannot originate nuclear flights");authority=true;
            var flights=(IDictionary)type.GetField("Flights",S).GetValue(null);var effects=(IList)type.GetField("Effects",S).GetValue(null);
            Action<float,bool> shot=(height,expect)=>
            {
                targetObject.transform.position=new Vector3(500,height,0);gun.transform.position=new Vector3(-10000,0,0);
                var state=new object[]{sim,null};type.GetMethod("Add",S).Invoke(null,state);Check(state[1]!=null,"Authoritative shot captured");
                var bullet=new BulletSim.Bullet(Vector3.right*15000,null,1,true,false,null){position=Vector3.zero.ToGlobalPosition(),velocity=Vector3.right*15000,active=true,destructTimer=10};
                var before=new object[]{bullet,gun.info,owner,null};type.GetMethod("Before",S).Invoke(null,before);type.GetMethod("Added",S).Invoke(null,new object[]{null,state[1]});Check(flights.Count==1,"Native flight associated exactly once");
                bullet.position=new Vector3(1000,0,0).ToGlobalPosition();int count=effects.Count;type.GetMethod("After",S).Invoke(null,new[]{(object)bullet,1f/15,before[3]});
                Check((effects.Count==count+1)==expect,"Swept proximity result matches moving target: expected="+expect+" effects="+count+"->"+effects.Count+" flightTarget="+(Get(Get(state[1],"Flight"),"Target")!=null)+" disabled="+target.Networkdisabled);
                if(expect){Check(!bullet.active&&flights.Count==0&&bullet.position.ToLocalPosition().x>400&&bullet.position.ToLocalPosition().x<600,"Proxy bursts inside travelled segment and retires once");var effect=(GameObject)effects[effects.Count-1];Check(Mathf.Approximately((float)Get(effect.GetComponentInChildren<Shockwave>(true),"yieldKilotons"),1.5f),"Native shockwave configured to1.5kt");Check(Mathf.Approximately((float)Get(effect.GetComponentInChildren<MushroomCloud>(true),"yield"),1500000f),"Native cloud configured to1500000kg TNT");}
                if(expect){int emitted=effects.Count;type.GetMethod("After",S).Invoke(null,new[]{(object)bullet,1f/15,before[3]});Check(effects.Count==emitted,"Duplicate callback cannot detonate a spent shot again");}
                type.GetMethod("Remove",S).Invoke(null,new object[]{bullet});
            };
            shot(50,true);shot(300,false);
            var network=a.GetType("GunmageddonAudio.F88FlakNetwork");network.GetMethod("Install",S).Invoke(null,null);
            var messageType=a.GetType("GunmageddonAudio.R230NuclearBurst");var message=Activator.CreateInstance(messageType);Set(message,"Point",new GlobalPosition(100,50000,200));Set(message,"Owner",123u);
            var packer=typeof(Mirage.Serialization.MessagePacker);var bytes=(byte[])packer.GetMethods().Single(m=>m.Name=="Pack"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==1).MakeGenericMethod(messageType).Invoke(null,new[]{message});var copy=packer.GetMethod("Unpack").MakeGenericMethod(messageType).Invoke(null,new object[]{bytes,null});
            Check((uint)Get(copy,"Owner")==123&&((GlobalPosition)Get(copy,"Point")).y==50000,"Native Mirage nuclear burst round-trip");int emittedBefore=effects.Count;network.GetMethod("ReceiveNuclear",S).Invoke(null,new[]{null,message});Check(effects.Count==emittedBefore,"Unconnected receiver cannot duplicate the blast");
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Laser_EW1_PLACEHOLDER.prefab");
            var warning=stage.AddComponent(a.GetType("GunmageddonAudio.R230Warning"));warning.GetType().GetMethod("SetNuclear",F).Invoke(warning,new object[]{true});Check((float)Get(warning,"warningScale")==4,"Variant warning four times larger");
            warning.GetType().GetMethod("Initialize",F).Invoke(warning,new object[]{gun.transform});var camera=stage.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,10000,-5000);
            warning.GetType().GetMethod("Show",F).Invoke(warning,new object[]{new Vector3(8000,3000,0).ToGlobalPosition(),1f,1f});warning.GetType().GetMethod("Render",F).Invoke(warning,new object[]{camera});
            var nativeWarning=(MeshRenderer)Get(warning,"nativeBeam");Check(nativeWarning&&nativeWarning.enabled&&!((LineRenderer)Get(warning,"beam")).enabled,"Native warning render path uses initialized property blocks");
            var properties=new MaterialPropertyBlock();nativeWarning.GetPropertyBlock(properties);Check(Mathf.Approximately(properties.GetFloat("_Emissive"),(float)Get(warning,"nativeEmission")*1.10f),"Bigger warning preserves modest native emission");
            warning.GetType().GetMethod("Hide",F).Invoke(warning,null);Check(!nativeWarning.enabled&&!((MeshRenderer)Get(warning,"auraGlow")).enabled,"Bigger native warning and aura hide together");warning.GetType().GetMethod("OnDestroy",F).Invoke(warning,null);UnityEngine.Object.DestroyImmediate(warning);
            return new{passed=true,checks,triangles,details="Native prefab, stock effect/yield parameters, swept crossing/miss, authority, retirement, separate standard gun; blast propagation/render/transport need mission verification"};
        }
        finally
        {
            var effects=(IList)type.GetField("Effects",S).GetValue(null);foreach(GameObject effect in effects)if(effect)UnityEngine.Object.DestroyImmediate(effect);type.GetMethod("Shutdown",S).Invoke(null,null);
            var registry=(IDictionary)typeof(Mirage.Serialization.MessagePacker).GetProperty("MessageTypes").GetValue(null);foreach(var name in new[]{"FlakHello","FlakBurst","FlakPose","R230ChargeCue","R230NuclearBurst"})registry.Remove(Mirage.Serialization.MessagePacker.GetId(a.GetType("GunmageddonAudio."+name)));
            h.UnpatchSelf();EditorSceneManager.ClosePreviewScene(scene);origin.SetValue(null,old);position.SetValue(null,oldPosition);authority=true;
        }
    }
}
