using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared by the CricketVR > Scene Objects builders, which put into the scenes and prefabs what the
/// game used to build in code at runtime, so the editor shows what plays. Each saves what it makes
/// as assets (a material, mesh or texture made in memory is lost when the editor closes) and
/// writes into the same asset when run again, so references to it stay.
/// </summary>
public static class SceneObjectAssets
{
    /// Generated materials, meshes and textures go under here, one folder per builder.
    public const string GeneratedFolder = "Assets/Art/Generated";

    /// Saves `m` at `path`, into the material already there if there is one. Returns the saved one.
    public static Material SaveMaterial(Material m, string path)
    {
        EnsureFolder(Folder(path));
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
        if (existing == m)
            return existing;
        existing.shader = m.shader;
        existing.CopyPropertiesFromMaterial(m);
        existing.shaderKeywords = m.shaderKeywords;
        existing.renderQueue = m.renderQueue;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(m);
        return existing;
    }

    /// Saves a mesh, texture or other asset at `path`, into the one already there if there is one.
    public static T Save<T>(T asset, string path) where T : Object
    {
        EnsureFolder(Folder(path));
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        if (existing == asset)
            return existing;
        string keepName = existing.name;
        EditorUtility.CopySerialized(asset, existing);
        existing.name = keepName;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(asset);
        return existing;
    }

    /// Creates `folder` (and any missing parents) under Assets.
    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;
        string parent = Folder(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }

    /// Opens `scenePath` on its own. Refuses (throws) if the open scene has unsaved changes, so a
    /// builder never throws away someone's work or stops on a save dialog: save first, then run it.
    public static Scene Open(string scenePath)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == scenePath)
            return active;
        if (active.isDirty)
            throw new System.InvalidOperationException(
                $"{active.name} has unsaved changes: save it, then run the command again.");
        return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
    }

    /// The root object `name` in `scene`, or null.
    public static GameObject Root(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    /// The component `T` on `go`, added if missing. (Not `GetComponent() ?? AddComponent()`: in the
    /// editor a missing built-in component comes back as a "fake null" object that `??` keeps.)
    public static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// `a` unless it is null or destroyed (Unity's own null test, which `??` skips), else `b`.
    public static T Or<T>(T a, T b) where T : Object => a != null ? a : b;

    public static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static string Folder(string path) => System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
}
