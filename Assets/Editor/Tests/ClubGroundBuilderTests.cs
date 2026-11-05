using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// The club ground's geometry, built whole without a scene (ClubGeometry is pure): the Quest
/// budget, faces wound the way they must face, every UV inside its atlas cell, and the clearances
/// the match needs (the replay camera, the bowler's arm, the rope).
public class ClubGroundBuilderTests
{
    static ClubParts Build() => ClubGeometry.Build(ClubAtlas.AllPeople(), false, new StringBuilder());

    static IEnumerable<MeshData> Batches(ClubParts p)
    {
        yield return p.Outfield; yield return p.Surround; yield return p.Structures; yield return p.Fence;
        yield return p.Trees; yield return p.Horizon; yield return p.Night;
        foreach (MeshData s in p.Spectators) yield return s;
    }

    [Test]
    public void FitsTheTriangleBudgetWithRoomForTheTexts()
    {
        ClubParts p = Build();
        int tris = 0;
        foreach (MeshData md in Batches(p)) tris += md.TriangleCount;
        // Two texts (2 triangles a character, ~200 characters) and the flag's 12 x 8 grid.
        tris += 400 + 192;
        Assert.LessOrEqual(tris, ClubGroundBuilder.MaxTriangles, "triangles");
        // Six static batches, three spectator groups, flag, night windows, two texts.
        int batches = 0;
        foreach (MeshData md in Batches(p)) if (md.TriangleCount > 0) batches++;
        Assert.LessOrEqual(batches + 3, ClubGroundBuilder.MaxDrawCalls, "draw calls");
    }

    [Test]
    public void CullBackFacesAreWoundTowardTheirNormals()
    {
        ClubParts p = Build();
        var culled = new List<MeshData> { p.Outfield, p.Surround, p.Structures, p.Horizon, p.Night };
        culled.AddRange(p.Spectators);
        foreach (MeshData md in culled)
            for (int i = 0; i < md.Tris.Count; i += 3)
            {
                Vector3 a = md.Verts[md.Tris[i]], b = md.Verts[md.Tris[i + 1]], c = md.Verts[md.Tris[i + 2]];
                Vector3 g = Vector3.Cross(b - a, c - a);
                if (g.sqrMagnitude < 1e-10f) continue;
                Vector3 n = md.Normals[md.Tris[i]] + md.Normals[md.Tris[i + 1]] + md.Normals[md.Tris[i + 2]];
                Assert.Greater(Vector3.Dot(g, n), 0f, "triangle " + i / 3 + " at " + a + " faces away from its normal");
            }
    }

    [Test]
    public void EveryAtlasTriangleSamplesOneCell()
    {
        ClubParts p = Build();
        foreach (MeshData md in new[] { p.Structures, p.Trees, p.Night })
            for (int i = 0; i < md.Tris.Count; i += 3)
            {
                Vector2 u0 = md.Uvs[md.Tris[i]], u1 = md.Uvs[md.Tris[i + 1]], u2 = md.Uvs[md.Tris[i + 2]];
                Assert.IsTrue(InOneCell(u0, u1, u2), "triangle " + i / 3 + " UVs " + u0 + u1 + u2 + " leave their cell");
            }
    }

    static bool InOneCell(Vector2 a, Vector2 b, Vector2 c)
    {
        for (int k = 0; k < 16; k++)
        {
            Rect r = ClubAtlas.UV((ClubCell)k);
            r.xMin -= 1e-4f; r.yMin -= 1e-4f; r.xMax += 1e-4f; r.yMax += 1e-4f;
            if (r.Contains(a) && r.Contains(b) && r.Contains(c)) return true;
        }
        return false;
    }

    [Test]
    public void TiledPolygonsKeepTheirArea()
    {
        var md = new MeshData();
        ClubMeshKit.Panel(md, Frame.World, Vector3.zero, Vector3.right, Vector3.up, 0f, 5.3f, 0f, 3.1f, ClubCell.Weatherboard);
        float area = 0f;
        for (int i = 0; i < md.Tris.Count; i += 3)
            area += 0.5f * Vector3.Cross(md.Verts[md.Tris[i + 1]] - md.Verts[md.Tris[i]], md.Verts[md.Tris[i + 2]] - md.Verts[md.Tris[i]]).magnitude;
        Assert.AreEqual(5.3f * 3.1f, area, 1e-3f);
        Assert.AreEqual(6 * 2, md.TriangleCount, "3 x 2 tiles of 2 m, two triangles each");
    }

    [Test]
    public void SightscreenFrameClearsTheReplayCameraAndStaysBehindTheScreen()
    {
        ClubParts p = Build();
        Assert.Greater(p.ScreenFrame.Verts.Count, 0);
        foreach (Vector3 v in p.ScreenFrame.Verts)
        {
            Assert.LessOrEqual(v.y, ClubGroundLayout.SightscreenFrameTop, "frame top");
            Assert.GreaterOrEqual((v - ClubGroundLayout.ReplayCam).magnitude, ClubGroundLayout.ReplayCamClear, "replay camera");
            Assert.Less(v.x, -63.15f, "in front of the black screen");
        }
    }

    [Test]
    public void OnlyTheSightscreenFrameStandsInTheBowlersArm()
    {
        ClubParts p = Build();
        int Tall(MeshData md)
        {
            int n = 0;
            foreach (Vector3 v in md.Verts)
                if (v.y > 1.2f && ClubGroundLayout.InBowlersArm(new Vector2(v.x, v.z))) n++;
            return n;
        }
        Assert.AreEqual(Tall(p.ScreenFrame), Tall(p.Structures), "structures in the wedge besides the frame");
        Assert.AreEqual(0, Tall(p.Trees), "trees in the wedge");
    }

