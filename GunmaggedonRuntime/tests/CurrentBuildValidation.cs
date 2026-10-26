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
        string previous=Environment.GetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER");
        try {
            string helper=previous;
            if(string.IsNullOrEmpty(helper)){
                var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,"GunmaggedonRuntime/build/validation/current/current.json")));
                helper=manifest.helper;
            }
            Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",helper);
            var results=new Dictionary<string,object>{{"helper",helper}};bool passed=true;
            var types=new[]{typeof(WreckageIntegration),typeof(WreckageLifecycleIntegration),typeof(C127Integration),typeof(DP127Integration),typeof(StaticReviewRegression),typeof(SearchlightCoastalIntegration),typeof(F88RigRegression),typeof(F88EffectsRegression),typeof(F88FlakIntegration),typeof(F88GroundIntegration),typeof(F120GroundIntegration),typeof(F88ColdOpticalIntegration),typeof(F120ColdOpticalIntegration),typeof(F88ReloadRegression),typeof(L25RigRegression),typeof(L25TrailerIntegration),typeof(UnitLODIntegration),typeof(F88FragmentsIntegration),typeof(RuntimeOptimizationIntegration),typeof(F120RigRegression),typeof(F120FlakIntegration),typeof(F120FragmentsIntegration),typeof(F120PresentationIntegration),typeof(F120DamageIntegration),typeof(F120IncendiaryIntegration),typeof(CD203ConcreteIntegration),typeof(CD203ClearanceRegression),typeof(CD203ScannerRegression),typeof(R230PressureRingRegression),typeof(R230PlayerAlertIntegration),typeof(R230Integration),typeof(R230LegacyControllerIntegration),typeof(R230LegacyIntegration),typeof(R230NuclearCommitmentIntegration),typeof(R230NuclearIntegration)};
            var names=new[]{"wreckage","wreckageLifecycle","c127","dp127","review","coastal","f88Rig","f88Effects","f88Flak","f88Ground","f120Ground","f88ColdOptical","f120ColdOptical","f88Reload","l25Rig","l25Trailer","lod","f88Fragments","optimization","f120Rig","f120Flak","f120Fragments","f120Presentation","f120Damage","f120Incendiary","cd203Concrete","cd203Clearance","cd203Scanner","r230Rings","r230PlayerAlert","r230","r230LegacyController","r230Legacy","r230NuclearCommitment","r230Nuclear"};
            for(int i=0;i<types.Length;i++){
                try{
                    var value=types[i].GetMethod("Run").Invoke(null,null);
                    var task=value as System.Threading.Tasks.Task<object>;if(task!=null)value=await task;
                    var json=value as string;
                    var result=json!=null?Newtonsoft.Json.Linq.JToken.Parse(json):Newtonsoft.Json.Linq.JToken.FromObject(value);
                    results[names[i]]=result;
                    var success=result["passed"];
                    if(success==null||success.Type!=Newtonsoft.Json.Linq.JTokenType.Boolean||!(bool)success)passed=false;
                }
                catch(Exception ex){passed=false;results[names[i]]=new Dictionary<string,object>{{"passed",false},{"error",ex.GetBaseException().ToString()}};}
            }
            results["passed"]=passed;return results;
        } finally { Environment.SetEnvironmentVariable("GUNMAGGEDON_TEST_HELPER",previous); }
    }
}
