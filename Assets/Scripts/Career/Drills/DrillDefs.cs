using System;
using B = eLengthBand;
using D = eDelivery;
using K = eBowlerKind;

/// <summary>
/// The five training drills, as data: twelve balls each, every one a ball that bowler really bowls
/// (NetsTrialPlan.Valid), at the length band and in the line window the drill is about. The window
/// is metres from middle stump at the stumps, batter's frame, off side + (DeliveryShape), always
/// inside [DeliveryShape.MinLine, MaxLine] so a drill ball is never a wide by line.
///
///   Yorkers            Defence, vs Pace. Every ball at the toes, on the stumps or wide of off.
///   Leave outside off  Judgement, Defence. Eight wide of off stump to leave, four around off
///                      stump (good length, in the corridor) to play.
///   Short ball         vs Pace, Power, Judgement. Bouncers and back of a length; three short and
///                      wide (loose balls, so Judgement sees them).
///   Spin               vs Spin, Timing, Defence. Six from the off-spinner, six from the leg-spinner.
///   Hitting the gaps   Timing, Power, vs Pace, vs Spin. Six from a medium-pacer to the off-stump
///                      field, then six from the off-spinner to the darts field (BowlingPlans).
///
/// The order is not fixed (a drill learnt by heart measures nothing): each attempt is shuffled with
/// its own seed (Plan, SeedFor), within blocks - the gaps drill keeps its seamer's half before its
/// spinner's, as the field changes between them. A restart of the scene keeps the attempt, so it
/// bowls the same plan. Pure: DrillTests checks it all.
/// </summary>
public static class DrillDefs
{
    public const int BallsPerDrill = 12, Count = 5;

    // RatingSet indexes.
    private const int Timing = 0, Power = 1, Pace = 2, Spin = 3, Defence = 4, Judgement = 5;

    private static DrillBall Ball(K k, D d, B b, float min, float max) => new DrillBall(k, d, b, min, max);

    private static readonly DrillDef Yorkers = new DrillDef
    {
        id = eDrill.Yorkers, name = "Yorkers", title = "YORKER DRILL",
        blurb = "Twelve at the toes from the quicks. Dig out the straight ones and leave the wide ones.",
        feeds = new[] { Defence, Pace },
        balls = new[]
        {
            Ball(K.Fast, D.Stock, B.Yorker, -0.08f, 0.08f),
            Ball(K.Fast, D.InSwinger, B.Yorker, -0.02f, 0.10f),
            Ball(K.Fast, D.Stock, B.Yorker, 0.30f, 0.55f),
            Ball(K.Fast, D.SlowerBall, B.Full, -0.05f, 0.10f),
            Ball(K.Fast, D.Stock, B.Yorker, -0.08f, 0.08f),
            Ball(K.Fast, D.OffCutter, B.Yorker, -0.05f, 0.08f),
            Ball(K.Medium, D.Stock, B.Yorker, -0.08f, 0.08f),
            Ball(K.Medium, D.OutSwinger, B.Yorker, 0f, 0.15f),
            Ball(K.Medium, D.Stock, B.Yorker, 0.30f, 0.55f),
            Ball(K.Medium, D.InSwinger, B.Yorker, -0.05f, 0.08f),
            Ball(K.Fast, D.Stock, B.Yorker, -0.08f, 0.08f),
            Ball(K.Fast, D.SlowerBall, B.Yorker, -0.05f, 0.10f),
        },
    };

