using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// People further away are drawn with lighter meshes (PersonLod, PersonLodBaker): facing the
/// bowler drew about three times what facing the keeper did, and lagged (2026-11-11).
public class PersonLodTests
{
    [Test]
    public void FurtherAwayIsLighterAndComingBackWaitsForTheMargin()
    {
        Assert.AreEqual(0, PersonLod.Level(0, 5f));
        Assert.AreEqual(1, PersonLod.Level(0, PersonLod.MidBeyond + 0.1f));
        Assert.AreEqual(2, PersonLod.Level(0, PersonLod.FarBeyond + 0.1f));
        // Just inside a line on the way back: stays, so someone at the line does not flick.
        Assert.AreEqual(1, PersonLod.Level(1, PersonLod.MidBeyond - 0.5f));
        Assert.AreEqual(2, PersonLod.Level(2, PersonLod.FarBeyond - 0.5f));
        // Clearly inside: back a level, or two.
        Assert.AreEqual(0, PersonLod.Level(1, PersonLod.MidBeyond - 2f));
        Assert.AreEqual(1, PersonLod.Level(2, PersonLod.FarBeyond - 2f));
        Assert.AreEqual(1, PersonLod.Level(2, PersonLod.MidBeyond - 0.5f));
        Assert.AreEqual(0, PersonLod.Level(2, 3f));
    }

    [Test]
    public void ClustersKeepAVertexOfTheirOwnAndDropFlatTriangles()
    {
        // A strip of 1 cm squares, 10 long: with 2.5 cm cells several corners share a cluster.
        var p = new List<Vector3>();
        var n = new List<Vector3>();
        for (int x = 0; x <= 10; x++)
            for (int y = 0; y <= 1; y++)
            {
                p.Add(new Vector3(x * 0.01f, y * 0.01f, 0f));
                n.Add(Vector3.back);
            }
        var tris = new List<int>();
        for (int x = 0; x < 10; x++)
        {
            int a = x * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.AddRange(new[] { a, b, c, b, d, c });
        }
        int[] rep = PersonLodBaker.Representatives(p.ToArray(), n.ToArray(), new Vector2[0], 0.025f, out int[] kept);
        Assert.Less(kept.Length, p.Count, "fewer vertices");
        var newIndex = new Dictionary<int, int>();
        for (int i = 0; i < kept.Length; i++) newIndex[kept[i]] = i;
        foreach (int r in rep)
            Assert.IsTrue(newIndex.ContainsKey(r), "every vertex stands in for by a kept one");
        int[] light = PersonLodBaker.Remap(tris.ToArray(), rep, newIndex);
        Assert.Less(light.Length, tris.Count);
        var seen = new HashSet<(int, int, int)>();
        for (int i = 0; i < light.Length; i += 3)
        {
            int a = light[i], b = light[i + 1], c = light[i + 2];
            Assert.IsTrue(a != b && b != c && a != c, "no triangle squashed flat");
            var t = a < b && a < c ? (a, b, c) : b < c ? (b, c, a) : (c, a, b);
            Assert.IsTrue(seen.Add(t), "no triangle twice");
        }
    }

    [Test]
    public void SkinKeepsItsInnerPointsAndClothesTheirOuter()
    {
        // One cluster (one 2 cm cube) of three points facing +Z, 4 mm apart in depth.
        var p = new[] { new Vector3(0.005f, 0.005f, 0.002f), new Vector3(0.005f, 0.005f, 0.006f), new Vector3(0.005f, 0.005f, 0.010f) };
        var n = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
        int[] outer = PersonLodBaker.Representatives(p, n, new Vector2[0], 0.02f, out _, 1f);
        int[] inner = PersonLodBaker.Representatives(p, n, new Vector2[0], 0.02f, out _, -1f);
        Assert.AreEqual(2, outer[0], "clothes: the outermost");
        Assert.AreEqual(0, inner[0], "skin: the innermost");
    }

    [Test]
    public void EveryPersonMeshHasLighterVersions()
    {
        var table = AssetDatabase.LoadAssetAtPath<PersonLodTable>(PersonLodBaker.TablePath);
        Assert.IsNotNull(table, "baked (Tools > CricketVR > Bake Person LODs)");
        Assert.AreEqual(table.full.Length, table.mid.Length);
        Assert.AreEqual(table.full.Length, table.far.Length);
        var have = new HashSet<Mesh>(table.full);
        foreach (string path in PersonLodBaker.Models)
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Mesh m && m.vertexCount >= PersonLodBaker.MinVertices && m.bindposes.Length > 0)
                    Assert.IsTrue(have.Contains(m), $"{path} {m.name} has lighter versions (re-bake)");
        for (int i = 0; i < table.full.Length; i++)
        {
            long full = PersonLodBaker.Triangles(table.full[i]), mid = PersonLodBaker.Triangles(table.mid[i]), far = PersonLodBaker.Triangles(table.far[i]);
            Assert.Less(mid, full, table.full[i].name);
            Assert.Less(far, mid, table.full[i].name);
            Assert.AreEqual(table.full[i].bindposes.Length, table.far[i].bindposes.Length, "same bones");
            Assert.AreEqual(table.far[i].vertexCount, table.far[i].GetBonesPerVertex().Length, "every vertex weighted");
            Assert.AreEqual(i, table.Find(table.far[i]));
        }
    }
}
