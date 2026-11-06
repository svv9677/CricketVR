using System;

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
                        Pulled, Ducked, Evaded, HitHead, UsedFeet, Swept, PlayedTurn, Gap, Fielder, Caught, OverTop, Six, Wasted,
                        InTheV, Blocked, Boundary, Mistimed, Scored }

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
