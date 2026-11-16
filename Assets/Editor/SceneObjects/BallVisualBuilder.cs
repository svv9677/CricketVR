using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the seamed ball on Assets/Resources/Prefabs/Ball.prefab: the BallVisual component and its
/// "Seamed Ball" child (generated mesh, seam texture and material, all saved under
/// Assets/Art/Generated/BallVisual), with the ball's own 444k-vertex MeshRenderer switched off.
/// Until 2026-11-15 BallVisual made the child, mesh, texture and material in code as the game
/// started. The material is made from ball-white.mat, the one the match scene's ball uses (Main.prefab
/// sets it over the prefab's own); TestLook still recolours the child's material in a Test match.
/// Run again to rebuild them; it changes nothing else on the prefab.
/// </summary>
public static class BallVisualBuilder
{
    private const string BallPrefab = "Assets/Resources/Prefabs/Ball.prefab";
    private const string SourceMaterial = "Assets/Resources/Materials/ball-white.mat";
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/BallVisual";
    private const float FallbackRadius = 0.0306f;

    [MenuItem("CricketVR/Scene Objects/Place Seamed Ball")]
    public static void Place()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BallPrefab);
        try
        {
            MeshRenderer own = root.GetComponent<MeshRenderer>();
            MeshFilter ownFilter = root.GetComponent<MeshFilter>();
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterial) ?? own.sharedMaterial;
            Transform child = root.transform.Find("Seamed Ball");
            if (child == null)
            {
                child = new GameObject("Seamed Ball").transform;
                child.SetParent(root.transform, false);
            }
            child.gameObject.layer = root.layer;
            // Same size as the old mesh (its bounds), whatever the ball's scale.
            float radius = ownFilter.sharedMesh != null ? ownFilter.sharedMesh.bounds.extents.x : FallbackRadius;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one * radius;
            (child.GetComponent<MeshFilter>() ?? child.gameObject.AddComponent<MeshFilter>()).sharedMesh =
                SceneObjectAssets.Save(BallVisualAssets.SeamedMesh(), Folder + "/Seamed Ball.asset");
            var r = child.GetComponent<MeshRenderer>() ?? child.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = SeamedMaterial(source);
            r.shadowCastingMode = own.shadowCastingMode;
            r.receiveShadows = own.receiveShadows;
            r.lightProbeUsage = own.lightProbeUsage;
            own.enabled = false;
            BallVisual visual = root.GetComponent<BallVisual>() ?? root.AddComponent<BallVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("visual").objectReferenceValue = child;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BallPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BallVisualBuilder] Ball prefab: Seamed Ball placed.");
    }

    /// The ball's material with the seam texture made from its own (a copy of it if it has none).
    private static Material SeamedMaterial(Material source)
    {
        Texture2D seamed = BallVisualAssets.SeamedTexture(source.mainTexture);
        var mat = new Material(source) { name = "Seamed Ball" };
        if (seamed != null)
        {
            Texture2D tex = VisualAssetFiles.SavePng(seamed, Folder + "/Seamed Ball.png", true, TextureWrapMode.Repeat, true);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        }
        return SceneObjectAssets.SaveMaterial(mat, Folder + "/Seamed Ball.mat");
    }
}
