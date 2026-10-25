// Temporary editor-only validation bridge. Does not modify scenes or prefabs.
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class GunmaggedonOptimizationValidationBootstrap
{
    const string Root=@"C:\Users\keena\OneDrive\Documents\Gunmaggedon";
    static readonly string Request=Root+"/GunmaggedonRuntime/build/optimization/editor-request.txt";
    static readonly string Output=Root+"/GunmaggedonRuntime/build/optimization/editor-validation.json";
    static bool running;static double nextPoll;
    [Serializable] sealed class Manifest {public string helper,fixtures,root;}
    static GunmaggedonOptimizationValidationBootstrap(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(running||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(Request))return;string nonce=File.ReadAllText(Request).Trim();if(SessionState.GetString("Gunmaggedon.Optimization.Completed","")==nonce)return;
        if(SessionState.GetString("Gunmaggedon.Optimization.Reloaded","")!=nonce){SessionState.SetString("Gunmaggedon.Optimization.Reloaded",nonce);EditorUtility.RequestScriptReload();return;}
        running=true;Run(nonce);
    }
    static async void Run(string nonce)
    {
        var results=new Dictionary<string,object>();results["nonce"]=nonce;results["state"]="running";Write(results);
        string oldRoot=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),oldHelper=Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER");
        ResolveEventHandler resolve=(sender,args)=>{var path=Path.Combine(@"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\core",new AssemblyName(args.Name).Name+".dll");return File.Exists(path)?Assembly.LoadFrom(path):null;};
        try{
            AppDomain.CurrentDomain.AssemblyResolve+=resolve;Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\core\0Harmony.dll");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/GunmaggedonRuntime/build/validation/current/current.json"));
            results["helper"]=manifest.helper;using(var sha=System.Security.Cryptography.SHA256.Create())results["helperSha256"]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(manifest.helper))).Replace("-","");Write(results);
            if(!Path.GetFullPath(manifest.fixtures).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFullPath(manifest.helper).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Validation artifacts must stay inside workspace");
            Environment.SetEnvironmentVariable("GUNMAGGEDON_ROOT",Root);Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",manifest.helper);var assembly=Assembly.Load(File.ReadAllBytes(manifest.fixtures));
            foreach(var name in new[]{"StaticReviewRegression","SearchlightCoastalIntegration","F88RigRegression","F88EffectsRegression","F88FlakIntegration","F88ReloadRegression","L25RigRegression","L25TrailerIntegration","UnitLODIntegration","F88FragmentsIntegration","RuntimeOptimizationIntegration"}){
                try{object value=assembly.GetType(name,true).GetMethod("Run").Invoke(null,null);var task=value as Task<object>;if(task!=null)value=await task;results[name]=value;}catch(Exception ex){results[name]=new{passed=false,error=ex.GetBaseException().ToString()};}
                Write(results);await Task.Delay(50);
            }
            results["state"]="completed";
        }catch(Exception ex){results["state"]="failed";results["error"]=ex.ToString();}
        finally{AppDomain.CurrentDomain.AssemblyResolve-=resolve;Environment.SetEnvironmentVariable("GUNMAGGEDON_ROOT",oldRoot);Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",oldHelper);Write(results);SessionState.SetString("Gunmaggedon.Optimization.Completed",nonce);running=false;}
    }
    static void Write(object value){var json=Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\NuclearOption_Data\Managed\Newtonsoft.Json.dll").GetType("Newtonsoft.Json.JsonConvert");File.WriteAllText(Output,(string)json.GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new[]{value}));}
}
