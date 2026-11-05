using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The drills' say over a delivery (DeliveryShape, BowlingAttack.Shape and ForceShape): a shaped
/// pick lands in its band's distances for the bowler's family and inside its line window, which
/// never reaches a wide; a shape replaces only the length and line (and the over-the-head
/// bouncer); no shape changes nothing; ForceShape is used once. Pure but for the last test, which
/// bowls through BowlingAttack.Next with a forced kind and delivery (no scene, no HUD).
/// </summary>
public class DrillShapeTests
{
    private static readonly eSwingType[] Families =
        { eSwingType.Pace, eSwingType.InSwing, eSwingType.OutSwing, eSwingType.OffSpin, eSwingType.LegSpin };
    private static readonly eLengthBand[] Bands =
        { eLengthBand.Yorker, eLengthBand.Full, eLengthBand.GoodLength, eLengthBand.BackOfALength, eLengthBand.Short };
    private const float Eps = 1e-4f;

    [TearDown]
    public void TearDown()
    {
        BowlingAttack.ForceShape = null;
        BowlingAttack.ForceKind = null;
        BowlingAttack.ForceDelivery = null;
    }

    private static System.Func<float> Seeded(int seed)
    {
        var rng = new System.Random(seed);
        return () => (float)rng.NextDouble();
    }

    private static DeliveryPicker.Band BandOf(eSwingType family, eLengthBand band)
    {
        foreach (DeliveryPicker.Band b in DeliveryPicker.BandsFor(family))
            if (b.band == band)
                return b;
        Assert.Fail("no band " + band + " for " + family);
        return default;
    }

    [Test]
    public void AShapedPickLandsInItsBandAndWindowForEveryFamily()
    {
        System.Func<float> rand = Seeded(5);
        foreach (eSwingType family in Families)
            foreach (eLengthBand band in Bands)
            {
                DeliveryPicker.Band b = BandOf(family, band);
                var shape = new DeliveryShape(band, 0.10f, 0.30f);
                for (int i = 0; i < 1000; i++)
                {
                    DeliveryPicker.Pick p = DeliveryShape.Pick(shape, family, rand);
                    string at = $"{family} {band} #{i}";
                    Assert.AreEqual(band, p.band, at);
                    Assert.GreaterOrEqual(p.distance, b.minDistance - Eps, at);
                    Assert.LessOrEqual(p.distance, b.maxDistance + Eps, at);
                    Assert.AreEqual(BallDelivery.BatsmanStumpsX - p.pitchX, p.distance, Eps, at);
                    Assert.GreaterOrEqual(p.pitchX, BallDelivery.MinPitchX, at);
                    Assert.LessOrEqual(p.pitchX, BallDelivery.BatsmanStumpsX - BallDelivery.PoppingCreaseDistance + Eps, at);
                    Assert.GreaterOrEqual(p.line, 0.10f - Eps, at);
                    Assert.LessOrEqual(p.line, 0.30f + Eps, at);
                }
            }
    }

    [Test]
    public void ASpinnersShortBallIsShortForASpinner()
    {
        System.Func<float> rand = Seeded(11);
        var shape = new DeliveryShape(eLengthBand.Short, 0f, 0.1f);
        for (int i = 0; i < 1000; i++)
        {
            Assert.LessOrEqual(DeliveryShape.Pick(shape, eSwingType.OffSpin, rand).distance,
                               DeliveryPicker.SpinBands[4].maxDistance + Eps, "the spin table");
            Assert.GreaterOrEqual(DeliveryShape.Pick(shape, eSwingType.Pace, rand).distance,
                                  DeliveryPicker.PaceBands[4].minDistance - Eps, "the pace table");
        }
    }

    [Test]
    public void TheWindowNeverReachesAWide()
    {
        System.Func<float> rand = Seeded(7);
        var wild = new DeliveryShape(eLengthBand.GoodLength, -1.2f, 1.4f);
        float lo = 9f, hi = -9f;
        for (int i = 0; i < 1000; i++)
        {
            float line = DeliveryShape.Pick(wild, eSwingType.Pace, rand).line;
            lo = Mathf.Min(lo, line);
            hi = Mathf.Max(hi, line);
        }
        Assert.GreaterOrEqual(lo, DeliveryShape.MinLine - Eps);
        Assert.LessOrEqual(hi, DeliveryShape.MaxLine + Eps);
        Assert.Less(lo, DeliveryShape.MinLine + 0.05f, "the whole clamped window is used");
        Assert.Greater(hi, DeliveryShape.MaxLine - 0.05f, "the whole clamped window is used");
        // Off side: inside the off-side wide line (0.889 + a ball's radius); leg side: inside the
        // leg-side one (0.114 + a ball's radius).
        Assert.Less(DeliveryShape.MaxLine, 0.889f + BallFlight.Radius);
        Assert.Greater(DeliveryShape.MinLine, -(0.114f + BallFlight.Radius));
    }

