using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Geometry accumulation for the club ground builder: plain lists of vertices that the builder
/// merges per material (one draw call each) and turns into a Mesh only at the end.
///
/// Nothing here touches the native engine (no Mesh, no Quaternion.Euler) until ToMesh, so every
/// piece of the ground can be built and checked by EditMode tests, and offline, without a scene.
/// Triangles are wound by the side they must face (Tri takes the facing direction and flips the
/// order to match), because a cull-back material shows nothing at all for a face wound backwards.
/// </summary>
public sealed class MeshData
{
    public readonly List<Vector3> Verts = new List<Vector3>();
    public readonly List<Vector3> Normals = new List<Vector3>();
    public readonly List<Vector2> Uvs = new List<Vector2>();
    public readonly List<Color> Colors = new List<Color>();
    public readonly List<int> Tris = new List<int>();
    /// Only the spectators carry vertex colours (CrowdStand reads phase and angle from them).
    public bool HasColors;

    public int TriangleCount => Tris.Count / 3;

    public int Vert(Vector3 p, Vector3 n, Vector2 uv) => Vert(p, n, uv, Color.white);

    public int Vert(Vector3 p, Vector3 n, Vector2 uv, Color c)
    {
        Verts.Add(p); Normals.Add(n); Uvs.Add(uv); Colors.Add(c);
        return Verts.Count - 1;
    }

    /// One triangle, wound so its front (Unity: clockwise as seen) faces `facing`.
    public void Tri(int a, int b, int c, Vector3 facing)
    {
        Vector3 g = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
        Tris.Add(a);
        if (Vector3.Dot(g, facing) < 0f) { Tris.Add(c); Tris.Add(b); }
        else { Tris.Add(b); Tris.Add(c); }
    }

    /// A quad from four corners in ring order (either way round).
    public void Quad(int a, int b, int c, int d, Vector3 facing)
    {
        Tri(a, b, c, facing);
        Tri(a, c, d, facing);
    }

    public void Append(MeshData o)
    {
        int b = Verts.Count;
        Verts.AddRange(o.Verts); Normals.AddRange(o.Normals); Uvs.AddRange(o.Uvs); Colors.AddRange(o.Colors);
        foreach (int i in o.Tris) Tris.Add(b + i);
        HasColors |= o.HasColors;
    }

    public void MinMax(out Vector3 min, out Vector3 max)
    {
        min = Vector3.one * float.MaxValue; max = Vector3.one * float.MinValue;
        foreach (Vector3 v in Verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
    }

    /// The engine mesh: normals as built (foliage carries rounded ones on purpose), tangents for
    /// any later normal map, 32-bit indices only when a merged batch needs them.
    public Mesh ToMesh(string name) => Fill(new Mesh { name = name });

    /// Writes this geometry into `m` (cleared first), so a rebuild keeps the saved mesh asset and
    /// with it every reference the prefab holds.
    public Mesh Fill(Mesh m)
    {
        m.Clear();
        m.indexFormat = Verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertices(Verts);
        m.SetNormals(Normals);
        m.SetUVs(0, Uvs);
        if (HasColors) m.SetColors(Colors);
        m.SetTriangles(Tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }
}

/// <summary>A placement: origin plus an orthonormal basis (right, up, forward), all world space.</summary>
public struct Frame
{
    public Vector3 o, r, u, f;

    public static Frame World => new Frame { o = Vector3.zero, r = Vector3.right, u = Vector3.up, f = Vector3.forward };

    /// Turned `yawDeg` about +Y the way Transform does it: forward (sin, 0, cos), right (cos, 0, -sin).
    public static Frame Yaw(Vector3 origin, float yawDeg)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return new Frame
        {
            o = origin, u = Vector3.up,
            f = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)),
            r = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)),
        };
    }

    /// Forward along `forward` (any direction but straight up or down), up as near world up as it can be.
    public static Frame Facing(Vector3 origin, Vector3 forward, Vector3 upHint)
    {
        Vector3 fw = forward.normalized;
        Vector3 right = Vector3.Cross(upHint, fw);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.right, fw);
        right.Normalize();
        return new Frame { o = origin, f = fw, r = right, u = Vector3.Cross(fw, right) };
    }

    /// Yaw (degrees) whose forward points from `from` toward `to` in the ground plane.
    public static float YawToward(Vector3 from, Vector3 to) =>
        Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

    public Vector3 P(float x, float y, float z) => o + r * x + u * y + f * z;
    public Vector3 P(Vector3 l) => o + r * l.x + u * l.y + f * l.z;
    public Vector3 D(Vector3 l) => r * l.x + u * l.y + f * l.z;

    /// A frame inside this one: at local `offset`, its forward and up given in this frame's terms.
    public Frame Child(Vector3 offset, Vector3 localForward, Vector3 localUp)
    {
        Frame c = Facing(P(offset), D(localForward), D(localUp));
        return c;
    }

    public Frame Child(Vector3 offset, float yawDeg)
    {
        float a = yawDeg * Mathf.Deg2Rad;
        return Child(offset, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
    }

    public float YawDeg => Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
}

