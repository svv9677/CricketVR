using System.Collections.Generic;
using NUnit.Framework;
using static DrillTests;

/// <summary>
/// The drills' coach and words (DrillCoach, DrillText): the drill for the weakest rating, the
/// attempt counter, the record of last and best, the training slot (used once per round, one Coach
/// message, sessions per season), and every line Latin-1 and saying what happened - the score, the
/// ratings fed, the commonest good thing and the one thing to work on. Standalone (no scene, no
/// save files); the ball records are DrillTests'.
/// </summary>
public class DrillCoachTests
{
    // ---- The coach --------------------------------------------------------------------------------

    private static RatingSet Ratings(float all, int low = -1, float lowValue = 10f)
    {
        var r = new RatingSet();
        for (int i = 0; i < RatingSet.Count; i++) r[i] = all;
        if (low >= 0) r[low] = lowValue;
        return r;
    }

    [Test]
    public void TheCoachPicksTheDrillForTheWeakestRating()
    {
        Assert.AreEqual(eDrill.Yorkers, DrillCoach.Recommend(new RatingSet()));
        Assert.AreEqual(eDrill.Yorkers, DrillCoach.Recommend(null));
        Assert.AreEqual(eDrill.Gaps, DrillCoach.Recommend(Ratings(50f, 0)));
        Assert.AreEqual(eDrill.PowerHitting, DrillCoach.Recommend(Ratings(50f, 1)));
        Assert.AreEqual(eDrill.Yorkers, DrillCoach.Recommend(Ratings(50f, 2)));
        Assert.AreEqual(eDrill.Spin, DrillCoach.Recommend(Ratings(50f, 3)));
        Assert.AreEqual(eDrill.SoftHands, DrillCoach.Recommend(Ratings(50f, 4)));
        Assert.AreEqual(eDrill.LeaveOutsideOff, DrillCoach.Recommend(Ratings(50f, 5)));
        RatingSet temper = Ratings(50f, 6);
        temper.spin = 30f;
        Assert.AreEqual(eDrill.Spin, DrillCoach.Recommend(temper), "temperament has no drill: the next weakest");
    }

    private static CareerProfile WithSeason()
    {
        var p = new CareerProfile { name = "Asha", created = "2026-11-05" };
        p.season = new Season { number = 1, yourTeam = "a" };
        p.season.fixtures.Add(new Fixture { round = 1, home = "a", away = "b" });
        p.season.fixtures.Add(new Fixture { round = 2, home = "c", away = "a" });
        return p;
    }

    private static DrillResult Result(eDrill d, int score) =>
        new DrillResult { drill = d, score = score, grade = DrillScoring.Grade(score) };

    [Test]
    public void RecordKeepsAttemptsLastAndBest()
    {
        var p = new CareerProfile();
        Assert.AreEqual(1, DrillCoach.Start(p, eDrill.Spin));
        Assert.AreEqual(2, DrillCoach.Start(p, eDrill.Spin));
        Assert.AreEqual(1, DrillCoach.Start(p, eDrill.Gaps));
        Assert.AreEqual(0, DrillCoach.Start(null, eDrill.Spin));

        DrillResult first = Result(eDrill.Spin, 55);
        DrillCoach.Record(p, first, "2026-11-05");
        Assert.IsTrue(first.newBest);
        Assert.AreEqual(55, first.best);
        DrillResult worse = Result(eDrill.Spin, 40);
        DrillCoach.Record(p, worse, "2026-11-05");
        Assert.IsFalse(worse.newBest);
        Assert.AreEqual(55, worse.best);
        DrillRecord rec = p.drills.For(eDrill.Spin);
        Assert.AreEqual(2, rec.attempts);
        Assert.AreEqual(40, rec.last);
        Assert.AreEqual(55, rec.best);
        Assert.AreEqual(2, rec.started);
        Assert.AreEqual(0, p.inbox.Count, "no season: no training slot, no message");
        Assert.IsFalse(DrillCoach.SlotOpen(p));
    }

