using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the stadium crowd as stepped rows of genuinely vertical quads.
///
/// The previous CrowdLowerCone / CrowdUpperCone meshes were smooth conical bands raked back
/// at about 27 degrees, with the crowd atlas stretched across the slope. That paints every
/// spectator lying on the rake instead of sitting upright, which is the "crowd leans back"
/// problem. A texture cannot fix that - the geometry has to be vertical - so this builder
/// samples the original cone's rake profile (keeping the stand in exactly the same volume)
/// and rebuilds it as N discrete rows, each a vertical riser carrying one row of the atlas.
///
/// Per-quad animation data is baked into vertex colours for CricketVR/CrowdStand:
///   r = random phase 0..1, g = row 0..1, b = angle around the centre 0..1.
///
/// Menu: Tools/CricketVR/Rebuild Crowd Stands
/// </summary>
public static class CrowdStandBuilder
{
    private const string LowerObject = "CrowdLower";
    private const string UpperObject = "CrowdUpper";
    private const string MeshFolder = "Assets/Resources/Meshes";

    // Tuning. These are deliberately conservative; re-run the menu item after changing them.
    private const int SeatRowsLower = 20;      // rows of people up the lower tier
    private const int SeatRowsUpper = 16;
    private const int Segments = 96;           // around the circumference
    private const float MetresPerAtlasRepeat = 26f;   // ~0.5 m per person for a 50-person atlas row
    private const int AtlasRows = 14;          // rows of people in CrowdProcedural.png
    private const float RiserOverlap = 1.85f;  // >1 so rows overlap and leave no see-through gaps

