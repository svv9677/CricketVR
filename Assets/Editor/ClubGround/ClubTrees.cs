using System.Collections.Generic;
using UnityEngine;
using static ClubMeshKit;

/// <summary>
/// The trees round the ground and the people on its benches: the two alpha-cut things.
///
/// Trees stand where ClubGroundLayout.Trees(seed) puts them (outside the fence, clear of the
/// buildings and the bowler's-arm wedge). Each is a bark trunk plus clusters of crossed quads,
/// each quad one foliage cell of the club atlas, all in a single batch on the cutout material:
/// a broadleaf is a domed crown of eight clusters, a poplar a narrow spire of six. Nothing moves
/// in the wind (no vertex animation to pay for on a Quest). About 90 triangles a tree.
///
/// Spectators are one quad per seat, a seated figure from the stadium crowd atlas on the
/// CrowdStand shader, split into three groups so ClubGround.ShowCrowd can fill the benches for an
/// empty, half or packed crowd.
/// </summary>
public static class ClubTrees
{
    static readonly ClubCell[] Broadleaf = { ClubCell.FoliageA, ClubCell.FoliageB, ClubCell.FoliageC };
    static readonly ClubCell[] Poplar = { ClubCell.FoliageC, ClubCell.FoliageD };

    /// Returns how many trees it planted. (The spot type is left to `var`: the builder reads only
    /// its pos, scale and kind, 0 broadleaf, 1 poplar.)
    public static int Trees(MeshData md, float g, int seed)
    {
        int i = 0, planted = 0;
        foreach (var t in ClubGroundLayout.Trees(seed))
        {
            var rng = new System.Random(seed * 7919 + i++);
            Vector3 foot = new Vector3(t.pos.x, g, t.pos.z);
            float half = (t.kind == 1 ? PoplarHalfWidth : BroadleafHalfWidth) * t.scale;
            float squeeze = Mathf.Clamp01((WedgeRoom(foot) - 0.4f * t.scale) / half);
            if (squeeze < 0.2f) continue;               // a trunk on the wedge's very edge: leave it out
            if (t.kind == 1) PoplarTree(md, foot, t.scale, squeeze, rng);
            else BroadleafTree(md, foot, t.scale, squeeze, rng);
            planted++;
        }
        return planted;
    }

    /// How far a crown reaches sideways from its trunk, per unit of the spot's scale.
    const float BroadleafHalfWidth = 4.5f, PoplarHalfWidth = 1.45f;

    /// The layout keeps every trunk out of the bowler's-arm wedge, but a crown 9 m across beside
    /// it would still reach in: the room (metres, square to the wedge's edge as the batter sees it)
    /// a crown has before it does. A tree that has less is drawn narrower, not moved.
    static float WedgeRoom(Vector3 foot)
    {
        Vector3 eye = ClubGroundLayout.BatterEye;
        Vector2 d = new Vector2(foot.x - eye.x, foot.z - eye.z);
        float off = Mathf.Abs(Mathf.Atan2(d.y, -d.x)) * Mathf.Rad2Deg - ClubGroundLayout.ArmWedgeDeg - 0.5f;
        if (off >= 90f) return float.MaxValue;
        return Mathf.Max(0f, d.magnitude * Mathf.Sin(off * Mathf.Deg2Rad));
    }

    static void BroadleafTree(MeshData md, Vector3 foot, float s, float k, System.Random rng)
    {
        float trunk = 3.4f * s;
        Frame w = Frame.World;
        Cylinder(md, w, foot + Vector3.down * 0.1f, foot + Vector3.up * trunk, 0.26f * s, 0.17f * s, 6, ClubCell.Bark, false, false);
        Vector3 crown = foot + Vector3.up * (trunk + 2.4f * s);
        for (int b = 0; b < 2; b++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 tip = foot + Vector3.up * (trunk + 1.3f * s) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (1.6f * s * k);
            Cylinder(md, w, foot + Vector3.up * (trunk * 0.8f), tip, 0.11f * s, 0.06f * s, 5, ClubCell.Bark, false, false);
        }
        ClubCell main = Broadleaf[rng.Next(Broadleaf.Length)];
        Crossed(md, crown, 6.2f * s * k, 5.4f * s, 3, Yaw(rng), ClubAtlas.UV(main), crown);
        for (int j = 0; j < 7; j++)
        {
            float a = (j + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / 7f;
            float up = ((float)rng.NextDouble() - 0.35f) * 1.8f * s;
            Vector3 c = crown + new Vector3(Mathf.Cos(a) * 2.3f * s * k, up, Mathf.Sin(a) * 2.3f * s * k);
            float size = (3.4f + (float)rng.NextDouble()) * s;
            ClubCell cell = rng.NextDouble() < 0.7 ? main : Broadleaf[rng.Next(Broadleaf.Length)];
            Crossed(md, c, size * k, size * 0.9f, 3, Yaw(rng), ClubAtlas.UV(cell), crown);
        }
    }

    static void PoplarTree(MeshData md, Vector3 foot, float s, float squeeze, System.Random rng)
    {
        float trunk = 2.2f * s;
        Cylinder(md, Frame.World, foot + Vector3.down * 0.1f, foot + Vector3.up * trunk, 0.2f * s, 0.14f * s, 6, ClubCell.Bark, false, false);
        ClubCell cell = Poplar[rng.Next(Poplar.Length)];
        Vector3 middle = foot + Vector3.up * (trunk + 6f * s);
        for (int k = 0; k < 6; k++)
        {
            Vector3 c = foot + Vector3.up * (trunk + 0.6f * s + k * 1.9f * s);
            float width = 2.9f * s * (1f - k / 7.5f) * squeeze;
            Crossed(md, c, width, 3.4f * s, 3, Yaw(rng), ClubAtlas.UV(cell), new Vector3(middle.x, c.y, middle.z));
        }
    }

    static float Yaw(System.Random rng) => (float)rng.NextDouble() * 180f;

    // ---- Spectators --------------------------------------------------------------------------------

    /// A seated figure, metres: the atlas cell is 41 x 73 px, so 0.55 x 0.98 keeps it in proportion.
    public const float PersonWidth = 0.55f, PersonHeight = 0.98f;

    /// Fills `p.Spectators[0..2]` from `p.Seats`, shuffled the same way every build: a quarter of
    /// the seats for an empty ground, sixty per cent for half, all of them packed (ShowCrowd turns
    /// on the first one, two or three groups).
    public static void Spectators(ClubParts p, List<Vector2Int> people, int seed)
    {
        var order = new List<int>();
        for (int i = 0; i < p.Seats.Count; i++) order.Add(i);
        var rng = new System.Random(seed);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        int n = order.Count, first = Mathf.RoundToInt(n * 0.25f), second = Mathf.RoundToInt(n * 0.6f);
        for (int k = 0; k < n; k++)
        {
            ClubSeat seat = p.Seats[order[k]];
            int group = k < first ? 0 : k < second ? 1 : 2;
            Vector2Int cell = people[rng.Next(people.Count)];
            float angle = Mathf.Atan2(seat.pos.z, seat.pos.x) / (Mathf.PI * 2f);
            if (angle < 0f) angle += 1f;
            // CrowdStand reads r = bob phase, g = row (no rows here), b = angle round the ground.
            var colour = new Color((float)rng.NextDouble(), 0f, angle, 1f);
            Vector3 bottom = seat.pos + Vector3.down * 0.06f + seat.facing * 0.04f;
            Billboard(p.Spectators[group], bottom, seat.facing, PersonWidth, PersonHeight, ClubAtlas.Person(cell), colour);
        }
    }
}
