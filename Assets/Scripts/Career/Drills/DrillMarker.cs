using TMPro;
using UnityEngine;

/// <summary>
/// One fielder's marker in Hitting the gaps: a flat disc on the grass with his position's name
/// above it. The prefab (Resources/Prefabs/Drills/DrillMarker, built by Scene Objects > Build
/// Nets Trial Prefabs) holds the disc mesh, the two unlit materials and the label's text setup;
/// DrillFieldMarkers places one per fielder. Until 2026-11-15 the mesh, the materials and the text
/// were made in code for each drill.
/// </summary>
public sealed class DrillMarker : MonoBehaviour
{
    [SerializeField] private MeshRenderer disc;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Material normal;
    [SerializeField] private Material marked;

    public TextMeshPro Label => label;

    /// The disc in the red material (the fielder who would have had the last shot) or the usual one.
    public void SetMarked(bool on)
    {
        if (disc != null)
            disc.sharedMaterial = on ? marked : normal;
    }
}
