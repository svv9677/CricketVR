using System;

/// One ball of the trial: who bowls it (the kind of bowler, so the right run-up and action come in)
/// and what he bowls.
public struct NetsTrialBall
{
    public eBowlerKind kind;
    public eDelivery delivery;

    public NetsTrialBall(eBowlerKind kind, eDelivery delivery)
    {
        this.kind = kind;
        this.delivery = delivery;
    }
}

/// <summary>
/// The career player's 30-ball trial in the nets, planned before the first ball: five overs, one
/// from each kind of bowler - a quick, a medium-pacer, an off-spinner, a leg-spinner - then a mixed
/// over (quick, mystery spinner, medium-pacer, twice round). So every rating gets balls to measure,
/// pace and spin alike, whoever the player is.
///
/// Each over is the way that bowler really bowls it: his stock ball mostly, with one or two of his
/// variations dropped in (OverPlanner, at a county-standard attack's rate), drawn from his own
/// repertoire's weights (BowlerRepertoire) - never a delivery he does not bowl. The mixed over has
/// one seam variation among its four seam balls; the mystery spinner has no stock ball, so his two
/// are both from his bag.
///
/// Pure and seeded: the same seed gives the same 30 balls, so a trial picked up again bowls the
/// same plan, and the EditMode tests can check it (NetsTrialTests).
/// </summary>
public static class NetsTrialPlan
{
    public const int BallsPerOver = 6;
    public const int Overs = NetsSession.TrialBalls / BallsPerOver;

    /// The attack the trial is bowled at (and the variations-per-over rate its overs are planned at).
    public const eBowlingLevel Level = eBowlingLevel.Medium;

    /// The first four overs: one kind of bowler each.
    public static readonly eBowlerKind[] SingleKindOvers = { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin };

    /// The last over, ball by ball.
    public static readonly eBowlerKind[] MixedOver =
    {
        eBowlerKind.Fast, eBowlerKind.Mystery, eBowlerKind.Medium,
        eBowlerKind.Fast, eBowlerKind.Mystery, eBowlerKind.Medium,
    };

    /// The 30 balls for this seed.
    public static NetsTrialBall[] Make(int seed)
    {
        var rng = new Random(seed);
        Func<float> rand01 = () => (float)rng.NextDouble();
        var plan = new NetsTrialBall[NetsSession.TrialBalls];
        float perOver = BowlerRepertoire.VariationsFor(Level);
        for (int over = 0; over < SingleKindOvers.Length; over++)
        {
            eBowlerKind kind = SingleKindOvers[over];
            var planner = new OverPlanner(kind, perOver, rand01);
            for (int b = 0; b < BallsPerOver; b++)
                plan[over * BallsPerOver + b] = new NetsTrialBall(kind, planner.Next(b, false, 0f, rand01));
        }
        MakeMixedOver(plan, SingleKindOvers.Length * BallsPerOver, rand01);
        return plan;
    }

    /// The mixed over at plan[start..start+5]: one of its seam balls (chosen at random) is that
    /// bowler's variation, the rest his stock ball; the mystery spinner's two differ from each other.
    private static void MakeMixedOver(NetsTrialBall[] plan, int start, Func<float> rand01)
    {
        int seamBalls = 0;
        foreach (eBowlerKind k in MixedOver)
            if (!BowlerRepertoire.IsSpin(k)) seamBalls++;
        int variedSeam = Math.Min(seamBalls - 1, (int)(rand01() * seamBalls));
        int seamSeen = 0;
        eDelivery lastMystery = eDelivery.Stock;
        for (int b = 0; b < MixedOver.Length; b++)
        {
            eBowlerKind kind = MixedOver[b];
            eDelivery d;
            if (kind == eBowlerKind.Mystery)
            {
                d = BowlerRepertoire.PickFrom(BowlerRepertoire.MysteryBalls, rand01, lastMystery);
                lastMystery = d;
            }
            else if (seamSeen++ == variedSeam)
                d = BowlerRepertoire.PickFrom(BowlerRepertoire.VariationWeights(kind), rand01, eDelivery.Stock);
            else
                d = BowlerRepertoire.StockBall(kind);
            plan[start + b] = new NetsTrialBall(kind, d);
        }
    }

    /// The over (0-based) a trial ball is in.
    public static int OverOf(int ball) => ball / BallsPerOver;

    /// Not his stock ball (every mystery ball counts: he has none).
    public static bool IsVariation(NetsTrialBall b) =>
        b.kind == eBowlerKind.Mystery || b.delivery != BowlerRepertoire.StockBall(b.kind);

    /// The bowler bowls this delivery: his stock ball (not the mystery spinner, who has none) or one
    /// of the variations his repertoire gives a weight.
    public static bool Valid(NetsTrialBall b)
    {
        if (b.kind != eBowlerKind.Mystery && b.delivery == BowlerRepertoire.StockBall(b.kind))
            return true;
        foreach (BowlerRepertoire.Weighted w in BowlerRepertoire.VariationWeights(b.kind))
            if (w.delivery == b.delivery && w.weight > 0f)
                return true;
        return false;
    }

    /// The counting rule: a delivery moves the trial on when it is a real ball the batter could play
    /// (BallRecord.Counts: not a wide) and was not called dead (the X button, a ball that stopped
    /// short). Otherwise the same planned ball is bowled again - 30 deliveries really are faced.
    public static bool Advances(BallRecord r) => r != null && r.Counts && r.end != eBallEnd.Dead;

    /// A seed fixed for one career in one slot (FNV-1a over its name and start date), so a trial that
    /// is picked up again bowls the same 30 balls. string.GetHashCode is not used: it is allowed to
    /// differ between runs.
    public static int SeedFor(CareerProfile p, int slot)
    {
        unchecked
        {
            uint h = 2166136261u;
            string key = p != null ? p.name + "|" + p.created + "|" + slot : "|" + slot;
            for (int i = 0; i < key.Length; i++)
            {
                h ^= key[i];
                h *= 16777619u;
            }
            return (int)h;
        }
    }
}
