using UnityEngine;

/// <summary>
/// What each ball of the second set of drills (DrillDefsMore) is worth, read off the real blade as
/// every drill is. Out is 0 in all of them (DrillScoring.Score). Pure: DrillTests.
///
///   Straight hitting  clean in the V (within 25 deg of straight): 1.0 hit hard (18 m/s+), 0.8
///                     placed; clean elsewhere 0.4; edged 0.15; beaten 0.2; left missing 0.3,
///                     left hitting 0.
///   Swing             a swinging-away ball outside off left 1.0; one on the stumps played cleanly
///                     along the ground 1.0, in the air 0.6; fished at outside off and middled 0.4;
///                     edged 0; beaten 0.3; left missing 0.6, left hitting 0.
///   Power hitting     six 1.0; four 0.7; a big hit in the air that falls short (30 m+) 0.4; any
///                     other clean 0.25; edged or beaten 0.1; left 0.
///   Death overs       by the runs: six 1.0, four 0.85, two or three 0.6, one 0.45, a clean dot
///                     0.25; edged 0.15 (or by the runs, if more); beaten 0.1; a wide one left
///                     that was missing 0.3, left hitting 0.
///   Soft hands        clean and dead (under 9 m/s) 1.0; clean but firmer 0.6; a soft edge that
///                     dies (under 9 m/s) 0.4; a hard edge 0.1; beaten 0.2; left missing 0.5,
///                     left hitting 0.
/// </summary>
public static class DrillScoringMore
{
    private const float InTheV = 25f, HardStraight = 18f, Dead = 9f, BigCarry = 30f, Airborne = 12f, OffSide = 0.3f;

    private static DrillBallScore S(float v, eDrillTag t) => new DrillBallScore { value = v, tag = t };
    private static bool Clean(BallRecord r) => r.action == eBallAction.Played && r.realContact == eRealContact.Clean;
    private static bool Edged(BallRecord r) => r.action == eBallAction.Played && r.realContact == eRealContact.Edge;
    private static bool Left(BallRecord r) => r.action == eBallAction.Left;
    private static DrillBallScore LeaveOf(BallRecord r, float missing) =>
        r.wouldHitStumps ? S(0f, eDrillTag.LeftStraight) : S(missing, eDrillTag.GoodLeave);

    private static int Runs(BallRecord r) =>
        r.end == eBallEnd.Six ? Mathf.Max(r.runs, 6) : r.end == eBallEnd.Four ? Mathf.Max(r.runs, 4) : r.runs;

    public static DrillBallScore Straight(BallRecord r)
    {
        if (Clean(r))
        {
            if (Mathf.Abs(r.shotAngle) <= InTheV)
                return S(r.exitSpeed >= HardStraight ? 1f : 0.8f, eDrillTag.InTheV);
            return S(0.4f, eDrillTag.Survived);
        }
        if (Edged(r)) return S(0.15f, eDrillTag.Edged);
        if (Left(r)) return LeaveOf(r, 0.3f);
        return S(0.2f, eDrillTag.Beaten);
    }

    public static DrillBallScore Swing(BallRecord r)
    {
        bool away = r.lineAtStumps > OffSide && !r.wouldHitStumps;
        if (away)
        {
            if (Left(r)) return S(1f, eDrillTag.GoodLeave);
            if (Clean(r)) return S(0.4f, eDrillTag.Fished);
            if (Edged(r)) return S(0f, eDrillTag.Edged);
            return S(0.3f, eDrillTag.Beaten);
        }
        if (Clean(r)) return r.launchDeg < Airborne ? S(1f, eDrillTag.Survived) : S(0.6f, eDrillTag.Mistimed);
        if (Edged(r)) return S(0f, eDrillTag.Edged);
        if (Left(r)) return LeaveOf(r, 0.6f);
        return S(0.3f, eDrillTag.Beaten);
    }

    public static DrillBallScore Power(BallRecord r)
    {
        if (Left(r)) return S(0f, eDrillTag.Wasted);
        if (!Clean(r)) return S(0.1f, Edged(r) ? eDrillTag.Edged : eDrillTag.Beaten);
        if (r.end == eBallEnd.Six) return S(1f, eDrillTag.Six);
        if (r.end == eBallEnd.Four) return S(0.7f, eDrillTag.Boundary);
        if (r.carry >= BigCarry && r.launchDeg >= Airborne) return S(0.4f, eDrillTag.Mistimed);
        return S(0.25f, eDrillTag.Survived);
    }

    public static DrillBallScore Death(BallRecord r)
    {
        int runs = Runs(r);
        float byRuns = runs >= 6 ? 1f : runs >= 4 ? 0.85f : runs >= 2 ? 0.6f : runs == 1 ? 0.45f : 0f;
        eDrillTag scored = runs >= 6 ? eDrillTag.Six : runs >= 4 ? eDrillTag.Boundary : eDrillTag.Scored;
        if (Clean(r)) return runs > 0 ? S(byRuns, scored) : S(0.25f, eDrillTag.Survived);
        if (Edged(r)) return byRuns > 0.15f ? S(byRuns, scored) : S(0.15f, eDrillTag.Edged);
        if (Left(r)) return r.lineAtStumps > OffSide ? LeaveOf(r, 0.3f) : LeaveOf(r, 0.1f);
        return S(0.1f, eDrillTag.Beaten);
    }

    public static DrillBallScore SoftHands(BallRecord r)
    {
        if (Clean(r)) return r.exitSpeed < Dead ? S(1f, eDrillTag.Blocked) : S(0.6f, eDrillTag.Survived);
        if (Edged(r)) return r.exitSpeed < Dead ? S(0.4f, eDrillTag.Edged) : S(0.1f, eDrillTag.Edged);
        if (Left(r)) return LeaveOf(r, 0.5f);
        return S(0.2f, eDrillTag.Beaten);
    }
}
