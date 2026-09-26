using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Line and length of the bowling (DeliveryPicker), plus the solver's reach over those lengths
/// (BallDelivery). Standalone - no scene, no PhysX. The picker is fed a seeded generator so a
/// failure reproduces exactly.
///
/// What "good bowling" means here: every length band turns up, lengths spread from yorker to
/// bouncer, and not one ball is a wide, a full toss, or over the batter's head.
/// Run: Window > General > Test Runner > EditMode > BowlingLengthTests.
/// </summary>
public class BowlingLengthTests
{
    private const int Samples = 2000;
    /// Off-side wide guideline and the outside of leg stump, from middle stump (see WideCollider).
    private const float OffWideLine = 0.889f;
    private const float LegWideLine = 0.1143f;
    private const float BallRadius = 0.036f;

    private static readonly eSwingType[] AllTypes =
        { eSwingType.Pace, eSwingType.InSwing, eSwingType.OutSwing, eSwingType.LegSpin, eSwingType.OffSpin };

    private static List<DeliveryPicker.Pick> Sample(eSwingType type, float minLine, float maxLine, int seed)
    {
        var rng = new System.Random(seed);
        var picks = new List<DeliveryPicker.Pick>(Samples);
        for (int i = 0; i < Samples; i++)
            picks.Add(DeliveryPicker.PickDelivery(type, minLine, maxLine, () => (float)rng.NextDouble()));
        return picks;
    }

    private static Dictionary<eLengthBand, int> CountBands(List<DeliveryPicker.Pick> picks)
    {
        var counts = new Dictionary<eLengthBand, int>();
        foreach (var p in picks)
            counts[p.band] = counts.TryGetValue(p.band, out int n) ? n + 1 : 1;
        return counts;
    }

    [Test]
    public void EveryBandOccurs([ValueSource(nameof(AllTypes))] eSwingType type)
    {
        var counts = CountBands(Sample(type, -1f, 1f, 11));
        foreach (var band in DeliveryPicker.BandsFor(type))
        {
            counts.TryGetValue(band.band, out int n);
            Assert.Greater(n, 0, $"{type}: no {band.band} in {Samples} balls");
        }
    }

    [Test]
    public void GoodLengthIsTheStockBall([ValueSource(nameof(AllTypes))] eSwingType type)
    {
        var counts = CountBands(Sample(type, -1f, 1f, 12));
        int good = counts[eLengthBand.GoodLength];
        foreach (var kv in counts)
            Assert.GreaterOrEqual(good, kv.Value, $"{type}: {kv.Key} ({kv.Value}) outnumbers good length ({good})");
        // Not all good length either - the player asked for variety.
        Assert.Less(good, Samples * 0.7f, $"{type}: {good} of {Samples} on a good length is too samey");
    }

    [Test]
    public void EachBallLandsInsideItsBand([ValueSource(nameof(AllTypes))] eSwingType type)
    {
        var bands = new Dictionary<eLengthBand, DeliveryPicker.Band>();
        foreach (var b in DeliveryPicker.BandsFor(type)) bands[b.band] = b;
        foreach (var p in Sample(type, -1f, 1f, 13))
        {
            var b = bands[p.band];
            Assert.That(p.distance, Is.InRange(b.minDistance - 1e-4f, b.maxDistance + 1e-4f), $"{type} {p.band}");
            Assert.AreEqual(BallDelivery.BatsmanStumpsX - p.distance, p.pitchX, 1e-4f);
        }
    }

