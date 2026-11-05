using UnityEngine;

/// Where a ball pitches, by the names a commentator would use.
public enum eLengthBand { Yorker, Full, GoodLength, BackOfALength, Short }

/// <summary>
/// Picks the line and length of a delivery. Pure (randomness is passed in), so it can be tested
/// without a scene.
///
/// Length is a weighted choice of band, then uniform within it. Distances are metres from the
/// batsman's stumps back to where the ball pitches. Pace bowlers mostly hit a good length - the
/// batter can't tell whether to go forward or back - with enough full balls, yorkers, and short
/// ones to keep them honest. Spinners pitch it up: a spinner dropping short gets cut away.
///
/// Every pick pitches before the popping crease (never a full toss, so never a beamer). The line
/// is not always legal: a few go down the leg side or too wide outside off and are called wides
/// at the stumps (WideCollider), as real bowlers' are - about one ball in twenty with the
/// default (full) line range.
/// </summary>
public static class DeliveryPicker
{
    public struct Band
    {
        public eLengthBand band;
        public float minDistance, maxDistance, weight;
        public Band(eLengthBand b, float min, float max, float w) { band = b; minDistance = min; maxDistance = max; weight = w; }
    }

    /// Line window at the stumps, metres from middle stump, off side positive.
    public struct Channel
    {
        public float minLine, maxLine, weight;
        public Channel(float min, float max, float w) { minLine = min; maxLine = max; weight = w; }
    }

    public struct Pick
    {
        public eLengthBand band;
        /// Metres from the batsman's stumps to the pitching point.
        public float distance;
        /// World X the ball pitches at - what BowlingParams.length and BallDelivery take.
        public float pitchX;
        /// Line at the stumps, off side positive.
        public float line;
    }

    /// Yorkers pitch at the batter's toes, 1.3 m out - just ahead of the popping crease (1.22 m),
    /// so even the fullest ball bounces before it reaches the batter.
    /// Weights spread wider than a stock-ball-heavy attack (good length was 40%, now 29%): the
    /// batter should not be able to settle on one length.
    public static readonly Band[] PaceBands =
    {
        new Band(eLengthBand.Yorker,        1.3f,  2f,  0.12f),
        new Band(eLengthBand.Full,          2f,    4f,  0.22f),
        new Band(eLengthBand.GoodLength,    4f,    7f,  0.29f),
        new Band(eLengthBand.BackOfALength, 7f,    9f,  0.21f),
        new Band(eLengthBand.Short,         9f,   12.5f, 0.16f),
    };
    public static readonly Band[] SpinBands =
    {
        new Band(eLengthBand.Yorker,        1.3f, 1.8f, 0.07f),
        new Band(eLengthBand.Full,          1.8f, 3.5f, 0.28f),
        new Band(eLengthBand.GoodLength,    3.5f, 5.5f, 0.38f),
        new Band(eLengthBand.BackOfALength, 5.5f, 6.8f, 0.17f),
        new Band(eLengthBand.Short,         6.8f, 8.5f, 0.10f),
    };

    /// Stumps span +-0.114. The corridor - off stump to a bat's width outside it - is where good
    /// bowlers live; some attack the stumps or leg stump; and some miss. The wide lines
    /// (WideCollider) are the off-side guideline 0.889 m and the outside of leg stump 0.114 m,
    /// each plus a ball's width, so "down leg" is part leg-side wides and "wide outside off" is
    /// nearly all off-side wides. Wides by the weights below: ~4% off side, ~4% leg side - about
    /// one an over between them - before a plan (BowlingPlans) moves them (2026-11-03: none at all
    /// at first, then ~2.5% each side, both too few for the player). The order is the order of
    /// BowlingPlan.lineWeights.
    public static readonly Channel[] Channels =
    {
        new Channel( 0.10f,  0.50f, 0.45f),   // corridor of uncertainty
        new Channel(-0.03f,  0.10f, 0.26f),   // at the stumps
        new Channel(-0.10f, -0.03f, 0.09f),   // on leg stump
        new Channel( 0.50f,  0.90f, 0.08f),   // well outside off: a leave
        new Channel(-0.50f, -0.13f, 0.045f),  // down the leg side, mostly wides
        new Channel( 0.95f,  1.45f, 0.04f),   // wide outside off
    };

