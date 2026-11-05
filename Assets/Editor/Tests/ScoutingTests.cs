using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The scouts (Scouting): what a scout makes of an innings (a big quick not out above a duck, the
/// ratings and form counting too), his note, the matches he watches (three of the side's league
/// games and the final; every match at International), a scouted match reported into the record
/// book and the inbox, and what the rung above needs before it offers.
/// </summary>
public class ScoutingTests
{
    private static InningsRecord Inn(int runs, int balls, bool isOut, eRung rung = eRung.Club) =>
        new InningsRecord { runs = runs, balls = balls, isOut = isOut, batted = true, rung = rung, opponent = "Old Mill CC" };

    private static RatingSet Ratings(float v)
    {
        var r = new RatingSet();
        for (int i = 0; i < RatingSet.Count; i++)
            r[i] = v;
        return r;
    }

    [Test]
    public void ABigQuickNotOutImpressesMoreThanADuck()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
        {
            RatingSet r = Ratings(50f);
            float big = Scouting.Impression(Inn(60, 30, false, rung), rung, r, 60f);
            float fair = Scouting.Impression(Inn(15, 15, true, rung), rung, r, 60f);
            float duck = Scouting.Impression(Inn(0, 3, true, rung), rung, r, 60f);
            Assert.Greater(big, fair, rung.ToString());
            Assert.Greater(fair, duck, rung.ToString());
            foreach (float x in new[] { big, fair, duck })
                Assert.That(x, Is.InRange(0f, 1f));
        }
        InningsRecord same = Inn(20, 18, true);
        Assert.Greater(Scouting.Impression(same, eRung.Club, Ratings(70f), 60f),
                       Scouting.Impression(same, eRung.Club, Ratings(30f), 60f), "the ratings count");
        Assert.Greater(Scouting.Impression(same, eRung.Club, Ratings(50f), 90f),
                       Scouting.Impression(same, eRung.Club, Ratings(50f), 10f), "form counts");
        Assert.Greater(Scouting.Impression(Inn(20, 18, true), eRung.Club, Ratings(50f), 50f),
                       Scouting.Impression(Inn(20, 18, true), eRung.ProLeague, Ratings(50f), 50f), "the bar rises up the ladder");
        Assert.That(Scouting.Impression(null, eRung.Club, null, RatingSet.Unknown), Is.InRange(0f, 1f));
    }

    [Test]
    public void TheReportNamesTheInningsInLatin1()
    {
        string club = Scouting.Report(Inn(34, 22, false), eRung.State, 0.75f);
        StringAssert.Contains("34* off 22 balls against Old Mill CC", club);
        StringAssert.Contains("State", club);
        Assert.AreNotEqual(club, Scouting.Report(Inn(34, 22, false), eRung.State, 0.1f), "a poor impression reads differently");
        string intl = Scouting.Report(Inn(5, 9, true), eRung.International, 0.2f);
        StringAssert.Contains("selectors", intl);
        StringAssert.Contains("no innings", Scouting.Report(new InningsRecord { opponent = "X" }, eRung.State, 0.4f));
        foreach (float i in new[] { 0f, 0.35f, 0.55f, 0.9f })
            foreach (eRung from in new[] { eRung.State, eRung.ProLeague, eRung.International })
                Assert.IsTrue(NameBank.IsLatin1(Scouting.Report(Inn(12, 10, true), from, i)));
        Assert.AreEqual("Scout, Pro League", Scouting.From(eRung.State));
        Assert.AreEqual("Selector", Scouting.From(eRung.International));
    }

    [Test]
    public void AScoutWatchesThreeLeagueGamesAndTheFinal()
    {
        var rounds = new HashSet<string>();
        for (int seed = 0; seed < 25; seed++)
        {
            Season s = LeagueBuilder.NewSeason(eRung.Club, 1, "AUS", null, new SquadPlayer { name = "Sam Carter" }, new Random(seed));
            List<Fixture> mine = s.fixtures.FindAll(f => f.Involves(s.yourTeam));
            Assert.AreEqual(Ladder.TeamsInLeague - 1, mine.Count);
            List<Fixture> watched = mine.FindAll(f => f.scout);
            Assert.AreEqual(Scouting.ScoutedGames, watched.Count, "seed " + seed);
            rounds.Add(string.Join(",", watched.ConvertAll(f => f.round)));

            Season again = LeagueBuilder.NewSeason(eRung.Club, 1, "AUS", null, new SquadPlayer { name = "Sam Carter" }, new Random(seed));
            CollectionAssert.AreEqual(s.fixtures.ConvertAll(f => f.scout), again.fixtures.ConvertAll(f => f.scout), "seeded");
        }
        Assert.Greater(rounds.Count, 5, "the scouted games fall in different rounds");

        Season intl = LeagueBuilder.NewSeason(eRung.International, 1, "AUS", null, new SquadPlayer { name = "Sam Carter" }, new Random(3));
        Assert.IsTrue(intl.fixtures.FindAll(f => f.Involves(intl.yourTeam)).TrueForAll(f => f.scout), "the selectors watch every match");

        CareerProfile p = CareerTestPlay.Career(4);
        CareerTestPlay.Season(p, new Random(5), 20, 18, true);
        Assert.IsTrue(p.season.fixtures.Find(f => f.final).scout, "a scout at every final");
    }

    [Test]
    public void AScoutedMatchIsReported()
    {
        CareerProfile p = CareerTestPlay.Career(6, eRung.Club, 50f);
        var rng = new Random(7);
        int scouted = 0, quiet = 0;
        while (CareerProgress.NextFixture(p) != null && !CareerProgress.NextFixture(p).final)
        {
            Fixture f = CareerProgress.NextFixture(p);
            bool watched = f.scout;
            int messages = p.inbox.FindAll(m => m.kind == eMessageKind.Scout).Count;
            MatchPlan plan = CareerTestPlay.Play(p, rng, 25, 20, true);
            Assert.AreEqual(watched, plan.scout, "the plan says a scout is there");
            Assert.AreEqual(eRung.State, plan.scoutFrom);
            InningsRecord rec = p.innings[p.innings.Count - 1];
            Assert.AreEqual(watched, rec.scouted);
            if (!watched) { quiet++; Assert.AreEqual(0f, rec.impression); continue; }
            scouted++;
            Assert.Greater(rec.impression, 0f);
            Assert.AreEqual(rec.impression, p.scoutReports[p.scoutReports.Count - 1]);
            CareerMessage m = CareerTestPlay.Last(p, eMessageKind.Scout);
            Assert.AreEqual(messages + 1, p.inbox.FindAll(x => x.kind == eMessageKind.Scout).Count);
            Assert.AreEqual("Scout, State", m.from);
            StringAssert.Contains("25 off 20 balls", m.body);
        }
        Assert.AreEqual(Scouting.ScoutedGames, scouted);
        Assert.AreEqual(Ladder.TeamsInLeague - 1 - Scouting.ScoutedGames, quiet);
        Assert.AreEqual(scouted, p.scoutReports.Count);
    }

    [Test]
    public void TheSelectorsReportEveryInternationalMatch()
    {
        CareerProfile p = CareerTestPlay.Career(8, eRung.International, 75f);
        var rng = new Random(9);
        CareerTestPlay.Play(p, rng, 30, 20, true);
        CareerTestPlay.Play(p, rng, 30, 20, true);
        Assert.AreEqual(2, p.scoutReports.Count);
        Assert.AreEqual("Selector", CareerTestPlay.Last(p, eMessageKind.Scout).from);
    }

    /// A Club player with five good innings, the overall at the State bar, and two good reports.
    private static CareerProfile Ready()
    {
        var p = new CareerProfile { name = "Sam Carter", rung = eRung.Club };
        for (int i = 0; i < RatingSet.Count; i++)
            p.ratings[i] = Ladder.PromotionRating(eRung.Club);
        for (int i = 0; i < 5; i++)
            p.AddInnings(Inn(14, 14, true));       // form about 89
        p.scoutReports.Add(0.5f);
        p.scoutReports.Add(0.5f);
        return p;
    }

    [Test]
    public void InterestNeedsTheRatingFormAndReports()
    {
        Assert.IsTrue(Scouting.Interested(Ready()), "every bar met exactly");

        CareerProfile p = Ready();
        p.ratings.timing -= 1f;
        Assert.IsFalse(Scouting.Interested(p), "overall below Ladder.PromotionRating");

        p = Ready();
        p.innings.Clear();
        for (int i = 0; i < 5; i++)
            p.AddInnings(Inn(4, 10, true));        // form about 27
        Assert.Less(CareerProgress.Form(p), Scouting.MinForm);
        Assert.IsFalse(Scouting.Interested(p), "form below 45");

        p = Ready();
        p.scoutReports.RemoveAt(0);
        Assert.IsFalse(Scouting.Interested(p), "one report is not enough");

        p = Ready();
        p.scoutReports[1] = 0.48f;
        Assert.IsFalse(Scouting.Interested(p), "reports averaging under 0.5");

        p = Ready();
        p.ratings = new RatingSet();
        Assert.IsFalse(Scouting.Interested(p), "nothing measured, nothing to offer on");

        p = Ready();
        p.rung = eRung.International;
        for (int i = 0; i < RatingSet.Count; i++)
            p.ratings[i] = 99f;
        Assert.IsFalse(Scouting.Interested(p), "nothing above the top");
    }

    [Test]
    public void TheVerdictSaysWhatTheScoutsThought()
    {
        StringAssert.Contains("No scout", Scouting.Verdict(new List<float>(), eRung.Club));
        StringAssert.Contains("liked what they saw", Scouting.Verdict(new List<float> { 0.8f, 0.9f }, eRung.Club));
        StringAssert.Contains("not convinced", Scouting.Verdict(new List<float> { 0.1f }, eRung.State));
        StringAssert.Contains("Pro League scouts", Scouting.Verdict(new List<float> { 0.5f }, eRung.State));
        StringAssert.Contains("selectors", Scouting.Verdict(new List<float> { 0.5f }, eRung.International));
        Assert.AreEqual(0.5f, Scouting.AverageReport(new List<float> { 0.4f, 0.6f }), 1e-5f);
    }
}
