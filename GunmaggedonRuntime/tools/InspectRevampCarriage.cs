using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class InspectRevampCarriage
{
    const string Dir="Assets/Blueprinter/Mods/Gunmageddon/";
    static float[] V(Vector3 v){return new[]{v.x,v.y,v.z};}
    public static object Run()
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Gunmaggedon Revamp Carriage.fbx");
        if(!asset)throw new Exception("Revamp carriage FBX not imported");
        var scene=EditorSceneManager.GetActiveScene();
        return new {scene=scene.path,dirty=scene.isDirty,selection=Selection.gameObjects.Select(g=>g.name).ToArray(),
            roots=scene.GetRootGameObjects().Select(g=>new {g.name,position=V(g.transform.position),rotation=V(g.transform.eulerAngles),scale=V(g.transform.localScale),components=g.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"MISSING").ToArray()}).ToArray(),
            model=asset.GetComponentsInChildren<Transform>(true).Select(t=>new{path=AnimationUtility.CalculateTransformPath(t,asset.transform),position=V(t.localPosition),rotation=V(t.localEulerAngles),scale=V(t.localScale),mesh=t.GetComponent<MeshFilter>()?t.GetComponent<MeshFilter>().sharedMesh.name:null,
                bounds=t.GetComponent<Renderer>()?new{center=V(t.GetComponent<Renderer>().bounds.center),size=V(t.GetComponent<Renderer>().bounds.size)}:null,
                triangles=t.GetComponent<MeshFilter>()?(int)(t.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3):0,
                materials=t.GetComponent<Renderer>()?t.GetComponent<Renderer>().sharedMaterials.Select(m=>m?m.name:"MISSING").ToArray():null}).ToArray(),
            layers=Enumerable.Range(0,32).Select(i=>new{i,name=LayerMask.LayerToName(i)}).Where(x=>x.name!="").ToArray()};
    }
}
