using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ClubGroundLayout: the fence clears a fielder's overrun; nothing but the sightscreen stands behind
/// the bowler's arm; the pavilion and the score hut are in the batter's view at 60-80 m; no two
/// footprints overlap and none is inside the fence; trees keep out of the wedge, off every footprint
/// and their spacing apart, the same for the same seed. And Tools/clubground/layout_table.json, the
/// copy the texture script reads, matches the layout (WriteLayoutTable regenerates it). Standalone.
/// </summary>
public class ClubGroundLayoutTests
{
    public const string TablePath = "Tools/clubground/layout_table.json";
    public const int TableSeed = 1;
    private static readonly int[] Seeds = { 1, 2, 3, 17, 2026 };

    private static List<ClubGroundLayout.Spot> Spots() => new List<ClubGroundLayout.Spot>(ClubGroundLayout.Footprints());

    [Test]
    public void TheFenceClearsTheOverrunAndTheGroundRunsPastIt()
    {
        Assert.Greater(ClubGroundLayout.FenceRadius, ClubGroundLayout.RopeRadius + 1.5f, "fielders overrun to rope + 1");
        Assert.Greater(ClubGroundLayout.OutfieldDisc, ClubGroundLayout.FenceRadius);
        Assert.Greater(ClubGroundLayout.SurroundRadius, ClubGroundLayout.HorizonRadius, "no bare strip under the horizon");
        Assert.Less(ClubGroundLayout.HorizonBottom, ClubGroundLayout.OutfieldTop);
        Assert.Greater(ClubGroundLayout.TreeInner, ClubGroundLayout.FenceRadius + 8f);
        Assert.Less(ClubGroundLayout.TreeOuter, ClubGroundLayout.HorizonRadius);
    }

    [Test]
    public void OnlyTheSightscreenIsBehindTheBowlersArm()
    {
        foreach (ClubGroundLayout.Spot s in Spots())
        {
            bool inWedge = ClubGroundLayout.InBowlersArm(ClubGroundLayout.XZ(s.pos));
            foreach (Vector2 c in ClubGroundLayout.Corners(s)) inWedge |= ClubGroundLayout.InBowlersArm(c);
            if (s.name != "SightscreenFrame") Assert.IsFalse(inWedge, $"{s.name} is behind the bowler's arm");
        }
        // The wedge points the right way: the bowler's-end sightscreen is in it, the rear one is not.
        Assert.IsTrue(ClubGroundLayout.InBowlersArm(ClubGroundLayout.XZ(ClubGroundLayout.SightscreenFrame.pos)));
        Assert.IsFalse(ClubGroundLayout.InBowlersArm(ClubGroundLayout.XZ(ClubGroundLayout.RearSightscreen.pos)));
        Assert.IsFalse(ClubGroundLayout.InBowlersArm(new Vector2(-30f, 0f)), "inside 50 m is the field of play");
        Assert.IsFalse(ClubGroundLayout.InBowlersArm(new Vector2(-70f, 20f)), "20 deg off is outside the wedge");
    }

    [Test]
    public void ThePavilionAndTheHutAreInViewAtSixtyToEightyMetres()
    {
        foreach (ClubGroundLayout.Spot s in new[] { ClubGroundLayout.Pavilion, ClubGroundLayout.ScoreHut })
        {
            Assert.LessOrEqual(ClubGroundLayout.OffLineDeg(ClubGroundLayout.XZ(s.pos)), 40f, s.name);
            float d = Vector3.Distance(s.pos, ClubGroundLayout.BatterEye);
            Assert.That(d, Is.InRange(60f, 80f), s.name);
        }
    }