    [Test]
    public void TheTrainingSlotIsUsedOncePerRoundAndPostsTheCoach()
    {
        CareerProfile p = WithSeason();
        Assert.IsTrue(DrillCoach.SlotOpen(p));
        DrillCoach.Record(p, Result(eDrill.Yorkers, 72), "2026-11-05");
        Assert.AreEqual(1, p.inbox.Count);
        CareerMessage m = p.inbox[0];
        Assert.AreEqual(eMessageKind.Coach, m.kind);
        Assert.AreEqual("Coach", m.from);
        Assert.AreEqual("2026-11-05", m.date);
        StringAssert.Contains("72", m.title);
        Assert.IsFalse(string.IsNullOrEmpty(m.body));
        Assert.AreEqual(1, p.drills.slotRound);
        Assert.AreEqual(1, p.drills.slotSeason);
        Assert.AreEqual(1, p.drills.sessions);
        Assert.IsFalse(DrillCoach.SlotOpen(p));

        DrillCoach.Record(p, Result(eDrill.Spin, 90), "2026-11-05");
        Assert.AreEqual(1, p.inbox.Count, "the slot is used once a round");
        Assert.AreEqual(1, p.drills.sessions);

        p.season.fixtures[0].played = true;
        Assert.IsTrue(DrillCoach.SlotOpen(p), "a new round, a new slot");
        DrillCoach.Record(p, Result(eDrill.Spin, 90), "2026-11-06");
        Assert.AreEqual(2, p.inbox.Count);
        Assert.AreEqual(2, p.drills.slotRound);
        Assert.AreEqual(2, p.drills.sessions);

        p.season = new Season { number = 2, yourTeam = "a" };
        p.season.fixtures.Add(new Fixture { round = 1, home = "a", away = "b" });
        DrillCoach.Record(p, Result(eDrill.Gaps, 30), "2026-11-07");
        Assert.AreEqual(1, p.drills.sessions, "sessions count per season");
        Assert.AreEqual(2, p.drills.sessionsSeason);

        p.season.finished = true;
        Assert.IsFalse(DrillCoach.SlotOpen(p), "no next fixture, no slot");
    }

    // ---- The words --------------------------------------------------------------------------------

