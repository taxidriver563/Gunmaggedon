using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Targeted source-publication repair. Does not export, install, save scenes or
// run the historical C127 setup migration. Pass the workspace and connected
// project roots explicitly rather than relying on the process working directory.
public static class FixC127AnimationReferences
{
    const string Production = "Assets/Blueprinter/Mods/Gunmageddon/C127/";
    const string Staging = "Assets/GunmaggedonStaging/C127/";
    static readonly string[] Clips = {
        "C127_Exposed_Aim_Preview.anim", "C127_Exposed_Reload_Preview.anim",
        "C127_Twin_Aim_Preview.anim", "C127_Twin_Recoil_Preview.anim"
    };
    static readonly string[] Prefabs = {
        "C127_Emplacement.prefab", "C127A_Emplacement.prefab",
        "C127B_Emplacement.prefab", "C127C_Emplacement.prefab",
        "C127T_Emplacement.prefab"
    };

    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    static string Root(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    static string[] Dependencies(string asset)
    {
        return AssetDatabase.GetDependencies(asset, true).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    static string LodSignature(GameObject root)
    {
        var groups = root.GetComponentsInChildren<LODGroup>(true);
        if (groups.Length == 0) throw new Exception(root.name + " has no LODGroup");
        var covered = new HashSet<Renderer>();
        var signatures = new List<string>();
        foreach (var group in groups)
        {
            var levels = group.GetLODs();
            if (!group.enabled || levels.Length < 2)
                throw new Exception(root.name + " lacks enabled multi-level LODs");
            var geometry = new List<int>();
            foreach (var level in levels)
            {
                int triangles = 0;
                foreach (var renderer in level.renderers)
                {
                    if (!renderer) throw new Exception(root.name + " has a null LOD renderer");
                    covered.Add(renderer);
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh && mesh.sharedMesh) triangles += mesh.sharedMesh.triangles.Length / 3;
                }
                geometry.Add(triangles);
            }
            if (!geometry.Skip(1).Any(x => x < geometry[0]))
                throw new Exception(root.name + " has no cheaper distant LOD");
            signatures.Add(group.enabled + "|" + group.size.ToString("R") + "|" + group.localReferencePoint + "|" +
                string.Join(";", levels.Select((l, i) => l.screenRelativeTransitionHeight.ToString("R") + ":" + l.renderers.Length + ":" + geometry[i])));
        }
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            if (!covered.Contains(renderer)) throw new Exception(root.name + " has an uncovered mesh renderer: " + renderer.name);
        return string.Join("/", signatures);
    }

    static int Replace(SerializedProperty property, Dictionary<UnityEngine.Object, AnimationClip> replacements)
    {
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            throw new Exception("Unexpected native Animation serialized layout");
        AnimationClip replacement;
        var previous = property.objectReferenceValue;
        if (previous && replacements.TryGetValue(previous, out replacement))
        {
            property.objectReferenceValue = replacement;
            return 1;
        }
        return 0;
    }

    public static object Run(string workspaceRoot, string expectedProjectRoot)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Stable edit mode required");
        var project = Root(Path.Combine(Application.dataPath, ".."));
        if (!String.Equals(project, Root(expectedProjectRoot), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Connected Unity project is not the explicitly selected project");
        var workspace = Root(workspaceRoot);
        if (!File.Exists(Path.Combine(workspace, "AGENTS.md")) || !Directory.Exists(Path.Combine(workspace, "context")))
            throw new Exception("Expected Gunmaggedon workspace not found");

        var scene = SceneManager.GetActiveScene();
        var sceneHandle = scene.handle;
        var scenePath = scene.path;
        var sceneDirty = scene.isDirty;
        var sceneRoots = scene.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray();
        var replacements = new Dictionary<UnityEngine.Object, AnimationClip>();
        var clipReport = new List<object>();
        foreach (var name in Clips)
        {
            var source = Staging + name;
            var target = Production + name;
            var sourceHash = Hash(Path.Combine(project, source));
            var targetHash = Hash(Path.Combine(project, target));
            if (sourceHash != targetHash) throw new Exception("Clip bodies differ; refusing behavioral change: " + name);
            var before = AssetDatabase.LoadAssetAtPath<AnimationClip>(source);
            var after = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
            if (!before || !after || before == after) throw new Exception("Missing or aliased clip: " + name);
            replacements.Add(before, after);
            clipReport.Add(new { source, target, sha256 = sourceHash,
                sourceGuid = AssetDatabase.AssetPathToGUID(source), targetGuid = AssetDatabase.AssetPathToGUID(target) });
        }

        var paths = Prefabs.Select(x => Production + x).ToArray();
        var beforeDependencies = paths.ToDictionary(x => x, Dependencies);
        var beforeLods = paths.ToDictionary(x => x, x => {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(x);
            if (!asset) throw new Exception("Missing production prefab: " + x);
            return LodSignature(asset);
        });
        var backup = Path.Combine(workspace, "UnitySetupBackup", "ReleaseCheck", "AnimationReferences",
            DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
        Directory.CreateDirectory(backup);
        foreach (var path in paths)
        {
            File.Copy(Path.Combine(project, path), Path.Combine(backup, Path.GetFileName(path)));
            File.Copy(Path.Combine(project, path + ".meta"), Path.Combine(backup, Path.GetFileName(path) + ".meta"));
        }
        File.WriteAllText(Path.Combine(backup, "before-dependencies.json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new { project, scenePath, sceneHandle, sceneDirty, clips = clipReport,
                dependencies = beforeDependencies, lods = beforeLods }, Newtonsoft.Json.Formatting.Indented));

        var changes = new List<object>();
        try
        {
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = 0;
                    foreach (var animation in root.GetComponentsInChildren<Animation>(true))
                    {
                        var serialized = new SerializedObject(animation);
                        int componentCount = Replace(serialized.FindProperty("m_Animation"), replacements);
                        var clips = serialized.FindProperty("m_Animations");
                        if (clips == null || !clips.isArray) throw new Exception("Missing Animation.m_Animations");
                        for (int index = 0; index < clips.arraySize; index++)
                            componentCount += Replace(clips.GetArrayElementAtIndex(index), replacements);
                        if (componentCount > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
                        count += componentCount;
                    }
                    if (LodSignature(root) != beforeLods[path]) throw new Exception("Unexpected LOD change: " + path);
                    if (count > 0)
                    {
                        bool saved;
                        PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
                        if (!saved) throw new Exception("Failed to save: " + path);
                    }
                    changes.Add(new { prefab = path, referencesRebound = count });
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var afterDependencies = paths.ToDictionary(x => x, Dependencies);
            foreach (var path in paths)
            {
                if (afterDependencies[path].Any(x => x.StartsWith("Assets/GunmaggedonStaging/", StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("A staging dependency remains: " + path);
                if (LodSignature(AssetDatabase.LoadAssetAtPath<GameObject>(path)) != beforeLods[path])
                    throw new Exception("Saved LOD configuration changed: " + path);
            }
            var current = SceneManager.GetActiveScene();
            if (current.handle != sceneHandle || current.path != scenePath || current.isDirty != sceneDirty ||
                !sceneRoots.SequenceEqual(current.GetRootGameObjects().Select(x => x.GetInstanceID()).OrderBy(x => x)))
                throw new Exception("Original source scene state changed unexpectedly");
            var result = new { project, backup, scenePath, sceneDirty, scenePreserved = true, clips = clipReport,
                changes, afterDependencies, lodsPreserved = true, exported = false, installed = false };
            File.WriteAllText(Path.Combine(backup, "after-dependencies.json"),
                Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { backup, scenePreserved = true, changes,
                stagingDependencies = afterDependencies.Values.SelectMany(x => x).Where(x => x.StartsWith(Staging)).Distinct().ToArray(),
                lodsPreserved = true, exported = false, installed = false });
        }
        catch (Exception error)
        {
            foreach (var path in paths)
            {
                File.Copy(Path.Combine(backup, Path.GetFileName(path)), Path.Combine(project, path), true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            File.WriteAllText(Path.Combine(backup, "rollback.txt"), error.ToString());
            throw;
        }
    }
}
