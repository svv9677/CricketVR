using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the pitch's white lines as the root object "Pitch Lines" in CricketVR.unity and Nets.unity,
/// with the mesh and the two materials (lines, pitch edges) saved under
/// Assets/Art/Generated/PitchLines, and turns off the old "Main/Pitch/Pitch Markings" renderer in
/// each scene (its collider stays). Until 2026-11-15 PitchLines.cs built the same thing in code as
/// each scene loaded. The geometry is unchanged: laid out from the two stumps to the Laws (bowling
/// crease, popping crease, return creases, wide guidelines, guard marks, pitch edges), each corner
/// dropped onto the visible Ground under it. Run it again after moving the stumps or the pitch.
/// The object keeps the name "Pitch Lines" (ClubGroundBuilder.ForbiddenNames and others look for it).
/// </summary>
public static class PitchLinesBuilder
{
    private static readonly string[] ScenePaths = { "Assets/Scenes/CricketVR.unity", "Assets/Scenes/Nets.unity" };
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/PitchLines";
    public const string ObjectName = "Pitch Lines";

    private const float Crease = 0.06f, Edge = PitchLines.EdgeWidth;
    /// Above the surface under them: creases over the edge lines where they cross, both just
    /// clear of the turf so they lie on it rather than float.
    private const float CreaseLift = 0.004f, EdgeLift = 0.002f;
    private const float BowlingCreaseHalf = 1.32f, PoppingHalf = 1.83f, PoppingOut = 1.22f;
    private const float ReturnZ = 1.32f, ReturnBack = 2.44f, WideZ = 0.89f;
    /// Guard marks: in line with each stump (9.525 cm apart), this long back from the popping
    /// crease and this wide.
    private static readonly float[] GuardZ = { -0.09525f, 0f, 0.09525f };
    private const float GuardLength = 0.3f, GuardWidth = 0.025f;

    [MenuItem("CricketVR/Scene Objects/Place Pitch Lines")]
    public static void PlaceAll()
    {
        foreach (string path in ScenePaths)
            Place(path);
    }