    private static void AssertLatin1(string s, string what)
    {
        Assert.IsNotNull(s, what);
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"{what}: '{c}' is not Latin-1 in \"{s}\"");
    }

    [Test]
    public void EveryLineIsLatin1AndTheCardSaysWhatHappened()
    {
        CareerProfile p = WithSeason();
        p.ratings = Ratings(50f, 3, 20f);
        foreach (DrillDef d in DrillDefs.All)
        {
            AssertLatin1(d.name + d.title + d.blurb, d.name);
            foreach (Case c in Cases)
            {
                if (c.drill != d.id) continue;
                var balls = Many(12, () => c.r);
                DrillResult r = DrillScoring.Tally(d, balls);
                r.before = Ratings(40f);
                r.after = Ratings(43f);
                string body = DrillText.ResultBody(d, r), verdict = DrillText.Verdict(d, r);
                AssertLatin1(body, c.name);
                AssertLatin1(verdict, c.name);
                Assert.IsFalse(string.IsNullOrEmpty(verdict), c.name);
                StringAssert.Contains($"<b>{r.score}</b> / 100", body, c.name);
                StringAssert.Contains("Press A to continue", body, c.name);
                foreach (int i in d.feeds)
                    StringAssert.Contains(RatingSet.Names[i], body, c.name);
                StringAssert.Contains("(+3)", body, c.name);
                AssertLatin1(DrillText.ProgressText(d, 4, eBowlerKind.LegSpin, 2, c.r, c.tag), c.name);
                StringAssert.Contains("counted (needed", body, c.name);
            }
            AssertLatin1(DrillText.Feeds(d), d.name);
            AssertLatin1(DrillText.RowDetail(d, null, true), d.name);
            AssertLatin1(DrillText.Heading(new DrillResult { drill = d.id }), d.name);
        }
        foreach (eDrillTag t in System.Enum.GetValues(typeof(eDrillTag)))
            if (t != eDrillTag.None)
                Assert.IsFalse(string.IsNullOrEmpty(DrillText.TagLabel(t)), t.ToString());
        AssertLatin1(DrillText.Header(p), "header");
    }

    [Test]
    public void TheLinesNameTheDrillTheBallAndTheRecord()
    {
        DrillDef y = DrillDefs.For(eDrill.Yorkers);
        Assert.AreEqual("Yorker drill complete", DrillText.Heading(new DrillResult { drill = eDrill.Yorkers }));
        Assert.AreEqual("Feeds Defence · vs Pace", DrillText.Feeds(y));

        string first = DrillText.ProgressText(y, 1, eBowlerKind.Fast, 0, null, eDrillTag.None);
        StringAssert.Contains("YORKER DRILL", first);
        StringAssert.Contains("Ball 1 of 12", first);
        StringAssert.Contains("Fast bowler", first);
        StringAssert.Contains("Dig out 8 of 12 yorkers", first);
        StringAssert.Contains("<b>0</b> of 8, 8 more from 12 balls", first);
        StringAssert.DoesNotContain("Score", first);
        StringAssert.DoesNotContain("Last", first);
        var dug = new BallRecord { action = eBallAction.Played, realContact = eRealContact.Clean, exitSpeed = 20f, end = eBallEnd.Runs, runs = 1 };
        string later = DrillText.ProgressText(y, 4, eBowlerKind.Medium, 3, dug, eDrillTag.DugOut);
        StringAssert.Contains("Ball 4 of 12", later);
        StringAssert.Contains("<b>3</b> of 8, 5 more from 9 balls", later);
        StringAssert.Contains("<b>+1</b> Dug out", later);
        StringAssert.Contains("72 km/h", later);
        var edged = new BallRecord { action = eBallAction.Played, realContact = eRealContact.Edge, exitSpeed = 10f };
        StringAssert.Contains("Edged</color> · 36 km/h · doesn't count", DrillText.ProgressText(y, 5, eBowlerKind.Fast, 3, edged, eDrillTag.Edged));
        StringAssert.Contains("Goal met (8)", DrillText.ProgressText(y, 11, eBowlerKind.Fast, 8, dug, eDrillTag.DugOut));
        StringAssert.Contains("out of reach: 2 of 8", DrillText.ProgressText(y, 8, eBowlerKind.Fast, 2, edged, eDrillTag.Edged));

        StringAssert.Contains("Not tried yet", DrillText.RowDetail(y, null, false));
        string row = DrillText.RowDetail(y, new DrillRecord { drill = eDrill.Yorkers, attempts = 2, last = 64, best = 72 }, true);
        StringAssert.Contains("Coach's pick", row);
        StringAssert.Contains("Last 64", row);
        StringAssert.Contains("Best 72", row);
        StringAssert.DoesNotContain("Coach's pick", DrillText.RowDetail(y, null, false));

        CareerProfile p = WithSeason();
        p.ratings = Ratings(50f, 5);
        string header = DrillText.Header(p);
        StringAssert.Contains("Training before Round 1", header);
        StringAssert.Contains("Coach's pick: Leave outside off", header);
        p.season.finished = true;
        Assert.AreEqual("Coach's pick: Leave outside off", DrillText.Header(p));
        Assert.AreEqual("", DrillText.Header(null));
    }

    [Test]
    public void TheVerdictPraisesTheBestAndNamesTheOneThingToWorkOn()
    {
        DrillDef y = DrillDefs.For(eDrill.Yorkers);
        var balls = Many(8, () => R(runs: 1));
        balls.AddRange(Many(4, Out));
        DrillResult r = DrillScoring.Tally(y, balls);
        string v = DrillText.Verdict(y, r);
        StringAssert.Contains("Good session.", v);
        StringAssert.Contains("You dug out 8 of them.", v);
        StringAssert.Contains("Get the bat down earlier", v);

        DrillResult clean = DrillScoring.Tally(y, Many(12, () => R(runs: 1)));
        StringAssert.Contains("Superb session.", DrillText.Verdict(y, clean));
        StringAssert.Contains("Nothing got past you", DrillText.Verdict(y, clean));

        DrillDef leave = DrillDefs.For(eDrill.LeaveOutsideOff);
        DrillResult fishing = DrillScoring.Tally(leave, Many(12, () => R(line: 0.6f)));
        StringAssert.Contains("fishing outside off", DrillText.Verdict(leave, fishing));

        DrillDef gaps = DrillDefs.For(eDrill.Gaps);
        DrillResult fielders = DrillScoring.Tally(gaps, Many(12, () => Shot(55f, 25f, 3f, 5f)));
        StringAssert.Contains("A tough one", DrillText.Verdict(gaps, fielders));
        StringAssert.Contains("Pick the gap before the ball", DrillText.Verdict(gaps, fielders));
        Assert.AreEqual("", DrillText.Verdict(gaps, null));
    }
}
