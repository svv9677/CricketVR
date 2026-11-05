using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each ball of a drill was worth (0..1) and why (eDrillTag), and the drill's score: 100 x
/// the sum over its twelve balls / 12, so a ball not faced is worth nothing. Out is 0 in every drill
/// (tag Bowled, shown as "Out"). Contact is read off the real blade, as the ratings read it.
///
///   Yorkers   dug out (clean) 1.0 with runs, 0.8 without; edged 0.4; beaten 0.25; a leave that was
///             missing 0.6; a leave that was hitting 0.
///   Leave     wide of off (line beyond OffSide, missing): left 1.0, played cleanly 0.5 (fished),
///             beaten 0.2, edged 0. Any other ball: played cleanly 1.0, edged or beaten 0.3, left
///             missing 0.6, left hitting 0.
///   Short     clean to leg (angle beyond -30) 1.0 for four or more, 0.8 else; the wide one cut
///             (angle beyond 45) for four or more 1.0; any other clean 0.6. A head-high ball let go:
///             well clear of the head 0.8 (ducked), clear 0.5 (evaded), would have hit him 0.1;
///             any other leave 0.6 (0 if hitting); edged 0.2, beaten 0.3.
///   Spin      clean 0.6, +0.2 for using the feet or a sweep, +0.2 for playing with the turn (or
///             four or more), at most 1; stumped (came down the pitch and missed or left it) 0;
///             edged 0.2, beaten 0.25, left missing 0.4, left hitting 0.
///   Gaps      by the virtual field (DrillField.Judge): a six 1.0; a gap 0.6 to 1.0 by how hard it
///             went (12 to 28 m/s); over the top 0.8; to a fielder 0.15; caught 0; to the keeper
///             0.2; edged or beaten 0.1; left 0.
/// Using the feet is only ever a bonus, never asked for (room-scale play: nobody has to walk).
/// </summary>
public static class DrillScoring
{
    public const float FeetStep = 0.4f, SweepHeight = 0.45f, HeadHigh = 1.2f, HitHead = 0.15f, ClearHead = 0.3f;

    /// Metres outside off stump (batter's frame) beyond which a ball is one to leave, as the
    /// ratings' Judgement has it.
    private const float OffSide = 0.3f;

    /// Shot angles: to the leg side for the pull, square on the off side for the cut, within this
    /// of straight for "with the turn" off a ball that goes straight on, the sweep's arc.
    private const float PullAngle = -30f, CutAngle = 45f, StraightOn = 30f, SweepFrom = -170f, SweepTo = -45f;

    /// The gaps drill's hard-hit scale (m/s) and what a gap is worth at each end of it.
    private const float GapSlow = 12f, GapFast = 28f, GapBase = 0.6f, GapExtra = 0.4f;

    /// The grade at or above each score.
    private const int Excellent = 80, Good = 60, GettingThere = 40;

    public static DrillBallScore Score(DrillDef d, BallRecord r)
    {
        if (d == null || r == null)
            return S(0f, eDrillTag.None);
        if (r.Out)
            return S(0f, eDrillTag.Bowled);
        switch (d.id)
        {
            case eDrill.Yorkers: return Yorker(r);
            case eDrill.LeaveOutsideOff: return Leave(r);
            case eDrill.ShortBall: return Short(r);
            case eDrill.Spin: return SpinBall(r);
            case eDrill.Gaps: return Gap(d, r);
            default: return S(0f, eDrillTag.None);
        }
    }

    /// +1 turning to the off side, -1 to leg, 0 straight (batter's frame): the off-break (and the
    /// off-spinner's flighted ball) and the googly into him, the leg-break (and the leg-spinner's
    /// flighted ball) and the carrom ball away (BowlingAttack.MakeDelivery, BowlingParams.Effects).
    public static int TurnSide(eBowlerKind k, eDelivery dl)
    {
        switch (dl)
        {
            case eDelivery.OffBreak:
            case eDelivery.Googly: return -1;
            case eDelivery.LegBreak:
            case eDelivery.Carrom: return 1;
            case eDelivery.Flighted: return k == eBowlerKind.LegSpin ? 1 : k == eBowlerKind.OffSpin ? -1 : 0;
            default: return 0;
        }
    }

