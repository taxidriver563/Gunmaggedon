using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Editor-only, repeatable migration. Original meshes and transform paths stay intact.
public static class SetupUnitLODs
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    const string Generated=Dir+"LODMeshes";
    static readonly Dictionary<string,Mesh> Cache=new Dictionary<string,Mesh>();
    static bool Preview;
    static bool Detail(string name){return name.Contains("crank")||name=="shell_casing"||name=="shell_loaded";}
    static bool Effect(string name){return name=="SearchlightBeam"||name=="LensGlow";}
    static Mesh Reduce(Mesh source,float ratio)
    {
        if(source.triangles.Length/3<=96)return source;
        string key=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+"_"+source.name+"_"+ratio.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
        Mesh cached;if(Cache.TryGetValue(key,out cached))return cached;
        var vertices=source.vertices;var min=source.bounds.min;float longest=Mathf.Max(source.bounds.size.x,source.bounds.size.y,source.bounds.size.z);
        var original=Enumerable.Range(0,source.subMeshCount).Select(source.GetTriangles).ToArray();
        Vector3[] bestPositions=null;int[][] bestTriangles=null;int bestCount=source.triangles.Length;
        int target=Mathf.CeilToInt(bestCount*ratio);
        // Spatial clustering keeps UVs, normal seams, materials and articulated parts.
        // Keep the least destructive grid that reaches the requested budget.
        foreach(int resolution in new[]{128,96,64,48,32,24,20,16,12,10,8,6,4})
        {
            float cell=longest/resolution;if(cell<=0)break;
            var ids=new int[vertices.Length];var sums=new List<Vector3>();var counts=new List<int>();var map=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++){
                var v=(vertices[i]-min)/cell;var k=new Vector3Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.y),Mathf.RoundToInt(v.z));int index;
                if(!map.TryGetValue(k,out index)){index=sums.Count;map.Add(k,index);sums.Add(Vector3.zero);counts.Add(0);}
                ids[i]=index;sums[index]+=vertices[i];counts[index]++;
            }
            var positions=new Vector3[vertices.Length];for(int i=0;i<positions.Length;i++)positions[i]=sums[ids[i]]/counts[ids[i]];
            var triangles=new int[original.Length][];int total=0;
            for(int sub=0;sub<original.Length;sub++){
                var kept=new List<int>();var seen=new HashSet<string>();var input=original[sub];
                for(int i=0;i<input.Length;i+=3){int a=input[i],b=input[i+1],c=input[i+2];if(ids[a]==ids[b]||ids[a]==ids[c]||ids[b]==ids[c])continue;
                    if(Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).sqrMagnitude<1e-16f)continue;
                    var sorted=new[]{ids[a],ids[b],ids[c]};Array.Sort(sorted);if(!seen.Add(string.Join(",",sorted)))continue;
                    kept.Add(a);kept.Add(b);kept.Add(c);
                }triangles[sub]=kept.ToArray();total+=kept.Count;
            }
            // Never erase a material section or reduce a whole part to a sliver.
            if(triangles.Where((t,s)=>original[s].Length>0&&t.Length==0).Any()||total<12)continue;
            if(total<bestCount){bestCount=total;bestPositions=positions;bestTriangles=triangles;}
            if(total<=target)break;
        }
        if(bestPositions==null){Cache[key]=source;return source;}
        var used=bestTriangles.SelectMany(t=>t).Distinct().OrderBy(i=>i).ToArray();var remap=new int[vertices.Length];for(int i=0;i<used.Length;i++)remap[used[i]]=i;
        var mesh=new Mesh();mesh.name=source.name+"_LOD"+(ratio>.4f?"1":"2");mesh.indexFormat=source.indexFormat;
        mesh.vertices=used.Select(i=>bestPositions[i]).ToArray();
        var normals=source.normals;if(normals.Length==vertices.Length)mesh.normals=used.Select(i=>normals[i]).ToArray();
        var tangents=source.tangents;if(tangents.Length==vertices.Length)mesh.tangents=used.Select(i=>tangents[i]).ToArray();
        var colors=source.colors;if(colors.Length==vertices.Length)mesh.colors=used.Select(i=>colors[i]).ToArray();
        for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count==vertices.Length)mesh.SetUVs(channel,used.Select(i=>uv[i]).ToList());}
        mesh.subMeshCount=bestTriangles.Length;for(int s=0;s<bestTriangles.Length;s++)mesh.SetTriangles(bestTriangles[s].Select(i=>remap[i]).ToArray(),s);
        mesh.RecalculateBounds();
        if(Preview){Cache[key]=mesh;return mesh;}
        string path=Generated+"/"+key.Replace('/','_')+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(previous){EditorUtility.CopySerialized(mesh,previous);UnityEngine.Object.DestroyImmediate(mesh);mesh=previous;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        Cache[key]=mesh;return mesh;
    }
    static void Configure(GameObject root,bool bomb)
    {
        // Remove only this migration's generated children, never gameplay components.
        foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="GM_LOD1"||t.name=="GM_LOD2").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
        foreach(var group in root.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(group);
        var originals=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>!Effect(r.name)&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        // The SPECIAL's inactive bomb template gets its own scale/bounds and LODs.
        var template=root.transform.Find("TopAttackSubmunitionTemplate");
        if(template){originals=originals.Where(r=>!r.transform.IsChildOf(template)).ToArray();Configure(template.gameObject,true);}
        var levels=new List<Renderer>[] {new List<Renderer>(originals),new List<Renderer>(),new List<Renderer>()};
        foreach(var renderer in originals){
            if(Detail(renderer.name))continue;
            for(int l=1;l<=2;l++){
                var go=new GameObject("GM_LOD"+l);go.layer=renderer.gameObject.layer;go.transform.SetParent(renderer.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=Reduce(renderer.GetComponent<MeshFilter>().sharedMesh,l==1?.6f:.3f);
                var copy=go.AddComponent<MeshRenderer>();EditorUtility.CopySerialized(renderer,copy);
                if(l==2){copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;copy.receiveShadows=false;}
                levels[l].Add(copy);
            }
        }
        var lod=root.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;lod.animateCrossFading=false;
        // At normal FOV, full detail until about 100-200m for trailers. Coastal
        // bases are larger, so their screen-space transition is farther away.
        float near=bomb?.06f:.045f,mid=bomb?.018f:.012f,cull=bomb?.001f:.0005f;
        lod.SetLODs(new[]{new LOD(near,levels[0].ToArray()),new LOD(mid,levels[1].ToArray()),new LOD(cull,levels[2].ToArray())});
        lod.RecalculateBounds();lod.size*=1.2f; // allow recoil, door travel and traversing silhouette
        if(lod.size<=0||float.IsNaN(lod.size))throw new Exception("Invalid LOD bounds: "+root.name);
    }
    public static object Run()
    {
        var root=Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT");if(string.IsNullOrEmpty(root))throw new Exception("GUNMAGGEDON_ROOT required");
        if(!AssetDatabase.IsValidFolder(Generated))AssetDatabase.CreateFolder(Dir.TrimEnd('/'),"LODMeshes");
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Dir}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>{var g=AssetDatabase.LoadAssetAtPath<GameObject>(p);return g.GetComponent<Unit>()||p.EndsWith("_Turret_Rig.prefab");}).ToArray();
        var backup=Path.Combine(root,"UnitySetupBackup/LOD/BeforeRetrofit");Directory.CreateDirectory(backup);
        foreach(var path in paths){var dest=Path.Combine(backup,Path.GetFileName(path));if(!File.Exists(dest))File.Copy(path,dest);}
        var reports=new List<object>();Cache.Clear();
        foreach(var path in paths){var contents=PrefabUtility.LoadPrefabContents(path);try{
            Configure(contents,path.Contains("TopAttackBomb"));PrefabUtility.SaveAsPrefabAsset(contents,path);
            reports.Add(new {path,groups=contents.GetComponentsInChildren<LODGroup>(true).Select(g=>new {g.name,g.size,levels=g.GetLODs().Select(l=>new {height=l.screenRelativeTransitionHeight,renderers=l.renderers.Length,triangles=l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)}).ToArray()}).ToArray()});
        }finally{PrefabUtility.UnloadPrefabContents(contents);}}
        AssetDatabase.SaveAssets();var json=Newtonsoft.Json.JsonConvert.SerializeObject(reports,Newtonsoft.Json.Formatting.Indented);File.WriteAllText(Path.Combine(root,"UnitySetupBackup/LOD/retrofit-report.json"),json);return json;
    }
    public static object PreviewOnly()
    {
        Preview=true;Cache.Clear();var report=new List<object>();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try{
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Dir}).Select(AssetDatabase.GUIDToAssetPath)){
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset.GetComponent<Unit>()&&!path.EndsWith("_Turret_Rig.prefab"))continue;
                var go=UnityEngine.Object.Instantiate(asset);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);
                var components=go.GetComponentsInChildren<Component>(true).Where(c=>!(c is Transform)&&!(c is Renderer)&&!(c is MeshFilter)&&!(c is LODGroup)).ToArray();
                var snapshot=components.Select(EditorJsonUtility.ToJson).ToArray();
                Configure(go,path.Contains("TopAttackBomb"));
                if(components.Any(c=>!c))throw new Exception("Gameplay component removed "+path);
                for(int i=0;i<components.Length;i++)if(snapshot[i]!=EditorJsonUtility.ToJson(components[i]))throw new Exception("Gameplay component changed: "+path+" "+components[i].GetType());
                report.Add(new {path,groups=go.GetComponentsInChildren<LODGroup>(true).Select(g=>new {g.name,g.size,levels=g.GetLODs().Select(l=>new {height=l.screenRelativeTransitionHeight,renderers=l.renderers.Length,triangles=l.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3)}).ToArray()}).ToArray()});
                UnityEngine.Object.DestroyImmediate(go);
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);
        }finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);foreach(var mesh in Cache.Values.Distinct())if(mesh&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);Cache.Clear();Preview=false;}
    }
    public static object RenderPreview()
    {
        Preview=true;Cache.Clear();var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var rt=new RenderTexture(480,320,24);rt.Create();var old=RenderTexture.active;var tile=new Texture2D(480,320,TextureFormat.RGB24,false);
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Dir}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<Unit>()).ToArray();
        var sheet=new Texture2D(1440,320*paths.Length,TextureFormat.RGB24,false);
        try{
            var co=new GameObject("LOD preview camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(co,scene);var camera=co.GetComponent<Camera>();camera.scene=scene;
            camera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);camera.targetTexture=rt;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.2f,.23f);camera.cullingMask=1<<31;camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.fieldOfView=40;
            var lo=new GameObject("LOD preview sun",typeof(Light));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lo,scene);var light=lo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<31;lo.transform.rotation=Quaternion.Euler(45,-30,0);
            for(int row=0;row<paths.Length;row++){
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[row]));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);Configure(go,paths[row].Contains("TopAttackBomb"));
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                go.SetActive(true);var group=go.GetComponent<LODGroup>();var bounds=new Bounds();bool first=true;
                foreach(var r in group.GetLODs()[0].renderers){if(!r.gameObject.activeInHierarchy)continue;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                var target=bounds.center;float size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);camera.transform.position=target+new Vector3(1,.65f,1.2f).normalized*size*1.9f;camera.transform.LookAt(target);
                for(int l=0;l<3;l++){group.ForceLOD(l);camera.Render();RenderTexture.active=rt;tile.ReadPixels(new Rect(0,0,480,320),0,0);tile.Apply();sheet.SetPixels(l*480,(paths.Length-1-row)*320,480,320,tile.GetPixels());}
                UnityEngine.Object.DestroyImmediate(go);
            }
            sheet.Apply();string output=Path.Combine(Environment.GetEnvironmentVariable("GUNMAGGEDON_ROOT"),"UnitySetupBackup/LOD/preview.png");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,sheet.EncodeToPNG());return output;
        }finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tile);UnityEngine.Object.DestroyImmediate(sheet);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);foreach(var mesh in Cache.Values.Distinct())if(mesh&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);Cache.Clear();Preview=false;}
    }
}
