using NUnit.Framework;
using UnityEngine;

/// <summary>
/// BowlerArm: who bowls left-arm, and the mirrored mark. The run-up itself was checked in play
/// mode (2026-11-06): a forced left-armer let go at root (-9.12, -0.89), hand z -0.42, against a
/// right-armer's (-9.18, 0.90), hand z 0.35 - the mirror of each other.
/// </summary>
public class BowlerArmTests
{
    [Test]
    public void OnlyPaceBowlersBowlLeftArm()
    {
        Assert.IsTrue(BowlerArm.CanBeLeftArm(eBowlerKind.Fast));
        Assert.IsTrue(BowlerArm.CanBeLeftArm(eBowlerKind.Medium));
        foreach (eBowlerKind k in new[] { eBowlerKind.OffSpin, eBowlerKind.LegSpin, eBowlerKind.Mystery })
        {
            Assert.IsFalse(BowlerArm.CanBeLeftArm(k), k.ToString());
            Assert.IsFalse(BowlerArm.NetsBall(k, 0f), k.ToString());
            for (int i = 0; i < 50; i++)
                Assert.IsFalse(BowlerArm.IsLeftArm("Bowler " + i, k));
        }
    }

    [Test]
    public void AMatchBowlerKeepsHisArmAndAboutAThirdAreLeftArm()
    {
        Assert.AreEqual(BowlerArm.IsLeftArm("Bhuvneshwar", eBowlerKind.Medium), BowlerArm.IsLeftArm("Bhuvneshwar", eBowlerKind.Medium));
        Assert.IsTrue(BowlerArm.IsLeftArm("Bhuvneshwar", eBowlerKind.Medium), "the quick match has a left-armer");
        int left = 0;
        for (int i = 0; i < 300; i++)
            if (BowlerArm.IsLeftArm("Seamer " + i, eBowlerKind.Fast))
                left++;
        Assert.That(left, Is.InRange(70, 130), "about one in three");
    }

    [Test]
    public void TheNetsMixTheArms()
    {
        Assert.IsTrue(BowlerArm.NetsBall(eBowlerKind.Fast, 0.1f));
        Assert.IsFalse(BowlerArm.NetsBall(eBowlerKind.Fast, 0.9f));
    }

    [Test]
    public void TheLeftArmersMarkIsAcrossThePitchAndTheRightArmersIsUntouched()
    {
        var right = new AFInfo(new Vector3(-15f, 0f, 2.32f), new Vector3(0f, 90f, 0f), 2, 4.5f, 1, 3);
        AFInfo left = right.MirroredCopy();
        Assert.AreEqual(-right.startPos.z, left.startPos.z, 1e-5f);
        Assert.AreEqual(right.startPos.x, left.startPos.x, 1e-5f);
        Assert.AreEqual(2.32f, right.startPos.z, 1e-5f);
        Assert.AreEqual(right.myIndex, left.myIndex);
    }
}
