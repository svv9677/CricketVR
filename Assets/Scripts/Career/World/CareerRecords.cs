using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// The career's records and milestones after each match he played (CareerProgress.RecordMatch, after
/// the innings is in the stats and the cap counted): the record book's five bests, the milestones
/// said once each in the inbox, and the coach's word after ducks in a row.
///
/// The bests (CareerRecordBook):
///   * highest score - more runs, or the same score not out where the best was out (as the stats'
///     high score); any other tie and the older record stands;
///   * fastest fifty - the fewest balls to 50 (balls kept as the innings is played, so a match
///     recorded without them sets none);
///   * best strike rate - innings of StrikeRateMinBalls balls or more; a tie goes to more runs, then
///     the older;
///   * most sixes - one or more;
///   * longest six - the real-blade carry (CareerMatchLogic.NoteSix: never a six the bat shield's
///     extra width found), with the stamp of its saved clip.
/// Only records that already existed and were beaten come back from AfterMatch, so a first innings
/// never reads as a new career best.
///
/// The milestones: "Your first fifty" (a hundred counts, so a hundred then a 55 says nothing), every
/// hundred ("Your first hundred" / "Another hundred"), career runs (one message a match, however many
/// it passed) and caps. Nothing here changes how a ball is played.
/// </summary>
public static class CareerRecords
{
    /// Balls an innings needs before its strike rate can be a record.
    public const int StrikeRateMinBalls = 10;
    public static readonly int[] RunsMilestones = { 100, 250, 500, 1000, 2500, 5000, 7500, 10000 };
    public static readonly int[] CapsMilestones = { 1, 10, 25, 50, 100 };
    /// Milestone ids in CareerRecordBook.reached.
    public const string FiftyId = "fifty", HundredId = "hundred";
    private const string Coach = "Coach";

    public static string RunsId(int runs) => "runs" + runs.ToString(CultureInfo.InvariantCulture);
    public static string CapsId(int caps) => "caps" + caps.ToString(CultureInfo.InvariantCulture);

    /// The record book and the inbox after a match he played. Returns the records the match broke
    /// (records that existed and were beaten; empty on a first innings).
    public static List<eRecord> AfterMatch(CareerProfile p, InningsRecord r, CareerMatchResult result)
    {
        var bests = new List<eRecord>();
        if (p == null || r == null)
            return bests;
        CareerRecordBook book = Book(p);
        if (r.batted && !r.leftOut)
        {
            Consider(book, r, bests);
            InningsMilestones(p, book, r);
            RunsMilestone(p, book, r);
        }
        CapsMilestone(p, book, r);   // a cap is a match played, batted or not
        if (r.batted)
            DuckNote(p);
        return bests;
    }

    /// An older save's records (v2 to v3), worked out from what it kept, silently: no messages, no
    /// bests, and running it again changes nothing.
    ///   * highest score, best strike rate and most sixes from the innings in the book, and the stats'
    ///     high score when the book has been trimmed past it (with no opponent or date);
    ///   * the longest six from the balls kept: a match six off the real blade, the longest carry
    ///     (no opponent: a ball does not say who bowled it);
    ///   * the fifty, hundred, runs and caps milestones already passed, marked reached.
    /// No fastest fifty (the balls to 50 were never kept) and no clip.
    public static void Backfill(CareerProfile p)
    {
        if (p == null)
            return;
        CareerRecordBook book = Book(p);
        if (p.innings != null)
            foreach (InningsRecord r in p.innings)   // oldest first, so a tie keeps the older
                if (r != null && r.batted && !r.leftOut)
                    Consider(book, r, null);
        CareerStats st = p.stats;
        if (st != null && st.innings > 0 && HigherScore(book.highScore, st.highScore, !st.highScoreNotOut))
        {
            Clear(book.highScore);
            book.highScore.set = true;
            book.highScore.value = book.highScore.runs = st.highScore;
            book.highScore.notOut = st.highScoreNotOut;
        }
        LongestSixFromBalls(p.balls, book.longestSix);
        MarkPassed(p, book);
    }

