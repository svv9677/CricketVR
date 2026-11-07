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
    public void ASideHasAsManyLeftArmersAsRightArmers()
    {
        foreach (string first in new[] { "Bumrah", "Pranav", "Starc", "" })
        {
            int left = 0;
            for (int i = 0; i < 8; i++)
                if (BowlerArm.IsLeftArm(i, first)) left++;
            Assert.AreEqual(4, left, first);
            Assert.AreNotEqual(BowlerArm.IsLeftArm(0, first), BowlerArm.IsLeftArm(1, first), "every other one");
        }
        Assert.IsFalse(BowlerArm.IsLeftArm(-1, "Bumrah"), "not in the side");
    }

    [Test]
    public void TheSettingDecidesAndBothFollowsTheBowlerOrTheSpell()
    {
        Assert.IsFalse(BowlerArm.Decide(BowlerArm.eSetting.Right, false, true, true));
        Assert.IsTrue(BowlerArm.Decide(BowlerArm.eSetting.Left, true, false, false), "spinners too");
        Assert.IsTrue(BowlerArm.Decide(BowlerArm.eSetting.Both, false, false, true), "his own arm in a match");
        Assert.IsFalse(BowlerArm.Decide(BowlerArm.eSetting.Both, false, true, false));
        Assert.IsTrue(BowlerArm.Decide(BowlerArm.eSetting.Both, true, true, false), "the nets: the spell's arm");
    }

    [Test]
    public void ALeftArmSpinnerTurnsItTheOtherWay()
    {
        Assert.AreEqual(-1f, BowlerArm.EffectSign(eSwingType.OffSpin, true), "left-arm orthodox: away from the right-hander");
        Assert.AreEqual(-1f, BowlerArm.EffectSign(eSwingType.LegSpin, true));
        Assert.AreEqual(1f, BowlerArm.EffectSign(eSwingType.OffSpin, false));
        Assert.AreEqual(1f, BowlerArm.EffectSign(eSwingType.OutSwing, true), "pace: judged against the batter");
        Assert.AreEqual(1f, BowlerArm.EffectSign(eSwingType.Pace, true));
        Assert.AreEqual("Left-arm orthodox", BowlerArm.KindName(eBowlerKind.OffSpin, true));
        Assert.AreEqual("Left-arm fast", BowlerArm.KindName(eBowlerKind.Fast, true));
        Assert.AreEqual(BowlerRepertoire.KindName(eBowlerKind.LegSpin), BowlerArm.KindName(eBowlerKind.LegSpin, false));
    }

    [Test]
    public void TheSettingIsSaved()
    {
        BowlerArm.eSetting was = BowlerArm.Setting;
        try
        {
            BowlerArm.Setting = BowlerArm.eSetting.Left;
            Assert.AreEqual(BowlerArm.eSetting.Left, BowlerArm.Setting);
            BowlerArm.Setting = BowlerArm.eSetting.Right;
            Assert.AreEqual(BowlerArm.eSetting.Right, BowlerArm.Setting);
        }
        finally { BowlerArm.Setting = was; }
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

    [Test]
    public void ALeftArmersRunUpEndsOnTheMirrorOfARightArmersAndClearOfTheStumps()
    {
        Vector3 quick = AnimatedBowler.RunUpEnd(new Vector3(-23f, 0f, -2.32f));
        Assert.AreEqual(-0.90f, quick.z, 0.01f, "the quick's mark: the right-armer let go at z 0.90");
        Vector3 medium = AnimatedBowler.RunUpEnd(new Vector3(-23f, 0f, -2.02f));
        Assert.LessOrEqual(medium.z, -AnimatedBowler.LeftClear + 1e-4f, "never on the stumps");
        Assert.Less(AnimatedBowler.RunUpEnd(new Vector3(-23f, 0f, 2.32f)).z * quick.z, 0f, "mirrored, the other side");
    }
}