    [Test]
    public void NoTwoFootprintsOverlapAndAllAreOutsideTheFence()
    {
        List<ClubGroundLayout.Spot> spots = Spots();
        Assert.AreEqual(18, spots.Count);
        for (int i = 0; i < spots.Count; i++)
        {
            foreach (Vector2 c in ClubGroundLayout.Corners(spots[i]))
                Assert.Greater(c.magnitude, ClubGroundLayout.FenceRadius + 0.5f, $"{spots[i].name} inside the fence");
            for (int j = i + 1; j < spots.Count; j++)
                Assert.IsFalse(ClubGroundLayout.Overlaps(spots[i], spots[j], 0.5f), $"{spots[i].name} on {spots[j].name}");
        }
        // The test can fail: a spot overlaps itself, and a footprint nudged onto the pylon does too.
        Assert.IsTrue(ClubGroundLayout.Overlaps(ClubGroundLayout.ScoreHut, ClubGroundLayout.ScoreHut));
        ClubGroundLayout.Spot hut = ClubGroundLayout.ScoreHut;
        hut.pos = ClubGroundLayout.Pylons[2].pos + new Vector3(3f, 0f, 3f);
        Assert.IsTrue(ClubGroundLayout.Overlaps(hut, ClubGroundLayout.Pylons[2]));
    }

    [Test]
    public void EverySpotFacesTheBatter()
    {
        foreach (ClubGroundLayout.Spot s in Spots())
        {
            float yaw = s.yawDeg * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            Vector2 toEye = (ClubGroundLayout.XZ(ClubGroundLayout.BatterEye) - ClubGroundLayout.XZ(s.pos)).normalized;
            Assert.Greater(Vector2.Dot(forward, toEye), 0.9999f, s.name);
            Assert.AreEqual(0f, s.pos.y, s.name);
        }
    }

    [Test]
    public void TreesKeepClearOfTheWedgeTheFootprintsAndEachOther()
    {
        foreach (int seed in Seeds)
        {
            List<ClubGroundLayout.TreeSpot> trees = ClubGroundLayout.Trees(seed);
            Assert.That(trees.Count, Is.InRange(35, 55), $"seed {seed}");
            for (int i = 0; i < trees.Count; i++)
            {
                Vector2 xz = ClubGroundLayout.XZ(trees[i].pos);
                string what = $"seed {seed} tree {i} at {xz}";
                Assert.GreaterOrEqual(xz.magnitude, ClubGroundLayout.FenceRadius + 8f, what);
                Assert.That(xz.magnitude, Is.InRange(ClubGroundLayout.TreeInner, ClubGroundLayout.TreeOuter), what);
                Assert.IsFalse(ClubGroundLayout.InBowlersArm(xz), what + " behind the bowler's arm");
                Assert.IsFalse(ClubGroundLayout.CanopyInBowlersArm(xz, ClubGroundLayout.CanopyRadius(trees[i].kind, trees[i].scale)),
                               what + ": its crown reaches behind the bowler's arm");
                foreach (ClubGroundLayout.Spot s in Spots())
                    Assert.IsFalse(ClubGroundLayout.Contains(s, xz, ClubGroundLayout.TreeClearance), what + " on " + s.name);
                for (int j = i + 1; j < trees.Count; j++)
                    Assert.GreaterOrEqual(Vector2.Distance(xz, ClubGroundLayout.XZ(trees[j].pos)),
                                          ClubGroundLayout.Spacing(trees[i].kind, trees[j].kind) - 1e-3f, what);
                Assert.That(trees[i].scale, Is.InRange(0.8f, 1.3f), what);
                int kind = ClubGroundLayout.BehindPavilion(xz) ? ClubGroundLayout.Poplar : ClubGroundLayout.Broadleaf;
                Assert.AreEqual(kind, trees[i].kind, what + ": poplars behind the pavilion, broadleaves elsewhere");
            }
            Assert.That(trees.FindAll(t => t.kind == ClubGroundLayout.Poplar).Count,
                        Is.InRange(4, ClubGroundLayout.PoplarTarget), $"seed {seed}: a row of poplars");
        }
    }

    [Test]
    public void ACrownReachingIntoTheWedgeCountsEvenWhenItsTrunkIsClear()
    {
        // A trunk 14 degrees off the batter's line, 80 m out: clear itself, but a 5.2 m crown spans 3.7.
        float b = 14f * Mathf.Deg2Rad;
        Vector2 xz = ClubGroundLayout.XZ(ClubGroundLayout.BatterEye) + 80f * new Vector2(-Mathf.Cos(b), Mathf.Sin(b));
        Assert.IsFalse(ClubGroundLayout.InBowlersArm(xz));
        Assert.IsTrue(ClubGroundLayout.CanopyInBowlersArm(xz, ClubGroundLayout.CanopyRadius(ClubGroundLayout.Broadleaf, 1.3f)));
        Assert.IsFalse(ClubGroundLayout.CanopyInBowlersArm(xz, ClubGroundLayout.CanopyRadius(ClubGroundLayout.Poplar, 1f)));
    }

