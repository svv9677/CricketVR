using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The two ends (EndPose): a pose placed for the crease carried to the non-striker's spot and
/// back. Standalone. Run: Window > General > Test Runner > EditMode > EndPoseTests.
/// </summary>
public class EndPoseTests
{
    private static Vector3 Head(Vector2 xz, float y) => new Vector3(xz.x, y, xz.y);

    [Test]
    public void AtTheCreaseNothingMoves()
    {
        var p = new Vector3(7f, 1.8f, 2.8f);
        Quaternion q = Quaternion.Euler(10f, 40f, 5f);
        Assert.AreEqual(p, EndPose.FromCrease(p, false));
        Assert.AreEqual(p, EndPose.ToCrease(p, false));
        Assert.AreEqual(0f, Quaternion.Angle(q, EndPose.FromCrease(q, false)), 1e-3f);
        Assert.AreEqual(0f, Quaternion.Angle(q, EndPose.ToCrease(q, false)), 1e-3f);
    }

    [Test]
    public void ThereAndBackIsTheSamePose()
    {
        var rng = new System.Random(7);
        for (int i = 0; i < 100; i++)
        {
            var p = new Vector3((float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 4f, (float)rng.NextDouble() * 20f - 10f);
            Quaternion q = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
            Assert.Less((EndPose.ToCrease(EndPose.FromCrease(p, true), true) - p).magnitude, 1e-4f);
            Assert.Less((EndPose.FromCrease(EndPose.ToCrease(p, true), true) - p).magnitude, 1e-4f);
            Assert.Less(Quaternion.Angle(q, EndPose.ToCrease(EndPose.FromCrease(q, true), true)), 0.05f);
        }
    }

    [Test]
    public void TheCreaseHeadLandsOnTheNonStrikerHead()
    {
        Vector3 at = EndPose.FromCrease(Head(EndPose.CreaseHeadXZ, 1.65f), true);
        Assert.Less((at - Head(EndPose.NonStrikerHeadXZ, 1.65f)).magnitude, 1e-4f);
        Assert.Less(EndPose.NonStrikerHeadXZ.y, 0f, "on -Z, clear of the run-up");
        Assert.Greater(EndPose.NonStrikerHeadXZ.x, BallDelivery.BowlerStumpsX, "in front of the bowler's stumps");
    }

    [Test]
    public void FacingTheBowlerBecomesFacingTheStriker()
    {
        Quaternion atCrease = Quaternion.LookRotation(Vector3.left);
        Vector3 facing = EndPose.FromCrease(atCrease, true) * Vector3.forward;
        Assert.Less((facing - Vector3.right).magnitude, 1e-4f);
        Vector3 offset = EndPose.FromCrease(Head(EndPose.CreaseHeadXZ, 1.6f) + Vector3.left * 2f, true) - Head(EndPose.NonStrikerHeadXZ, 1.6f);
        Assert.Less((offset - Vector3.right * 2f).magnitude, 1e-4f, "two metres in front of him there too");
    }

    [Test]
    public void TheShotCardStaysClearOfTheRunUp()
    {
        Vector3 card = EndPose.FromCrease(new Vector3(7.0f, 1.8f, 2.8f), true);
        Assert.Less(card.z, -2f);
        Assert.AreEqual(1.8f, card.y, 1e-5f);
    }

    [Test]
    public void TheFarStumpsAreAtTheOtherEnd()
    {
        Assert.AreEqual(BallDelivery.BowlerStumpsX, EndPose.FarStumps(false).x, 1e-5f);
        Assert.AreEqual(BallDelivery.BatsmanStumpsX, EndPose.FarStumps(true).x, 1e-5f);
    }
}
