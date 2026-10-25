// Copy into Assets/Editor. Editor menus and Play Mode glue; never bundled.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class RevampCarriageEditorPreview
{
    static RevampCarriageEditorPreview(){EditorApplication.playModeStateChanged+=Changed;try{var assembly=EditorPrefs.GetString("Gunmaggedon.CarriagePreview.Assembly","");if(File.Exists(assembly))File.WriteAllText(Path.Combine(Path.GetDirectoryName(assembly),"preview-editor-ready.txt"),"RevampCarriageEditorPreview loaded "+DateTime.UtcNow.ToString("O"));}catch(IOException){}}
    static GameObject Root(){var selected=Selection.activeGameObject;var root=selected?selected.transform.root.gameObject:null;if(root&&root.transform.Find("RigReferences/FrontPistonRest"))return root;return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.transform.Find("RigReferences/FrontPistonRest"));}
    [MenuItem("Gunmaggedon/Revamp carriage/Preview towing")]
    static void Towing(){Pose(true);}
    [MenuItem("Gunmaggedon/Revamp carriage/Preview deployed")]
    static void Deployed(){Pose(false);}
    static void Pose(bool towed)
    {
        var root=Root();if(!root){Debug.LogWarning("Select the revamp carriage rig");return;}
        SessionState.SetBool("Gunmaggedon.CarriagePreview.Towed",towed);
        if(EditorApplication.isPlaying){var motion=root.GetComponents<MonoBehaviour>().FirstOrDefault(c=>c&&c.GetType().FullName=="GunmageddonAudio.RevampCarriageMotion");if(motion)motion.GetType().GetMethod("SetTowed").Invoke(motion,new object[]{towed,null});return;}
        Undo.RecordObjects(root.GetComponentsInChildren<Transform>(true).Cast<UnityEngine.Object>().Concat(root.GetComponentsInChildren<Collider>(true)).ToArray(),"Preview carriage deployment");
        var sides=root.transform.Find("StationarySupports");Undo.RecordObject(sides.gameObject,"Preview carriage supports");sides.gameObject.SetActive(!towed);
        foreach(var name in new[]{"Front","Rear"}){var piston=root.transform.Find(name+"Piston");piston.localPosition=root.transform.Find("RigReferences/"+name+"PistonRest").localPosition+(towed?Vector3.up*.85f:Vector3.zero);foreach(var collider in piston.GetComponentsInChildren<Collider>(true))collider.enabled=!towed;}
        foreach(var wheel in root.GetComponentsInChildren<WheelCollider>())wheel.brakeTorque=towed?0:20000;
        EditorUtility.SetDirty(root);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);SceneView.RepaintAll();
    }
    static void Changed(PlayModeStateChange change)
    {
        if(change!=PlayModeStateChange.EnteredPlayMode)return;var root=Root();if(!root)return;
        var path=EditorPrefs.GetString("Gunmaggedon.CarriagePreview.Assembly","");if(!File.Exists(path)){Debug.LogWarning("Re-run the carriage editor setup to configure its preview assembly");return;}
        var loaded=Assembly.Load(File.ReadAllBytes(path));var a=AppDomain.CurrentDomain.GetAssemblies().First(x=>x.ManifestModule.ModuleVersionId==loaded.ManifestModule.ModuleVersionId);var type=a.GetType("GunmageddonAudio.RevampCarriageMotion",true);
        var motion=root.AddComponent(type);motion.hideFlags=HideFlags.DontSave;type.GetMethod("Initialize").Invoke(motion,null);type.GetMethod("SetTowed").Invoke(motion,new object[]{SessionState.GetBool("Gunmaggedon.CarriagePreview.Towed",false),null});
        // This model-only scene needs a temporary surface for Play Mode physics.
        var ground=new GameObject("Carriage preview ground (Play Mode only)");ground.hideFlags=HideFlags.DontSave;ground.transform.position=root.transform.position+new Vector3(0,-.5f,0);ground.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
        Debug.Log("Revamp carriage physics preview active. Use Gunmaggedon > Revamp carriage to switch deployment states.");
    }
}
