using UnityEditor;
using UnityEngine;

/// <summary>
/// The bat model's toe guard comes in partly inside out: about half its faces (and their normals)
/// point into the bat, so from those sides the guard is culled and you see through the toe
/// (2026-11-09: "the toe of the bat is hollow. I can see through the bat"). Measured on
/// Models/Bat.fbx: submesh 3 (Bat_004, the blue guard) 100 of 193 faces facing in, every other
/// submesh facing out; the mesh has no open edges at the toe, only faces turned the wrong way.
///
/// The fixed mesh is a saved asset, <see cref="FixedMesh"/>, and that is what Bat.prefab and
/// ShotReplay.prefab show: the editor shows what plays. Run the menu command again only if Bat.fbx
/// changes. (Until 2026-11-15 this was an import hook that changed the FBX's mesh as it was imported.)
///
/// Every face in the bottom ToeSpan of the blade that faces toward a point inside the toe is turned
/// round (its winding), and so is any corner normal there pointing in. Faces already facing out are
/// untouched, so the rest of the bat, and a re-exported model that is already right, come through
/// as they were. The model's length is local Z with the toe at the lowest Z (BatGeometry).
/// </summary>
public static class BatMeshFix
{
    private const string BatModel = "Assets/Resources/Models/Bat.fbx";
    public const string FixedMesh = "Assets/Resources/Models/BatFixed.asset";
    /// How far up from the toe the faces are checked (local units), and the point inside the toe
    /// they should face away from: this far up the blade, on its middle line.
    private const float ToeSpan = 0.08f, InsideUp = 0.12f;

    [MenuItem("CricketVR/Models/Bake Fixed Bat Mesh")]
    public static void Bake()
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(BatModel);
        if (source == null)
        {
            Debug.LogError($"[BatMeshFix] no mesh in {BatModel}");
            return;
        }
        Mesh fixedMesh = Object.Instantiate(source);
        fixedMesh.name = source.name;
        int turned = Fix(fixedMesh);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(FixedMesh);
        if (existing != null)
        {
            EditorUtility.CopySerialized(fixedMesh, existing);    // same asset, so references stay
            Object.DestroyImmediate(fixedMesh);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(fixedMesh, FixedMesh);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BatMeshFix] {FixedMesh}: {turned} toe faces turned outward");
    }

    /// Turns the toe's inward faces outward; returns how many.
    public static int Fix(Mesh mesh)
    {
        Vector3[] v = mesh.vertices;
        Vector3[] n = mesh.normals;
        Bounds b = mesh.bounds;
        float toe = b.min.z;
        var inside = new Vector3(b.center.x, b.center.y, toe + InsideUp);
        int turned = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] t = mesh.GetTriangles(s);
            bool changed = false;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], c1 = v[t[i + 1]], c2 = v[t[i + 2]];
                Vector3 centre = (a + c1 + c2) / 3f;
                if (centre.z > toe + ToeSpan)
                    continue;
                Vector3 face = Vector3.Cross(c1 - a, c2 - a);
                if (Vector3.Dot(face, centre - inside) >= 0f)
                    continue;
                (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                changed = true;
                turned++;
            }
            if (changed)
                mesh.SetTriangles(t, s);
        }
        // A corner's normal by where it points, not by its faces (a corner can be shared by a face
        // that was right and one that was not).
        if (turned > 0 && n.Length == v.Length)
        {
            for (int i = 0; i < v.Length; i++)
                if (v[i].z <= toe + ToeSpan && Vector3.Dot(n[i], v[i] - inside) < 0f)
                    n[i] = -n[i];
            mesh.normals = n;
            mesh.RecalculateTangents();
        }
        return turned;
    }
}