    [Test]
    public void FenceIsClosedByItsCollidersOutsideTheRope()
    {
        ClubParts p = Build();
        Assert.AreEqual(ClubFence.FenceColliders, p.FenceColliders.Count);
        float chord = 2f * ClubGroundLayout.FenceRadius * Mathf.Sin(Mathf.PI / ClubFence.FenceColliders);
        foreach (ClubBox b in p.FenceColliders)
        {
            float r = new Vector2(b.centre.x, b.centre.z).magnitude;
            Assert.That(r, Is.InRange(ClubGroundLayout.FenceRadius, ClubGroundLayout.FenceRadius + 0.5f));
            Assert.GreaterOrEqual(b.size.x, chord, "gap between fence colliders");
        }
        Assert.Greater(ClubGroundLayout.FenceRadius - 0.15f, ClubGroundLayout.RopeRadius + 1f, "fielders overrun to rope + 1 m");
        foreach (Vector3 v in p.Fence.Verts)
            Assert.AreEqual(ClubGroundLayout.FenceRadius, new Vector2(v.x, v.z).magnitude, 1e-3f);
    }

    [Test]
    public void SpectatorsFillThreeGrowingGroupsAndFaceTheField()
    {
        ClubParts p = Build();
        int quads = 0;
        foreach (MeshData s in p.Spectators)
        {
            Assert.Greater(s.TriangleCount, 0);
            quads += s.TriangleCount / 2;
            for (int i = 0; i < s.Verts.Count; i++)
                Assert.Greater(Vector3.Dot(s.Normals[i], -new Vector3(s.Verts[i].x, 0f, s.Verts[i].z)), 0f, "faces away");
        }
        Assert.AreEqual(p.Seats.Count, quads);
        Assert.Less(p.Spectators[0].TriangleCount / 2, quads / 2, "an empty ground is under half full");
    }

    [Test]
    public void TextsFaceTheBatterAndPylonHeadsAreTheGlowPoints()
    {
        ClubParts p = Build();
        foreach (ClubLabel l in new[] { p.Figures, p.ClubName })
        {
            Vector3 to = ClubGroundLayout.BatterEye - l.pos;
            to.y = 0f;
            Assert.Greater(Vector3.Dot(l.facing, to.normalized), 0.9f);
            Assert.Greater(l.rect.x, 1f);
        }
        Assert.AreEqual(ClubGroundLayout.GlowPoints(), p.PylonHeads);
        Assert.AreEqual(p.PylonHeads.Length + 2, p.Colliders.Count, "a collider per pylon, the pavilion and the hut");
    }

    [Test]
    public void SameInputsBuildTheSameGround()
    {
        ClubParts a = Build(), b = Build();
        Assert.AreEqual(a.Trees.Verts.Count, b.Trees.Verts.Count);
        Assert.AreEqual(a.Trees.Verts[a.Trees.Verts.Count - 1], b.Trees.Verts[b.Trees.Verts.Count - 1]);
        Assert.AreEqual(a.Spectators[2].Verts[0], b.Spectators[2].Verts[0]);
    }

    [Test]
    public void SurroundShadeCheckReadsTheImageTheWayTheMeshMapsIt()
    {
        List<Vector3> feet = ClubGeometry.ShadedFootprints(), trees = ClubGeometry.ShadedTrees();
        Color32[] upright = PaintPools(feet, trees, false), upsideDown = PaintPools(feet, trees, true);
        Assert.Less(ClubTextures.FootprintShade(upright, PoolSize, PoolSize, feet, false), 0.9f, "footprints");
        Assert.Less(ClubTextures.FootprintShade(upright, PoolSize, PoolSize, trees, false), 0.9f, "trees");
        Assert.IsFalse(ClubTextures.SurroundLooksFlipped(upright, PoolSize, PoolSize, feet, trees));
        Assert.IsTrue(ClubTextures.SurroundLooksFlipped(upsideDown, PoolSize, PoolSize, feet, trees));
    }

    const int PoolSize = 400;

    /// A surround painted as the mesh maps it (u = x, v = z: bottom-up pixel rows rise with z),
    /// or upside down, with a dark pool under every footprint and tree.
    static Color32[] PaintPools(List<Vector3> feet, List<Vector3> trees, bool upsideDown)
    {
        float range = ClubGroundLayout.SurroundRadius;
        var all = new List<Vector3>(feet);
        all.AddRange(trees);
        var px = new Color32[PoolSize * PoolSize];
        for (int y = 0; y < PoolSize; y++)
            for (int x = 0; x < PoolSize; x++)
            {
                float v = y / (PoolSize - 1f);
                if (upsideDown) v = 1f - v;
                Vector2 w = new Vector2((x / (PoolSize - 1f) - 0.5f) * 2f * range, (v - 0.5f) * 2f * range);
                bool pool = false;
                foreach (Vector3 s in all) pool |= (w - new Vector2(s.x, s.z)).magnitude < 5f;
                px[y * PoolSize + x] = pool ? new Color32(40, 60, 30, 255) : new Color32(90, 140, 70, 255);
            }
        return px;
    }

    [Test]
    public void CircleMarkersFollowTheLawsOval()
    {
        float r = FieldSets.RingRadius, x0 = BallDelivery.BowlerStumpsX, x1 = BallDelivery.BatsmanStumpsX;
        float perimeter = 2f * (x1 - x0) + 2f * Mathf.PI * r;
        for (int i = 0; i < 40; i++)
        {
            Vector3 v = ClubFence.OvalPoint(i * perimeter / 40f, r, x0, x1);
            float nx = Mathf.Clamp(v.x, x0, x1);
            Assert.AreEqual(r, new Vector2(v.x - nx, v.z).magnitude, 1e-3f, "marker " + i);
        }
    }
}
