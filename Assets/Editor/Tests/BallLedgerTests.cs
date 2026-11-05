using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The per-ball measurement's pure rules (BallLedgerMath) and the real-blade judgement of a contact
/// (RealBlade): at the real width it agrees with the game's own BatContact; a hit only the shield's
/// extra width reached is a miss; the stumps; nets runs; the line for a left-hander. Standalone - a
/// synthetic blade in metres (scale 1), the game's own Sweep and Respond, no scene.
/// </summary>
public class BallLedgerTests
{
    private static readonly BatContact.Blade Blade = new BatContact.Blade
    {
        toeZ = 0f, sweetZ = 0.18f, shoulderZ = 0.56f, halfWidth = 0.054f, faceY = 0.02f, backY = -0.04f,
    };
    private const float R = BallFlight.Radius;

    /// A ball coming straight at the face of a still bat (identity pose, scale 1) at (x, z) on it.
    private static bool FaceOn(float x, float z, float widthMultiplier, out BatContact.Hit hit)
    {
        Vector3 from = new Vector3(x, Blade.faceY + 0.3f, z), to = new Vector3(x, Blade.faceY - 0.05f, z);
        return BatContact.Sweep(Vector3.zero, Quaternion.identity, Vector3.zero, Quaternion.identity, 1f,
                                from, to, Blade, widthMultiplier, R, Vector3.zero, out hit);
    }

    private static BatContact.Result Respond(BatContact.Hit hit, float widthMultiplier) =>
        BatContact.Respond(hit, Blade, 1f, new Vector3(0f, -30f, 0f), Vector3.zero, BallFlight.Mass, widthMultiplier);

    // ---- RealBlade --------------------------------------------------------------------------------

    [Test]
    public void AtTheRealWidthItAgreesWithBatContact()
    {
        float[] zs = { 0.02f, 0.10f, Blade.sweetZ, 0.35f, 0.54f };
        int checkedHits = 0;
        foreach (float z in zs)
            for (float x = 0f; x <= Blade.halfWidth + 0.9f * R; x += 0.004f)
            {
                if (!FaceOn(x, z, 1f, out BatContact.Hit hit))
                    continue;
                BatContact.Result game = Respond(hit, 1f);
                RealBlade.Contact real = RealBlade.Evaluate(hit, Blade, 1f, 1f);
                string at = $"x={x:F3} z={z:F2}";
                Assert.AreEqual(game.quality, real.quality, 1e-5f, at);
                Assert.AreEqual(game.edge, real.edge, at);
                Assert.AreEqual(game.kind, real.kind, at);
                Assert.AreEqual(game.edge ? eRealContact.Edge : eRealContact.Clean, real.contact, at);
                checkedHits++;
            }
        Assert.Greater(checkedHits, 50, "the grid should meet the blade");
    }

    [Test]
    public void AWidenedBladeIsMeasuredAtTheRealWidth()
    {
        float x = 0.3f * Blade.halfWidth;
        Assert.IsTrue(FaceOn(x, Blade.sweetZ + 0.05f, 1f, out BatContact.Hit realHit));
        Assert.IsTrue(FaceOn(x, Blade.sweetZ + 0.05f, 2f, out BatContact.Hit wideHit));
        float onReal = Respond(realHit, 1f).quality;
        float onWide = Respond(wideHit, 2f).quality;
        Assert.Greater(onWide, onReal + 0.01f, "the shield flatters the game's quality");
        Assert.AreEqual(onReal, RealBlade.Evaluate(wideHit, Blade, 1f, 2f).quality, 1e-5f, "but not the measurement");
    }

    [Test]
    public void AHitOnlyTheShieldReachedWouldHaveMissed()
    {
        float x = 2.5f * Blade.halfWidth;   // beyond the real edge plus a ball, inside the 4x blade
        Assert.IsFalse(FaceOn(x, Blade.sweetZ, 1f, out _), "the real bat misses it");
        Assert.IsTrue(FaceOn(x, Blade.sweetZ, 4f, out BatContact.Hit hit), "the Easy bat finds it");
        RealBlade.Contact real = RealBlade.Evaluate(hit, Blade, 1f, 4f);
        Assert.AreEqual(eRealContact.WouldHaveMissed, real.contact);
        Assert.AreEqual(0f, real.quality);
        Assert.AreEqual(eBallAction.Missed, BallLedgerMath.Action(true, real.contact, 0f, 20f), "a touch the real bat misses is a miss");
    }