/// <summary>The primitive shapes, each textured from one atlas cell (see ClubAtlas).</summary>
public static class ClubMeshKit
{
    /// A flat convex polygon. `poly` is in metres along local axes `s` (right, seen from the front)
    /// and `t` (up) through local `origin`; its face points along Cross(t, s). A tiled cell is laid
    /// out from the polygon's lower-left corner and the polygon is cut at every tile edge, since an
    /// atlas cell cannot wrap: a 22 m wall gets eleven 2 m tiles, each mapping the whole cell.
    public static void Poly(MeshData md, Frame fr, Vector3 origin, Vector3 s, Vector3 t, IList<Vector2> poly, ClubCell cell)
    {
        Vector3 n = fr.D(Vector3.Cross(t, s)).normalized;
        Rect uv = ClubAtlas.UV(cell);
        Vector2 min = poly[0], max = poly[0];
        foreach (Vector2 p in poly) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
        ClubCellMode mode = ClubAtlas.ModeOf(cell);
        if (mode != ClubCellMode.Tiled)
        {
            Vector2 size = Vector2.Max(max - min, Vector2.one * 1e-4f);
            Fan(md, fr, origin, s, t, new List<Vector2>(poly), n, p => mode == ClubCellMode.Solid
                ? uv.center
                : uv.min + Vector2.Scale(new Vector2((p.x - min.x) / size.x, (p.y - min.y) / size.y), uv.size));
            return;
        }
        Vector2 tile = ClubAtlas.Tile(cell);
        int nx = Mathf.Max(1, Mathf.CeilToInt((max.x - min.x) / tile.x - 1e-3f));
        int ny = Mathf.Max(1, Mathf.CeilToInt((max.y - min.y) / tile.y - 1e-3f));
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                Vector2 lo = min + new Vector2(i * tile.x, j * tile.y);
                Vector2 hi = lo + tile;
                List<Vector2> piece = Clip(new List<Vector2>(poly), lo, hi);
                if (piece.Count < 3) continue;
                Fan(md, fr, origin, s, t, piece, n, p =>
                    uv.min + Vector2.Scale(new Vector2((p.x - lo.x) / tile.x, (p.y - lo.y) / tile.y), uv.size));
            }
    }

    static void Fan(MeshData md, Frame fr, Vector3 origin, Vector3 s, Vector3 t, List<Vector2> pts, Vector3 n,
                    System.Func<Vector2, Vector2> uvOf)
    {
        int first = md.Verts.Count;
        foreach (Vector2 p in pts)
            md.Vert(fr.P(origin + s * p.x + t * p.y), n, uvOf(p));
        for (int k = 1; k + 1 < pts.Count; k++)
            md.Tri(first, first + k, first + k + 1, n);
    }

    /// Sutherland-Hodgman: the part of a convex polygon inside the box lo..hi.
    public static List<Vector2> Clip(List<Vector2> pts, Vector2 lo, Vector2 hi)
    {
        pts = ClipEdge(pts, 0, lo.x, true);
        pts = ClipEdge(pts, 0, hi.x, false);
        pts = ClipEdge(pts, 1, lo.y, true);
        return ClipEdge(pts, 1, hi.y, false);
    }

    static List<Vector2> ClipEdge(List<Vector2> pts, int axis, float bound, bool keepAbove)
    {
        var res = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
            bool ina = keepAbove ? a[axis] >= bound - 1e-5f : a[axis] <= bound + 1e-5f;
            bool inb = keepAbove ? b[axis] >= bound - 1e-5f : b[axis] <= bound + 1e-5f;
            if (ina) res.Add(a);
            if (ina != inb)
            {
                float k = (bound - a[axis]) / (b[axis] - a[axis]);
                res.Add(Vector2.Lerp(a, b, k));
            }
        }
        return res;
    }

    /// An axis-aligned rectangle s0..s1 x t0..t1 on a plane (see Poly).
    public static void Panel(MeshData md, Frame fr, Vector3 origin, Vector3 s, Vector3 t,
                            float s0, float s1, float t0, float t1, ClubCell cell)
    {
        Poly(md, fr, origin, s, t, new[] { new Vector2(s0, t0), new Vector2(s1, t0), new Vector2(s1, t1), new Vector2(s0, t1) }, cell);
    }

    /// A box centred at local `c`: `side` on the four walls, `top` on the lid, the underside only if asked.
    public static void Box(MeshData md, Frame fr, Vector3 c, Vector3 size, ClubCell side, ClubCell top, bool bottom = false)
    {
        Vector3 h = size * 0.5f;
        Face(md, fr, c + Vector3.forward * h.z, Vector3.left, Vector3.up, h.x, h.y, side);
        Face(md, fr, c + Vector3.back * h.z, Vector3.right, Vector3.up, h.x, h.y, side);
        Face(md, fr, c + Vector3.right * h.x, Vector3.forward, Vector3.up, h.z, h.y, side);
        Face(md, fr, c + Vector3.left * h.x, Vector3.back, Vector3.up, h.z, h.y, side);
        Face(md, fr, c + Vector3.up * h.y, Vector3.right, Vector3.forward, h.x, h.z, top);
        if (bottom) Face(md, fr, c + Vector3.down * h.y, Vector3.left, Vector3.forward, h.x, h.z, top);
    }

    public static void Box(MeshData md, Frame fr, Vector3 c, Vector3 size, ClubCell cell) => Box(md, fr, c, size, cell, cell);

    static void Face(MeshData md, Frame fr, Vector3 centre, Vector3 s, Vector3 t, float a, float b, ClubCell cell) =>
        Panel(md, fr, centre, s, t, -a, a, -b, b, cell);

    /// A square beam from local `a` to local `b`, `thick` on a side (struts, rails, handles).
    public static void Beam(MeshData md, Frame fr, Vector3 a, Vector3 b, float thick, ClubCell cell)
    {
        Vector3 d = fr.D(b - a);
        float len = d.magnitude;
        if (len < 1e-4f) return;
        Vector3 hint = Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        Frame bf = Frame.Facing(fr.P(a), d, hint);
        Box(md, bf, new Vector3(0f, 0f, len * 0.5f), new Vector3(thick, thick, len), cell, cell, true);
    }

    /// A (tapered) cylinder from local `a` (radius ra) to local `b` (radius rb). Tiled cells are
    /// split into tile-length rings; the seam is duplicated so the cell wraps once around.
    public static void Cylinder(MeshData md, Frame fr, Vector3 a, Vector3 b, float ra, float rb, int sides,
                                ClubCell cell, bool capA = false, bool capB = true)
    {
        Vector3 wa = fr.P(a), wb = fr.P(b), ax = (wb - wa);
        float len = ax.magnitude;
        ax /= len;
        Vector3 p = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
        Vector3 q = Vector3.Cross(ax, p);
        Rect uv = ClubAtlas.UV(cell);
        ClubCellMode mode = ClubAtlas.ModeOf(cell);
        float tileLen = mode == ClubCellMode.Tiled ? ClubAtlas.Tile(cell).y : len;
        int segs = Mathf.Max(1, Mathf.CeilToInt(len / tileLen - 1e-3f));
        float slope = (ra - rb) / len;
        for (int k = 0; k < segs; k++)
        {
            float z0 = k * tileLen, z1 = Mathf.Min(len, z0 + tileLen);
            int ring0 = md.Verts.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                float z = ring == 0 ? z0 : z1;
                float rad = Mathf.Lerp(ra, rb, z / len);
                float v = mode == ClubCellMode.Solid ? uv.center.y : uv.yMin + uv.height * (z - z0) / tileLen;
                for (int j = 0; j <= sides; j++)
                {
                    float ang = j * Mathf.PI * 2f / sides;
                    Vector3 radial = p * Mathf.Cos(ang) + q * Mathf.Sin(ang);
                    float u = mode == ClubCellMode.Solid ? uv.center.x : uv.xMin + uv.width * j / sides;
                    md.Vert(wa + ax * z + radial * rad, (radial + ax * slope).normalized, new Vector2(u, v));
                }
            }
            for (int j = 0; j < sides; j++)
            {
                int i0 = ring0 + j, i1 = ring0 + j + 1, i2 = ring0 + sides + 1 + j + 1, i3 = ring0 + sides + 1 + j;
                Vector3 out0 = md.Normals[i0] + md.Normals[i1];
                md.Quad(i0, i1, i2, i3, out0);
            }
        }
        if (capA) Cap(md, wa, -ax, p, q, ra, sides, cell);
        if (capB) Cap(md, wb, ax, p, q, rb, sides, cell);
    }

    static void Cap(MeshData md, Vector3 c, Vector3 n, Vector3 p, Vector3 q, float r, int sides, ClubCell cell)
    {
        Vector2 uv = ClubAtlas.UV(cell).center;
        int centre = md.Vert(c, n, uv);
        int first = md.Verts.Count;
        for (int j = 0; j < sides; j++)
        {
            float ang = j * Mathf.PI * 2f / sides;
            md.Vert(c + (p * Mathf.Cos(ang) + q * Mathf.Sin(ang)) * r, n, uv);
        }
        for (int j = 0; j < sides; j++)
            md.Tri(centre, first + j, first + (j + 1) % sides, n);
    }

    /// A flat ring (r0 = 0: a disc) at height y facing up, `rings` bands deep, UVs from world xz.
    public static void Annulus(MeshData md, float y, float r0, float r1, int segments, int rings,
                               System.Func<Vector3, Vector2> uvOf)
    {
        int bands = Mathf.Max(1, rings);
        int first = md.Verts.Count;
        for (int k = 0; k <= bands; k++)
        {
            float rad = Mathf.Lerp(r0, r1, k / (float)bands);
            for (int j = 0; j < segments; j++)
            {
                float a = j * Mathf.PI * 2f / segments;
                Vector3 v = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                md.Vert(v, Vector3.up, uvOf(v));
            }
        }
        for (int k = 0; k < bands; k++)
            for (int j = 0; j < segments; j++)
            {
                int a0 = first + k * segments + j, a1 = first + k * segments + (j + 1) % segments;
                int b0 = a0 + segments, b1 = a1 + segments;
                if (k == 0 && r0 <= 0f) md.Tri(a0, b1, b0, Vector3.up);   // the centre ring is one point
                else md.Quad(a0, a1, b1, b0, Vector3.up);
            }
    }

    /// An upright ring of wall, radius `radius`, y0..y1, its face toward the centre (`inward`) or
    /// out; `arc0..arc1` radians (anticlockwise from +X seen from above). U runs in metres of arc
    /// over `uRepeatMetres`, so a wrapping texture repeats along it; V runs 0..1 bottom to top.
    /// `lift` tips the normals up (1 = 45 degrees): a two-sided band of pickets lit by its true,
    /// sideways normal would go dark on whichever half of the ring has the sun behind it.
    public static void Band(MeshData md, float radius, float y0, float y1, float arc0, float arc1,
                            int segments, float uRepeatMetres, bool inward, float lift = 0f)
    {
        int first = md.Verts.Count;
        for (int j = 0; j <= segments; j++)
        {
            float a = Mathf.Lerp(arc0, arc1, j / (float)segments);
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 n = ((inward ? -radial : radial) + Vector3.up * lift).normalized;
            float u = (a - arc0) * radius / uRepeatMetres;
            md.Vert(radial * radius + Vector3.up * y0, n, new Vector2(u, 0f));
            md.Vert(radial * radius + Vector3.up * y1, n, new Vector2(u, 1f));
        }
        for (int j = 0; j < segments; j++)
        {
            int b = first + j * 2;
            md.Quad(b, b + 2, b + 3, b + 1, md.Normals[b]);
        }
    }

    /// A thin tube lying round a circle (the boundary rope): `sides` round, `segments` along.
    public static void TubeRing(MeshData md, float radius, float y, float tube, int sides, int segments, ClubCell cell)
    {
        Vector2 uv = ClubAtlas.UV(cell).center;
        int first = md.Verts.Count;
        for (int j = 0; j < segments; j++)
        {
            float a = j * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int k = 0; k < sides; k++)
            {
                float b = k * Mathf.PI * 2f / sides + Mathf.PI * 0.5f;
                Vector3 n = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                md.Vert(radial * radius + Vector3.up * y + n * tube, n, uv);
            }
        }
        for (int j = 0; j < segments; j++)
            for (int k = 0; k < sides; k++)
            {
                int a0 = first + j * sides + k, a1 = first + j * sides + (k + 1) % sides;
                int b0 = first + ((j + 1) % segments) * sides + k, b1 = first + ((j + 1) % segments) * sides + (k + 1) % sides;
                md.Quad(a0, a1, b1, b0, md.Normals[a0] + md.Normals[a1]);
            }
    }

    /// A camera-agnostic foliage cluster: `planes` upright quads crossing at `centre`, mapping one
    /// cell each. Normals point away from `roundFrom` (the crown's middle), so the cluster shades
    /// like a ball of leaves from every side instead of flipping with each plane.
    public static void Crossed(MeshData md, Vector3 centre, float w, float h, int planes, float yawDeg,
                               Rect uv, Vector3 roundFrom)
    {
        for (int k = 0; k < planes; k++)
        {
            float a = (yawDeg + k * 180f / planes) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (w * 0.5f);
            Vector3 up = Vector3.up * (h * 0.5f);
            Vector3[] c = { centre - d - up, centre + d - up, centre + d + up, centre - d + up };
            Vector2[] t = { uv.min, new Vector2(uv.xMax, uv.yMin), uv.max, new Vector2(uv.xMin, uv.yMax) };
            int first = md.Verts.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 n = (c[i] - roundFrom + Vector3.up * (h * 0.25f)).normalized;
                md.Vert(c[i], n, t[i]);
            }
            md.Quad(first, first + 1, first + 2, first + 3, Vector3.Cross(Vector3.up, d));
        }
    }

    /// An upright picture quad standing on `bottom`, facing `facing`, not mirrored (u runs to the
    /// viewer's right). Used for the seated spectators.
    public static void Billboard(MeshData md, Vector3 bottom, Vector3 facing, float w, float h, Rect uv, Color c)
    {
        Vector3 fw = new Vector3(facing.x, 0f, facing.z).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, -fw) * (w * 0.5f);
        Vector3 up = Vector3.up * h;
        int a = md.Vert(bottom - right, fw, uv.min, c);
        int b = md.Vert(bottom + right, fw, new Vector2(uv.xMax, uv.yMin), c);
        int d = md.Vert(bottom + right + up, fw, uv.max, c);
        int e = md.Vert(bottom - right + up, fw, new Vector2(uv.xMin, uv.yMax), c);
        md.Quad(a, b, d, e, fw);
    }

    /// A regular polygon on a plane (clock faces, marker discs), see Poly.
    public static void NGon(MeshData md, Frame fr, Vector3 origin, Vector3 s, Vector3 t, float radius, int n, ClubCell cell)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        Poly(md, fr, origin, s, t, pts, cell);
    }
}