    [Test]
    public void AWindowGivenBackwardsIsTheSameWindow()
    {
        System.Func<float> rand = Seeded(3);
        var back = new DeliveryShape(eLengthBand.Full, 0.40f, 0.20f);
        for (int i = 0; i < 500; i++)
        {
            float line = DeliveryShape.Pick(back, eSwingType.OutSwing, rand).line;
            Assert.GreaterOrEqual(line, 0.20f - Eps);
            Assert.LessOrEqual(line, 0.40f + Eps);
        }
    }

    [Test]
    public void TheSameDrawsGiveTheSamePick()
    {
        var shape = new DeliveryShape(eLengthBand.Yorker, -0.08f, 0.08f);
        DeliveryPicker.Pick a = DeliveryShape.Pick(shape, eSwingType.Pace, Seeded(42));
        DeliveryPicker.Pick b = DeliveryShape.Pick(shape, eSwingType.Pace, Seeded(42));
        Assert.AreEqual(a.pitchX, b.pitchX);
        Assert.AreEqual(a.line, b.line);
        // Draws in order: the length, then the line.
        DeliveryPicker.Pick lowHigh = DeliveryShape.Pick(shape, eSwingType.Pace, Sequence(0f, 1f));
        Assert.AreEqual(BallDelivery.BatsmanStumpsX - 1.3f, lowHigh.pitchX, Eps, "nearest the stumps the band goes");
        Assert.AreEqual(0.08f, lowHigh.line, Eps);
    }

    private static System.Func<float> Sequence(params float[] values)
    {
        int i = 0;
        return () => values[Mathf.Min(i++, values.Length - 1)];
    }

    private static BowlingParams Bowled() => new BowlingParams
    {
        swingType = eSwingType.InSwing, kind = eBowlerKind.Medium, delivery = eDelivery.InSwinger,
        speedX = 31f, length = 2.5f, lengthBand = eLengthBand.Short, speedZ = 1.2f, swing = 0.4f,
        pitchTurn = 0.1f, overHead = true, dip = 0.2f, bounceScale = 0.9f,
    };

    [Test]
    public void NoShapeChangesNothing()
    {
        BowlingParams p = Bowled();
        BowlingParams q = BowlingAttack.Shape(p, null, () => 0.5f);
        Assert.AreSame(p, q);
        Assert.AreEqual(2.5f, q.length);
        Assert.AreEqual(eLengthBand.Short, q.lengthBand);
        Assert.AreEqual(1.2f, q.speedZ);
        Assert.IsTrue(q.overHead);
        Assert.IsNull(BowlingAttack.Shape(null, new DeliveryShape(eLengthBand.Full, 0f, 0.1f), () => 0.5f));
    }

    [Test]
    public void AShapeSetsTheLengthAndLineAndNothingElse()
    {
        BowlingParams p = Bowled();
        var shape = new DeliveryShape(eLengthBand.Yorker, 0.30f, 0.55f);
        DeliveryPicker.Pick expect = DeliveryShape.Pick(shape, eSwingType.InSwing, Seeded(9));
        BowlingParams q = BowlingAttack.Shape(p, shape, Seeded(9));
        Assert.AreEqual(expect.pitchX, q.length, Eps);
        Assert.AreEqual(eLengthBand.Yorker, q.lengthBand);
        Assert.AreEqual(expect.line, q.speedZ, Eps);
        Assert.IsFalse(q.overHead, "a drill ball is never let go over the head");
        // The bowler's own ball otherwise: pace, movement, who and what.
        Assert.AreEqual(31f, q.speedX);
        Assert.AreEqual(0.4f, q.swing);
        Assert.AreEqual(0.1f, q.pitchTurn);
        Assert.AreEqual(0.2f, q.dip);
        Assert.AreEqual(0.9f, q.bounceScale);
        Assert.AreEqual(eBowlerKind.Medium, q.kind);
        Assert.AreEqual(eDelivery.InSwinger, q.delivery);
        Assert.AreEqual(eSwingType.InSwing, q.swingType);
    }

    [Test]
    public void ForceShapeShapesTheNextBallOnce()
    {
        var profiles = new BowlingProfileManager();
        var shape = new DeliveryShape(eLengthBand.Yorker, -0.05f, 0.05f);
        BowlingAttack.ForceKind = eBowlerKind.Fast;
        BowlingAttack.ForceDelivery = eDelivery.Stock;
        BowlingAttack.ForceShape = shape;
        BowlingParams p = BowlingAttack.Next(null, profiles);
        Assert.IsNull(BowlingAttack.ForceShape, "used once");
        Assert.AreEqual(eLengthBand.Yorker, p.lengthBand);
        DeliveryPicker.Band y = DeliveryPicker.PaceBands[0];
        Assert.GreaterOrEqual(BallDelivery.BatsmanStumpsX - p.length, y.minDistance - Eps);
        Assert.LessOrEqual(BallDelivery.BatsmanStumpsX - p.length, y.maxDistance + Eps);
        Assert.GreaterOrEqual(p.speedZ, -0.05f - Eps);
        Assert.LessOrEqual(p.speedZ, 0.05f + Eps);
        Assert.IsFalse(p.overHead);
        Assert.AreEqual(eBowlerKind.Fast, p.kind);
        Assert.AreEqual(eDelivery.Stock, p.delivery);
    }
}