    // ---- The bests --------------------------------------------------------------------------------

    /// Each record this innings beats is written; one that existed before goes into `bests` (null:
    /// nobody is told, as in Backfill).
    private static void Consider(CareerRecordBook b, InningsRecord r, List<eRecord> bests)
    {
        if (HigherScore(b.highScore, r.runs, r.isOut))
            Write(b.highScore, eRecord.HighScore, r, r.runs, bests);
        if (r.ballsToFifty > 0 && (!b.fastestFifty.set || r.ballsToFifty < Math.Round(b.fastestFifty.value)))
            Write(b.fastestFifty, eRecord.FastestFifty, r, r.ballsToFifty, bests);
        if (r.balls >= StrikeRateMinBalls && BetterRate(b.bestStrikeRate, r))
            Write(b.bestStrikeRate, eRecord.BestStrikeRate, r, r.StrikeRate, bests);
        if (r.sixes > 0 && (!b.mostSixes.set || r.sixes > b.mostSixes.sixes))
            Write(b.mostSixes, eRecord.MostSixes, r, r.sixes, bests);
        if (r.longestSix > 0f && (!b.longestSix.set || r.longestSix > b.longestSix.value))
        {
            Write(b.longestSix, eRecord.LongestSix, r, r.longestSix, bests);
            b.longestSix.clipStamp = r.sixStamp ?? "";   // the clip goes with the six that set it
        }
    }

    /// More runs, or the same not out where the best was out (CareerProgress.AddToStats' rule).
    private static bool HigherScore(RecordEntry e, int runs, bool isOut) =>
        !e.set || runs > e.runs || (runs == e.runs && !isOut && !e.notOut);

    /// A higher strike rate, or the same with more runs. Compared as runs x balls, exactly: no float
    /// rounding decides a tie.
    private static bool BetterRate(RecordEntry e, InningsRecord r)
    {
        if (!e.set || e.balls <= 0)
            return true;
        long mine = (long)r.runs * e.balls, theirs = (long)e.runs * r.balls;
        return mine > theirs || (mine == theirs && r.runs > e.runs);
    }

    private static void Write(RecordEntry e, eRecord which, InningsRecord r, float value, List<eRecord> bests)
    {
        if (e.set)
            bests?.Add(which);
        e.set = true;
        e.value = value;
        e.runs = r.runs; e.balls = r.balls; e.sixes = r.sixes; e.notOut = !r.isOut;
        e.team = r.team ?? ""; e.opponent = r.opponent ?? ""; e.date = r.date ?? "";
        e.rung = r.rung; e.season = r.season;
        e.clipStamp = "";
    }

    private static void Clear(RecordEntry e)
    {
        e.set = e.notOut = false;
        e.value = 0f;
        e.runs = e.balls = e.sixes = e.season = 0;
        e.team = e.opponent = e.date = e.clipStamp = "";
        e.rung = eRung.Club;
    }

    /// The longest real-blade match six in the balls kept, when it beats the book's.
    private static void LongestSixFromBalls(List<BallRecord> balls, RecordEntry e)
    {
        if (balls == null)
            return;
        foreach (BallRecord b in balls)
        {
            if (b == null || b.source != eBallSource.Match || b.end != eBallEnd.Six)
                continue;
            if (b.realContact != eRealContact.Clean && b.realContact != eRealContact.Edge)
                continue;
            if (!(b.carry > 0f) || (e.set && b.carry <= e.value))
                continue;
            Clear(e);
            e.set = true;
            e.value = b.carry;
        }
    }

    // ---- The milestones ---------------------------------------------------------------------------

    /// A fifty (the first only) or a hundred (every one), with the old titles.
    private static void InningsMilestones(CareerProfile p, CareerRecordBook book, InningsRecord r)
    {
        string score = $"{r.runs}{(r.isOut ? "" : "*")} off {r.balls} balls against {Name(r.opponent)}";
        if (r.runs >= 100)
        {
            Mark(book, FiftyId);
            Mark(book, HundredId);
            Milestone(p, book, p.stats.hundreds == 1 ? "Your first hundred" : "Another hundred",
                      score + ". Raise the bat - that is a day you will remember.");
        }
        else if (r.runs >= 50 && Mark(book, FiftyId))
            Milestone(p, book, "Your first fifty", score + ". The side will remember it at selection.");
    }

