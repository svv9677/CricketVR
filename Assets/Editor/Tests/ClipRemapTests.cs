using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A saved clip's poses into the playing scene's layout (ClipRemap): matched by channel name in
/// whatever order the scene has them, a live channel the clip lacks held at the live pose, and the
/// hologram alone (no poses) once fewer than 80% of the clip's rotation channels are found. And the
/// names themselves (ReplayActors.PathKey): same-named siblings told apart by their order.
/// </summary>
public class ClipRemapTests
{
    private static readonly string[] Saved = { "W:A", "B:A/b", "B:A/c", "B:A/d", "B:A/e" };
    private static readonly string[] SavedPos = { "W:A", "P:A/b" };

    /// Two poses; saved rotation channel c of pose j is (j, c, 7, 1), position (j, c, 7).
    private static SavedClip Clip()
    {
        var c = new SavedClip
        {
            header = new ClipHeader { stamp = "s", released = true, releaseTime = 1.2f, hasContact = true, contactTime = 1.7f },
            interval = 0.5f,
            samples = new ShotRecording.Sample[4],
            rotKeys = (string[])Saved.Clone(),
            posKeys = (string[])SavedPos.Clone(),
            poseTimes = new[] { -0.01f, 0.9f },
            rotations = new Quaternion[2 * Saved.Length],
            positions = new Vector3[2 * SavedPos.Length],
        };
        for (int i = 0; i < c.samples.Length; i++)
            c.samples[i].ball = new Vector3(i, 0f, 0f);
        for (int j = 0; j < 2; j++)
        {
            for (int k = 0; k < Saved.Length; k++) c.rotations[j * Saved.Length + k] = new Quaternion(j, k, 7f, 1f);
            for (int k = 0; k < SavedPos.Length; k++) c.positions[j * SavedPos.Length + k] = new Vector3(j, k, 7f);
        }
        return c;
    }

    private static Quaternion[] Live(int n)
    {
        var q = new Quaternion[n];
        for (int i = 0; i < n; i++) q[i] = new Quaternion(-1f, i, -1f, 1f);
        return q;
    }

    private static Vector3[] LivePos(int n)
    {
        var v = new Vector3[n];
        for (int i = 0; i < n; i++) v[i] = new Vector3(-1f, i, -1f);
        return v;
    }

    [Test]
    public void Match_IsTheShareOfSavedChannelsTheSceneHas()
    {
        Assert.AreEqual(1f, ClipRemap.Match(Saved, new[] { "B:A/e", "W:A", "B:A/c", "B:A/b", "B:A/d", "W:Z" }));
        Assert.AreEqual(0.6f, ClipRemap.Match(Saved, new[] { "W:A", "B:A/b", "B:A/c" }), 1e-6f);
        Assert.AreEqual(0f, ClipRemap.Match(new string[0], Saved));
        Assert.AreEqual(0f, ClipRemap.Match(Saved, null));
    }

    [Test]
    public void ReorderedKeys_EachLiveChannelGetsItsOwnSavedData()
    {
        string[] live = { "B:A/e", "B:A/c", "W:A", "B:A/b", "B:A/d" };
        string[] livePos = { "P:A/b", "W:A" };
        ShotRecording s = ClipRemap.ToLayout(Clip(), live, livePos, Live(live.Length), LivePos(livePos.Length));
        Assert.IsNotNull(s.Poses);
        Assert.AreEqual(live.Length, s.Poses.Rotations);
        Assert.AreEqual(livePos.Length, s.Poses.Positions);
        Assert.AreEqual(2, s.Poses.Count);
        for (int j = 0; j < 2; j++)
        {
            int slot = s.Poses.SlotAt(j);
            for (int c = 0; c < live.Length; c++)
            {
                Quaternion q = s.Poses.rotations[slot * live.Length + c];
                Assert.AreEqual(j, q.x);
                Assert.AreEqual(Array.IndexOf(Saved, live[c]), (int)q.y, $"{live[c]} in pose {j}");
            }
            Assert.AreEqual(new Vector3(j, 1f, 7f), s.Poses.positions[slot * 2 + 0], "P:A/b");
            Assert.AreEqual(new Vector3(j, 0f, 7f), s.Poses.positions[slot * 2 + 1], "W:A");
        }
        Assert.AreEqual(-0.01f, s.Poses.TimeAt(0));
        Assert.AreEqual(0.9f, s.Poses.TimeAt(1));
        Assert.AreEqual(4, s.Count);
        Assert.AreEqual(new Vector3(3f, 0f, 0f), s[3].ball);
        Assert.IsTrue(s.Released && s.HasContact);
        Assert.AreEqual(1.2f, s.ReleaseTime);
        Assert.AreEqual(1.7f, s.ContactTime);
        Assert.AreEqual(1.5f, s.Duration, 1e-6f);
    }

