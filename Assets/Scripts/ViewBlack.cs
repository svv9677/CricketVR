using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A black quad just in front of the headset's eyes, for a blink: the career's change of ends
/// (NonStrikerView) and a match's (EndChange). Plain C#, owned by the component that blinks;
/// Dispose when it goes.
/// </summary>
public class ViewBlack
{
    private GameObject blackQuad;
    private MeshRenderer blackRenderer;
    private Material blackMaterial;
    private Mesh blackMesh;

    public void Dispose()
    {
        if (blackQuad != null) Object.Destroy(blackQuad);
        if (blackMaterial != null) Object.Destroy(blackMaterial);
        if (blackMesh != null) Object.Destroy(blackMesh);
    }

    public bool Ensure()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        if (blackQuad != null && blackQuad.transform.parent == cam.transform)
            return true;
        if (blackMaterial == null)
        {
            // Both in the build's always-included shaders; UI/Default's depth test can be switched off.
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return false;
            blackMaterial = new Material(shader) { name = "Blink", renderQueue = 5000, color = new Color(0f, 0f, 0f, 0f) };
            blackMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        }
        if (blackMesh == null)
            blackMesh = QuadMesh();
        if (blackQuad == null)
        {
            blackQuad = new GameObject("Blink", typeof(MeshFilter), typeof(MeshRenderer));
            blackQuad.GetComponent<MeshFilter>().sharedMesh = blackMesh;
            blackRenderer = blackQuad.GetComponent<MeshRenderer>();
            blackRenderer.sharedMaterial = blackMaterial;
            blackRenderer.shadowCastingMode = ShadowCastingMode.Off;
            blackRenderer.receiveShadows = false;
            blackRenderer.enabled = false;
        }
        blackQuad.layer = FirstLayer(cam.cullingMask);
        blackQuad.transform.SetParent(cam.transform, false);
        // Just past the near plane, and wide enough to fill any headset's view from there.
        float z = Mathf.Max(cam.nearClipPlane * 1.5f, 0.02f);
        blackQuad.transform.localPosition = new Vector3(0f, 0f, z);
        blackQuad.transform.localRotation = Quaternion.identity;
        blackQuad.transform.localScale = Vector3.one * (z * 6f);
        return true;
    }

    /// Off when clear: a full-view transparent quad is not free on a Quest.
    public void Set(float alpha, bool visible)
    {
        if (!visible || blackRenderer == null)
            return;
        blackMaterial.color = new Color(0f, 0f, 0f, alpha);
        blackRenderer.enabled = alpha > 0.001f;
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
