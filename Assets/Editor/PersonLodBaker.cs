using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the far versions of the people's meshes (PersonLod) into Resources/PersonLodTable.asset.
///
/// Vertex clustering: the mesh is cut into Cell-sized cubes and every vertex in a cube (with the
/// same facing and the same patch of texture) becomes one of them near their middle (see IsSkin), with that
/// vertex's own normal, UV and bone weights - so it bends with the same bones and nothing is
/// averaged into a colour or a weight the model never had. Triangles left with two corners in one
/// cluster go. Facing is kept apart so the two sides of a thin sleeve are not merged into one dark
/// sheet, and the texture patch so a UV seam does not smear across.
///
/// Two of them, each cell under 0.1 degrees where it starts (about two of the Quest 2's pixels):
/// 2 cm from PersonLod.MidBeyond (12 m), 4 cm from PersonLod.FarBeyond (25 m). A fielder's
/// David.fbx, 46.9k triangles: 19.2k and 8.6k (2026-11-11).
///
/// Menu: Tools/CricketVR/Bake Person LODs. Re-run it after changing a person model.
/// </summary>
public static class PersonLodBaker
{
    public const string TablePath = "Assets/Resources/" + PersonLodTable.ResourceName + ".asset";
    public const float MidCell = 0.02f, FarCell = 0.04f;
    /// Meshes smaller than this are left as they are (eyelashes, socks): little to gain.
    public const int MinVertices = 1500;
    /// Texture patches kept apart: the UV square cut into this many per side.
    private const int UvCells = 8;

    public static readonly string[] Models =
    {
        "Assets/Resources/Models/David.fbx",                // fielders, bowler's look, umpire, team-mates
        "Assets/Resources/Models/Bowler.fbx",
        "Assets/Resources/Models/Animated Fielder.fbx",     // the keeper
    };

