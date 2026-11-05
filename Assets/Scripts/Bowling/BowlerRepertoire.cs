using System.Collections.Generic;
using UnityEngine;

/// What kind of bowler: two kinds of seamer, three of spinner.
public enum eBowlerKind { Fast, Medium, OffSpin, LegSpin, Mystery }

/// <summary>
/// What a delivery is, as the bowler means it. Seamers: the stock ball (seam upright, straight but
/// for a little nip either way off the seam), the swingers, the cutters, the slower ball. Spinners:
/// the stock spinning ball (off-break, leg-break), the top-spinner, the slider (the off-spinner's arm
/// ball, the wrist-spinner's flipper), the flighted slower ball that turns more, the googly, and the
/// mystery spinner's carrom ball.
/// </summary>
public enum eDelivery
{
    Stock,
    InSwinger, OutSwinger, OffCutter, LegCutter, SlowerBall,
    OffBreak, LegBreak, TopSpinner, Slider, Flighted, Googly, Carrom,
}

/// <summary>
/// Each kind of bowler's repertoire (2026-11-05: "it shouldn't be a decided type of order"):
///
///   * Fast and Medium both swing it, cut it and bowl slower balls - but a quick is likelier to cut
///     it than swing it, and a medium-pacer the other way round (VariationWeights).
///   * The off-spinner's stock ball is the off-break; his variations the top-spinner, the arm ball
///     (quicker, straight on, skids) and the flighted ball (slower, loops, turns more).
///   * The leg-spinner's is the leg-break; his the googly, the top-spinner, the flipper and the
///     flighted ball.
///   * The mystery spinner has no stock ball: off-break, leg-break, googly or carrom ball, any of
///     them any ball, at a pace that varies more than anyone's.
///
/// Which balls of an over are stock and which are the variations is the over plan's (OverPlanner):
/// a few variations an over, never the same one twice running, more of them the better the attack
/// (MatchSetup's bowling level), and the slower ball more often when the plan says so (death
/// overs). Pure: randomness is passed in, so EditMode tests can measure it.
/// </summary>
public static class BowlerRepertoire
{
    public static bool IsSpin(eBowlerKind k) => k == eBowlerKind.OffSpin || k == eBowlerKind.LegSpin || k == eBowlerKind.Mystery;

    /// The physics family and the rest of the game's view of the bowler: where the keeper stands,
    /// the run-up and action, the field plans (eSwingType is what all of those already key on).
    public static eSwingType BaseType(eBowlerKind k)
    {
        switch (k)
        {
            case eBowlerKind.Fast: return eSwingType.Pace;
            case eBowlerKind.Medium: return eSwingType.OutSwing;   // a medium-pacer's keeper position and action
            case eBowlerKind.LegSpin: return eSwingType.LegSpin;
            default: return eSwingType.OffSpin;                    // off-spinner, mystery spinner (finger-spin action)
        }
    }

    public static eDelivery StockBall(eBowlerKind k) =>
        k == eBowlerKind.OffSpin ? eDelivery.OffBreak : k == eBowlerKind.LegSpin ? eDelivery.LegBreak : eDelivery.Stock;

    public struct Weighted
    {
        public eDelivery delivery;
        public float weight;
        public Weighted(eDelivery d, float w) { delivery = d; weight = w; }
    }

    /// The variations, and how often each is the one picked when a variation is due.
    public static readonly Weighted[] FastVariations =
    {
        new Weighted(eDelivery.OffCutter, 0.25f),
        new Weighted(eDelivery.LegCutter, 0.20f),
        new Weighted(eDelivery.InSwinger, 0.13f),
        new Weighted(eDelivery.OutSwinger, 0.12f),
        new Weighted(eDelivery.SlowerBall, 0.30f),
    };
    public static readonly Weighted[] MediumVariations =
    {
        new Weighted(eDelivery.OutSwinger, 0.30f),
        new Weighted(eDelivery.InSwinger, 0.25f),
        new Weighted(eDelivery.OffCutter, 0.10f),
        new Weighted(eDelivery.LegCutter, 0.10f),
        new Weighted(eDelivery.SlowerBall, 0.25f),
    };
    public static readonly Weighted[] OffSpinVariations =
    {
        new Weighted(eDelivery.TopSpinner, 0.30f),
        new Weighted(eDelivery.Slider, 0.35f),
        new Weighted(eDelivery.Flighted, 0.35f),
    };
    public static readonly Weighted[] LegSpinVariations =
    {
        new Weighted(eDelivery.Googly, 0.30f),
        new Weighted(eDelivery.TopSpinner, 0.25f),
        new Weighted(eDelivery.Slider, 0.20f),
        new Weighted(eDelivery.Flighted, 0.25f),
    };
    /// Every ball of the mystery spinner's is one of these (no stock ball).
    public static readonly Weighted[] MysteryBalls =
    {
        new Weighted(eDelivery.OffBreak, 0.28f),
        new Weighted(eDelivery.LegBreak, 0.22f),
        new Weighted(eDelivery.Googly, 0.16f),
        new Weighted(eDelivery.Carrom, 0.22f),
        new Weighted(eDelivery.TopSpinner, 0.12f),
    };

    public static Weighted[] VariationWeights(eBowlerKind k)
    {
        switch (k)
        {
            case eBowlerKind.Fast: return FastVariations;
            case eBowlerKind.Medium: return MediumVariations;
            case eBowlerKind.OffSpin: return OffSpinVariations;
            case eBowlerKind.LegSpin: return LegSpinVariations;
            default: return MysteryBalls;
        }
    }