    /// The line slider runs +-LineLimit (twice the old +-0.75 m) and the line never goes past it.
    public const float LineLimit = 1.5f;
    public const float MaxOffLine = LineLimit;
    public const float MaxLegLine = -LineLimit;

    public static bool IsSpin(eSwingType t) => t == eSwingType.LegSpin || t == eSwingType.OffSpin;

    public static Band[] BandsFor(eSwingType t) => IsSpin(t) ? SpinBands : PaceBands;

    /// <param name="minLine">The profile's line range (the Z sliders), which narrows the channels.
    /// The legal window always wins over it.</param>
    /// <param name="rand01">Uniform random in [0, 1].</param>
    /// <param name="plan">The bowler's plan (BowlingPlans), which re-weights the bands and the
    /// line channels; null for plain bowling.</param>
    public static Pick PickDelivery(eSwingType type, float minLine, float maxLine, System.Func<float> rand01,
                                    BowlingPlan plan = null)
    {
        Band[] bands = BandsFor(type);
        Band b = bands[PickIndex(bands.Length, i => bands[i].weight * (plan != null ? plan.lengthWeights[i] : 1f), rand01())];
        float distance = Mathf.Lerp(b.minDistance, b.maxDistance, rand01());
        // Keep inside what BallDelivery will accept, so it never has to clamp (and move) the ball.
        float pitchX = Mathf.Clamp(BallDelivery.BatsmanStumpsX - distance, BallDelivery.MinPitchX,
                                   BallDelivery.BatsmanStumpsX - BallDelivery.PoppingCreaseDistance);
        return new Pick
        {
            band = b.band,
            distance = BallDelivery.BatsmanStumpsX - pitchX,
            pitchX = pitchX,
            line = PickLine(minLine, maxLine, rand01, plan),
        };
    }

    public static float PickLine(float minLine, float maxLine, System.Func<float> rand01, BowlingPlan plan = null)
    {
        float lo = Mathf.Max(Mathf.Min(minLine, maxLine), MaxLegLine);
        float hi = Mathf.Min(Mathf.Max(minLine, maxLine), MaxOffLine);
        // Sliders set entirely outside the legal window: bowl the nearest legal line.
        if (hi < lo)
            return Mathf.Clamp(0.5f * (minLine + maxLine), MaxLegLine, MaxOffLine);
        if (hi - lo < 1e-4f)
            return lo;

        // Only channels the slider range overlaps can be chosen, each clipped to that range.
        int c = PickIndex(Channels.Length, i => Overlap(Channels[i], lo, hi) > 0f
                                                    ? Channels[i].weight * (plan != null ? plan.lineWeights[i] : 1f) : 0f, rand01());
        if (c < 0)
            return Mathf.Lerp(lo, hi, rand01());
        float from = Mathf.Max(Channels[c].minLine, lo), to = Mathf.Min(Channels[c].maxLine, hi);
        return Mathf.Lerp(from, to, rand01());
    }

    static float Overlap(Channel ch, float lo, float hi) =>
        Mathf.Min(ch.maxLine, hi) - Mathf.Max(ch.minLine, lo);

    /// Weighted choice; r in [0, 1]. Returns -1 when every weight is zero.
    static int PickIndex(int count, System.Func<int, float> weight, float r)
    {
        float total = 0f;
        int last = -1;
        for (int i = 0; i < count; i++)
        {
            float w = weight(i);
            if (w <= 0f) continue;
            total += w;
            last = i;
        }
        if (last < 0) return -1;
        float target = Mathf.Clamp01(r) * total;
        for (int i = 0; i < count; i++)
        {
            float w = weight(i);
            if (w <= 0f) continue;
            if (target < w) return i;
            target -= w;
        }
        return last;   // r == 1, or float rounding
    }
}