    [Test]
    public void JustOutsideTheRealEdgeIsAnEdge()
    {
        float x = Blade.halfWidth + 0.5f * R;   // on the face of a 2x blade, off the edge of the real one
        Assert.IsTrue(FaceOn(x, Blade.sweetZ, 2f, out BatContact.Hit wide));
        Assert.IsFalse(Respond(wide, 2f).edge, "the widened bat calls it a face hit");
        RealBlade.Contact real = RealBlade.Evaluate(wide, Blade, 1f, 2f);
        Assert.AreEqual(eRealContact.Edge, real.contact);
        Assert.IsTrue(FaceOn(x, Blade.sweetZ, 1f, out BatContact.Hit onReal));
        Assert.IsTrue(Respond(onReal, 1f).edge, "and the real bat would have edged it");
    }

    // ---- The stumps, the line ---------------------------------------------------------------------

    [Test]
    public void WouldHitTheStumpsWithinTheirWidthAndHeightPlusABall()
    {
        Assert.IsTrue(BallLedgerMath.WouldHitStumps(0f, 0.3f), "middle stump");
        Assert.IsTrue(BallLedgerMath.WouldHitStumps(0.14f, 0.3f), "clips off stump (0.114 + a ball)");
        Assert.IsTrue(BallLedgerMath.WouldHitStumps(-0.14f, 0.3f), "clips leg stump");
        Assert.IsFalse(BallLedgerMath.WouldHitStumps(0.16f, 0.3f), "past off stump");
        Assert.IsFalse(BallLedgerMath.WouldHitStumps(-0.16f, 0.3f), "past leg stump");
        Assert.IsTrue(BallLedgerMath.WouldHitStumps(0f, 0.74f), "takes the bails");
        Assert.IsFalse(BallLedgerMath.WouldHitStumps(0f, 0.76f), "over the top");
    }

    [Test]
    public void TheLineIsMirroredForALeftHanderAsTheBowlerAimsIt()
    {
        Assert.AreEqual(0.3f, BallLedgerMath.LineInBatterFrame(0.3f, false));
        Assert.AreEqual(-0.3f, BallLedgerMath.LineInBatterFrame(0.3f, true));
        // Main.DeliverBall: world target = offSign * line in the batter's frame.
        foreach (bool left in new[] { false, true })
        {
            float offSign = left ? -1f : 1f;
            const float corridor = 0.25f;   // outside off stump
            Assert.AreEqual(corridor, BallLedgerMath.LineInBatterFrame(offSign * corridor, left), 1e-6f);
        }
    }

    // ---- Played, left, missed ---------------------------------------------------------------------

    [Test]
    public void PlayedLeftOrMissed()
    {
        Assert.AreEqual(eBallAction.Played, BallLedgerMath.Action(true, eRealContact.Clean, 0f, 10f));
        Assert.AreEqual(eBallAction.Played, BallLedgerMath.Action(true, eRealContact.Edge, 0f, 10f));
        Assert.AreEqual(eBallAction.Missed, BallLedgerMath.Action(false, eRealContact.None, 0.1f, 8f), "swung and beaten");
        Assert.AreEqual(eBallAction.Left, BallLedgerMath.Action(false, eRealContact.None, 0.1f, 1f), "bat still: a leave");
        Assert.AreEqual(eBallAction.Left, BallLedgerMath.Action(false, eRealContact.None, 0.8f, 8f), "nowhere near it");
    }

    // ---- Nets runs --------------------------------------------------------------------------------

    [Test]
    public void NetsRunsFromTheShot()
    {
        // Forward of the wicket is -X (back down the pitch); behind square is +X.
        Assert.AreEqual(0, BallLedgerMath.NetsRunsEquivalent(new Vector3(-30f, 15f, 0f), 80f, eRealContact.WouldHaveMissed, false), "not hit on a real bat");
        Assert.AreEqual(6, BallLedgerMath.NetsRunsEquivalent(new Vector3(-30f, 15f, 5f), 70f, eRealContact.Clean, false), "carried 70 m");
        Assert.AreEqual(4, BallLedgerMath.NetsRunsEquivalent(new Vector3(-28f, 1f, 4f), 8f, eRealContact.Clean, false), "driven hard along the ground");
        Assert.AreEqual(3, BallLedgerMath.NetsRunsEquivalent(new Vector3(-22f, 10f, 5f), 40f, eRealContact.Clean, false), "lofted, short of the rope");
        Assert.AreEqual(1, BallLedgerMath.NetsRunsEquivalent(new Vector3(-12f, 1f, 2f), 10f, eRealContact.Clean, false), "pushed for one");
        Assert.AreEqual(0, BallLedgerMath.NetsRunsEquivalent(new Vector3(-5f, 0.5f, 1f), 3f, eRealContact.Clean, false), "a block");
        Assert.AreEqual(1, BallLedgerMath.NetsRunsEquivalent(new Vector3(20f, 3f, 5f), 30f, eRealContact.Clean, false), "glanced fine, hard");
        Assert.AreEqual(1, BallLedgerMath.NetsRunsEquivalent(new Vector3(-20f, 2f, 5f), 25f, eRealContact.Edge, false), "a hard edge");
        Assert.AreEqual(0, BallLedgerMath.NetsRunsEquivalent(new Vector3(-5f, 1f, 1f), 4f, eRealContact.Edge, false), "a soft edge");
        Assert.AreEqual(6, BallLedgerMath.NetsRunsEquivalent(new Vector3(-30f, 15f, 30f), 70f, eRealContact.Clean, false),
            "a 70 m carry is six on any line");
    }