    [MenuItem("Tools/CricketVR/Bake Person LODs")]
    public static void Bake()
    {
        if (AssetDatabase.LoadAssetAtPath<PersonLodTable>(TablePath) != null)
            AssetDatabase.DeleteAsset(TablePath);
        var table = ScriptableObject.CreateInstance<PersonLodTable>();
        AssetDatabase.CreateAsset(table, TablePath);
        var full = new List<Mesh>();
        var mid = new List<Mesh>();
        var far = new List<Mesh>();
        var log = new System.Text.StringBuilder("[PersonLodBaker] ");
        foreach (string path in Models)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is Mesh source) || source.vertexCount < MinVertices || source.bindposes.Length == 0)
                    continue;
                Mesh m = Simplify(source, MidCell, " (mid)");
                Mesh f = Simplify(source, FarCell, " (far)");
                AssetDatabase.AddObjectToAsset(m, table);
                AssetDatabase.AddObjectToAsset(f, table);
                full.Add(source);
                mid.Add(m);
                far.Add(f);
                log.Append($"{source.name} {Triangles(source)}/{Triangles(m)}/{Triangles(f)}; ");
            }
        table.full = full.ToArray();
        table.mid = mid.ToArray();
        table.far = far.ToArray();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    public static long Triangles(Mesh m)
    {
        long t = 0;
        for (int s = 0; s < m.subMeshCount; s++)
            t += m.GetIndexCount(s) / 3;
        return t;
    }

    /// The body under the clothes. Simplified separately, the two layers crossed: skin showed
    /// through a sleeve in flecks. So the body keeps the inner of its clustered points and
    /// clothes and hair the outer, and each layer can only move away from the other.
    public static bool IsSkin(Mesh m) => m.name.Contains("Body") || m.name.StartsWith("base.");

    /// A lighter copy of a skinned mesh: vertices clustered into `cell`-sized cubes (see above).
    public static Mesh Simplify(Mesh source, float cell, string suffix = " (light)")
    {
        Mesh m = Object.Instantiate(source);     // an editor copy is readable whatever the import says
        try
        {
            int[] rep = Representatives(m.vertices, m.normals, m.uv, cell, out int[] kept, IsSkin(source) ? -1f : 1f);
            Mesh light = Build(m, source, rep, kept);
            light.name = source.name + suffix;
            return light;
        }
        finally
        {
            Object.DestroyImmediate(m);
        }
    }

    /// For each vertex, the vertex standing in for it; `kept` lists the stand-ins. `lean` +1 picks
    /// them toward the outside of the surface, -1 the inside, 0 the middle.
    public static int[] Representatives(Vector3[] p, Vector3[] n, Vector2[] uv, float cell, out int[] kept, float lean = 0f)
    {
        var cluster = new Dictionary<long, int>();
        var of = new int[p.Length];
        var sum = new List<Vector3>();
        for (int i = 0; i < p.Length; i++)
        {
            long key = Key(p[i], n.Length == p.Length ? n[i] : Vector3.up, uv.Length == p.Length ? uv[i] : Vector2.zero, cell);
            if (!cluster.TryGetValue(key, out int c))
            {
                c = sum.Count;
                cluster[key] = c;
                sum.Add(Vector3.zero);
            }
            of[i] = c;
            sum[c] += p[i];
        }
        var size = new int[sum.Count];
        var facing = new Vector3[sum.Count];
        for (int i = 0; i < p.Length; i++)
        {
            size[of[i]]++;
            if (n.Length == p.Length)
                facing[of[i]] += n[i];
        }
        var best = new int[sum.Count];
        var bestD = new float[sum.Count];
        for (int c = 0; c < best.Length; c++) { best[c] = -1; bestD[c] = float.MaxValue; }
        for (int i = 0; i < p.Length; i++)
        {
            int c = of[i];
            Vector3 off = p[i] - sum[c] / size[c];
            // Near the middle, and (by `lean`) toward the outside or the inside of the surface.
            float d = off.magnitude - 2f * lean * Vector3.Dot(off, facing[c].normalized);
            if (d < bestD[c]) { bestD[c] = d; best[c] = i; }
        }
        var rep = new int[p.Length];
        for (int i = 0; i < p.Length; i++)
            rep[i] = best[of[i]];
        kept = best;
        return rep;
    }

    private static long Key(Vector3 p, Vector3 n, Vector2 uv, float cell)
    {
        long x = Mathf.FloorToInt(p.x / cell) & 0xFFFF, y = Mathf.FloorToInt(p.y / cell) & 0xFFFF, z = Mathf.FloorToInt(p.z / cell) & 0xFFFF;
        Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
        int axis = a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        long facing = axis * 2 + (n[axis] < 0f ? 1 : 0);
        long u = Mathf.FloorToInt(Mathf.Repeat(uv.x, 1f) * UvCells) & 0xF, v = Mathf.FloorToInt(Mathf.Repeat(uv.y, 1f) * UvCells) & 0xF;
        return x | (y << 16) | (z << 32) | (facing << 48) | (u << 51) | (v << 55);
    }

    private static Mesh Build(Mesh m, Mesh source, int[] rep, int[] kept)
    {
        var newIndex = new Dictionary<int, int>(kept.Length);
        for (int i = 0; i < kept.Length; i++)
            newIndex[kept[i]] = i;
        var light = new Mesh { name = source.name + " (far)" };
        light.indexFormat = kept.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        light.vertices = Pick(m.vertices, kept);
        if (m.normals.Length > 0) light.normals = Pick(m.normals, kept);
        if (m.tangents.Length > 0) light.tangents = Pick(m.tangents, kept);
        if (m.uv.Length > 0) light.uv = Pick(m.uv, kept);
        if (m.colors.Length > 0) light.colors = Pick(m.colors, kept);
        CopyWeights(m, light, kept);
        light.bindposes = m.bindposes;
        light.subMeshCount = m.subMeshCount;
        for (int s = 0; s < m.subMeshCount; s++)
            light.SetTriangles(Remap(m.GetTriangles(s), rep, newIndex), s, false);
        light.bounds = source.bounds;
        return light;
    }

    private static T[] Pick<T>(T[] all, int[] kept)
    {
        var o = new T[kept.Length];
        for (int i = 0; i < kept.Length; i++)
            o[i] = all[kept[i]];
        return o;
    }

    private static void CopyWeights(Mesh m, Mesh light, int[] kept)
    {
        NativeArray<byte> per = m.GetBonesPerVertex();
        NativeArray<BoneWeight1> all = m.GetAllBoneWeights();
        if (per.Length == 0)
            return;
        var start = new int[per.Length];
        for (int i = 1; i < per.Length; i++)
            start[i] = start[i - 1] + per[i - 1];
        var outPer = new NativeArray<byte>(kept.Length, Allocator.Temp);
        var outAll = new List<BoneWeight1>();
        for (int i = 0; i < kept.Length; i++)
        {
            int v = kept[i];
            outPer[i] = per[v];
            for (int k = 0; k < per[v]; k++)
                outAll.Add(all[start[v] + k]);
        }
        var weights = new NativeArray<BoneWeight1>(outAll.ToArray(), Allocator.Temp);
        light.SetBoneWeights(outPer, weights);
        outPer.Dispose();
        weights.Dispose();
    }

    /// The triangles through the stand-ins, less any squashed flat or repeated.
    public static int[] Remap(int[] tris, int[] rep, Dictionary<int, int> newIndex)
    {
        var o = new List<int>(tris.Length / 3);
        var seen = new HashSet<(int, int, int)>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = newIndex[rep[tris[i]]], b = newIndex[rep[tris[i + 1]]], c = newIndex[rep[tris[i + 2]]];
            if (a == b || b == c || a == c)
                continue;
            // The same triangle in the same turn, however its corners are listed.
            var t = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
            if (!seen.Add(t))
                continue;
            o.Add(a); o.Add(b); o.Add(c);
        }
        return o.ToArray();
    }
}