    /// This attempt's balls: source Drill, the same drill and attempt, the ones that count
    /// (NetsTrialPlan.Advances: no wides, no dead balls). Oldest first.
    public static List<BallRecord> AttemptBalls(IList<BallRecord> balls, eDrill d, int attempt)
    {
        var list = new List<BallRecord>();
        if (balls == null)
            return list;
        foreach (BallRecord r in balls)
            if (r != null && r.source == eBallSource.Drill && r.drill == d && r.drillAttempt == attempt && NetsTrialPlan.Advances(r))
                list.Add(r);
        return list;
    }

    /// The drill's result over its first BallsPerDrill balls: the score, the grade, a count per tag.
    /// The best, newBest and the ratings are filled in by whoever records it (DrillCoach, the nets).
    public static DrillResult Tally(DrillDef d, IList<BallRecord> attempt)
    {
        var res = new DrillResult { drill = d != null ? d.id : eDrill.None };
        float sum = 0f;
        int n = attempt == null ? 0 : Mathf.Min(attempt.Count, DrillDefs.BallsPerDrill);
        for (int i = 0; i < n; i++)
        {
            DrillBallScore s = Score(d, attempt[i]);
            sum += s.value;
            res.tags[(int)s.tag]++;
        }
        res.balls = n;
        res.attempt = n > 0 ? attempt[0].drillAttempt : 0;
        res.score = Mathf.Clamp(Mathf.RoundToInt(100f * sum / DrillDefs.BallsPerDrill), 0, 100);
        res.grade = Grade(res.score);
        return res;
    }

    /// 0..100 over the balls so far (the running score the progress line shows).
    public static float Running(DrillDef d, IList<BallRecord> attempt)
    {
        int n = attempt == null ? 0 : Mathf.Min(attempt.Count, DrillDefs.BallsPerDrill);
        if (n == 0)
            return 0f;
        float sum = 0f;
        for (int i = 0; i < n; i++)
            sum += Score(d, attempt[i]).value;
        return 100f * sum / n;
    }

    /// "Excellent" (80+), "Good" (60+), "Getting there" (40+), "Needs work".
    public static string Grade(int score) =>
        score >= Excellent ? "Excellent" : score >= Good ? "Good" : score >= GettingThere ? "Getting there" : "Needs work";

    // ---- The drills -------------------------------------------------------------------------------

    private static DrillBallScore S(float v, eDrillTag t) => new DrillBallScore { value = v, tag = t };

    private static bool Clean(BallRecord r) => r.action == eBallAction.Played && r.realContact == eRealContact.Clean;
    private static bool Edged(BallRecord r) => r.action == eBallAction.Played && r.realContact == eRealContact.Edge;
    private static bool Left(BallRecord r) => r.action == eBallAction.Left;

    /// Runs off the bat, at least four for a four and six for a six.
    private static int Runs(BallRecord r) =>
        r.end == eBallEnd.Six ? Mathf.Max(r.runs, 6) : r.end == eBallEnd.Four ? Mathf.Max(r.runs, 4) : r.runs;

    /// A leave: fine when it was missing, as bad as out when it was hitting.
    private static DrillBallScore LeaveOf(BallRecord r, float missing) =>
        r.wouldHitStumps ? S(0f, eDrillTag.LeftStraight) : S(missing, eDrillTag.GoodLeave);

    private static DrillBallScore Yorker(BallRecord r)
    {
        if (Clean(r)) return S(Runs(r) > 0 ? 1f : 0.8f, eDrillTag.DugOut);
        if (Edged(r)) return S(0.4f, eDrillTag.Edged);
        if (Left(r)) return LeaveOf(r, 0.6f);
        return S(0.25f, eDrillTag.Beaten);
    }

