using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Puts the "Blink" quad on the XR camera in Assets/Prefabs/Scene/Controllers.prefab: a black quad
/// just past the near plane that ViewBlack fades for the change of ends (EndChange, NonStrikerView).
/// Its mesh and material are saved under Assets/Art/Generated/Blink. The quad is switched off
/// (inactive) in the prefab. Until 2026-11-15 ViewBlack made the quad, mesh and material on the
/// camera in code the first time a blink ran. Run again to put it back to these settings.
/// </summary>
public static class BlinkQuadBuilder
{
    private const string RigPrefab = "Assets/Prefabs/Scene/Controllers.prefab";
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/Blink";

    [MenuItem("CricketVR/Scene Objects/Place Blink Quad")]
    public static void Place()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RigPrefab);
        try
        {
            Camera cam = MainCamera(root);
            if (cam == null)
            {
                Debug.LogError("[BlinkQuadBuilder] No camera under " + RigPrefab);
                return;
            }
            Transform quad = cam.transform.Find(ViewBlack.ChildName);
            if (quad == null)
            {
                quad = new GameObject(ViewBlack.ChildName).transform;
                quad.SetParent(cam.transform, false);
            }
            quad.gameObject.layer = FirstLayer(cam.cullingMask);
            // Just past the near plane, and wide enough to fill any headset's view from there.
            float z = Mathf.Max(cam.nearClipPlane * 1.5f, 0.02f);
            quad.localPosition = new Vector3(0f, 0f, z);
            quad.localRotation = Quaternion.identity;
            quad.localScale = Vector3.one * (z * 6f);
            (SceneObjectAssets.Ensure<MeshFilter>(quad.gameObject)).sharedMesh =
                SceneObjectAssets.Save(QuadMesh(), Folder + "/Blink.asset");
            var r = SceneObjectAssets.Ensure<MeshRenderer>(quad.gameObject);
            r.sharedMaterial = BlinkMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            quad.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, RigPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BlinkQuadBuilder] Controllers prefab: Blink quad placed.");
    }

    /// The camera the game finds as Camera.main: the one tagged MainCamera, else the first.
    private static Camera MainCamera(GameObject root)
    {
        Camera first = null;
        foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
        {
            if (c.CompareTag("MainCamera"))
                return c;
            if (first == null)
                first = c;
        }
        return first;
    }

    /// Both shaders are in the build's always-included shaders; UI/Default's depth test can be switched off.
    private static Material BlinkMaterial()
    {
        Shader shader = SceneObjectAssets.Or(Shader.Find("UI/Default"), Shader.Find("Sprites/Default"));
        var mat = new Material(shader) { name = "Blink", renderQueue = 5000, color = new Color(0f, 0f, 0f, 0f) };
        mat.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        return SceneObjectAssets.SaveMaterial(mat, Folder + "/Blink.mat");
    }

    /// Unit quad facing the camera, white vertex colours (both shaders multiply by them).
    private static Mesh QuadMesh()
    {
        var mesh = new Mesh { name = "Blink" };
        mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        return mesh;
    }

    private static int FirstLayer(int mask)
    {
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0)
                return layer;
        return 0;
    }
}
