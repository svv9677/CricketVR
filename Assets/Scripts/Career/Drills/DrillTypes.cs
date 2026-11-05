using System;
using System.Collections.Generic;

/// One ball of a drill: who bowls it, what, and the length/line it must be.
public struct DrillBall
{
    public eBowlerKind kind;
    public eDelivery delivery;
    public DeliveryShape shape;

    public DrillBall(eBowlerKind k, eDelivery d, eLengthBand b, float min, float max)
    {
        kind = k; delivery = d; shape = new DeliveryShape(b, min, max);
    }

    public NetsTrialBall Ball => new NetsTrialBall(kind, delivery);
}

/// A drill: its name and words, the ratings it feeds (RatingSet indexes), its balls, and (Gaps) the
/// field it is played against.
public sealed class DrillDef
{
    public eDrill id;
    public string name = "", title = "", blurb = "";
    public int[] feeds = new int[0];
    public DrillBall[] balls = new DrillBall[0];
    /// Balls are shuffled within blocks of this size (Gaps: the pace half, then the spin half).
    public int blockSize = 12;
    public FieldSets.Position[] paceField, spinField;
}

/// What one ball of a drill was worth, and why.
public enum eDrillTag { None, Survived, DugOut, Bowled, Edged, Beaten, GoodLeave, Fished, LeftStraight,
                        Pulled, Ducked, Evaded, HitHead, UsedFeet, Swept, PlayedTurn, Gap, Fielder, Caught, OverTop, Six, Wasted }

public struct DrillBallScore { public float value; public eDrillTag tag; }

/// A finished drill: the score 0..100, the grade, how the balls went, the ratings before and after.
public sealed class DrillResult
{
    public eDrill drill;
    public int attempt, score, balls, best;
    public bool newBest;
    public string grade = "";
    /// Counts per eDrillTag.
    public int[] tags = new int[Enum.GetValues(typeof(eDrillTag)).Length];
    public RatingSet before, after;
}

/// Hitting the gaps: what the field would have done with a shot.
public enum eGapCall { Wasted, Gap, Fielder, Caught, OverTop, Six }
public struct GapJudgement { public eGapCall call; public string fielder; }

/// <summary>The five drills. CONTRACT STUB - Unit 2 fills the bodies.</summary>
public static class DrillDefs
{
    public const int BallsPerDrill = 12, Count = 5;
    public static readonly DrillDef[] All = new DrillDef[0];
    public static DrillDef For(eDrill d) => null;
    /// The drill's balls shuffled (Fisher-Yates within blocks) for this attempt's seed.
    public static DrillBall[] Plan(eDrill d, int seed) => new DrillBall[0];
    public static int SeedFor(CareerProfile p, int slot, eDrill d, int attempt) => 0;
    /// The field for Gaps (null for the others).
    public static FieldSets.Position[] FieldFor(DrillDef d, eBowlerKind k) => null;
}

/// <summary>Scoring a drill's balls. CONTRACT STUB.</summary>
public static class DrillScoring
{
    public const float FeetStep = 0.4f, SweepHeight = 0.45f, HeadHigh = 1.2f, HitHead = 0.15f, ClearHead = 0.3f;
    public static DrillBallScore Score(DrillDef d, BallRecord r) => default;
    /// +1 turning to the off side, -1 to leg, 0 straight (batter's frame).
    public static int TurnSide(eBowlerKind k, eDelivery dl) => 0;
    /// This attempt's balls: source Drill, the same drill and attempt, the ones that count.
    public static List<BallRecord> AttemptBalls(IList<BallRecord> balls, eDrill d, int attempt) => new List<BallRecord>();
    public static DrillResult Tally(DrillDef d, IList<BallRecord> attempt) => new DrillResult();
    /// 0..100 over the balls so far.
    public static float Running(DrillDef d, IList<BallRecord> attempt) => 0f;
    public static string Grade(int score) => "";
}

/// <summary>The virtual field for Hitting the gaps (the nets has no fielders). CONTRACT STUB.</summary>
public static class DrillField
{
    public const float Reach = 1.5f, RunSpeed = 5f, KeeperCone = 165f, GroundCarry = 20f, Boundary = 56f;
    public static FieldSets.Ground NetsGround(eBowlerKind k) => default;
    public static GapJudgement Judge(BallRecord r, FieldSets.Position[] field, FieldSets.Ground g) => default;
    public static float Cover(float distance, float ballSpeed) => 0f;
}

/// <summary>The coach's side of drills: which to do, the attempt counter, the training slot,
/// recording a result. CONTRACT STUB.</summary>
public static class DrillCoach
{
    public static eDrill Recommend(RatingSet r) => eDrill.Yorkers;
    /// A new attempt begins: ++started, returned.
    public static int Start(CareerProfile p, eDrill d) => 0;
    /// The training slot before the next fixture is still open.
    public static bool SlotOpen(CareerProfile p) => false;
    public static void Record(CareerProfile p, DrillResult res, string date) { }
}

/// <summary>Every line of drill text (Latin-1). CONTRACT STUB.</summary>
public static class DrillText
{
    public static string Heading(DrillResult d) => "";
    public static string ProgressText(DrillDef d, int ballNumber, eBowlerKind next, float running, eDrillTag last) => "";
    public static string ResultBody(DrillDef d, DrillResult r) => "";
    public static string Verdict(DrillDef d, DrillResult r) => "";
    public static string TagLabel(eDrillTag t) => "";
    public static string Feeds(DrillDef d) => "";
    public static string RowDetail(DrillDef d, DrillRecord r, bool coachPick) => "";
    public static string Header(CareerProfile p) => "";
}
