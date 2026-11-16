using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Where the virtual fielders stand for Hitting the gaps: the nets have no fielders, so each one is
/// a flat disc on the grass at his place (FieldSets.Place on the nets ground) with his position's
/// name above it, turned to face the batter. The fielder who would have had the last shot (stopped
/// it, or caught it) is marked in red until the next ball's call.
///
/// Each marker is the DrillMarker prefab (the disc mesh, the two unlit materials and the label's
/// text setup, saved by Scene Objects > Build Nets Trial Prefabs), instantiated under a "Drill
/// Field" object per Show and dropped onto whatever is tagged Ground under each place. It fails
/// soft: no prefab, no ground, or anything else that goes wrong leaves fewer markers or none,
/// never a broken drill - the progress line still says how the shot went.
/// </summary>
public sealed class DrillFieldMarkers
{
    /// The disc (m) and how far it sits above the grass.
    private const float Lift = 0.03f;
    private const string MarkerResource = "Prefabs/Drills/DrillMarker";
    /// A label's size grows with its distance from the batter so a deep fielder's name still reads
    /// (TMP world size; NetsTrialCard's progress line is 3 at 7 m), within these.
    private const float LabelPerMetre = 0.3f, MinLabel = 3f, MaxLabel = 14f;
    /// Label height above the disc: a fixed part and a part of its size (bigger text, higher).
    private const float LabelUp = 1.2f, LabelUpPerSize = 0.12f;
    /// The batter's eyes, for which way the labels face.
    private const float EyeHeight = 1.6f;

    /// The disc's colours (the prefab's two materials are these) and the marked fielder's label.
    public static readonly Color Normal = new Color(0.98f, 0.86f, 0.35f);
    public static readonly Color Marked = new Color(0.94f, 0.3f, 0.28f);

    private struct Marker
    {
        public string name;
        public DrillMarker marker;
    }

    private readonly Transform parent;
    private readonly List<Marker> markers = new List<Marker>();
    private GameObject root;
    private GameObject markerPrefab;

    public DrillFieldMarkers(Transform parent) => this.parent = parent;

    /// Where each position stands on this ground (y 0, before it is dropped onto the grass). Pure.
    public static Vector3[] Places(FieldSets.Position[] field, FieldSets.Ground g)
    {
        if (field == null)
            return new Vector3[0];
        var at = new Vector3[field.Length];
        for (int i = 0; i < field.Length; i++)
            at[i] = FieldSets.Place(field[i], g);
        return at;
    }

    /// The label's size for a fielder this far (m) from the batter.
    public static float LabelSize(float distance) => Mathf.Clamp(distance * LabelPerMetre, MinLabel, MaxLabel);

    /// This field on this ground, in place of whatever was shown.
    public void Show(FieldSets.Position[] field, FieldSets.Ground g)
    {
        Hide();
        if (field == null || field.Length == 0)
            return;
        try
        {
            root = new GameObject("Drill Field");
            if (parent != null)
                root.transform.SetParent(parent, false);
            Vector3 eye = new Vector3(g.strikerX, EyeHeight, 0f);
            Vector3[] at = Places(field, g);
            for (int i = 0; i < field.Length; i++)
                markers.Add(Make(field[i].name, OnGrass(at[i]), eye));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DrillFieldMarkers] no field markers: {e.Message}");
            Hide();
        }
    }

    /// Mark this fielder (by position name) and no other; null or no match marks none.
    public void Highlight(string fielder)
    {
        foreach (Marker m in markers)
        {
            if (m.marker == null)
                continue;
            bool on = fielder != null && m.name == fielder;
            m.marker.SetMarked(on);
            m.marker.Label.color = on ? Marked : Color.white;
        }
    }

    /// The markers go.
    public void Hide()
    {
        markers.Clear();
        Kill(root);
        root = null;
    }

    /// Everything goes.
    public void Dispose() => Hide();

    private Marker Make(string name, Vector3 place, Vector3 eye)
    {
        var m = new Marker { name = name };
        markerPrefab ??= Resources.Load<GameObject>(MarkerResource);
        if (markerPrefab == null)
            throw new InvalidOperationException($"no prefab at Resources/{MarkerResource}");
        GameObject go = UnityEngine.Object.Instantiate(markerPrefab, root.transform);
        go.name = $"Fielder {name}";
        go.transform.position = place;
        m.marker = go.GetComponent<DrillMarker>();
        TextMeshPro label = m.marker.Label;
        label.gameObject.name = $"Fielder {name} Label";
        float size = LabelSize(Vector3.ProjectOnPlane(place - eye, Vector3.up).magnitude);
        label.text = NetsTrialCard.Latin1(name);
        label.fontSize = size;
        label.rectTransform.sizeDelta = new Vector2(size * 4f, size * 0.5f);
        Vector3 up = place + Vector3.up * (LabelUp + size * LabelUpPerSize);
        Vector3 away = up - eye;
        away.y = 0f;
        label.transform.SetPositionAndRotation(up, away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away, Vector3.up) : Quaternion.identity);
        return m;
    }

    /// `p` dropped onto the highest visible Ground under it (as PitchLines lays its lines), or the
    /// world's y 0 when there is none, Lift above.
    private static Vector3 OnGrass(Vector3 p)
    {
        var from = new Vector3(p.x, p.y + 20f, p.z);
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
        {
            Renderer r = hit.collider.GetComponent<Renderer>();
            if (hit.collider.CompareTag("Ground") && r != null && r.enabled && hit.point.y > best)
                best = hit.point.y;
        }
        p.y = (float.IsNegativeInfinity(best) ? 0f : best) + Lift;
        return p;
    }

    private static void Kill(UnityEngine.Object o)
    {
        if (o == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(o);
        else
            UnityEngine.Object.DestroyImmediate(o);
    }
}
