using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the root object "ProceduralClouds" (the ProceduralClouds component) in CricketVR.unity and
/// Nets.unity, the two scenes that had a sky, with its billboard material and 1x1 quad mesh saved
/// under Assets/Art/Generated/ProceduralClouds. Until 2026-11-15 the game spawned the component at
/// scene load with code defaults, so its inspector fields could only be edited in Play and never
/// stuck. Now the placed component's fields are the ones used. Run again, it only re-links the
/// material and mesh and leaves tuned values alone. The clouds themselves are still spawned under
/// it each time it is enabled: their layout is random per run by design.
/// </summary>
public static class ProceduralCloudsBuilder
{
    private static readonly string[] ScenePaths = { "Assets/Scenes/CricketVR.unity", "Assets/Scenes/Nets.unity" };
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/ProceduralClouds";
    public const string ObjectName = "ProceduralClouds";

    [MenuItem("CricketVR/Scene Objects/Place Procedural Clouds")]
    public static void PlaceAll()
    {
        Shader shader = Shader.Find("CricketVR/CloudBillboard");
        if (shader == null)
        {
            Debug.LogError("[ProceduralCloudsBuilder] Shader CricketVR/CloudBillboard not found; nothing placed.");
            return;
        }
        var made = new Material(shader) { name = "CloudBillboardMat", enableInstancing = true };
        Material material = SceneObjectAssets.SaveMaterial(made, Folder + "/CloudBillboardMat.mat");
        Mesh quad = SceneObjectAssets.Save(BuildQuad(), Folder + "/CloudQuad.asset");
        foreach (string path in ScenePaths)
            Place(path, material, quad);
    }

    private static void Place(string scenePath, Material material, Mesh quad)
    {
        Scene scene = SceneObjectAssets.Open(scenePath);
        GameObject go = SceneObjectAssets.Root(scene, ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        if (!go.TryGetComponent(out ProceduralClouds clouds))
            clouds = go.AddComponent<ProceduralClouds>();
        var so = new SerializedObject(clouds);
        so.FindProperty("cloudMaterial").objectReferenceValue = material;
        so.FindProperty("cloudQuad").objectReferenceValue = quad;
        so.ApplyModifiedProperties();
        SceneObjectAssets.Save(scene);
        Debug.Log($"[ProceduralCloudsBuilder] {ObjectName} placed in {scenePath}");
    }

    /// <summary>A 1x1 quad on the XY plane, facing +Z, which the billboard then aims.</summary>
    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "CloudQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        // The quad is aimed every frame, so Unity's own bounds would be recomputed for nothing.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
        return mesh;
    }
}
