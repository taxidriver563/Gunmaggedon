using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

public static class AuditReleaseFixesExport
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static string Hash(string path){using(var file=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(file)).Replace("-","");}
    static void Passed(JObject report,string name)
    {
        Check((bool?)report["passed"]==true,name+" must pass");
        foreach(var property in report.Properties())if(property.Value is JObject&&property.Value["passed"]!=null)Check((bool?)property.Value["passed"]==true,name+" failed group: "+property.Name);
    }
    static bool ObsoleteDebrisRemap(JToken patch)
    {
        if(!Regex.IsMatch((patch["GameAsset"]??new JObject()).ToString(),@"debris\s*[234]",RegexOptions.IgnoreCase))return false;
        foreach(var location in patch["PatchLocations"]??new JArray()){
            string target=(string)location["hierarchyPath"]??"";target+=" "+((string)location["id"]??"");
            string member=(string)location["memberPath"]??"";
            if(target.IndexOf("shrapnel",StringComparison.OrdinalIgnoreCase)>=0&&Regex.IsMatch(member,@"^(?:m_Mesh[123]|mesh[123]|meshes\[[123]\])$",RegexOptions.IgnoreCase))return true;
        }
        return false;
    }
    public static object Run()
    {
        checks=0;
        string root=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??(System.Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT") ?? throw new System.InvalidOperationException("Set GUNMAGGEDON_ROOT before running editor tools or fixtures."));
        string reportDir=Path.Combine(root,"GunmaggedonRuntime/build/release-check");
        string path=Path.Combine(root,"ModBuild/0.0.42/Gunmageddon_0.0.42.nobp");
        string runtimePath=Path.Combine(root,"GunmaggedonRuntime/build/1.22.1/Gunmaggedon.Runtime.dll");
        string identityPath=Path.Combine(reportDir,"source-identity.json"),nativePath=Path.Combine(reportDir,"native.json");
        var identity=JObject.Parse(File.ReadAllText(identityPath));var native=JObject.Parse(File.ReadAllText(nativePath));Passed(identity,"Source identity");Passed(native,"Native validation");
        Check((string)identity["runtime"]=="1.22.1"&&(string)identity["pack"]=="0.0.42"&&(int?)identity["protocol"]==10,"Validated pair/protocol identity");
        Check((string)identity["helper"]==(string)native["helper"],"Native report matches validated helper identity");
        string runtimeHash=Hash(runtimePath);Check(runtimeHash==(string)identity["runtimeSha256"],"Candidate DLL matches compiled/validated binary identity");
        var sourceRoot=Path.GetFullPath(Path.Combine(root,"GunmaggedonRuntime/src"))+Path.DirectorySeparatorChar;
        var sources=(JArray)identity["sources"];Check(sources!=null&&sources.Count==(int?)identity["sourceCount"]&&Directory.GetFiles(sourceRoot,"*.cs",SearchOption.AllDirectories).Length==sources.Count,"Source snapshot membership");
        foreach(var source in sources){string file=Path.GetFullPath(Path.Combine(root,"GunmaggedonRuntime",(string)source["path"]));Check(file.StartsWith(sourceRoot,StringComparison.OrdinalIgnoreCase)&&Hash(file)==(string)source["sha256"],"Current source still matches native-tested snapshot: "+source["path"]);}
        string shaderSource=File.ReadAllText(Path.Combine(Application.dataPath,"Blueprinter/Mods/Gunmageddon/Wreckage/WreckScorch.shader"));
        Check(Regex.IsMatch(shaderSource,"\"LightMode\"\\s*=\\s*\"UniversalForwardOnly\""),"Wreck shader uses the ForwardOnly pass for forward/deferred compatibility");
        Check(Hash(Path.Combine(root,"GunmaggedonRuntime/tools/WreckScorch.shader"))==Hash(Path.Combine(Application.dataPath,"Blueprinter/Mods/Gunmageddon/Wreckage/WreckScorch.shader")),"Owned shader source matches Unity export source");
        var bundle=AssetBundle.LoadFromFile(path);if(!bundle)throw new Exception("Cannot load release-fix pack");
        try{
            var names=bundle.GetAllAssetNames();
            var c127Clips=names.Where(n=>n.Contains("/c127/")&&n.EndsWith(".anim")).Select(n=>bundle.LoadAsset<AnimationClip>(n)).ToArray();
            Check(c127Clips.Length==4&&c127Clips.All(c=>c),"Four owned production C127 clips exported");
            int c127Animations=0;
            foreach(var name in names.Where(n=>n.Contains("/c127/")&&n.EndsWith(".prefab"))){var prefab=bundle.LoadAsset<GameObject>(name);if(!prefab||!prefab.GetComponent<Unit>())continue;foreach(var animation in prefab.GetComponentsInChildren<Animation>(true)){Check(!animation.clip||c127Clips.Contains(animation.clip),"C127 default animation binds production clip");foreach(AnimationState state in animation)Check(c127Clips.Contains(state.clip),"C127 animation state binds production clip");c127Animations++;}}
            Check(c127Animations>=5,"All C127 animation components audited");
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Blueprinter/Mods/Gunmageddon/C127"})){var source=AssetDatabase.GUIDToAssetPath(guid);Check(!AssetDatabase.GetDependencies(source,true).Any(p=>p.Contains("/GunmaggedonStaging/")),"No C127 source staging dependency");}
            Check(!names.Any(n=>n.Contains("_donotship")||n.Contains("blueprintersetupbackups")||n.Contains("gunmaggedonstaging")||n.EndsWith(".dll")||n.EndsWith(".cs")),"No private dependency bodies, source or backups bundled");
            var shader=bundle.LoadAsset<Shader>("assets/blueprinter/mods/gunmageddon/wreckage/wreckscorch.shader");Check(shader&&shader.isSupported,"Compiled shared wreck shader is supported");
            var soot=bundle.LoadAsset<Texture2D>("assets/blueprinter/mods/gunmageddon/wreckage/shared_wreck_soot.asset");Check(soot&&soot.width==128&&soot.mipmapCount>1,"Shared mipmapped soot texture");
            var shardPaths=names.Where(n=>n.Contains("/impactshards/")&&bundle.LoadAsset<Mesh>(n)).ToArray();Check(shardPaths.Length==4,"Exactly four owned impact shard mesh assets");
            var shards=shardPaths.Select(n=>bundle.LoadAsset<Mesh>(n)).ToArray();foreach(var shard in shards){long triangles=0;for(int i=0;i<shard.subMeshCount;i++)triangles+=(long)shard.GetIndexCount(i)/3;Check(shard.name.StartsWith("Gunmaggedon_ImpactShard",StringComparison.Ordinal)&&shard.vertexCount==5&&triangles==6,"Owned economical impact geometry");}
            int shrapnelSystems=0;
            foreach(string effect in new[]{"203mm_impactEffectArmor","203mm_impactEffectGround"}){
                var prefab=bundle.LoadAsset<GameObject>("assets/blueprinter/mods/gunmageddon/"+effect.ToLowerInvariant()+".prefab");Check(prefab,"Impact prefab exists: "+effect);
                var renderers=prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(r=>r.name=="shrapnel").ToArray();Check(renderers.Length==1,"One shrapnel renderer in "+effect);
                foreach(var renderer in renderers){var meshes=new Mesh[renderer.meshCount];int count=renderer.GetMeshes(meshes);Check(count==4&&meshes.All(m=>m&&shards.Contains(m))&&shards.All(meshes.Contains),"All shrapnel mesh slots use owned geometry in "+effect);shrapnelSystems++;}
            }
            int wrecks=0,assigned=0,groups=0,platforms=0;
            foreach(string name in names.Where(n=>n.EndsWith(".prefab"))){
                var prefab=bundle.LoadAsset<GameObject>(name);if(!prefab)continue;var unit=prefab.GetComponent<Unit>();
                if(unit is Building||unit is GroundVehicle){platforms++;var wreck=new SerializedObject(unit).FindProperty("wreckage").objectReferenceValue as GameObject;Check(wreck,"Every platform has exported wreckage reference");if(wreck.name.EndsWith("_Wreck",StringComparison.Ordinal))assigned++;}
                foreach(var group in prefab.GetComponentsInChildren<LODGroup>(true)){
                    groups++;var levels=group.GetLODs();var triangles=levels.Select(l=>l.renderers.Sum(r=>{Check(r,"LOD renderer survived export");var mesh=r.GetComponent<MeshFilter>().sharedMesh;long total=0;for(int s=0;s<mesh.subMeshCount;s++)total+=(long)mesh.GetIndexCount(s)/3;return total;})).ToArray();
                    Check(group.enabled&&levels.Length>=3&&triangles[0]>triangles[1]&&triangles[1]>triangles[2],"Meaningful progressively cheaper exported model LODs");
                }
                if(!name.Contains("/wreckage/")||!name.EndsWith("_wreck.prefab"))continue;wrecks++;
                Check(!unit&&!prefab.GetComponent<Mirage.NetworkIdentity>(),"Wreck cannot become an operational unit");
                Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c is Wreckage),"No targeting/weapon controllers on wreck");
                foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))foreach(var material in renderer.sharedMaterials)Check(material&&material.shader==shader&&material.GetTexture("_SootMap")==soot,"Exported wreck materials share owned shader/texture");
            }
            Check(platforms==17&&wrecks==15&&assigned==15&&groups==37,"Expected17platforms,15new wrecks and37LODGroups");
            Check(names.Count(n=>n.Contains("/wreckage/")&&bundle.LoadAsset<Texture2D>(n))==1,"No per-wreck texture sets");Check(names.Count(n=>bundle.LoadAsset<AudioClip>(n))==9,"Existing nine custom sounds unchanged");
            Check(names.Where(n=>n.EndsWith(".mp3")).All(n=>bundle.LoadAsset<AudioClip>(n)&&!bundle.LoadAsset<TextAsset>(n)),"Audio labels represent imported clips rather than raw text payloads");
            var text=bundle.LoadAsset<TextAsset>("assets/blueprinter/generated/patch_manifest.json");Check(text,"Native reference remap manifest exists");var manifest=JObject.Parse(text.text);Check((string)manifest["modVersion"]=="0.0.42","Correct exported pack version");
            var patches=manifest["Patches"] as JArray;Check(patches!=null&&patches.Count>0,"Native dependency remaps remain");Check(!patches.Any(ObsoleteDebrisRemap),"No obsolete native debris2/3/4 shrapnel m_Mesh1/2/3 remaps");Check(!patches.Any(p=>(p["GameAsset"]??new JObject()).ToString().IndexOf("debris",StringComparison.OrdinalIgnoreCase)>=0),"No native debris dependencies remain");
            var result=new{passed=true,checks,assets=names.Length,platforms,wrecks,assigned,groups,ownedImpactMeshes=shards.Length,shrapnelSystems,sharedSootTextures=1,pack="0.0.42",runtime="1.22.1",protocol=10,packSha256=Hash(path),runtimeSha256=runtimeHash,sourceIdentitySha256=Hash(identityPath),nativeReportSha256=Hash(nativePath),sourceCount=sources.Count};
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(result);Directory.CreateDirectory(reportDir);File.WriteAllText(Path.Combine(reportDir,"export-audit.json"),json);return json;
        }finally{bundle.Unload(true);}
    }
}