    /// Career runs: every milestone passed is marked, one message names the highest.
    private static void RunsMilestone(CareerProfile p, CareerRecordBook book, InningsRecord r)
    {
        int top = 0;
        foreach (int m in RunsMilestones)
            if (p.stats.runs >= m && Mark(book, RunsId(m)))
                top = m;
        if (top == 0)
            return;
        Milestone(p, book, $"{Count(top)} career runs",
                  $"Your {r.runs} against {Name(r.opponent)} took you past {Count(top)} runs in your career.");
    }

    /// Caps for the International side: 1, 10, 25, 50 and 100.
    private static void CapsMilestone(CareerProfile p, CareerRecordBook book, InningsRecord r)
    {
        int top = 0;
        foreach (int m in CapsMilestones)
            if (p.caps >= m && Mark(book, CapsId(m)))
                top = m;
        if (top == 0)
            return;
        if (top == 1)
            Milestone(p, book, "Your first cap", $"Your first match for {Name(r.team)}, against {Name(r.opponent)}. The cap is yours for good.");
        else
            Milestone(p, book, $"{top} caps", $"{top} matches for {Name(r.team)}. Not many players get there.");
    }

    /// The coach after two ducks or more in a row (unchanged from phase 2).
    private static void DuckNote(CareerProfile p)
    {
        int ducks = CareerProgressMath.DuckStreak(p.innings);
        if (ducks >= 2)
            CareerProgress.Post(p, eMessageKind.Coach, Coach, ducks == 2 ? "Two ducks in a row" : ducks + " ducks in a row",
                "It happens to everyone. Next time watch the ball onto the bat and look for a single to get off the mark.");
    }

    /// The milestones an older save had already passed, marked without a word (Backfill).
    private static void MarkPassed(CareerProfile p, CareerRecordBook book)
    {
        CareerStats st = p.stats ?? new CareerStats();
        if (st.fifties > 0 || st.hundreds > 0) Mark(book, FiftyId);
        if (st.hundreds > 0) Mark(book, HundredId);
        foreach (int m in RunsMilestones)
            if (st.runs >= m) Mark(book, RunsId(m));
        foreach (int m in CapsMilestones)
            if (p.caps >= m) Mark(book, CapsId(m));
    }

    private static void Milestone(CareerProfile p, CareerRecordBook book, string title, string body)
    {
        CareerProgress.Post(p, eMessageKind.Milestone, Coach, title, body);
        book.lastMilestone = title;
        book.lastMilestoneDate = CareerProgressMath.Today();
    }

    /// Marks `id` reached; true the first time.
    private static bool Mark(CareerRecordBook book, string id)
    {
        if (book.reached.Contains(id))
            return false;
        book.reached.Add(id);
        return true;
    }

    // ---- Small helpers ----------------------------------------------------------------------------

    /// The profile's record book, with nothing in it null (an old or hand-edited save).
    private static CareerRecordBook Book(CareerProfile p)
    {
        CareerRecordBook b = p.recordBook ??= new CareerRecordBook();
        b.reached ??= new List<string>();
        b.highScore ??= new RecordEntry();
        b.fastestFifty ??= new RecordEntry();
        b.bestStrikeRate ??= new RecordEntry();
        b.mostSixes ??= new RecordEntry();
        b.longestSix ??= new RecordEntry();
        b.lastMilestone ??= "";
        b.lastMilestoneDate ??= "";
        p.stats ??= new CareerStats();
        return b;
    }

    /// "1,000", in the game's own (invariant) style whatever the device's language.
    private static string Count(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    private static string Name(string s) => NetsTrialCard.Latin1(s ?? "");
}