    [Test]
    public void TheSameSeedGivesTheSameTrees()
    {
        List<ClubGroundLayout.TreeSpot> a = ClubGroundLayout.Trees(TableSeed), b = ClubGroundLayout.Trees(TableSeed);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].pos, b[i].pos);
            Assert.AreEqual(a[i].scale, b[i].scale);
            Assert.AreEqual(a[i].kind, b[i].kind);
        }
        Assert.AreNotEqual(a[0].pos, ClubGroundLayout.Trees(TableSeed + 1)[0].pos, "the seed changes the wood");
        Assert.IsTrue(a.Exists(t => t.kind == ClubGroundLayout.Poplar), "poplars behind the pavilion");
        Assert.IsTrue(a.Exists(t => t.kind == ClubGroundLayout.Broadleaf));
    }

    [Test]
    public void PylonHeadsAreTheGlowPointsAndTheReplayCamIsClear()
    {
        Vector3[] glows = ClubGroundLayout.GlowPoints();
        Assert.AreEqual(4, glows.Length);
        foreach (Vector3 g in glows)
        {
            Assert.AreEqual(ClubGroundLayout.PylonHeight, g.y, 1e-4f);
            Assert.AreEqual(ClubGroundLayout.PylonRadius, new Vector2(g.x, g.z).magnitude, 1e-3f);
        }
        Assert.Less(ClubGroundLayout.SightscreenFrameTop, ClubGroundLayout.ReplayCam.y);
        Assert.IsTrue(ClubGroundLayout.ClearOfReplayCam(new Vector3(-63f, 8.4f, 0f)), "a frame top bar at 8.4 m");
        Assert.IsTrue(ClubGroundLayout.ClearOfReplayCam(new Vector3(-63f, 9.3f, 9.5f)), "a corner post at the frame top");
        Assert.IsFalse(ClubGroundLayout.ClearOfReplayCam(new Vector3(-63f, 9.3f, 0f)), "under the camera at 9.3 m");
        Assert.IsFalse(ClubGroundLayout.ClearOfReplayCam(new Vector3(-63f, 9.5f, 9.5f)), "above the frame top");
    }

    [Test]
    public void TheLayoutTableOnDiskMatchesTheLayout()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), TablePath);
        Assert.IsTrue(File.Exists(path), $"{TablePath} is missing: run ClubGroundLayoutTests.WriteLayoutTable");
        string disk = File.ReadAllText(path), now = TableJson(TableSeed);
        var number = new Regex(@"-?\d+(\.\d+)?");
        Assert.AreEqual(number.Replace(now, "#"), number.Replace(disk.Replace("\r\n", "\n"), "#"),
                        $"{TablePath} has another shape: run ClubGroundLayoutTests.WriteLayoutTable");
        MatchCollection a = number.Matches(now), b = number.Matches(disk);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
            Assert.AreEqual(Parse(a[i].Value), Parse(b[i].Value), 0.005,
                            $"{TablePath} number {i} is stale: run ClubGroundLayoutTests.WriteLayoutTable");
    }

    /// Regenerates the layout table after a layout change. Explicit: it writes into the repo.
    [Test, Explicit]
    public void WriteLayoutTable() => WriteTable();

    public static void WriteTable() =>
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), TablePath), TableJson(TableSeed));

    private static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string XZ(Vector2 p) => "[" + F(p.x) + ", " + F(p.y) + "]";

    /// The layout as the texture script reads it: radii, the eye, each footprint with its corners
    /// (so the script never re-derives the yaw convention), the trees for `seed`, the pylon heads.
    public static string TableJson(int seed)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"note\": \"Generated from ClubGroundLayout.cs by ClubGroundLayoutTests.WriteLayoutTable; do not edit.\",\n");
        sb.Append("  \"seed\": ").Append(seed).Append(",\n");
        sb.Append("  \"radii\": {\"rope\": ").Append(F(ClubGroundLayout.RopeRadius))
          .Append(", \"fence\": ").Append(F(ClubGroundLayout.FenceRadius))
          .Append(", \"fenceHeight\": ").Append(F(ClubGroundLayout.FenceHeight))
          .Append(", \"outfieldTop\": ").Append(F(ClubGroundLayout.OutfieldTop))
          .Append(", \"outfieldDisc\": ").Append(F(ClubGroundLayout.OutfieldDisc))
          .Append(", \"surround\": ").Append(F(ClubGroundLayout.SurroundRadius))
          .Append(", \"treeInner\": ").Append(F(ClubGroundLayout.TreeInner))
          .Append(", \"treeOuter\": ").Append(F(ClubGroundLayout.TreeOuter))
          .Append(", \"horizon\": ").Append(F(ClubGroundLayout.HorizonRadius))
          .Append(", \"horizonHeight\": ").Append(F(ClubGroundLayout.HorizonHeight))
          .Append(", \"horizonBottom\": ").Append(F(ClubGroundLayout.HorizonBottom))
          .Append(", \"bench\": ").Append(F(ClubGroundLayout.BenchRadius))
          .Append(", \"pylon\": ").Append(F(ClubGroundLayout.PylonRadius))
          .Append(", \"pylonHeight\": ").Append(F(ClubGroundLayout.PylonHeight)).Append("},\n");
        Vector3 eye = ClubGroundLayout.BatterEye;
        sb.Append("  \"batterEye\": [").Append(F(eye.x)).Append(", ").Append(F(eye.y)).Append(", ").Append(F(eye.z)).Append("],\n");
        sb.Append("  \"armWedge\": {\"deg\": ").Append(F(ClubGroundLayout.ArmWedgeDeg))
          .Append(", \"from\": ").Append(F(ClubGroundLayout.ArmWedgeFrom)).Append("},\n");
        AppendFootprints(sb);
        AppendTrees(sb, seed);
        sb.Append("  \"glowPoints\": [");
        Vector3[] glows = ClubGroundLayout.GlowPoints();
        for (int i = 0; i < glows.Length; i++)
            sb.Append(i > 0 ? ", " : "").Append("[").Append(F(glows[i].x)).Append(", ").Append(F(glows[i].y))
              .Append(", ").Append(F(glows[i].z)).Append("]");
        sb.Append("]\n}\n");
        return sb.ToString();
    }

    private static void AppendFootprints(StringBuilder sb)
    {
        sb.Append("  \"footprints\": [\n");
        List<ClubGroundLayout.Spot> spots = Spots();
        for (int i = 0; i < spots.Count; i++)
        {
            ClubGroundLayout.Spot s = spots[i];
            sb.Append("    {\"name\": \"").Append(s.name).Append("\", \"x\": ").Append(F(s.pos.x))
              .Append(", \"z\": ").Append(F(s.pos.z)).Append(", \"yaw\": ").Append(F(s.yawDeg))
              .Append(", \"w\": ").Append(F(s.footprint.x)).Append(", \"d\": ").Append(F(s.footprint.y))
              .Append(", \"corners\": [");
            Vector2[] c = ClubGroundLayout.Corners(s);
            for (int k = 0; k < c.Length; k++) sb.Append(k > 0 ? ", " : "").Append(XZ(c[k]));
            sb.Append("]}").Append(i < spots.Count - 1 ? ",\n" : "\n");
        }
        sb.Append("  ],\n");
    }

    private static void AppendTrees(StringBuilder sb, int seed)
    {
        sb.Append("  \"trees\": [\n");
        List<ClubGroundLayout.TreeSpot> trees = ClubGroundLayout.Trees(seed);
        for (int i = 0; i < trees.Count; i++)
        {
            ClubGroundLayout.TreeSpot t = trees[i];
            sb.Append("    {\"x\": ").Append(F(t.pos.x)).Append(", \"z\": ").Append(F(t.pos.z))
              .Append(", \"scale\": ").Append(F(t.scale)).Append(", \"kind\": ").Append(t.kind)
              .Append(i < trees.Count - 1 ? "},\n" : "}\n");
        }
        sb.Append("  ],\n");
    }
}
