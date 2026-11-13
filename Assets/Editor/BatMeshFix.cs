using UnityEditor;
using UnityEngine;

/// <summary>
/// The bat model's toe guard comes in partly inside out: about half its faces (and their normals)
/// point into the bat, so from those sides the guard is culled and you see through the toe
/// (2026-11-09: "the toe of the bat is hollow. I can see through the bat"). Measured on
/// Models/Bat.fbx: submesh 3 (Bat_004, the blue guard) 100 of 193 faces facing in, every other
/// submesh facing out; the mesh has no open edges at the toe, only faces turned the wrong way.
///
/// On import, every face in the bottom ToeSpan of the blade that faces toward a point inside the
/// toe is turned round (its winding), and so is any corner normal there pointing in. Faces already facing out are
/// untouched, so the rest of the bat, and a re-exported model that is already right, come through
/// as they were. The model's length is local Z with the toe at the lowest Z (BatGeometry).
/// </summary>
public class BatMeshFix : AssetPostprocessor
{
    private const string BatModel = "Assets/Resources/Models/Bat.fbx";
    /// How far up from the toe the faces are checked (local units), and the point inside the toe
    /// they should face away from: this far up the blade, on its middle line.
    private const float ToeSpan = 0.08f, InsideUp = 0.12f;

    private void OnPostprocessModel(GameObject model)
    {
        if (assetPath != BatModel)
            return;
        foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null)
                Debug.Log($"[BatMeshFix] {mf.sharedMesh.name}: {Fix(mf.sharedMesh)} toe faces turned outward");
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