    private static readonly DrillDef Leave = new DrillDef
    {
        id = eDrill.LeaveOutsideOff, name = "Leave outside off", title = "LEAVING DRILL",
        blurb = "Eight wide of off stump to leave alone, four around off stump to play. Know where your off stump is.",
        feeds = new[] { Judgement, Defence },
        balls = new[]
        {
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0.45f, 0.80f),
            Ball(K.Medium, D.Stock, B.GoodLength, 0.50f, 0.85f),
            Ball(K.Medium, D.InSwinger, B.GoodLength, 0f, 0.15f),
            Ball(K.Medium, D.OutSwinger, B.BackOfALength, 0.45f, 0.80f),
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0.10f, 0.25f),
            Ball(K.Medium, D.Stock, B.BackOfALength, 0.55f, 0.85f),
            Ball(K.Fast, D.Stock, B.GoodLength, 0.45f, 0.80f),
            Ball(K.Fast, D.OutSwinger, B.GoodLength, 0.50f, 0.85f),
            Ball(K.Fast, D.Stock, B.GoodLength, -0.05f, 0.10f),
            Ball(K.Fast, D.LegCutter, B.BackOfALength, 0.45f, 0.80f),
            Ball(K.Fast, D.InSwinger, B.GoodLength, 0f, 0.12f),
            Ball(K.Fast, D.Stock, B.BackOfALength, 0.50f, 0.85f),
        },
    };

    private static readonly DrillDef ShortBall = new DrillDef
    {
        id = eDrill.ShortBall, name = "Short ball", title = "SHORT-BALL DRILL",
        blurb = "Bouncers and short balls from the quicks. Pull it, cut the wide one, or get out of the way.",
        feeds = new[] { Pace, Power, Judgement },
        balls = new[]
        {
            Ball(K.Fast, D.Stock, B.Short, -0.08f, 0.12f),
            Ball(K.Fast, D.Stock, B.Short, -0.05f, 0.15f),
            Ball(K.Fast, D.Stock, B.BackOfALength, 0f, 0.20f),
            Ball(K.Fast, D.Stock, B.Short, 0.40f, 0.70f),
            Ball(K.Fast, D.Stock, B.Short, -0.08f, 0.10f),
            Ball(K.Fast, D.SlowerBall, B.Short, -0.05f, 0.12f),
            Ball(K.Medium, D.Stock, B.Short, -0.05f, 0.15f),
            Ball(K.Medium, D.Stock, B.Short, 0.40f, 0.70f),
            Ball(K.Fast, D.Stock, B.Short, -0.08f, 0.10f),
            Ball(K.Fast, D.OffCutter, B.BackOfALength, 0f, 0.15f),
            Ball(K.Fast, D.Stock, B.Short, 0.45f, 0.75f),
            Ball(K.Fast, D.Stock, B.Short, -0.05f, 0.12f),
        },
    };

    private static readonly DrillDef SpinDrill = new DrillDef
    {
        id = eDrill.Spin, name = "Spin", title = "SPIN DRILL",
        blurb = "Six from the off-spinner, six from the leg-spinner. Play with the turn, use your feet, try the sweep.",
        feeds = new[] { Spin, Timing, Defence },
        balls = new[]
        {
            Ball(K.OffSpin, D.OffBreak, B.Full, 0.10f, 0.35f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, -0.05f, 0.10f),
            Ball(K.OffSpin, D.Flighted, B.Full, 0.20f, 0.45f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, -0.10f, 0.05f),
            Ball(K.OffSpin, D.Slider, B.GoodLength, -0.05f, 0.10f),
            Ball(K.OffSpin, D.OffBreak, B.BackOfALength, 0.10f, 0.30f),
            Ball(K.LegSpin, D.LegBreak, B.GoodLength, 0f, 0.20f),
            Ball(K.LegSpin, D.LegBreak, B.Full, 0.15f, 0.40f),
            Ball(K.LegSpin, D.Flighted, B.Full, 0.10f, 0.35f),
            Ball(K.LegSpin, D.Googly, B.GoodLength, 0.05f, 0.20f),
            Ball(K.LegSpin, D.LegBreak, B.GoodLength, -0.10f, 0.05f),
            Ball(K.LegSpin, D.TopSpinner, B.GoodLength, -0.05f, 0.10f),
        },
    };

    private static readonly DrillDef Gaps = new DrillDef
    {
        id = eDrill.Gaps, name = "Hitting the gaps", title = "GAPS DRILL",
        blurb = "A field is set. Six from a seamer, then six from a spinner: beat the fielders and keep it on the ground.",
        feeds = new[] { Timing, Power, Pace, Spin },
        blockSize = 6,
        paceField = BowlingPlans.OffStump.field,
        spinField = BowlingPlans.Darts.field,
        balls = new[]
        {
            Ball(K.Medium, D.Stock, B.GoodLength, 0.10f, 0.30f),
            Ball(K.Medium, D.Stock, B.Full, 0.05f, 0.25f),
            Ball(K.Medium, D.Stock, B.BackOfALength, 0.10f, 0.35f),
            Ball(K.Medium, D.InSwinger, B.Full, -0.05f, 0.10f),
            Ball(K.Medium, D.Stock, B.GoodLength, -0.05f, 0.10f),
            Ball(K.Medium, D.OutSwinger, B.Full, 0.20f, 0.45f),
            Ball(K.OffSpin, D.OffBreak, B.Full, 0.05f, 0.25f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, -0.05f, 0.15f),
            Ball(K.OffSpin, D.Flighted, B.Full, 0.10f, 0.30f),
            Ball(K.OffSpin, D.Slider, B.GoodLength, 0f, 0.15f),
            Ball(K.OffSpin, D.OffBreak, B.BackOfALength, 0.10f, 0.30f),
            Ball(K.OffSpin, D.TopSpinner, B.Full, -0.05f, 0.15f),
        },
    };

    /// In the order of the drills screen's rows.
    public static readonly DrillDef[] All = { Yorkers, Leave, ShortBall, SpinDrill, Gaps };

    public static DrillDef For(eDrill d) => Array.Find(All, x => x.id == d);

    /// The drill's balls shuffled (Fisher-Yates within blocks) for this attempt's seed. Empty for
    /// no drill.
    public static DrillBall[] Plan(eDrill d, int seed)
    {
        DrillDef def = For(d);
        if (def == null)
            return new DrillBall[0];
        var plan = (DrillBall[])def.balls.Clone();
        var rng = new Random(seed);
        int block = def.blockSize > 0 ? def.blockSize : plan.Length;
        for (int start = 0; start < plan.Length; start += block)
        {
            int end = Math.Min(plan.Length, start + block);
            for (int i = end - 1; i > start; i--)
            {
                int j = start + rng.Next(i - start + 1);
                (plan[i], plan[j]) = (plan[j], plan[i]);
            }
        }
        return plan;
    }

    /// A seed for one attempt at one drill in one career slot: the trial's seed for the slot
    /// (NetsTrialPlan.SeedFor, stable between runs) mixed with the drill and the attempt number,
    /// so every attempt bowls its own order and a restart of the same attempt bowls the same one.
    public static int SeedFor(CareerProfile p, int slot, eDrill d, int attempt)
    {
        unchecked
        {
            uint h = (uint)NetsTrialPlan.SeedFor(p, slot);
            h = (h ^ (uint)d) * 16777619u;
            h = (h ^ (uint)attempt) * 16777619u;
            // Finish (murmur3's fmix32) so neighbouring attempts are far apart.
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (int)h;
        }
    }

    /// The field for Gaps - the seamer's or the spinner's (null for the other drills).
    public static FieldSets.Position[] FieldFor(DrillDef d, eBowlerKind k)
    {
        if (d == null)
            return null;
        return BowlerRepertoire.IsSpin(k) ? d.spinField : d.paceField;
    }
}