    private static DrillBallScore Leave(BallRecord r)
    {
        bool toLeave = r.lineAtStumps > OffSide && !r.wouldHitStumps;
        if (toLeave)
        {
            if (Left(r)) return S(1f, eDrillTag.GoodLeave);
            if (Clean(r)) return S(0.5f, eDrillTag.Fished);
            if (Edged(r)) return S(0f, eDrillTag.Edged);
            return S(0.2f, eDrillTag.Beaten);
        }
        if (Left(r)) return LeaveOf(r, 0.6f);
        if (Clean(r)) return S(1f, eDrillTag.Survived);
        if (Edged(r)) return S(0.3f, eDrillTag.Edged);
        return S(0.3f, eDrillTag.Beaten);
    }

    private static DrillBallScore Short(BallRecord r)
    {
        if (Clean(r))
        {
            if (r.shotAngle < PullAngle) return S(Runs(r) >= 4 ? 1f : 0.8f, eDrillTag.Pulled);
            if (r.lineAtStumps > OffSide && r.shotAngle > CutAngle && Runs(r) >= 4) return S(1f, eDrillTag.Pulled);
            return S(0.6f, eDrillTag.Survived);
        }
        if (Edged(r)) return S(0.2f, eDrillTag.Edged);
        bool headHigh = r.heightAtStumps >= HeadHigh;
        if (!r.Touched && headHigh && r.headClearance < HitHead) return S(0.1f, eDrillTag.HitHead);
        if (!Left(r)) return S(0.3f, eDrillTag.Beaten);
        if (headHigh && !r.wouldHitStumps)
            return r.headClearance >= ClearHead ? S(0.8f, eDrillTag.Ducked) : S(0.5f, eDrillTag.Evaded);
        return LeaveOf(r, 0.6f);
    }

    private static DrillBallScore SpinBall(BallRecord r)
    {
        bool feet = r.headStep >= FeetStep;
        if (Clean(r))
        {
            bool sweep = r.contactHeight < SweepHeight && r.shotAngle >= SweepFrom && r.shotAngle <= SweepTo;
            int turn = TurnSide(r.bowlerKind, r.delivery);
            bool withTurn = (turn != 0 ? Mathf.Sign(r.shotAngle) == turn && r.shotAngle != 0f : Mathf.Abs(r.shotAngle) < StraightOn)
                            || Runs(r) >= 4;
            float v = 0.6f + (feet || sweep ? 0.2f : 0f) + (withTurn ? 0.2f : 0f);
            eDrillTag tag = sweep ? eDrillTag.Swept : feet ? eDrillTag.UsedFeet : withTurn ? eDrillTag.PlayedTurn : eDrillTag.Survived;
            return S(Mathf.Min(1f, v), tag);
        }
        if (Edged(r)) return S(0.2f, eDrillTag.Edged);
        if (feet && !r.wouldHitStumps) return S(0f, eDrillTag.Bowled);    // down the pitch and past it: stumped
        if (Left(r)) return LeaveOf(r, 0.4f);
        return S(0.25f, eDrillTag.Beaten);
    }

    private static DrillBallScore Gap(DrillDef d, BallRecord r)
    {
        if (Left(r)) return S(0f, eDrillTag.Wasted);
        if (Edged(r)) return S(0.1f, eDrillTag.Edged);
        if (!Clean(r)) return S(0.1f, eDrillTag.Beaten);
        GapJudgement j = DrillField.Judge(r, DrillDefs.FieldFor(d, r.bowlerKind), DrillField.NetsGround(r.bowlerKind));
        switch (j.call)
        {
            case eGapCall.Six: return S(1f, eDrillTag.Six);
            case eGapCall.Gap: return S(GapBase + GapExtra * Mathf.Clamp01((r.exitSpeed - GapSlow) / (GapFast - GapSlow)), eDrillTag.Gap);
            case eGapCall.OverTop: return S(0.8f, eDrillTag.OverTop);
            case eGapCall.Fielder: return S(0.15f, eDrillTag.Fielder);
            case eGapCall.Caught: return S(0f, eDrillTag.Caught);
            default: return S(0.2f, eDrillTag.Wasted);                     // back to the keeper
        }
    }
}
