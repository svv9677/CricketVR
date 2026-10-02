using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The one recording both replays play (ShotRecording, PoseTrack): the pre-roll ring that keeps
/// only the last moments of the run-up, the wrap-around after it, and finding a pose by time.
/// Standalone - no scene. Intervals are powers of two so the sample times are exact.
/// Run: Window > General > Test Runner > EditMode > ShotRecordingTests.
/// </summary>
public class ShotRecordingTests
{
    private const float Interval = 0.125f;

    private static ShotRecording.Sample BallAtX(float x) =>
        new ShotRecording.Sample { ball = new Vector3(x, 0f, 0f), batRotation = Quaternion.identity };

    [Test]
    public void PreRollKeepsOnlyTheLastSecondsAndMovesTheStart()
    {
        var shot = new ShotRecording(8, Interval, null);
        shot.Clear(100f);
        for (int i = 0; i < 50; i++)
        {
            Assert.IsTrue(shot.Add(BallAtX(i)));
            shot.DropOlderThan(0.5f);
        }
        Assert.AreEqual(5, shot.Count, "0.5 s at 8 Hz is four intervals: five samples");
        Assert.AreEqual(45f, shot[0].ball.x, "the oldest kept is the 46th sample");
        Assert.AreEqual(49f, shot[shot.Count - 1].ball.x);
        Assert.AreEqual(100f + 45 * Interval, shot.StartedAt, 1e-4f, "time zero moves with what is dropped");
        Assert.AreEqual(45 * Interval, shot.SinceBegin(0f), 1e-4f);
        // 45 dropped from a ring of 8: the kept five sit in slots 5, 6, 7, 0, 1.
        Assert.AreEqual(47.5f, shot.BallAt(2.5f * Interval).x, 1e-4f, "interpolates across the ring's seam");
    }

    [Test]
    public void AfterTheReleaseItFillsTheWholeRingThenRefuses()
    {
        var shot = new ShotRecording(8, Interval, null);
        shot.Clear(0f);
        for (int i = 0; i < 6; i++)
        {
            shot.Add(BallAtX(i));
            shot.DropOlderThan(0.25f);      // keeps three: the ring's start is now mid-array
        }
        for (int i = 6; i < 11; i++)
            Assert.IsTrue(shot.Add(BallAtX(i)), $"sample {i} fits");
        Assert.AreEqual(8, shot.Count);
        Assert.IsFalse(shot.Add(BallAtX(99f)), "full: never overwrites what is held");
        for (int i = 0; i < shot.Count; i++)
            Assert.AreEqual(3f + i, shot[i].ball.x, $"sample {i} in order across the wrap");
        Assert.AreEqual((shot.Count - 1) * Interval, shot.Duration, 1e-5f);
    }

    [Test]
    public void PosesAreFoundByTimeAcrossTheWrap()
    {
        var poses = new PoseTrack(4, 1, 1);
        for (int i = 0; i < 4; i++)
            Fill(poses, poses.Add(i), i);
        poses.DropBefore(1.5f);             // keeps 1 (the last at or before 1.5), 2 and 3
        Assert.AreEqual(3, poses.Count);
        Fill(poses, poses.Add(4f), 4f);     // into the slot the pose at 0 left
        Assert.AreEqual(-1, poses.Add(5f), "full");

        Assert.IsTrue(poses.Locate(3.25f, out int a, out int b, out float blend));
        Assert.AreEqual(3f, poses.positions[a].x);
        Assert.AreEqual(4f, poses.positions[b].x);
        Assert.AreEqual(0.25f, blend, 1e-5f);

        poses.Locate(0f, out a, out b, out blend);
        Assert.AreEqual(1f, poses.positions[a].x, "before the first: held at the first");
        Assert.AreEqual(a, b);
        poses.Locate(10f, out a, out b, out blend);
        Assert.AreEqual(4f, poses.positions[a].x, "after the last: held at the last");
        Assert.AreEqual(a, b);
    }

    [Test]
    public void ClearingARecordingClearsItsPoses()
    {
        var poses = new PoseTrack(4, 1, 1);
        var shot = new ShotRecording(8, Interval, poses);
        shot.Clear(0f);
        Fill(poses, poses.Add(0f), 0f);
        shot.Released = true;
        shot.HasContact = true;
        shot.Clear(5f);
        Assert.AreEqual(0, poses.Count);
        Assert.IsFalse(shot.Released);
        Assert.IsFalse(shot.HasContact);
        Assert.AreEqual(5f, shot.StartedAt);
        Assert.IsFalse(poses.Locate(0f, out _, out _, out _), "nothing to find");
    }

    private static void Fill(PoseTrack poses, int slot, float value)
    {
        poses.rotations[slot] = Quaternion.identity;
        poses.positions[slot] = new Vector3(value, 0f, 0f);
    }
}
