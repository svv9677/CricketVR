using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The record book and the milestones (CareerRecords, phase 7): every best's rule and its ties, the
/// strike rate's minimum balls, only beaten records named as new bests, the three phase-2 titles
/// kept (through CareerProgress.RecordMatch, as the game posts them), the first fifty said once even
/// after a hundred, one runs message a match, the caps, and Backfill for an older save - silent and
/// the same however often it runs. Pure: no saves, no scene.
/// </summary>
public class CareerRecordsTests
{
    // ---- Helpers ----------------------------------------------------------------------------------

    private static CareerProfile Profile() => new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05" };

    private static InningsRecord Innings(int runs, int balls, bool isOut = true, int sixes = 0, string opponent = "Harbour CC",
                                         int toFifty = 0, float longestSix = 0f, string stamp = "")
    {
        return new InningsRecord
        {
            season = 1, rung = eRung.Club, team = "Rivertown", opponent = opponent, runs = runs, balls = balls,
            sixes = sixes, batted = true, isOut = isOut, ballsToFifty = toFifty, longestSix = longestSix,
            sixStamp = stamp, date = "2026-11-05",
        };
    }

    /// The innings into the book and the stats as RecordMatch puts them (AddToStats), then AfterMatch.
    private static List<eRecord> Play(CareerProfile p, InningsRecord r)
    {
        p.AddInnings(r);
        CareerStats st = p.stats;
        st.matches++;
        if (r.batted)
        {
            st.innings++;
            if (!r.isOut) st.notOuts++;
            st.runs += r.runs; st.balls += r.balls; st.sixes += r.sixes;
            if (r.runs >= 100) st.hundreds++;
            else if (r.runs >= 50) st.fifties++;
        }
        return CareerRecords.AfterMatch(p, r, new CareerMatchResult());
    }

    private static int Titled(CareerProfile p, string title) => p.inbox.FindAll(m => m.title == title).Count;