    [Test]
    public void NetsRunsAreTheSameShotForALeftHander()
    {
        Vector3[] shots = { new Vector3(-28f, 1f, 6f), new Vector3(-30f, 15f, -8f), new Vector3(15f, 2f, 4f), new Vector3(-12f, 1f, -3f) };
        float[] carries = { 8f, 70f, 20f, 10f };
        for (int i = 0; i < shots.Length; i++)
        {
            Vector3 mirrored = new Vector3(shots[i].x, shots[i].y, -shots[i].z);
            Assert.AreEqual(BallLedgerMath.NetsRunsEquivalent(shots[i], carries[i], eRealContact.Clean, false),
                            BallLedgerMath.NetsRunsEquivalent(mirrored, carries[i], eRealContact.Clean, true), $"shot {i}");
        }
    }

    // ---- How it ended, the situation ---------------------------------------------------------------

    [Test]
    public void HowOutAndHowItEnded()
    {
        Assert.AreEqual(eHowOut.Bowled, BallLedgerMath.HowOut(eGameState.InGame_Bowled, ""));
        Assert.AreEqual(eHowOut.CaughtAndBowled, BallLedgerMath.HowOut(eGameState.InGame_BallFielded, "Bowler"));
        Assert.AreEqual(eHowOut.CaughtBehind, BallLedgerMath.HowOut(eGameState.InGame_BallFielded, "Keeper"));
        Assert.AreEqual(eHowOut.Caught, BallLedgerMath.HowOut(eGameState.InGame_BallFielded, "Fielder (3)"));
        Assert.AreEqual(eBallEnd.Out, BallLedgerMath.End(true, false, false, 0, false));
        Assert.AreEqual(eBallEnd.Wide, BallLedgerMath.End(false, true, false, 0, false));
        Assert.AreEqual(eBallEnd.Six, BallLedgerMath.End(false, false, true, 6, true), "a six off a no-ball is a six");
        Assert.AreEqual(eBallEnd.Four, BallLedgerMath.End(false, false, false, 4, true));
        Assert.AreEqual(eBallEnd.Runs, BallLedgerMath.End(false, false, false, 4, false), "four run");
        Assert.AreEqual(eBallEnd.NoBall, BallLedgerMath.End(false, false, true, 0, false));
        Assert.AreEqual(eBallEnd.Dot, BallLedgerMath.End(false, false, false, 0, false));
    }

    [Test]
    public void PressureInAChase()
    {
        Assert.AreEqual(10f, BallLedgerMath.RequiredRate(50, 30), 1e-5f);
        Assert.IsTrue(BallLedgerMath.UnderPressure(true, 50, 30, false), "RRR 10");
        Assert.IsFalse(BallLedgerMath.UnderPressure(true, 30, 30, false), "RRR 6, five overs left");
        Assert.IsTrue(BallLedgerMath.UnderPressure(true, 10, 12, false), "last two overs, within reach");
        Assert.IsFalse(BallLedgerMath.UnderPressure(false, 0, 30, false), "setting a total");
        Assert.IsTrue(BallLedgerMath.UnderPressure(false, 0, 30, true), "a wicket just fell");
        Assert.IsFalse(BallLedgerMath.UnderPressure(true, 0, 12, false), "already won");
    }

    [Test]
    public void ACleanShotSquareOrBehindSquareScoresLikeAnyOther()
    {
        Assert.AreEqual(6, BallLedgerMath.NetsRunsEquivalent(new Vector3(4f, 16f, 30f), 72f, eRealContact.Clean, false), "pulled for six, a touch behind square");
        Assert.AreEqual(4, BallLedgerMath.NetsRunsEquivalent(new Vector3(10f, 1f, -26f), 9f, eRealContact.Clean, false), "swept hard along the ground");
        Assert.AreEqual(1, BallLedgerMath.NetsRunsEquivalent(new Vector3(25f, 2f, 3f), 30f, eRealContact.Clean, false), "straight back to the keeper");
        Assert.AreEqual(1, BallLedgerMath.NetsRunsEquivalent(new Vector3(4f, 16f, 30f), 72f, eRealContact.Edge, false), "an edge is still an edge");
    }
}