    [Test]
    public void ALiveChannelTheClipLacks_HoldsTheLivePose_AtEightyPercent()
    {
        string[] live = { "W:A", "B:A/b", "B:A/c", "B:A/d", "B:Partner/x" };   // 4 of the 5 saved: 80%
        string[] livePos = { "W:A", "P:Partner/x" };
        ShotRecording s = ClipRemap.ToLayout(Clip(), live, livePos, Live(live.Length), LivePos(livePos.Length));
        Assert.IsNotNull(s.Poses, "80% is enough");
        int slot = s.Poses.SlotAt(1);
        Quaternion held = s.Poses.rotations[slot * live.Length + 4];
        Assert.AreEqual(-1f, held.x);
        Assert.AreEqual(4f, held.y, "the live pose of that channel");
        Assert.AreEqual(new Vector3(-1f, 1f, -1f), s.Poses.positions[slot * 2 + 1]);
        Assert.AreEqual(new Vector3(1f, 0f, 7f), s.Poses.positions[slot * 2 + 0]);
    }

    [Test]
    public void BelowEightyPercent_PlaysTheHologramOnly()
    {
        string[] live = { "W:A", "B:A/b", "B:A/c", "B:X/y", "B:X/z" };       // 3 of 5: 60%
        ShotRecording s = ClipRemap.ToLayout(Clip(), live, new[] { "W:A" }, Live(5), LivePos(1));
        Assert.IsNotNull(s);
        Assert.IsNull(s.Poses, "no poses for the big screen");
        Assert.AreEqual(4, s.Count, "the ball and bat still play");
        Assert.IsTrue(s.HasContact);

        Assert.IsNull(ClipRemap.ToLayout(Clip(), new string[0], new string[0], new Quaternion[0], new Vector3[0]).Poses,
                      "a scene with no actors to pose");
        SavedClip none = Clip();
        none.poseTimes = new float[0];
        Assert.IsNull(ClipRemap.ToLayout(none, Saved, SavedPos, Live(5), LivePos(2)).Poses, "a clip saved without poses");
        Assert.IsNull(ClipRemap.ToLayout(null, Saved, SavedPos, Live(5), LivePos(2)));
        Assert.IsNull(ClipRemap.ToLayout((SavedClip)null, (ReplayActors)null));
    }

    [Test]
    public void PathKeys_TellSameNamedSiblingsApartByTheirOrder()
    {
        var root = new GameObject("cvr_remap_" + Guid.NewGuid().ToString("N"));
        try
        {
            Transform a = new GameObject("Fielder").transform, b = new GameObject("Fielder").transform, k = new GameObject("Keeper").transform;
            a.SetParent(root.transform, false);
            k.SetParent(root.transform, false);
            b.SetParent(root.transform, false);
            Transform hips = new GameObject("Hips").transform;
            hips.SetParent(b, false);
            string r = root.name;
            Assert.AreEqual(r + "/Fielder[0]", ReplayActors.PathKey(a));
            Assert.AreEqual(r + "/Fielder[1]", ReplayActors.PathKey(b));
            Assert.AreEqual(r + "/Keeper", ReplayActors.PathKey(k), "a name of its own has no index");
            var memo = new System.Collections.Generic.Dictionary<Transform, string>();
            Assert.AreEqual(r + "/Fielder[1]/Hips", ReplayActors.PathKey(hips, memo));
            Assert.AreEqual(r + "/Fielder[1]", memo[b], "the parents are remembered on the way");
            Assert.AreEqual("", ReplayActors.PathKey(null));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
