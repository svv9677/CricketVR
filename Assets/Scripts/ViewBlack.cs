using UnityEngine;

/// <summary>
/// A black quad just in front of the headset's eyes, for a blink: the career's change of ends
/// (NonStrikerView) and a match's (EndChange). Plain C#, owned by the component that blinks;
/// Dispose when it goes.
///
/// The quad is the "Blink" child of the XR camera in Controllers.prefab, with its mesh and
/// material saved as assets (BlinkQuadBuilder, CricketVR > Scene Objects > Place Blink Quad). It is
/// switched off in the prefab; this fades it through a property block (the saved material is never
/// written to) and switches it on only while it shows. Both blinkers share the one quad; they
/// never blink at the same time.
/// </summary>
public class ViewBlack
{
    public const string ChildName = "Blink";
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private GameObject blackQuad;
    private Renderer blackRenderer;
    // Made on first use: a MaterialPropertyBlock cannot be made in a component's field initialiser,
    // and EndChange and NonStrikerView create their ViewBlack there.
    private MaterialPropertyBlock block;

    public void Dispose()
    {
        if (blackQuad != null) blackQuad.SetActive(false);
        blackQuad = null;
        blackRenderer = null;
    }

    public bool Ensure()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        if (blackQuad != null && blackQuad.transform.parent == cam.transform)
            return true;
        Transform child = cam.transform.Find(ChildName);
        if (child == null)
            return false;
        blackRenderer = child.GetComponent<Renderer>();
        if (blackRenderer == null)
            return false;
        blackQuad = child.gameObject;
        return true;
    }

    /// Off when clear: a full-view transparent quad is not free on a Quest.
    public void Set(float alpha, bool visible)
    {
        if (!visible || blackRenderer == null)
            return;
        block ??= new MaterialPropertyBlock();
        blackRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
        blackRenderer.SetPropertyBlock(block);
        blackQuad.SetActive(alpha > 0.001f);
    }
}
