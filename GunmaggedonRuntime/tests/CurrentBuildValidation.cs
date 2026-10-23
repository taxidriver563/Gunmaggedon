using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CurrentBuildValidation
{
    [Serializable] sealed class Manifest { public string helper = null; }
    public static async System.Threading.Tasks.Task<object> Run()
    {
        string root=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT")??throw new InvalidOperationException("Set GUNMAGGEDON_ROOT, then prepare-current-validation.ps1.");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,"GunmaggedonRuntime/build/validation/current/current.json")));
        string previous=Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER");
        try {
            Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",manifest.helper);
            var results=new Dictionary<string,object>{{"helper",manifest.helper}};bool passed=true;
            var types=new[]{typeof(StaticReviewRegression),typeof(SearchlightCoastalIntegration),typeof(F88RigRegression),typeof(F88EffectsRegression),typeof(F88FlakIntegration),typeof(F88ReloadRegression),typeof(L25RigRegression),typeof(L25TrailerIntegration),typeof(UnitLODIntegration),typeof(F88FragmentsIntegration),typeof(RuntimeOptimizationIntegration)};
            var names=new[]{"review","coastal","f88Rig","f88Effects","f88Flak","f88Reload","l25Rig","l25Trailer","lod","f88Fragments","optimization"};
            for(int i=0;i<types.Length;i++){
                try{var value=types[i].GetMethod("Run").Invoke(null,null);var task=value as System.Threading.Tasks.Task<object>;if(task!=null)value=await task;results[names[i]]=value;}
                catch(Exception ex){passed=false;results[names[i]]=new Dictionary<string,object>{{"passed",false},{"error",ex.GetBaseException().ToString()}};}
            }
            results["passed"]=passed;return results;
        } finally { Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",previous); }
    }
}