    [Test]
    public void PaceLengthsSpanYorkerToBouncer()
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var p in Sample(eSwingType.Pace, -1f, 1f, 14))
        {
            min = Mathf.Min(min, p.distance);
            max = Mathf.Max(max, p.distance);
        }
        Assert.GreaterOrEqual(max - min, 8f, $"pace lengths only span {min:F2}..{max:F2} m");
    }

    [Test]
    public void NeverAFullTossOrOffTheStrip([ValueSource(nameof(AllTypes))] eSwingType type)
    {
        foreach (var p in Sample(type, -1f, 1f, 15))
        {
            Assert.GreaterOrEqual(p.distance, BallDelivery.PoppingCreaseDistance, $"{type} pitches past the crease");
            Assert.That(p.pitchX, Is.InRange(BallDelivery.MinPitchX, BallDelivery.MaxPitchX), $"{type} pitch x");
        }
    }

    /// Wide sliders, narrow sliders, sliders set outside the legal window, and the shipped configs.
    private static IEnumerable<float[]> LineRanges()
    {
        yield return new[] { -1f, 1f };
        yield return new[] { -3f, 3f };
        yield return new[] { 0.05f, 0.40f };
        yield return new[] { 0.9f, 1.5f };
        yield return new[] { -0.6f, -0.3f };
        yield return new[] { 0.2f, 0.2f };
        yield return new[] { 0.3f, -0.05f };   // reversed
        foreach (var cfg in new[] { Constants.paceCfg, Constants.inSwingCfg, Constants.outSwingCfg,
                                    Constants.legSpinCfg, Constants.offSpinCfg })
            yield return new[] { cfg[4], cfg[5] };
    }

    [Test]
    public void EveryLineIsLegal([ValueSource(nameof(LineRanges))] float[] range)
    {
        foreach (var type in AllTypes)
            foreach (var p in Sample(type, range[0], range[1], 16))
            {
                Assert.That(p.line, Is.InRange(DeliveryPicker.MaxLegLine, DeliveryPicker.MaxOffLine),
                            $"{type} line {p.line:F3} for sliders {range[0]}..{range[1]}");
                // The whole ball well inside the off-side guideline, and touching leg stump at worst.
                Assert.Less(p.line + BallRadius, OffWideLine - 0.2f);
                Assert.Greater(p.line + BallRadius, -LegWideLine);
            }
    }

    [Test]
    public void LineSlidersNarrowTheChannels()
    {
        foreach (var p in Sample(eSwingType.OutSwing, 0.05f, 0.40f, 17))
            Assert.That(p.line, Is.InRange(0.05f, 0.40f));
    }

    [Test]
    public void LinesUseTheCorridorMost()
    {
        int corridor = 0, legStump = 0;
        foreach (var p in Sample(eSwingType.Pace, -1f, 1f, 18))
        {
            if (p.line >= 0.10f) corridor++;
            if (p.line < -0.03f) legStump++;
        }
        Assert.Greater(corridor, Samples / 2, "most balls should be in the corridor outside off");
        Assert.Greater(legStump, 0, "some balls should attack leg stump");
    }

    // ---- The solver has to be able to bowl what the picker asks for ------------------------

    private static readonly float[] ReleaseHeights = { 0.63f, 1.2f, 2.1f, 3.0f };
    private static readonly float[] PaceSpeeds = { 34.5f, 39f, 43f };

    [Test]
    public void SolverHitsEveryPaceLength()
    {
        foreach (float h in ReleaseHeights)
            foreach (float v in PaceSpeeds)
                foreach (float d in new[] { 1.3f, 2f, 4f, 7f, 9f, 12f })
                {
                    var sol = BallDelivery.Solve(new Vector3(-8.95f, h, 0f), BallDelivery.BatsmanStumpsX - d, v,
                                                 0.2f, BallFlight.DeliveryEffects.None);
                    Assert.Less(sol.heightAtStumps, BallDelivery.MaxHeightAtStumps + 0.05f,
                                $"release {h} m, {v} m/s, {d} m: {sol}");
                    Assert.AreEqual(0.2f, sol.lineZ, 0.05f, $"line missed: {sol}");
                    // Pitch exactly where asked, unless the bouncer guard pulled it fuller.
                    if (!sol.warnings.Contains("bouncer"))
                        Assert.AreEqual(BallDelivery.BatsmanStumpsX - d, sol.pitchX, 0.1f, $"length missed: {sol}");
                    Assert.Less(sol.pitchX, BallDelivery.BatsmanStumpsX - BallDelivery.PoppingCreaseDistance + 0.1f);
                }
    }

    [Test]
    public void SolverHitsEverySpinLength()
    {
        foreach (float h in ReleaseHeights)
            foreach (float v in new[] { 19f, 23.5f })
                foreach (float d in new[] { 1.5f, 3.5f, 5.5f, 7f })
                {
                    var sol = BallDelivery.Solve(new Vector3(-8.95f, h, 0f), BallDelivery.BatsmanStumpsX - d, v,
                                                 0.1f, BallFlight.DeliveryEffects.None);
                    Assert.AreEqual(BallDelivery.BatsmanStumpsX - d, sol.pitchX, 0.1f, $"length missed: {sol}");
                    Assert.AreEqual(0.1f, sol.lineZ, 0.05f, $"line missed: {sol}");
                }
    }

    [Test]
    public void BouncerFromAForwardHighReleaseStaysUnderHeadHeight()
    {
        // Release box corner: as far forward and as high as the solver allows.
        var sol = BallDelivery.Solve(new Vector3(-5f, 3f, 0f), BallDelivery.BatsmanStumpsX - 12f, 43f,
                                     0f, BallFlight.DeliveryEffects.None);
        Assert.Less(sol.heightAtStumps, BallDelivery.MaxHeightAtStumps + 0.05f, sol.ToString());
        Assert.Greater(sol.pitchX, BallDelivery.MinPitchX, sol.ToString());
    }
}