    private static void AssertLatin1(string s)
    {
        foreach (char c in s ?? "")
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    // ---- The bests --------------------------------------------------------------------------------

    [Test]
    public void AFirstInningsSetsTheRecordsButBreaksNone()
    {
        CareerProfile p = Profile();
        List<eRecord> bests = Play(p, Innings(64, 40, false, 3, toFifty: 31, longestSix: 88f, stamp: "s1"));
        Assert.IsEmpty(bests, "nothing existed to beat");
        CareerRecordBook b = p.recordBook;
        Assert.IsTrue(b.highScore.set && b.fastestFifty.set && b.bestStrikeRate.set && b.mostSixes.set && b.longestSix.set);
        Assert.AreEqual(64, b.highScore.runs);
        Assert.IsTrue(b.highScore.notOut);
        Assert.AreEqual("Harbour CC", b.highScore.opponent);
        Assert.AreEqual(31f, b.fastestFifty.value);
        Assert.AreEqual(160f, b.bestStrikeRate.value, 0.001f);
        Assert.AreEqual(3, b.mostSixes.sixes);
        Assert.AreEqual(88f, b.longestSix.value);
        Assert.AreEqual("s1", b.longestSix.clipStamp);
        Assert.AreEqual("", b.highScore.clipStamp, "only the longest six has a clip");
    }

    [Test]
    public void TheHighScore_MoreRunsOrTheSameNotOut_AnyOtherTieKeepsTheOlder()
    {
        CareerProfile p = Profile();
        Play(p, Innings(40, 30, true, opponent: "First"));
        Assert.IsFalse(Play(p, Innings(40, 30, true, opponent: "Second")).Contains(eRecord.HighScore), "a tie is no record");
        Assert.AreEqual("First", p.recordBook.highScore.opponent, "the older stands");
        Assert.Contains(eRecord.HighScore, Play(p, Innings(40, 25, false, opponent: "Third")), "the same score not out beats it");
        Assert.AreEqual("Third", p.recordBook.highScore.opponent);
        Assert.IsFalse(Play(p, Innings(40, 22, false, opponent: "Fourth")).Contains(eRecord.HighScore), "not out against not out: a tie");
        Assert.AreEqual("Third", p.recordBook.highScore.opponent);
        Assert.IsFalse(Play(p, Innings(39, 10, false)).Contains(eRecord.HighScore));
        Assert.Contains(eRecord.HighScore, Play(p, Innings(41, 50, true, opponent: "Fifth")));
        Assert.AreEqual(41, p.recordBook.highScore.runs);
        Assert.IsFalse(p.recordBook.highScore.notOut);
    }

    [Test]
    public void TheFastestFifty_FewestBalls_NeverZero_TieKeepsTheOlder()
    {
        CareerProfile p = Profile();
        Play(p, Innings(70, 60, toFifty: 0));
        Assert.IsFalse(p.recordBook.fastestFifty.set, "no balls to fifty kept: no record");
        Play(p, Innings(55, 40, toFifty: 35, opponent: "First"));
        Assert.AreEqual(35f, p.recordBook.fastestFifty.value);
        Assert.IsFalse(Play(p, Innings(52, 38, toFifty: 35, opponent: "Second")).Contains(eRecord.FastestFifty));
        Assert.AreEqual("First", p.recordBook.fastestFifty.opponent);
        Assert.IsFalse(Play(p, Innings(60, 45, toFifty: 40)).Contains(eRecord.FastestFifty));
        Assert.Contains(eRecord.FastestFifty, Play(p, Innings(51, 26, toFifty: 25, opponent: "Third")));
        Assert.AreEqual(25f, p.recordBook.fastestFifty.value);
        Assert.AreEqual("Third", p.recordBook.fastestFifty.opponent);
    }

    [Test]
    public void TheStrikeRate_NeedsTenBalls_TieGoesToMoreRunsThenTheOlder()
    {
        CareerProfile p = Profile();
        Play(p, Innings(30, 9));
        Assert.IsFalse(p.recordBook.bestStrikeRate.set, "under 10 balls is no strike rate record");
        Play(p, Innings(20, 10, opponent: "First"));
        Assert.AreEqual(200f, p.recordBook.bestStrikeRate.value, 0.001f);
        Assert.IsFalse(Play(p, Innings(20, 10, opponent: "Second")).Contains(eRecord.BestStrikeRate), "the same rate, the same runs");
        Assert.AreEqual("First", p.recordBook.bestStrikeRate.opponent);
        Assert.Contains(eRecord.BestStrikeRate, Play(p, Innings(40, 20, opponent: "Third")), "the same rate with more runs");
        Assert.AreEqual("Third", p.recordBook.bestStrikeRate.opponent);
        Assert.IsFalse(Play(p, Innings(39, 20)).Contains(eRecord.BestStrikeRate));
        Assert.IsFalse(Play(p, Innings(60, 9)).Contains(eRecord.BestStrikeRate), "a faster 9-ball innings still does not count");
        Assert.Contains(eRecord.BestStrikeRate, Play(p, Innings(25, 12, opponent: "Fourth")));
        Assert.AreEqual(25 * 100f / 12, p.recordBook.bestStrikeRate.value, 0.001f);
    }

    [Test]
    public void MostSixes_NeedsOne_TieKeepsTheOlder()
    {
        CareerProfile p = Profile();
        Play(p, Innings(30, 20, sixes: 0));
        Assert.IsFalse(p.recordBook.mostSixes.set, "no six, no record");
        Play(p, Innings(30, 20, sixes: 3, opponent: "First"));
        Assert.IsFalse(Play(p, Innings(30, 20, sixes: 3, opponent: "Second")).Contains(eRecord.MostSixes));
        Assert.AreEqual("First", p.recordBook.mostSixes.opponent);
        Assert.Contains(eRecord.MostSixes, Play(p, Innings(30, 20, sixes: 4, opponent: "Third")));
        Assert.AreEqual(4, p.recordBook.mostSixes.sixes);
        Assert.AreEqual(4f, p.recordBook.mostSixes.value);
    }

    [Test]
    public void TheLongestSix_KeepsItsClip_TieKeepsTheOlder()
    {
        CareerProfile p = Profile();
        Play(p, Innings(30, 20, sixes: 1, longestSix: 0f));
        Assert.IsFalse(p.recordBook.longestSix.set, "no real-blade six measured: no record");
        Play(p, Innings(30, 20, sixes: 1, longestSix: 82.5f, stamp: "aaaa-b7"));
        Assert.IsFalse(Play(p, Innings(30, 20, sixes: 1, longestSix: 82.5f, stamp: "bbbb-b3")).Contains(eRecord.LongestSix));
        Assert.AreEqual("aaaa-b7", p.recordBook.longestSix.clipStamp, "the older stands, with its clip");
        Assert.IsFalse(Play(p, Innings(30, 20, sixes: 1, longestSix: 70f, stamp: "cccc-b2")).Contains(eRecord.LongestSix));
        Assert.Contains(eRecord.LongestSix, Play(p, Innings(30, 20, sixes: 1, longestSix: 96f, stamp: "dddd-b9")));
        Assert.AreEqual(96f, p.recordBook.longestSix.value);
        Assert.AreEqual("dddd-b9", p.recordBook.longestSix.clipStamp, "the new six's clip");
        Play(p, Innings(30, 20, sixes: 1, longestSix: 99f, stamp: ""));
        Assert.AreEqual("", p.recordBook.longestSix.clipStamp, "a record with no clip has none, not the old one's");
    }

    [Test]
    public void AnInningsHeDidNotBatSetsNoRecord()
    {
        CareerProfile p = Profile();
        var r = Innings(0, 0);
        r.batted = false;
        r.isOut = false;
        Assert.IsEmpty(Play(p, r));
        Assert.IsFalse(p.recordBook.highScore.set);
        Assert.AreEqual(0, p.inbox.Count);
    }

    // ---- The milestones ---------------------------------------------------------------------------

    private static CareerProfile Career(int seed)
    {
        CareerProfile p = Profile();
        CareerProgress.EnsureSeason(p, new Random(seed));
        return p;
    }

    /// One match through CareerProgress.RecordMatch, his innings as given.
    private static void Record(CareerProfile p, Random rng, int runs, int balls, bool isOut)
    {
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        Assert.IsNotNull(plan, "there is a next match");
        var yours = new InningsCard { team = plan.yourTeamName, overs = plan.overs, runs = Math.Max(runs + 40, 120), wickets = 6, balls = plan.overs * 6 };
        InningsCard theirs = plan.batFirst
            ? new InningsCard { team = plan.opponentName, overs = plan.overs, runs = yours.runs - 10, wickets = 9, balls = plan.overs * 6 }
            : plan.theirs;
        var result = new CareerMatchResult
        {
            runs = runs, balls = balls, batted = true, isOut = isOut, how = isOut ? "b Someone" : "not out",
            yours = yours, theirs = theirs, won = true,
        };
        CareerProgress.RecordMatch(p, plan, result, rng);
    }

    [Test]
    public void TheThreeOldTitlesAreKept()
    {
        CareerProfile p = Career(1);
        var rng = new Random(2);
        Record(p, rng, 55, 40, false);
        Assert.AreEqual(1, Titled(p, "Your first fifty"));
        Record(p, rng, 120, 70, true);
        Assert.AreEqual(1, Titled(p, "Your first hundred"));
        Record(p, rng, 101, 80, true);
        Assert.AreEqual(1, Titled(p, "Another hundred"));
        Record(p, rng, 0, 2, true);
        Record(p, rng, 0, 1, true);
        Assert.AreEqual(1, Titled(p, "Two ducks in a row"));
        Assert.IsTrue(p.inbox.Exists(m => m.title == "Two ducks in a row" && m.kind == eMessageKind.Coach));
        Assert.AreEqual(1, Titled(p, "Your first fifty"), "said once");
    }

    [Test]
    public void AHundredFirstThenAFifty_SaysNoFirstFifty()
    {
        CareerProfile p = Career(3);
        var rng = new Random(4);
        Record(p, rng, 104, 66, true);
        Assert.AreEqual(1, Titled(p, "Your first hundred"));
        Assert.Contains(CareerRecords.FiftyId, p.recordBook.reached, "a hundred is past fifty");
        Record(p, rng, 55, 41, true);
        Assert.AreEqual(0, Titled(p, "Your first fifty"));
    }

    [Test]
    public void SeveralRunsMilestonesInOneMatchPostOneMessage()
    {
        CareerProfile p = Profile();
        p.stats.runs = 90;
        Play(p, Innings(170, 120, opponent: "Harbour CC"));   // 260: past 100 and 250
        List<CareerMessage> runs = p.inbox.FindAll(m => m.title.EndsWith("career runs"));
        Assert.AreEqual(1, runs.Count, "one message for the match");
        Assert.AreEqual("250 career runs", runs[0].title);
        Assert.AreEqual(eMessageKind.Milestone, runs[0].kind);
        Assert.Contains(CareerRecords.RunsId(100), p.recordBook.reached);
        Assert.Contains(CareerRecords.RunsId(250), p.recordBook.reached);
        Assert.IsFalse(p.recordBook.reached.Contains(CareerRecords.RunsId(500)));
        Play(p, Innings(10, 8));
        Assert.AreEqual(1, p.inbox.FindAll(m => m.title.EndsWith("career runs")).Count, "nothing new passed");
        p.stats.runs = 990;
        Play(p, Innings(15, 9));
        Assert.IsTrue(p.inbox.Exists(m => m.title == "1,000 career runs"), "the thousands written with a comma");
        Assert.AreEqual("1,000 career runs", p.recordBook.lastMilestone);
        Assert.AreEqual(CareerProgressMath.Today(), p.recordBook.lastMilestoneDate);
    }

    [Test]
    public void CapsOneTenAndTwentyFiveEachPost()
    {
        CareerProfile p = Profile();
        p.caps = 1;
        Play(p, Innings(12, 10));
        Assert.AreEqual(1, Titled(p, "Your first cap"));
        p.caps = 10;
        Play(p, Innings(12, 10));
        Assert.AreEqual(1, Titled(p, "10 caps"));
        p.caps = 11;
        Play(p, Innings(12, 10));
        p.caps = 25;
        var dnb = Innings(0, 0);
        dnb.batted = false;
        Play(p, dnb);
        Assert.AreEqual(1, Titled(p, "25 caps"), "a cap counts whether he batted or not");
        Assert.AreEqual(3, p.inbox.FindAll(m => m.kind == eMessageKind.Milestone && m.title.Contains("cap")).Count);
        Assert.AreEqual("25 caps", p.recordBook.lastMilestone);
    }

    [Test]
    public void EveryMessageIsLatin1()
    {
        CareerProfile p = Profile();
        p.caps = 1;
        Play(p, Innings(120, 70, opponent: "São Paulo — XI"));
        p.caps = 10;
        Play(p, Innings(0, 2, opponent: "Łódź"));
        Play(p, Innings(0, 1));
        Assert.Greater(p.inbox.Count, 3);
        foreach (CareerMessage m in p.inbox)
        {
            AssertLatin1(m.title);
            AssertLatin1(m.body);
        }
        AssertLatin1(p.recordBook.lastMilestone);
    }

    // ---- Backfill ---------------------------------------------------------------------------------

    private static CareerProfile OldSave()
    {
        CareerProfile p = Career(5);
        var rng = new Random(6);
        Record(p, rng, 30, 20, true);
        Record(p, rng, 64, 30, false);
        Record(p, rng, 12, 15, true);
        p.recordBook = new CareerRecordBook();     // a v2 save: no record book yet
        p.balls.Add(new BallRecord { source = eBallSource.Match, end = eBallEnd.Six, realContact = eRealContact.Clean, carry = 77f });
        p.balls.Add(new BallRecord { source = eBallSource.Match, end = eBallEnd.Six, realContact = eRealContact.WouldHaveMissed, carry = 120f });
        p.balls.Add(new BallRecord { source = eBallSource.Nets, end = eBallEnd.Six, realContact = eRealContact.Clean, carry = 110f });
        p.balls.Add(new BallRecord { source = eBallSource.Match, end = eBallEnd.Four, realContact = eRealContact.Clean, carry = 90f });
        p.balls.Add(new BallRecord { source = eBallSource.Match, end = eBallEnd.Six, realContact = eRealContact.Edge, carry = 81f });
        return p;
    }

    [Test]
    public void BackfillWorksTheBookOutSilently()
    {
        CareerProfile p = OldSave();
        int messages = p.inbox.Count;
        CareerRecords.Backfill(p);
        Assert.AreEqual(messages, p.inbox.Count, "no messages");
        CareerRecordBook b = p.recordBook;
        Assert.AreEqual(64, b.highScore.runs);
        Assert.IsTrue(b.highScore.notOut);
        Assert.IsNotEmpty(b.highScore.opponent, "from the innings: the match is known");
        Assert.AreEqual(64 * 100f / 30, b.bestStrikeRate.value, 0.001f);
        Assert.IsFalse(b.fastestFifty.set, "v2 never kept the balls to fifty");
        Assert.IsTrue(b.longestSix.set);
        Assert.AreEqual(81f, b.longestSix.value, "match sixes off the real blade only: not the shield's, not the nets'");
        Assert.AreEqual("", b.longestSix.opponent);
        Assert.AreEqual("", b.longestSix.clipStamp);
        Assert.Contains(CareerRecords.FiftyId, b.reached);
        Assert.Contains(CareerRecords.RunsId(100), b.reached, "106 runs: past 100");
        Assert.IsFalse(b.reached.Contains(CareerRecords.RunsId(250)));
    }

    [Test]
    public void BackfillIsTheSameRunTwice()
    {
        CareerProfile p = OldSave();
        CareerRecords.Backfill(p);
        CareerRecordBook b = p.recordBook;
        var before = new List<string>(b.reached);
        string high = b.highScore.opponent + b.highScore.runs, six = b.longestSix.value.ToString();
        int messages = p.inbox.Count;
        CareerRecords.Backfill(p);
        CollectionAssert.AreEqual(before, b.reached);
        Assert.AreEqual(high, b.highScore.opponent + b.highScore.runs);
        Assert.AreEqual(six, b.longestSix.value.ToString());
        Assert.AreEqual(messages, p.inbox.Count);
    }

    [Test]
    public void BackfillFallsBackOnTheStatsWhenTheBookWasTrimmed()
    {
        CareerProfile p = OldSave();
        p.stats.highScore = 150;               // an innings since trimmed from the book
        p.stats.highScoreNotOut = false;
        p.stats.hundreds = 1;
        p.caps = 12;
        CareerRecords.Backfill(p);
        Assert.AreEqual(150, p.recordBook.highScore.runs);
        Assert.AreEqual("", p.recordBook.highScore.opponent, "the match is not known");
        Assert.Contains(CareerRecords.HundredId, p.recordBook.reached);
        Assert.Contains(CareerRecords.CapsId(1), p.recordBook.reached);
        Assert.Contains(CareerRecords.CapsId(10), p.recordBook.reached);
        Assert.IsFalse(p.recordBook.reached.Contains(CareerRecords.CapsId(25)));
        // His fifties were before the backfill: a new one is not his first.
        int firsts = Titled(p, "Your first fifty");
        Play(p, Innings(55, 40));
        Assert.AreEqual(firsts, Titled(p, "Your first fifty"));
    }

    [Test]
    public void BackfillOfNothingIsHarmless()
    {
        CareerRecords.Backfill(null);
        var p = new CareerProfile { recordBook = null, stats = null, innings = null, balls = null };
        CareerRecords.Backfill(p);
        Assert.IsNotNull(p.recordBook);
        Assert.IsEmpty(p.recordBook.reached);
        Assert.IsFalse(p.recordBook.highScore.set);
        Assert.IsEmpty(CareerRecords.AfterMatch(null, null, null));
    }
}
