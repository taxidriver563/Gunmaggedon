using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
public static class RenameUnitDesignations
{
    public static readonly Dictionary<string,string> Names=new Dictionary<string,string>{
        {"CoastalTopAttackBomb","TA-1"},{"Emplacement203mmCD","CD-203"},{"Emplacement203mmCD_SPECIAL","CD-203S"},
        {"F88_AA_Definition","F-88"},{"F120_AA_Definition","F-120"},{"L25_AA_Definition","L-25"},
        {"L40_AA_Definition","L-40"},{"Searchlight_Definition","SL-1"}};
    public static object Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        var workspace=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");
        var backup=Path.Combine(workspace,"UnitySetupBackup/Designations/BeforeRename");Directory.CreateDirectory(backup);
        var report=new List<object>();
        foreach(var pair in Names){
            string path="Assets/Blueprinter/Mods/Gunmageddon/"+pair.Key+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);if(!asset)throw new Exception("Missing "+path);
            if(!File.Exists(Path.Combine(backup,pair.Key+".asset")))File.Copy(path,Path.Combine(backup,pair.Key+".asset"));
            var before=JObject.Parse(EditorJsonUtility.ToJson(asset));var old=asset.unitName;
            var so=new SerializedObject(asset);so.FindProperty("unitName").stringValue=pair.Value;so.FindProperty("code").stringValue=pair.Value;so.ApplyModifiedPropertiesWithoutUndo();
            var after=JObject.Parse(EditorJsonUtility.ToJson(asset));
            foreach(var data in new[]{before,after})foreach(var property in data.Descendants().OfType<JProperty>().Where(p=>p.Name=="unitName"||p.Name=="code").ToArray())property.Remove();
            if(!JToken.DeepEquals(before,after))throw new Exception("Unrelated definition changes for "+pair.Key);
            report.Add(new{asset=pair.Key,oldName=old,newName=asset.unitName,key=asset.jsonKey});
        }
        AssetDatabase.SaveAssets();
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);File.WriteAllText(Path.Combine(backup,"rename-report.json"),json);return json;
    }
}
