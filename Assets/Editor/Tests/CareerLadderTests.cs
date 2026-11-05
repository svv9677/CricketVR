using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Matches played into a career as the match scene would report them, for the phase 4 tests
/// (CareerLadderTests, ScoutingTests, SelectionTests): a career at a rung with its ratings set,
/// one match, and a whole season with the matches he is left out of played without him.
/// </summary>
internal static class CareerTestPlay
{
    public static CareerProfile Career(int seed, eRung rung = eRung.Club, float ratings = -1f)
    {
        var p = new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05", rung = rung };
        if (ratings >= 0f)
            for (int i = 0; i < RatingSet.Count; i++)
                p.ratings[i] = ratings;
        CareerProgress.EnsureSeason(p, new Random(seed));
        return p;
    }

    /// The match as the match scene would report it, with both cards agreeing with won/lost.
    public static CareerMatchResult Result(MatchPlan plan, int runs, int balls, bool isOut, bool won)
    {
        int full = plan.overs * 6;
        var yours = new InningsCard { team = plan.yourTeamName, overs = plan.overs, wickets = isOut ? 5 : 3, balls = full };
        InningsCard theirs;
        if (plan.batFirst)
        {
            yours.runs = Math.Max(runs + 40, 90);
            theirs = new InningsCard { team = plan.opponentName, overs = plan.overs, balls = full, wickets = 7,
                                       target = yours.runs + 1, runs = won ? yours.runs - 12 : yours.runs + 1 };
        }
        else
        {
            theirs = plan.theirs;
            yours.target = theirs.runs + 1;
            yours.runs = won ? Math.Max(theirs.runs + 1, runs) : Math.Max(0, theirs.runs - 10);
        }
        return new CareerMatchResult
        {
            runs = runs, balls = balls, batted = true, isOut = isOut, how = isOut ? "b Someone" : "not out",
            yours = yours, theirs = theirs, won = won,
        };
    }

    public static MatchPlan Play(CareerProfile p, Random rng, int runs, int balls, bool isOut, bool won = true)
    {
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        Assert.IsNotNull(plan, "there is a next match to play");
        CareerProgress.RecordMatch(p, plan, Result(plan, runs, balls, isOut, won), rng);
        return plan;
    }

    /// The season played to its end, every innings the same; returns the matches he played.
    public static int Season(CareerProfile p, Random rng, int runs, int balls, bool isOut, bool won = true)
    {
        int played = 0;
        for (int guard = 0; !p.season.finished; guard++)
        {
            Assert.Less(guard, 30, "the season ends");
            if (CareerProgress.LeftOutNext(p))
                CareerProgress.PlayLeftOut(p, rng);
            else
            {
                Play(p, rng, runs, balls, isOut, won);
                played++;
            }
        }
        return played;
    }

    public static CareerMessage Last(CareerProfile p, eMessageKind kind) =>
        p.inbox.FindLast(m => m.kind == kind);
}

/// <summary>
/// The ladder (phase 4): a strong season brings the rung above's offer (one to three of its sides,
/// and "stay"), joining one moves rung, side and slot, declining stays; offers lapse once the next
/// season's first match is played; from the Pro League the offer is the national call-up, and
/// International matches earn caps; a poor season below the rung's standard is a release down a
/// rung; an ordinary one keeps the side; and the whole career saves and loads with it.
/// </summary>
public class CareerLadderTests
{
    private static CareerProfile StrongSeason(int seed, eRung rung, out Random rng)
    {
        CareerProfile p = CareerTestPlay.Career(seed, rung, 80f);
        rng = new Random(seed + 1);
        CareerTestPlay.Season(p, rng, 40, 25, false);
        CareerProgress.EnsureSeason(p, rng);    // the review, and the next season begun at once
        return p;
    }

    [Test]
    public void AStrongSeasonBringsAnOfferAndJoiningMovesUp()
    {
        CareerProfile p = StrongSeason(30, eRung.Club, out Random rng);
        string side = p.season.Yours.name;
        Assert.AreEqual(eRung.Club, p.season.rung, "the next season is begun at his rung");
        Assert.AreEqual(2, p.season.number);
        Assert.IsEmpty(p.scoutReports, "the reports start again each season");
        Assert.IsTrue(p.inbox.Exists(m => m.title == "Season 1 review" && m.body.Contains("scouts")), "the review gives the scouts' verdict");

        CareerMessage offer = CareerProgress.PendingOffer(p);
        Assert.IsNotNull(offer);
        Assert.AreEqual(eMessageKind.Offer, offer.kind);
        Assert.AreEqual("Scout, State", offer.from);
        Assert.That(offer.options.Count, Is.InRange(2, 4), "one to three sides, and stay");
        Assert.AreEqual("stay", offer.options[offer.options.Count - 1].action);
        var names = new HashSet<string>();
        for (int i = 0; i < offer.options.Count - 1; i++)
        {
            string[] a = offer.options[i].action.Split(':');
            Assert.AreEqual("join", a[0]);
            Assert.AreEqual(((int)eRung.State).ToString(), a[1]);
            Assert.IsTrue(Array.Exists(NameBank.Teams(eRung.State), t => t.name == a[2]), a[2] + " is a State side");
            Assert.IsTrue(names.Add(a[2]), "the sides are different");
        }
        Assert.IsTrue(NameBank.IsLatin1(offer.title + offer.body));

        MessageOption join = offer.options[0];
        string chosen = join.action.Split(':')[2];
        CareerProgress.Choose(p, offer, join, rng);
        Assert.IsTrue(offer.answered);
        Assert.IsNull(CareerProgress.PendingOffer(p));
        Assert.AreEqual(eRung.State, p.rung);
        Assert.AreEqual(eRung.State, p.season.rung);
        Assert.AreEqual(2, p.season.number, "the season he had not started is replaced");
        Assert.AreEqual(chosen, p.season.Yours.name);
        Assert.AreNotEqual(side, p.season.Yours.name);
        Assert.AreEqual(1, p.season.Yours.players.FindAll(x => x.isYou).Count);
        Assert.AreEqual(Ladder.LowestSlot(eRung.State), p.battingSlot);
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        Assert.AreEqual(Ladder.Overs(eRung.State), plan.overs);
        Assert.AreEqual(Ladder.LowestSlot(eRung.State) - 1, plan.slot);
    }

