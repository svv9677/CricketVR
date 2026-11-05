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
/// Built at runtime under NetsTrial, so it goes with the scene; no imported assets. One disc mesh and
/// two unlit materials (URP Unlit, as PitchLines makes its own), dropped onto whatever is tagged
/// Ground under each place. It fails soft: no shader, no ground, or anything else that goes wrong
/// leaves fewer markers or none, never a broken drill - the progress line still says how the shot
/// went.
/// </summary>
public sealed class DrillFieldMarkers
{
    /// The disc (m) and how far it sits above the grass.
    private const float DiscRadius = 0.9f, Lift = 0.03f;
    private const int Segments = 24;
    /// A label's size grows with its distance from the batter so a deep fielder's name still reads
    /// (TMP world size; NetsTrialCard's progress line is 3 at 7 m), within these.
    private const float LabelPerMetre = 0.3f, MinLabel = 3f, MaxLabel = 14f;
    /// Label height above the disc: a fixed part and a part of its size (bigger text, higher).
    private const float LabelUp = 1.2f, LabelUpPerSize = 0.12f;
    /// The batter's eyes, for which way the labels face.
    private const float EyeHeight = 1.6f;

    private static readonly Color Normal = new Color(0.98f, 0.86f, 0.35f);
    private static readonly Color Marked = new Color(0.94f, 0.3f, 0.28f);

    private struct Marker
    {
        public string name;
        public MeshRenderer disc;
        public TextMeshPro label;
    }

    private readonly Transform parent;
    private readonly List<Marker> markers = new List<Marker>();
    private GameObject root;
    private Mesh discMesh;
    private Material normal, marked;
    private bool triedMaterials;

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
            bool on = fielder != null && m.name == fielder;
            if (m.disc != null && normal != null)
                m.disc.sharedMaterial = on ? marked : normal;
            if (m.label != null)
                m.label.color = on ? Marked : Color.white;
        }
    }

    /// The markers go; the mesh and materials are kept for the next Show.
    public void Hide()
    {
        markers.Clear();
        Kill(root);
        root = null;
    }

    /// Everything goes, the mesh and materials too.
    public void Dispose()
    {
        Hide();
        Kill(discMesh);
        Kill(normal);
        Kill(marked);
        discMesh = null;
        normal = marked = null;
        triedMaterials = false;
    }

    private Marker Make(string name, Vector3 place, Vector3 eye)
    {
        var m = new Marker { name = name };
        if (Materials())
        {
            var go = new GameObject($"Fielder {name}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root.transform, false);
            go.transform.position = place;
            go.GetComponent<MeshFilter>().sharedMesh = discMesh ??= Disc();
            m.disc = go.GetComponent<MeshRenderer>();
            m.disc.sharedMaterial = normal;
            m.disc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.disc.receiveShadows = false;
        }
        float size = LabelSize(Vector3.ProjectOnPlane(place - eye, Vector3.up).magnitude);
        var text = new GameObject($"Fielder {name} Label");
        text.transform.SetParent(root.transform, false);
        m.label = text.AddComponent<TextMeshPro>();
        m.label.text = NetsTrialCard.Latin1(name);
        m.label.fontSize = size;
        m.label.alignment = TextAlignmentOptions.Center;
        m.label.textWrappingMode = TextWrappingModes.NoWrap;
        m.label.overflowMode = TextOverflowModes.Overflow;
        m.label.rectTransform.sizeDelta = new Vector2(size * 4f, size * 0.5f);
        m.label.color = Color.white;
        Vector3 up = place + Vector3.up * (LabelUp + size * LabelUpPerSize);
        Vector3 away = up - eye;
        away.y = 0f;
        text.transform.SetPositionAndRotation(up, away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away, Vector3.up) : Quaternion.identity);
        return m;
    }

    /// The two unlit materials, made once; false when the shader is not there (labels only then).
    private bool Materials()
    {
        if (normal != null)
            return true;
        if (triedMaterials)
            return false;
        triedMaterials = true;
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
            return false;
        normal = new Material(unlit) { name = "Drill Fielder" };
        normal.SetColor("_BaseColor", Normal);
        normal.SetFloat("_Cull", 0f);
        marked = new Material(unlit) { name = "Drill Fielder Marked" };
        marked.SetColor("_BaseColor", Marked);
        marked.SetFloat("_Cull", 0f);
        return true;
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