    /// Variations an over, on average, by bowling level (Easy .. Pro): a club bowler mostly bowls
    /// his stock ball; a professional mixes it up two or three times an over.
    public static readonly float[] VariationsPerOver = { 1.2f, 1.8f, 2.4f, 2.9f };

    public static float VariationsFor(eBowlingLevel level) =>
        VariationsPerOver[Mathf.Clamp((int)level, 0, VariationsPerOver.Length - 1)];

    public static string Name(eDelivery d, eBowlerKind k)
    {
        switch (d)
        {
            case eDelivery.Stock: return "Stock ball";
            case eDelivery.InSwinger: return "Inswinger";
            case eDelivery.OutSwinger: return "Outswinger";
            case eDelivery.OffCutter: return "Off-cutter";
            case eDelivery.LegCutter: return "Leg-cutter";
            case eDelivery.SlowerBall: return "Slower ball";
            case eDelivery.OffBreak: return "Off-break";
            case eDelivery.LegBreak: return "Leg-break";
            case eDelivery.TopSpinner: return "Top-spinner";
            case eDelivery.Slider: return k == eBowlerKind.LegSpin ? "Flipper" : "Arm ball";
            case eDelivery.Flighted: return "Flighted";
            case eDelivery.Googly: return "Googly";
            case eDelivery.Carrom: return "Carrom ball";
        }
        return d.ToString();
    }

    public static string KindName(eBowlerKind k)
    {
        switch (k)
        {
            case eBowlerKind.Fast: return "Fast";
            case eBowlerKind.Medium: return "Medium pace";
            case eBowlerKind.OffSpin: return "Off-spin";
            case eBowlerKind.LegSpin: return "Leg-spin";
            default: return "Mystery spin";
        }
    }

    public static eDelivery PickFrom(Weighted[] table, System.Func<float> rand01, eDelivery avoid, float slowerBoost = 0f)
    {
        float total = 0f;
        foreach (Weighted w in table)
            total += Weight(w, avoid, slowerBoost);
        if (total <= 0f)
            return table[0].delivery;
        float r = Mathf.Clamp01(rand01()) * total;
        foreach (Weighted w in table)
        {
            float x = Weight(w, avoid, slowerBoost);
            if (r < x)
                return w.delivery;
            r -= x;
        }
        return table[table.Length - 1].delivery;
    }

    private static float Weight(Weighted w, eDelivery avoid, float slowerBoost) =>
        w.delivery == avoid ? 0f : w.weight + (w.delivery == eDelivery.SlowerBall || w.delivery == eDelivery.Flighted ? slowerBoost : 0f);
}

/// <summary>
/// One bowler's over: which balls are his stock ball and which a variation, decided when the over
/// starts, then nudged ball by ball - after a boundary a bowler changes it up more often than not.
/// </summary>
public class OverPlanner
{
    /// Chance the next ball is a variation after the last one went for four or six, if it was not one
    /// already.
    public const float ChangeUpAfterBoundary = 0.45f;
    /// The first ball of an over is less often the variation (a bowler finds his rhythm).
    public const float FirstBallWeight = 0.5f;

    private readonly eBowlerKind kind;
    private readonly bool[] varied = new bool[6];
    private eDelivery last = eDelivery.Stock;

    public eBowlerKind Kind => kind;
    /// Which of the six balls are planned as variations (for tests and the debug overlay).
    public bool IsVariation(int ball) => ball >= 0 && ball < varied.Length && varied[ball];

    public OverPlanner(eBowlerKind kind, float variationsPerOver, System.Func<float> rand01)
    {
        this.kind = kind;
        // How many this over: the mean, rounded up or down at random so it averages out right.
        int n = Mathf.FloorToInt(variationsPerOver) + (rand01() < variationsPerOver - Mathf.Floor(variationsPerOver) ? 1 : 0);
        n = Mathf.Clamp(n, 0, 6);
        for (int k = 0; k < n; k++)
        {
            // A free ball, the first one weighted down.
            float total = 0f;
            for (int i = 0; i < 6; i++)
                if (!varied[i]) total += i == 0 ? FirstBallWeight : 1f;
            float r = rand01() * total;
            for (int i = 0; i < 6; i++)
            {
                if (varied[i]) continue;
                float w = i == 0 ? FirstBallWeight : 1f;
                if (r < w || i == 5) { varied[i] = true; break; }
                r -= w;
            }
        }
    }

    /// The delivery for this ball of the over (0..5; extra balls after wides and no-balls are
    /// 6 and on). lastWasBoundary: the previous ball went for four or six. slowerBoost: the plan's
    /// extra weight on the slower ball (death overs), 0 normally.
    public eDelivery Next(int ball, bool lastWasBoundary, float slowerBoost, System.Func<float> rand01)
    {
        bool vary;
        if (kind == eBowlerKind.Mystery)
            vary = true;                                   // every ball is one of his
        else if (ball < varied.Length)
            vary = varied[ball] || (lastWasBoundary && rand01() < ChangeUpAfterBoundary);
        else
            vary = rand01() < 0.3f;                        // an extra ball after a wide or no-ball
        eDelivery d = vary
            ? BowlerRepertoire.PickFrom(BowlerRepertoire.VariationWeights(kind), rand01,
                                        kind == eBowlerKind.Mystery ? eDelivery.Stock : last, slowerBoost)
            : BowlerRepertoire.StockBall(kind);
        last = d;
        return d;
    }
}