    [MenuItem("Tools/CricketVR/Rebuild Crowd Stands")]
    public static void Rebuild()
    {
        var log = new System.Text.StringBuilder();
        int built = 0;

        built += BuildOne(LowerObject, "CrowdLowerRows", SeatRowsLower, log) ? 1 : 0;
        built += BuildOne(UpperObject, "CrowdUpperRows", SeatRowsUpper, log) ? 1 : 0;

        if (built > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        Debug.Log("[CrowdStandBuilder]\n" + log);
    }

    private static bool BuildOne(string objectName, string meshName, int seatRows,
                                 System.Text.StringBuilder log)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null) { log.AppendLine("SKIP " + objectName + ": not found in scene"); return false; }

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            log.AppendLine("SKIP " + objectName + ": no mesh"); return false;
        }

        // --- 1. recover the rake profile (radius, height) from whatever mesh is there now ---
        List<Vector2> profile = ExtractProfile(mf.sharedMesh);
        if (profile.Count < 2)
        {
            log.AppendLine("SKIP " + objectName + ": could not read a rake profile"); return false;
        }
        log.AppendLine(objectName + ": profile r " + profile[0].x.ToString("F1") + " -> "
                       + profile[profile.Count - 1].x.ToString("F1") + " m, y "
                       + profile[0].y.ToString("F1") + " -> " + profile[profile.Count - 1].y.ToString("F1") + " m");

        // --- 2. build stepped rows along that profile ---
        Mesh mesh = BuildSteppedRows(profile, seatRows, meshName, log);

        // --- 3. save and assign ---
        string path = MeshFolder + "/" + meshName + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            CopyInto(mesh, existing);
            EditorUtility.SetDirty(existing);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }

        Undo.RecordObject(mf, "Assign crowd mesh");
        mf.sharedMesh = mesh;
        EditorUtility.SetDirty(mf);

        log.AppendLine("  -> " + path + "  verts=" + mesh.vertexCount
                       + " tris=" + (mesh.triangles.Length / 3) + " rows=" + seatRows);
        return true;
    }

    /// Collapse a ring-based mesh into an ordered list of (radius, height) samples.
    private static List<Vector2> ExtractProfile(Mesh source)
    {
        var byHeight = new SortedDictionary<int, Vector2>();
        Vector3[] v = source.vertices;
        for (int i = 0; i < v.Length; i++)
        {
            float r = new Vector2(v[i].x, v[i].z).magnitude;
            int key = Mathf.RoundToInt(v[i].y * 100f);
            if (!byHeight.ContainsKey(key)) byHeight[key] = new Vector2(r, v[i].y);
        }
        return new List<Vector2>(byHeight.Values);
    }

    /// Sample the piecewise-linear profile at 0..1 along its length.
    private static Vector2 SampleProfile(List<Vector2> profile, float t)
    {
        float f = Mathf.Clamp01(t) * (profile.Count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), profile.Count - 2);
        return Vector2.Lerp(profile[i], profile[i + 1], f - i);
    }

    private static Mesh BuildSteppedRows(List<Vector2> profile, int seatRows, string meshName,
                                         System.Text.StringBuilder log)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var cols = new List<Color>();
        var tris = new List<int>();

        var rng = new System.Random(12345);   // deterministic, so rebuilds are reproducible

        float segStep = Mathf.PI * 2f / Segments;

        // ---- evenly spaced rows, straight rake ----------------------------------------------
        // Each row carries exactly one atlas cell, i.e. one spectator. That makes two things
        // matter equally: the RISER, which is the height the person is drawn at, and the STEP
        // between rows, which is how much of that person is left visible above the row in front.
        // Following the extracted profile per row got both wrong, because the profile recovered
        // from the source mesh is lumpy at its ends and not even monotonic in the middle:
        //
        //     CrowdLower  row 0->1  step 1.130 but radius +0.063  (a near vertical wall)
        //                 row 16->17 radius 82.75 -> 81.23        (it goes BACKWARDS)
        //     CrowdUpper  row 0->1  step 1.123 but radius +0.086  (the same wall)
        //
        // so the front two rows of each stand reared up and the lower stand's top row folded back
        // on itself. Instead: keep the stand's overall extent - the first and last row stay
        // exactly where the profile puts them - and distribute everything between them evenly,
        // with the radius running straight from bottom to top. Every row then occupies the same
        // vertical space and shows the same fraction of each spectator.
        Vector2 first = profile[0];
        Vector2 last = profile[profile.Count - 1];
        float rowStep = seatRows > 1 ? (last.y - first.y) / (seatRows - 1) : 0.5f;
        float radiusStep = seatRows > 1 ? (last.x - first.x) / (seatRows - 1) : 0f;
        float uniformRiser = Mathf.Max(0.35f, rowStep * RiserOverlap);
        log.AppendLine("  rows: y " + first.y.ToString("F2") + " -> " + last.y.ToString("F2")
                       + ", step " + rowStep.ToString("F4")
                       + " m, riser " + uniformRiser.ToString("F4")
                       + " m (" + (rowStep / uniformRiser * 100f).ToString("F0") + "% of each person visible)");
        log.AppendLine("       r " + first.x.ToString("F2") + " -> " + last.x.ToString("F2")
                       + ", step " + radiusStep.ToString("F4") + " m");

        for (int row = 0; row < seatRows; row++)
        {
            float radius = first.x + radiusStep * row;
            float baseY = first.y + rowStep * row;
            float riser = uniformRiser;

            // How many times the atlas repeats around this row, rounded so the seam closes.
            float circumference = 2f * Mathf.PI * radius;
            int repeats = Mathf.Max(1, Mathf.RoundToInt(circumference / MetresPerAtlasRepeat));

            // Which row of the atlas this seating row samples.
            int atlasRow = row % AtlasRows;
            float v0 = atlasRow / (float)AtlasRows;
            float v1 = (atlasRow + 1) / (float)AtlasRows;

            float rowNorm = seatRows > 1 ? row / (float)(seatRows - 1) : 0f;

            for (int s = 0; s < Segments; s++)
            {
                float a0 = s * segStep;
                float a1 = (s + 1) * segStep;

                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radius, baseY, Mathf.Sin(a0) * radius);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radius, baseY, Mathf.Sin(a1) * radius);
                Vector3 t0v = b0 + Vector3.up * riser;
                Vector3 t1v = b1 + Vector3.up * riser;

                float u0 = s / (float)Segments * repeats;
                float u1 = (s + 1) / (float)Segments * repeats;

                // One phase per quad so the whole quad bobs together instead of shearing.
                float phase = (float)rng.NextDouble();
                float angNorm = ((a0 + a1) * 0.5f) / (Mathf.PI * 2f);
                Color c = new Color(phase, rowNorm, angNorm, 1f);

                int baseIndex = verts.Count;
                verts.Add(b0); verts.Add(b1); verts.Add(t1v); verts.Add(t0v);
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));
                cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);

                // Wind so the face points inward, toward the pitch. Verified below.
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }

        // The stand is only ever seen from inside the ground, and the shader culls back faces,
        // so every quad's normal must point at the centre. Check the first triangle against the
        // radial direction and flip the whole buffer if it came out inside-out - getting this
        // backwards silently renders nothing at all.
        Vector3 n0 = Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]);
        Vector3 radial = new Vector3(verts[tris[0]].x, 0f, verts[tris[0]].z).normalized;
        if (Vector3.Dot(n0.normalized, -radial) < 0f)
        {
            log.AppendLine("  (winding was outward - flipped)");
            for (int i = 0; i < tris.Count; i += 3)
            {
                int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t;
            }
        }

        var mesh = new Mesh { name = meshName };
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        log.AppendLine("  rows=" + seatRows + " segments=" + Segments
                       + " quads=" + (seatRows * Segments));
        return mesh;
    }

    private static void CopyInto(Mesh from, Mesh to)
    {
        to.indexFormat = from.indexFormat;
        to.SetVertices(new List<Vector3>(from.vertices));
        to.SetUVs(0, new List<Vector2>(from.uv));
        to.SetColors(new List<Color>(from.colors));
        to.SetTriangles(from.triangles, 0);
        to.RecalculateNormals();
        to.RecalculateBounds();
        to.name = from.name;
    }
}