    [Test]
    public void DecliningStaysWithTheSameSide()
    {
        CareerProfile p = StrongSeason(31, eRung.Club, out Random rng);
        string side = p.season.Yours.name;
        CareerMessage offer = CareerProgress.PendingOffer(p);
        CareerProgress.Choose(p, offer, offer.options.Find(o => o.action == "stay"), rng);
        Assert.IsTrue(offer.answered);
        Assert.AreEqual(eRung.Club, p.rung);
        Assert.AreEqual(side, p.season.Yours.name);
        CareerProgress.Choose(p, offer, offer.options[0], rng);
        Assert.AreEqual(eRung.Club, p.rung, "an answered offer cannot be taken after all");
    }

    [Test]
    public void AnUnansweredOfferLapsesOnceTheSeasonBegins()
    {
        CareerProfile p = StrongSeason(32, eRung.Club, out Random rng);
        CareerMessage offer = CareerProgress.PendingOffer(p);
        Assert.IsNotNull(offer);
        CareerTestPlay.Play(p, rng, 20, 18, true);
        Assert.IsTrue(offer.answered);
        StringAssert.Contains(CareerProgress.ExpiredNote, offer.body);
        Assert.IsNull(CareerProgress.PendingOffer(p));
        CareerProgress.Choose(p, offer, offer.options[0], rng);
        Assert.AreEqual(eRung.Club, p.rung, "a lapsed offer moves nobody");
    }

    [Test]
    public void TheProLeagueOfferIsANationalCallUp()
    {
        CareerProfile p = StrongSeason(33, eRung.ProLeague, out Random rng);
        CareerMessage offer = CareerProgress.PendingOffer(p);
        Assert.IsNotNull(offer);
        Assert.AreEqual("Selector", offer.from);
        CollectionAssert.AreEqual(new[] { "accept-callup", "stay" }, offer.options.ConvertAll(o => o.action));
        CareerProgress.Choose(p, offer, offer.options[0], rng);

        Assert.AreEqual(eRung.International, p.rung);
        Assert.AreEqual(Nations.Name("AUS"), p.season.Yours.name);
        Assert.AreEqual(Ladder.LowestSlot(eRung.International), p.battingSlot);
        Assert.IsTrue(p.season.fixtures.TrueForAll(f => !f.Involves(p.season.yourTeam) || f.scout), "the selectors watch every match");
        Assert.AreEqual(0, p.caps);
        CareerTestPlay.Play(p, rng, 30, 22, true);
        CareerTestPlay.Play(p, rng, 30, 22, true);
        Assert.AreEqual(2, p.caps, "a cap for each international match");
    }

    [Test]
    public void TheTopRungKeepsAGoodPlayerAndHisCaps()
    {
        CareerProfile p = CareerTestPlay.Career(34, eRung.International, 80f);
        var rng = new Random(35);
        int played = CareerTestPlay.Season(p, rng, 40, 25, false);
        CareerProgress.EnsureSeason(p, rng);
        Assert.AreEqual(eRung.International, p.rung);
        Assert.AreEqual(Nations.Name("AUS"), p.season.Yours.name);
        Assert.AreEqual(played, p.caps);
        Assert.IsNull(CareerProgress.PendingOffer(p), "nothing above the top");
    }

    [Test]
    public void APoorSeasonBelowTheStandardIsAReleaseDownARung()
    {
        CareerProfile p = CareerTestPlay.Career(36, eRung.State, 20f);
        var rng = new Random(37);
        string side = p.season.Yours.name;
        CareerTestPlay.Season(p, rng, 0, 3, true, false);
        CareerProgress.EnsureSeason(p, rng);

        Assert.AreEqual(eRung.Club, p.rung);
        Assert.AreEqual(eRung.Club, p.season.rung);
        Assert.IsTrue(Array.Exists(NameBank.Teams(eRung.Club), t => t.name == p.season.Yours.name), "a new club side");
        Assert.AreEqual(Ladder.LowestSlot(eRung.Club), p.battingSlot);
        Assert.AreEqual(0, p.benchedFor);
        CareerMessage m = CareerTestPlay.Last(p, eMessageKind.Selection);
        Assert.AreEqual("Released by " + side, m.title);
        StringAssert.Contains(p.season.Yours.name, m.body);
        Assert.IsNull(CareerProgress.PendingOffer(p));
    }

