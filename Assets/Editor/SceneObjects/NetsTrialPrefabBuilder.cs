using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds what the nets' career sessions used to make in code, as prefabs and assets:
///   * Resources/Prefabs/UI/NetsTrialProgress - the progress line (NetsTrialCard): world-space text
///     with a soft-shadow font material and a dark sprite behind it (NetsTrialCard sizes the backing
///     to the words);
///   * Resources/Prefabs/Drills/DrillMarker - one fielder's marker for Hitting the gaps
///     (DrillFieldMarkers): a flat disc in an unlit material, the red one for the fielder who would
///     have had the last shot, and his position's name above it.
/// The material, mesh and sprite assets go under Assets/Art/Generated/NetsTrial. Run again to put
/// everything back to these settings (assets and prefabs are written in place). Until 2026-11-15
/// the game made all of it in code, with Shader.Find and Sprite.Create, in every nets session.
/// </summary>
public static class NetsTrialPrefabBuilder
{
    private const string Generated = SceneObjectAssets.GeneratedFolder + "/NetsTrial";
    private const string ProgressPath = "Assets/Resources/Prefabs/UI/NetsTrialProgress.prefab";
    private const string MarkerPath = "Assets/Resources/Prefabs/Drills/DrillMarker.prefab";
    /// Above the nets' scoreboard canvas (sorting order 10), which draws over anything nearer the eye.
    private const int OverTheScoreboard = 20;
    private const float DiscRadius = 0.9f;
    private const int Segments = 24;

    [MenuItem("CricketVR/Scene Objects/Build Nets Trial Prefabs")]
    public static void Build()
    {
        SceneObjectAssets.EnsureFolder(Generated);
        BuildProgress();
        BuildMarker();
        AssetDatabase.SaveAssets();
        Debug.Log("[NetsTrialPrefabBuilder] NetsTrialProgress and DrillMarker saved; assets under " + Generated);
    }

    // ---- Progress line --------------------------------------------------------------------------

    private static void BuildProgress()
    {
        var go = new GameObject("NetsTrialProgress");
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.fontSize = 1.4f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(4.5f, 1.6f);
        t.color = Color.white;
        t.fontSharedMaterial = Underlay(t.font.material);
        t.text = "";
        t.GetComponent<MeshRenderer>().sortingOrder = OverTheScoreboard;

        // A dark panel behind the lines so they read against the stands and the net.
        var back = new GameObject("Back", typeof(SpriteRenderer));
        back.transform.SetParent(go.transform, false);
        var sr = back.GetComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        sr.color = new Color(0.06f, 0.07f, 0.09f, 0.82f);
        sr.sortingOrder = OverTheScoreboard - 1;

        go.SetActive(false);        // shown once placed (NetsTrialCard.PlaceProgress)
        SceneObjectAssets.EnsureFolder(Path.GetDirectoryName(ProgressPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(go, ProgressPath);
        Object.DestroyImmediate(go);
    }

    /// A soft shadow under the letters (BallResultText's look).
    private static Material Underlay(Material fontMaterial)
    {
        var m = new Material(fontMaterial) { name = "Nets Trial Progress Underlay" };
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        m.SetFloat("_UnderlayOffsetX", 0.6f);
        m.SetFloat("_UnderlayOffsetY", -0.6f);
        m.SetFloat("_UnderlaySoftness", 0.5f);
        return SceneObjectAssets.SaveMaterial(m, Generated + "/Nets Trial Progress Underlay.mat");
    }

    /// A 4 x 4 px white square at 4 px per unit: one unit across, tinted and scaled by the user.
    private static Sprite WhiteSprite()
    {
        string path = Generated + "/White.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- Drill marker ---------------------------------------------------------------------------

    private static void BuildMarker()
    {
        Material normal = Unlit("Drill Fielder", DrillFieldMarkers.Normal);
        Material marked = Unlit("Drill Fielder Marked", DrillFieldMarkers.Marked);
        Mesh disc = SceneObjectAssets.Save(Disc(), Generated + "/Drill Fielder Disc.asset");

        var go = new GameObject("DrillMarker", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = disc;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = normal;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var text = new GameObject("Label");
        text.transform.SetParent(go.transform, false);
        TextMeshPro label = text.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.color = Color.white;
        label.text = "";                 // the name, size and place are set per fielder

        var marker = go.AddComponent<DrillMarker>();
        var so = new SerializedObject(marker);
        so.FindProperty("disc").objectReferenceValue = renderer;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("normal").objectReferenceValue = normal;
        so.FindProperty("marked").objectReferenceValue = marked;
        so.ApplyModifiedProperties();

        SceneObjectAssets.EnsureFolder(Path.GetDirectoryName(MarkerPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(go, MarkerPath);
        Object.DestroyImmediate(go);
    }

    /// An unlit, double-sided URP material of one colour.
    private static Material Unlit(string name, Color colour)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Cull", 0f);
        return SceneObjectAssets.SaveMaterial(m, $"{Generated}/{name}.mat");
    }

    /// A flat disc about the origin, facing up.
    private static Mesh Disc()
    {
        var v = new Vector3[Segments + 1];
        var n = new Vector3[Segments + 1];
        var tris = new int[Segments * 3];
        n[0] = Vector3.up;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            v[i + 1] = new Vector3(Mathf.Cos(a) * DiscRadius, 0f, Mathf.Sin(a) * DiscRadius);
            n[i + 1] = Vector3.up;
            // Clockwise seen from above (Unity's front face).
            tris[i * 3] = 0;
            tris[i * 3 + 1] = (i + 1) % Segments + 1;
            tris[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "Drill Fielder Disc" };
        mesh.vertices = v;
        mesh.normals = n;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
