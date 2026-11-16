using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small helpers shared by the stadium builders (pitch surfaces, lighting, club ground, Test look):
/// finding objects in the match scene, putting a component under its Systems object, and setting
/// serialized fields on it the way the inspector does.
/// </summary>
public static class StadiumBuilderUtil
{
    public const string MatchScene = "Assets/Scenes/CricketVR.unity";

    /// The object at `path` under the scene's root object `root`, or null.
    public static Transform Find(Scene scene, string root, string path)
    {
        GameObject r = SceneObjectAssets.Root(scene, root);
        return r != null ? r.transform.Find(path) : null;
    }

    /// The child `name` of the scene's Systems object (made if it is not there), always on.
    public static GameObject SystemsChild(Scene scene, string name)
    {
        GameObject systems = SceneObjectAssets.Root(scene, "Systems");
        if (systems == null)
        {
            systems = new GameObject("Systems", typeof(SceneSystems));
            SceneManager.MoveGameObjectToScene(systems, scene);
        }
        Transform child = systems.transform.Find(name);
        if (child != null)
            return child.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(systems.transform, false);
        return go;
    }

    /// `go`'s component of type T, added if it is not there.
    public static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// Sets the serialized field (or nested field, "preset.key") `property` of `target` to `value`.
    public static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[StadiumBuilder] {target.GetType().Name} has no field '{property}'.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetFloat(Object target, string property, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetBool(Object target, string property, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetColor(Object target, string property, Color value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Sets the object-reference array `property` of `target` to `values`.
    public static void SetRefs(Object target, string property, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