    [Test]
    public void APoorInternationalSeasonDropsHimToTheProLeague()
    {
        CareerProfile p = CareerTestPlay.Career(38, eRung.International, 30f);
        var rng = new Random(39);
        CareerTestPlay.Season(p, rng, 0, 3, true, false);
        CareerProgress.EnsureSeason(p, rng);
        Assert.AreEqual(eRung.ProLeague, p.rung);
        Assert.AreEqual("Dropped by " + Nations.Name("AUS"), CareerTestPlay.Last(p, eMessageKind.Selection).title);
    }

    [Test]
    public void AnOrdinarySeasonKeepsTheSide()
    {
        CareerProfile p = CareerTestPlay.Career(40, eRung.State, 45f);
        var rng = new Random(41);
        string side = p.season.Yours.name;
        CareerTestPlay.Season(p, rng, 15, 15, true);
        CareerProgress.EnsureSeason(p, rng);
        Assert.AreEqual(eRung.State, p.rung);
        Assert.AreEqual(side, p.season.Yours.name);
        Assert.IsNull(CareerProgress.PendingOffer(p), "45 overall is short of what the Pro League wants");
    }

    [Test]
    public void TheSameSeedMakesTheSameCareer()
    {
        CareerProfile a = StrongSeason(42, eRung.Club, out _), b = StrongSeason(42, eRung.Club, out _);
        CollectionAssert.AreEqual(CareerProgress.PendingOffer(a).options.ConvertAll(o => o.action),
                                  CareerProgress.PendingOffer(b).options.ConvertAll(o => o.action));
        Assert.AreEqual(a.season.Yours.name, b.season.Yours.name);
        Assert.AreEqual(a.innings.ConvertAll(r => r.impression), b.innings.ConvertAll(r => r.impression));
    }

    [Test]
    public void MissingPiecesNeverThrow()
    {
        var rng = new Random(1);
        CareerProgress.EndSeason(null, rng);
        CareerProgress.PlayLeftOut(null, rng);
        CareerProgress.Choose(null, null, null, rng);
        Assert.IsFalse(CareerProgress.LeftOutNext(null));
        Assert.IsNull(CareerProgress.PendingOffer(null));
        var bare = new CareerProfile { name = "Sam Carter", benchedFor = 1 };
        Assert.IsFalse(CareerProgress.LeftOutNext(bare), "no season, no match to sit out");
        CareerProgress.PlayLeftOut(bare, rng);
        Assert.IsFalse(Scouting.Interested(bare));
        Assert.IsFalse(Selection.ShouldDrop(bare));
        Assert.IsFalse(Selection.ShouldRelegate(bare));
        CareerProgress.Choose(bare, new CareerMessage { kind = eMessageKind.Offer }, new MessageOption { action = "join:1:Northmoor Herons" }, rng);
        Assert.AreEqual(eRung.State, bare.rung, "joining with no season begun is a new season at the rung");
        CareerProgress.EndSeason(new CareerProfile { name = "Sam Carter", season = new Season() }, rng);
    }

    [Test]
    public void TheLadderStateSurvivesASaveAndLoad()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cvr_ladder_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        try
        {
            CareerProfile p = StrongSeason(43, eRung.Club, out _);
            p.benchedFor = 1;
            p.caps = 3;
            p.scoutReports.Add(0.62f);
            CareerSaves.Create(0, p);
            CareerProfile q = CareerSaves.Load(0);

            CareerMessage offer = CareerProgress.PendingOffer(q);
            Assert.IsNotNull(offer, "the offer waits through a save");
            CollectionAssert.AreEqual(CareerProgress.PendingOffer(p).options.ConvertAll(o => o.action), offer.options.ConvertAll(o => o.action));
            Assert.AreEqual(1, q.benchedFor);
            Assert.AreEqual(3, q.caps);
            CollectionAssert.AreEqual(p.scoutReports, q.scoutReports);
            Assert.AreEqual(p.innings.FindAll(r => r.scouted).Count, q.innings.FindAll(r => r.scouted).Count);
            Assert.Greater(q.innings.FindAll(r => r.scouted).Count, 0);
            Assert.AreEqual(p.innings.ConvertAll(r => r.impression), q.innings.ConvertAll(r => r.impression));
            Assert.AreEqual(p.season.fixtures.ConvertAll(f => f.scout), q.season.fixtures.ConvertAll(f => f.scout));
            Assert.AreEqual(p.season.Yours.name, q.season.Yours.name);
        }
        finally
        {
            CareerSaves.UseDirectoryForTests(null);
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
        }
    }
}
