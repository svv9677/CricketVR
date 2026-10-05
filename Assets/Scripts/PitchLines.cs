using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The pitch's white lines as geometry, built when the match scene loads, in place of the
/// markings texture. That texture stretched 2048 x 318 pixels over the whole 23.5 m pitch, with
/// bilinear filtering and no anisotropy: from the crease, looking down the pitch at a low angle,
/// the far end's creases blurred into the turf and the near ones went soft and blocky. Geometry
/// stays a sharp edge at any distance and angle (the eye buffers' 4x MSAA smooths it).
///
/// Laid out from the stumps, to the Laws: at each end the bowling crease through the stumps
/// (2.64 m), the popping crease 1.22 m in front of it (across the whole marked width), the return
/// creases 1.32 m either side of the middle stump, and the wide guidelines (0.89 m). Plus the
/// edges of the pitch itself, 1.525 m either side (a 3.05 m pitch), softer than the creases, so
/// the width of the pitch reads at a glance. One mesh, one unlit material: one draw call.
/// </summary>
public static class PitchLines
{
    private const string MatchScene = "CricketVR";
    private const float Crease = 0.06f, Edge = 0.03f;
    /// Above the surface under them: creases over the edge lines where they cross, both just
    /// clear of the turf so they lie on it rather than float (they sat 2.7 cm up before).
    private const float CreaseLift = 0.004f, EdgeLift = 0.002f;
    /// The far end's creases across the pitch, seen from the batter's end. A line across the turf
    /// 20 m away, from eye height, is foreshortened ~12 times: a 6 cm crease drew under a third of
    /// a pixel and vanished, and 0.2 m was still about one pixel on a Quest 2. 0.3 m reads as a line
    /// there; return creases, seen along their length, need no help.
    private const float FarCrease = 0.3f;
    private const float BowlingCreaseHalf = 1.32f, PoppingHalf = 1.83f, PoppingOut = 1.22f;
    private const float ReturnZ = 1.32f, ReturnBack = 2.44f, WideZ = 0.89f, PitchHalf = 1.525f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != MatchScene || GameObject.Find("Pitch Lines") != null)
            return;
        GameObject battingEnd = GameObject.Find("Main/Stumps_BattingEnd");
        GameObject bowlingEnd = GameObject.Find("Main/Stumps_BowlingEnd");
        GameObject markings = GameObject.Find("Main/Pitch/Pitch Markings");
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (battingEnd == null || bowlingEnd == null || unlit == null)
            return;
        // The texture's lines are replaced, not doubled - first, so the lines below are laid on the
        // pitch, not on that texture's plane 1.5 cm above it. Its collider stays.
        if (markings != null && markings.TryGetComponent(out MeshRenderer old))
            old.enabled = false;
        float y = 0f;   // placeholder: every vertex is dropped onto the surface below it (OnSurface)
        float a = battingEnd.transform.position.x, b = bowlingEnd.transform.position.x;

        var white = new List<Vector3>();
        var edge = new List<Vector3>();
        End(white, a, Mathf.Sign(b - a), y, Crease);
        End(white, b, Mathf.Sign(a - b), y, FarCrease);
        float from = Mathf.Min(a, b) - PoppingOut, to = Mathf.Max(a, b) + PoppingOut;
        Strip(edge, new Vector3(from, y, PitchHalf), new Vector3(to, y, PitchHalf), Edge);
        Strip(edge, new Vector3(from, y, -PitchHalf), new Vector3(to, y, -PitchHalf), Edge);

        var go = new GameObject("Pitch Lines", typeof(MeshFilter), typeof(MeshRenderer));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.GetComponent<MeshFilter>().sharedMesh = Combine(white, edge);
        var lines = new Material(unlit) { name = "Pitch Lines" };
        lines.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.94f));
        var edges = new Material(unlit) { name = "Pitch Edges" };
        edges.SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.7f));
        lines.SetFloat("_Cull", 0f);   // both faces: whichever way a strip runs, it is seen from above
        edges.SetFloat("_Cull", 0f);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { lines, edges };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
