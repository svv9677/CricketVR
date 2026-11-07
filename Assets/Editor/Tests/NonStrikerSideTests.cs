using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The non-striker stands on the side away from the bowler's run-up: -z for a right-armer over the
/// wicket, +z for a left-armer (EndPose, 2026-11-06).
/// </summary>
public class NonStrikerSideTests
{
    [TearDown]
    public void Reset() => EndPose.SetBowlerArm(false);

    [Test]
    public void TheNonStrikerCrossesForALeftArmer()
    {
        EndPose.SetBowlerArm(false);
        Assert.Less(EndPose.NonStrikerHeadXZ.y, 0f, "right-armer: -z");
        Assert.Less(PartnerStance.NonStrikerZ, 0f);
        EndPose.SetBowlerArm(true);
        Assert.Greater(EndPose.NonStrikerHeadXZ.y, 0f, "left-armer: +z, out of his run-up on -z");
        Assert.Greater(PartnerStance.NonStrikerZ, 0f);
        Assert.AreEqual(EndPose.NonStrikerOut, EndPose.NonStrikerHeadXZ.y, 1e-5f);
    }
}
