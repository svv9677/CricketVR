using System.Collections.Generic;

/// <summary>
/// The career's records and milestones after each match he played (CareerProgress.RecordMatch). For
/// now it holds the old milestone messages, moved here from CareerProgress unchanged: the first
/// fifty, a hundred, ducks in a row. Phase 7's record book fills in around them.
/// </summary>
public static class CareerRecords
{
    /// Balls an innings needs before its strike rate can be a record.
    public const int StrikeRateMinBalls = 10;
    public static readonly int[] RunsMilestones = { 100, 250, 500, 1000, 2500, 5000, 7500, 10000 };
    public static readonly int[] CapsMilestones = { 1, 10, 25, 50, 100 };
    private const string Coach = "Coach";

    /// A short word in the inbox after a milestone: the first fifty, a hundred, ducks in a row.
    /// Returns the records the match broke (none yet).
    public static List<eRecord> AfterMatch(CareerProfile p, InningsRecord r, CareerMatchResult result)
    {
        var bests = new List<eRecord>();
        if (p == null || r == null || !r.batted)
            return bests;
        string score = $"{r.runs}{(r.isOut ? "" : "*")} off {r.balls} balls against {r.opponent}";
        if (r.runs >= 100)
            CareerProgress.Post(p, eMessageKind.Milestone, Coach, p.stats.hundreds == 1 ? "Your first hundred" : "Another hundred",
                                score + ". Raise the bat - that is a day you will remember.");
        else if (r.runs >= 50 && p.stats.fifties == 1)
            CareerProgress.Post(p, eMessageKind.Milestone, Coach, "Your first fifty", score + ". The side will remember it at selection.");

        int ducks = CareerProgressMath.DuckStreak(p.innings);
        if (ducks >= 2)
            CareerProgress.Post(p, eMessageKind.Coach, Coach, ducks == 2 ? "Two ducks in a row" : ducks + " ducks in a row",
                "It happens to everyone. Next time watch the ball onto the bat and look for a single to get off the mark.");
        return bests;
    }

    /// An older save's records, worked out from its record book (silently: no messages).
    public static void Backfill(CareerProfile p) { }
}