    private static void Place(string scenePath)
    {
        Scene scene = SceneObjectAssets.Open(scenePath);
        GameObject battingEnd = GameObject.Find("Main/Stumps_BattingEnd");
        GameObject bowlingEnd = GameObject.Find("Main/Stumps_BowlingEnd");
        Shader unlit = Shader.Find(PitchLines.LinesShader);
        if (battingEnd == null || bowlingEnd == null || unlit == null)
        {
            Debug.LogError($"[PitchLinesBuilder] {scenePath}: stumps or the PitchLines shader not found; nothing placed.");
            return;
        }
        // The texture's lines are replaced, not doubled - first, so the lines are laid on the
        // pitch, not on that texture's plane 1.5 cm above it.
        GameObject markings = GameObject.Find("Main/Pitch/Pitch Markings");
        if (markings != null && markings.TryGetComponent(out MeshRenderer old))
        {
            old.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(old);
        }
        Physics.SyncTransforms();

        Mesh mesh = SceneObjectAssets.Save(BuildMesh(battingEnd.transform.position.x, bowlingEnd.transform.position.x),
                                           Folder + "/PitchLines_" + scene.name + ".asset");   // per scene: ground heights may differ
        Material lines = MakeMaterial(unlit, "Pitch Lines", new Color(0.96f, 0.96f, 0.94f));
        Material edges = MakeMaterial(unlit, "Pitch Edges", new Color(0.82f, 0.8f, 0.7f));

        GameObject go = SceneObjectAssets.Root(scene, ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { lines, edges };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SceneObjectAssets.Save(scene);
        Debug.Log($"[PitchLinesBuilder] {ObjectName} placed in {scenePath}");
    }

    private static Material MakeMaterial(Shader unlit, string name, Color colour)
    {
        var m = new Material(unlit) { name = name };
        m.SetColor("_BaseColor", colour);
        m.SetFloat("_Cull", 0f);   // both faces: whichever way a strip runs, it is seen from above
        return SceneObjectAssets.SaveMaterial(m, Folder + "/" + name + ".mat");
    }

    private static Mesh BuildMesh(float a, float b)
    {
        float y = 0f;   // placeholder: every vertex is dropped onto the surface below it (OnSurface)
        var white = new List<Vector3>();
        var edge = new List<Vector3>();
        End(white, a, Mathf.Sign(b - a), y, Crease);
        End(white, b, Mathf.Sign(a - b), y, Crease);
        float from = Mathf.Min(a, b) - PoppingOut, to = Mathf.Max(a, b) + PoppingOut;
        Strip(edge, new Vector3(from, y, PitchLines.PitchHalf), new Vector3(to, y, PitchLines.PitchHalf), Edge);
        Strip(edge, new Vector3(from, y, -PitchLines.PitchHalf), new Vector3(to, y, -PitchLines.PitchHalf), Edge);
        return Combine(white, edge);
    }

    /// One end's creases, at stumps x = `stumps`, the pitch running toward `inward` (+1 or -1);
    /// `across` is the width of the creases that run across the pitch.
    private static void End(List<Vector3> q, float stumps, float inward, float y, float across)
    {
        float popping = stumps + inward * PoppingOut;
        // Widened toward the pitch's middle, so the crease's back edge stays on its true line.
        Vector3 grow = new Vector3(inward * (across - Crease) * 0.5f, 0f, 0f);
        Strip(q, new Vector3(stumps, y, -BowlingCreaseHalf) + grow, new Vector3(stumps, y, BowlingCreaseHalf) + grow, across);
        Strip(q, new Vector3(popping, y, -PoppingHalf) + grow, new Vector3(popping, y, PoppingHalf) + grow, across);
        foreach (float side in new[] { -1f, 1f })
        {
            Strip(q, new Vector3(popping, y, side * ReturnZ), new Vector3(popping - inward * ReturnBack, y, side * ReturnZ), Crease);
            // The wide guidelines run back from the popping crease to the stumps' line, like the
            // return creases - not out into the pitch.
            Strip(q, new Vector3(popping, y, side * WideZ), new Vector3(stumps, y, side * WideZ), Crease);
        }
        // Guard marks: leg, middle and off, from the crease's back edge toward the stumps.
        float crease = popping - inward * Crease * 0.5f;
        foreach (float z in GuardZ)
            Strip(q, new Vector3(crease, y, z), new Vector3(crease - inward * GuardLength, y, z), GuardWidth);
    }

    /// A flat strip from `p` to `q`, `width` wide: four corners.
    private static void Strip(List<Vector3> corners, Vector3 p, Vector3 q, float width)
    {
        Vector3 across = Vector3.Cross(Vector3.up, (q - p).normalized) * (width * 0.5f);
        corners.Add(p - across);
        corners.Add(p + across);
        corners.Add(q + across);
        corners.Add(q - across);
    }

    private static Mesh Combine(List<Vector3> white, List<Vector3> edge)
    {
        var vertices = new List<Vector3>(white.Count + edge.Count);
        foreach (Vector3 v in white) vertices.Add(OnSurface(v, CreaseLift));
        foreach (Vector3 v in edge) vertices.Add(OnSurface(v, EdgeLift));
        var mesh = new Mesh { name = "Pitch Lines" };
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(Quads(0, white.Count), 0);
        mesh.SetTriangles(Quads(white.Count, edge.Count), 1);
        var normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// `v` dropped onto whatever is under it (the pitch, or the grass past its edge), `lift` above.
    private static Vector3 OnSurface(Vector3 v, float lift)
    {
        var from = new Vector3(v.x, 5f, v.z);
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.CompareTag("Ground") && hit.collider.GetComponent<Renderer>() != null &&
                hit.collider.GetComponent<Renderer>().enabled && hit.point.y > best)
                best = hit.point.y;
        v.y = (float.IsNegativeInfinity(best) ? 0f : best) + lift;
        return v;
    }

    private static int[] Quads(int start, int count)
    {
        var tris = new int[count / 4 * 6];
        for (int k = 0, v = start; k < tris.Length; k += 6, v += 4)
        {
            tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1;
            tris[k + 3] = v; tris[k + 4] = v + 3; tris[k + 5] = v + 2;
        }
        return tris;
    }
}
