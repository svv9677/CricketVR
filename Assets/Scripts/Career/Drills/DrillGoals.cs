using UnityEngine;

/// <summary>
/// One clear goal per drill, with a concrete rule for whether each ball counts toward it
/// (2026-11-06: "have a clear goal for each drill and a better criteria thats more concrete").
/// The goal is "N of 12", a ball either counts or it does not, and the rule says exactly what
/// makes it count - what the nets' left-hand text shows ball by ball, and what the result card
/// leads with. The 0..100 score (DrillScoring) stays underneath for the grade, the best and the
/// coach, but it is no longer what the player is told to chase.
///
/// Every rule reads the same measures as the score (the contact on the real blade, the line, the
/// runs, the exit speed), so a ball that counts is always one the score also rated well. Pure:
/// DrillGoalTests.
/// </summary>
public static class DrillGoals
{
    /// m/s under which a block is dead (SoftHands), as DrillScoringMore has it: 9 m/s, 32 km/h.
    private const float DeadKph = 32f;

    public struct Goal
    {
        public int target;
        /// "Dig out 8 of 12 yorkers".
        public string text;
        /// "Clean bat on it: no edge, not bowled".
        public string rule;
    }

    public static Goal For(eDrill d) => d switch
    {
        eDrill.Yorkers => G(8, "Dig out 8 of 12 yorkers", "Middle it: no edge, not bowled"),
        eDrill.LeaveOutsideOff => G(9, "Judge 9 of 12 right", "Leave it 30 cm+ outside off; middle it on the stumps"),
        eDrill.ShortBall => G(8, "Handle 8 of 12 short balls", "Middle it, or let it go well clear of your head"),
        eDrill.Spin => G(7, "Play 7 of 12 with the turn", "Middle it with the turn, sweep it, or use your feet"),
        eDrill.Gaps => G(7, "Beat the field 7 times", "Middle it past or over every fielder"),
        eDrill.StraightHitting => G(8, "Hit 8 of 12 in the V", "Middle it within 25 degrees of straight"),
        eDrill.Swing => G(8, "Judge 8 of 12 right", "Leave the wide swingers; middle the straight ones along the ground"),
        eDrill.PowerHitting => G(5, "Hit 5 boundaries", "Hit a clean four or six"),
        eDrill.DeathOvers => G(7, "Score 2+ off 7 balls", "Score two runs or more off the ball"),
        eDrill.SoftHands => G(8, "Kill 8 of 12 dead", "Middle it under " + DeadKph + " km/h off the bat"),
        _ => G(DrillDefs.BallsPerDrill, "", ""),
    };

    private static Goal G(int target, string text, string rule) => new Goal { target = target, text = text, rule = rule };

    /// Whether ball `r` (scored `tag` by DrillScoring) counts toward drill `d`'s goal.
    public static bool Counts(eDrill d, BallRecord r, eDrillTag tag)
    {
        if (r == null || r.Out)
            return false;
        switch (d)
        {
            case eDrill.Yorkers: return tag == eDrillTag.DugOut;
            case eDrill.LeaveOutsideOff:
            case eDrill.Swing: return tag == eDrillTag.GoodLeave || tag == eDrillTag.Survived;
            case eDrill.ShortBall:
                return tag == eDrillTag.Pulled || tag == eDrillTag.Survived || tag == eDrillTag.Ducked || tag == eDrillTag.GoodLeave;
            case eDrill.Spin: return tag == eDrillTag.Swept || tag == eDrillTag.UsedFeet || tag == eDrillTag.PlayedTurn;
            case eDrill.Gaps: return tag == eDrillTag.Gap || tag == eDrillTag.OverTop || tag == eDrillTag.Six;
            case eDrill.StraightHitting: return tag == eDrillTag.InTheV;
            case eDrill.PowerHitting: return tag == eDrillTag.Six || tag == eDrillTag.Boundary;
            case eDrill.DeathOvers:   // off the bat (the score's Scored, Boundary or Six), not byes after a miss
                return (tag == eDrillTag.Scored || tag == eDrillTag.Boundary || tag == eDrillTag.Six) && Runs(r) >= 2;
            case eDrill.SoftHands: return tag == eDrillTag.Blocked;
            default: return false;
        }
    }

    /// How many of `balls` (an attempt's, oldest first; the first BallsPerDrill count) count.
    public static int Hits(DrillDef d, System.Collections.Generic.IList<BallRecord> balls)
    {
        if (d == null || balls == null)
            return 0;
        int hits = 0, n = Mathf.Min(balls.Count, DrillDefs.BallsPerDrill);
        for (int i = 0; i < n; i++)
            if (Counts(d.id, balls[i], DrillScoring.Score(d, balls[i]).tag))
                hits++;
        return hits;
    }

    /// Why a ball did or did not count, in two or three words for this drill: "Dug out",
    /// "Against the turn", "Only 1 run", "Too firm" - never "Played well" on a ball that missed.
    public static string Why(eDrill d, BallRecord r, eDrillTag tag, bool counted)
    {
        if (r != null && r.Out)
            return "Out";
        if (!counted)
        {
            switch (d)
            {
                case eDrill.Spin when tag == eDrillTag.Survived: return "Against the turn";
                case eDrill.StraightHitting when tag == eDrillTag.Survived: return "Not in the V";
                case eDrill.PowerHitting when tag == eDrillTag.Survived || tag == eDrillTag.Mistimed: return "No boundary";
                case eDrill.DeathOvers when tag == eDrillTag.Survived: return "Dot ball";
                case eDrill.DeathOvers when tag == eDrillTag.Scored: return "Only 1 run";
                case eDrill.SoftHands when tag == eDrillTag.Survived: return "Too firm";
                case eDrill.SoftHands when tag == eDrillTag.Edged: return "Edged";
                case eDrill.Swing when tag == eDrillTag.Mistimed: return "In the air";
                case eDrill.ShortBall when tag == eDrillTag.Evaded: return "Too close to your head";
                case eDrill.Yorkers when tag == eDrillTag.GoodLeave: return "Left it";
                case eDrill.Gaps when tag == eDrillTag.Wasted && r != null && r.action == eBallAction.Left: return "No shot";
            }
        }
        if (counted && d == eDrill.DeathOvers && r != null)
            return Runs(r) >= 6 ? "Six" : Runs(r) >= 4 ? "Four" : Runs(r) + " runs";
        string label = DrillText.TagLabel(tag);
        return label.Length > 0 ? label : counted ? "Counts" : "Doesn't count";
    }

    private static int Runs(BallRecord r) =>
        r.end == eBallEnd.Six ? Mathf.Max(r.runs, 6) : r.end == eBallEnd.Four ? Mathf.Max(r.runs, 4) : r.runs;
}
