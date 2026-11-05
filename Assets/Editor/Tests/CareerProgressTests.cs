using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The career moving on (CareerProgress, CareerProgressMath) over the real LeagueBuilder and
/// CareerSim: the first season is made once with the player in his side; the plan puts him at his
/// slot with HUD's eight bowlers, and a chase carries the other side's innings; a match goes into
/// the stats (fifties, hundreds, ducks, the high score, not outs) once, plays out the rest of its
/// round, and the final follows the league and finishes the season; the slot moves two places at
/// most and not into the top two in the first Club season; form rewards the better innings over
/// the last five; and a whole season ends into season two with the same side. Pure: no saves.
/// </summary>
public class CareerProgressTests
{
    private static CareerProfile NewCareer(int seed)
    {
        var p = new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05" };
        CareerProgress.EnsureSeason(p, new Random(seed));
        return p;
    }

    private static void SetRatings(CareerProfile p, float v)
    {
        for (int i = 0; i < RatingSet.Count; i++)
            p.ratings[i] = v;
    }

    /// The match as the match scene would report it, with both cards agreeing with won/lost.
    private static CareerMatchResult Result(MatchPlan plan, int runs, int balls, bool isOut, bool won,
                                            int fours = 0, int sixes = 0)
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
            runs = runs, balls = balls, fours = fours, sixes = sixes,
            batted = true, isOut = isOut, how = isOut ? "b Someone" : "not out",
            yours = yours, theirs = theirs, won = won,
        };
    }

    private static MatchPlan Play(CareerProfile p, Random rng, int runs, int balls, bool isOut, bool won = true)
    {
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        Assert.IsNotNull(plan, "there is a next match");
        CareerProgress.RecordMatch(p, plan, Result(plan, runs, balls, isOut, won), rng);
        return plan;
    }

    private static int CountYou(Team t) => t.players.FindAll(x => x.isYou).Count;

    [Test]
    public void EnsureSeasonMakesOneSeasonWithYouInIt()
    {
        CareerProfile p = NewCareer(1);
        Season s = p.season;
        Assert.IsNotNull(s);
        Assert.AreEqual(1, s.number);
        Assert.AreEqual(eRung.Club, s.rung);
        Assert.AreEqual(Ladder.TeamsInLeague, s.teams.Count);
        Assert.IsNotNull(s.Yours, "the season knows the player's side");
        Assert.AreEqual(1, CountYou(s.Yours));
        Assert.AreEqual("Sam Carter", s.Yours.You.name);
        Assert.AreEqual(CareerProgress.BattingRating(p), s.Yours.You.bat, 0.001f);
        Assert.AreEqual(7, p.battingSlot, "a new career bats at 7");

        CareerProgress.EnsureSeason(p, new Random(2));
        Assert.AreSame(s, p.season, "a second call keeps the season");

        p.season = new Season();                     // what JsonUtility leaves for a missing season
        CareerProgress.EnsureSeason(p, new Random(3));
        Assert.AreEqual(Ladder.TeamsInLeague, p.season.teams.Count, "an empty season is replaced");
    }

    [Test]
    public void PlanNextPutsYouAtYourSlotWithEightBowlers()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            CareerProfile p = NewCareer(seed);
            p.battingSlot = 4;
            MatchPlan plan = CareerProgress.PlanNext(p, new Random(seed + 100));
            Fixture f = CareerProgress.NextFixture(p);
            Assert.AreSame(f, p.season.fixtures[plan.fixture]);
            Assert.AreEqual(f.Opponent(p.season.yourTeam), plan.opponent);
            Assert.AreEqual(p.season.yourTeam, plan.yourTeam);
            Assert.AreEqual(Ladder.Overs(eRung.Club), plan.overs);
            Assert.AreEqual(Ladder.Bowling(eRung.Club), plan.bowling);
            Assert.AreEqual(Ladder.Crowd(eRung.Club), plan.crowd);
            Assert.AreEqual(Ladder.Venue(eRung.Club), plan.venue);
            Assert.AreEqual(eTimeOfDay.Afternoon, plan.time, "no evening or night games at Club");

            Assert.AreEqual(11, plan.order.Count);
            Assert.AreEqual(11, plan.orderRatings.Count);
            Assert.AreEqual(3, plan.slot);
            Assert.AreEqual("Sam Carter", plan.order[plan.slot]);
            Assert.AreEqual(CareerProgress.BattingRating(p), plan.orderRatings[plan.slot], 0.001f);
            var others = new List<string>();
            foreach (SquadPlayer s in p.season.Yours.players)
                if (!s.isYou) others.Add(s.name);
            others.Insert(3, "Sam Carter");
            CollectionAssert.AreEqual(others, plan.order, "the others keep their order around him");

            Assert.AreEqual(8, plan.bowlers.Count);
            Team them = p.season.Team(plan.opponent);
            foreach (string b in plan.bowlers)
                Assert.IsTrue(them.players.Exists(x => x.name == b), b + " plays for the other side");
        }
    }

    [Test]
    public void AChaseCarriesTheirInnings()
    {
        int chases = 0, setting = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            CareerProfile p = NewCareer(seed);
            MatchPlan plan = CareerProgress.PlanNext(p, new Random(seed * 7 + 1));
            if (plan.batFirst) { setting++; Assert.IsNull(plan.theirs); Assert.AreEqual(0, plan.Target); continue; }
            chases++;
            Assert.IsNotNull(plan.theirs);
            Assert.Greater(plan.theirs.runs, 0);
            Assert.AreEqual(plan.theirs.runs + 1, plan.Target);
        }
        Assert.Greater(chases, 0, "the toss sometimes has him chasing");
        Assert.Greater(setting, 0, "and sometimes setting a total");
    }

    [Test]
    public void AMatchGoesIntoTheStats()
    {
        CareerProfile p = NewCareer(5);
        var rng = new Random(6);
        Play(p, rng, 0, 3, true);          // a duck
        Play(p, rng, 55, 40, false);       // a fifty, not out
        Play(p, rng, 120, 70, true);       // a hundred
        Play(p, rng, 30, 25, true);

        CareerStats st = p.stats;
        Assert.AreEqual(4, st.matches);
        Assert.AreEqual(4, st.innings);
        Assert.AreEqual(1, st.notOuts);
        Assert.AreEqual(205, st.runs);
        Assert.AreEqual(138, st.balls);
        Assert.AreEqual(1, st.fifties, "a hundred is not also a fifty");
        Assert.AreEqual(1, st.hundreds);
        Assert.AreEqual(1, st.ducks);
        Assert.AreEqual(120, st.highScore);
        Assert.IsFalse(st.highScoreNotOut);

        Assert.AreEqual(4, p.innings.Count);
        InningsRecord fifty = p.innings[1];
        Assert.AreEqual(55, fifty.runs);
        Assert.IsFalse(fifty.isOut);
        Assert.IsTrue(fifty.batted);
        Assert.AreEqual(1, fifty.season);
        Assert.AreEqual(p.season.Yours.name, fifty.team);
        Assert.IsTrue(fifty.won);
        Assert.IsNotEmpty(fifty.teamScore);
        Assert.IsNotEmpty(fifty.result);
        Assert.IsTrue(p.inbox.Exists(m => m.title == "Your first fifty"));
        Assert.IsTrue(p.inbox.Exists(m => m.title == "Your first hundred"));
    }

    [Test]
    public void TheSameScoreNotOutBecomesTheHighScore()
    {
        CareerProfile p = NewCareer(8);
        var rng = new Random(9);
        Play(p, rng, 40, 30, true);
        Play(p, rng, 40, 28, false);
        Assert.AreEqual(40, p.stats.highScore);
        Assert.IsTrue(p.stats.highScoreNotOut);
    }

    [Test]
    public void TwoDucksInARowBringTheCoach()
    {
        CareerProfile p = NewCareer(10);
        var rng = new Random(11);
        Play(p, rng, 0, 2, true);
        Assert.IsFalse(p.inbox.Exists(m => m.kind == eMessageKind.Coach));
        Play(p, rng, 0, 1, true);
        Assert.IsTrue(p.inbox.Exists(m => m.kind == eMessageKind.Coach && m.title == "Two ducks in a row"));
    }

    [Test]
    public void AMatchPlaysOutTheRestOfItsRound()
    {
        CareerProfile p = NewCareer(12);
        var rng = new Random(13);
        Fixture first = CareerProgress.NextFixture(p);
        MatchPlan plan = Play(p, rng, 25, 20, true);

        Assert.IsTrue(first.played);
        Assert.IsNotEmpty(first.homeScore);
        Assert.IsNotEmpty(first.awayScore);
        Assert.IsNotEmpty(first.result);
        Fixture next = CareerProgress.NextFixture(p);
        Assert.IsNotNull(next);
        Assert.AreNotSame(first, next);
        Assert.Greater(next.round, first.round);
        foreach (Fixture f in p.season.fixtures)
            if (f.round == first.round && !f.final)
                Assert.IsTrue(f.played, $"{f.home} v {f.away} in round {f.round} was simulated");
        foreach (Team t in p.season.teams)
            Assert.AreEqual(1, t.played, t.name + " has one game in the table");

        CareerProgress.RecordMatch(p, plan, Result(plan, 25, 20, true, true), rng);
        Assert.AreEqual(1, p.stats.matches, "a match is recorded once");
    }

    /// Plays the season out, checking the final appears only after the league; returns matches.
    private static int PlaySeason(CareerProfile p, Random rng, bool win)
    {
        int matches = 0;
        while (!p.season.finished)
        {
            Assert.Less(matches, 20, "the season ends");
            bool leagueLeft = p.season.fixtures.Exists(f => !f.final && !f.played);
            Assert.AreEqual(leagueLeft, !p.season.fixtures.Exists(f => f.final), "the final follows the league");
            Assert.IsNotNull(CareerProgress.NextFixture(p), "an unfinished season has his next match");
            Play(p, rng, 20, 18, true, win);
            matches++;
        }
        Assert.IsNull(CareerProgress.NextFixture(p));
        Fixture final = p.season.fixtures.Find(f => f.final);
        Assert.IsNotNull(final);
        Assert.IsTrue(final.played);
        Assert.IsTrue(p.season.fixtures.TrueForAll(f => f.played || f.final), "every league game was played");
        return matches;
    }

    [Test]
    public void WinningEverythingPlaysTheFinal()
    {
        CareerProfile p = NewCareer(14);
        int matches = PlaySeason(p, new Random(15), true);
        Assert.AreEqual(Ladder.TeamsInLeague, matches, "seven league games and the final");
        Assert.IsTrue(p.season.fixtures.Find(f => f.final).Involves(p.season.yourTeam));
    }

    [Test]
    public void LosingEverythingStillFinishesTheSeason()
    {
        CareerProfile p = NewCareer(16);
        int matches = PlaySeason(p, new Random(17), false);
        Assert.AreEqual(Ladder.TeamsInLeague - 1, matches, "seven league games, then the final without him");
        Assert.IsFalse(p.season.fixtures.Find(f => f.final).Involves(p.season.yourTeam));
    }

    [Test]
    public void AFullSeasonEndsIntoSeasonTwoWithTheSameSide()
    {
        CareerProfile p = NewCareer(18);
        var rng = new Random(19);
        PlaySeason(p, rng, true);
        string side = p.season.Yours.name;

        CareerProgress.EnsureSeason(p, rng);
        Assert.AreEqual(1, p.seasonsPlayed);
        Assert.AreEqual(2, p.season.number);
        Assert.IsFalse(p.season.finished);
        Assert.AreEqual(eRung.Club, p.season.rung);
        Assert.AreEqual(side, p.season.Yours.name);
        Assert.AreEqual(1, CountYou(p.season.Yours), "he is in his side once");
        Assert.AreEqual(0, p.season.Yours.played, "a fresh table");
        Assert.IsTrue(p.season.fixtures.TrueForAll(f => !f.played));
        Assert.IsTrue(p.inbox.Exists(m => m.title == "Season 1 review" && m.body.Contains(side)));
        Assert.IsNotNull(CareerProgress.PlanNext(p, rng));
    }

    [Test]
    public void TheSlotMovesAtMostTwoPlaces()
    {
        CareerProfile p = NewCareer(20);
        p.seasonsPlayed = 1;                       // past the first Club season
        SetRatings(p, 99f);
        var rng = new Random(21);
        int[] expect = { 5, 3, 1, 1 };
        foreach (int slot in expect)
        {
            int before = p.battingSlot;
            Play(p, rng, 40, 20, false);
            Assert.LessOrEqual(Math.Abs(p.battingSlot - before), 2);
            Assert.AreEqual(slot, p.battingSlot);
        }

        SetRatings(p, 1f);
        p.innings.Clear();                         // form from the ducks alone
        foreach (int slot in new[] { 3, 5, 7, 7 })
        {
            Play(p, rng, 0, 4, true);
            Assert.AreEqual(slot, p.battingSlot, "down the order two at a time, no lower than 7");
        }
    }

    [Test]
    public void TheFirstClubSeasonDoesNotOpen()
    {
        CareerProfile p = NewCareer(22);
        SetRatings(p, 99f);
        var rng = new Random(23);
        foreach (int slot in new[] { 5, 3, 3 })
        {
            Play(p, rng, 40, 20, false);
            Assert.AreEqual(slot, p.battingSlot);
        }
        Assert.AreEqual(3, CareerProgressMath.NextSlot(3, 1, true));
        Assert.AreEqual(1, CareerProgressMath.NextSlot(3, 1, false));
    }

    private static InningsRecord Inn(int runs, int balls, bool isOut, eRung rung = eRung.Club) =>
        new InningsRecord { runs = runs, balls = balls, isOut = isOut, batted = true, rung = rung };

    private static float FormOf(params InningsRecord[] list)
    {
        var p = new CareerProfile();
        foreach (InningsRecord r in list) p.AddInnings(r);
        return CareerProgress.Form(p);
    }

    [Test]
    public void FormRewardsTheBetterInnings()
    {
        Assert.AreEqual(RatingSet.Unknown, FormOf(), "no innings, no form");
        Assert.AreEqual(RatingSet.Unknown, FormOf(new InningsRecord { leftOut = true }), "left out is not an innings");

        float great = FormOf(Inn(30, 20, false), Inn(30, 20, false), Inn(30, 20, false));
        float fair = FormOf(Inn(10, 15, true), Inn(10, 15, true), Inn(10, 15, true));
        float poor = FormOf(Inn(0, 3, true), Inn(0, 3, true), Inn(0, 3, true));
        Assert.AreEqual(99f, great, 0.01f);
        Assert.AreEqual(0f, poor, 0.01f);
        Assert.Greater(great, fair);
        Assert.Greater(fair, poor);
        Assert.Greater(FormOf(Inn(10, 15, false)), FormOf(Inn(10, 15, true)), "not out counts");
        Assert.Greater(FormOf(Inn(14, 10, true)), FormOf(Inn(14, 30, true)), "the strike rate counts");
        Assert.Greater(FormOf(Inn(20, 20, true, eRung.Club)), FormOf(Inn(20, 20, true, eRung.ProLeague)),
                       "par is higher up the ladder");
    }

    [Test]
    public void FormIsTheLastFiveInnings()
    {
        var good = Inn(30, 20, false);
        var duck = Inn(0, 2, true);
        Assert.AreEqual(99f, FormOf(duck, duck, duck, duck, duck, good, good, good, good, good), 0.01f);
        Assert.AreEqual(0f, FormOf(good, good, good, good, good, duck, duck, duck, duck, duck), 0.01f);
        for (int i = 0; i <= 20; i++)
        {
            float f = FormOf(Inn(i * 5, Math.Max(1, i * 4), i % 2 == 0));
            Assert.GreaterOrEqual(f, 0f);
            Assert.LessOrEqual(f, 99f);
        }
    }

    [Test]
    public void BattingRatingIsTheMeasuredOverall()
    {
        var p = new CareerProfile();
        Assert.AreEqual(35f, CareerProgress.BattingRating(p), 0.001f, "a beginner before anything is measured");
        p.ratings.timing = 60f;
        p.ratings.power = 40f;
        Assert.AreEqual(50f, CareerProgress.BattingRating(p), 0.001f);
    }

    [Test]
    public void TheBowlersFollowHudsAttack()
    {
        var t = new Team { name = "Test XI" };
        t.players.Add(new SquadPlayer { name = "Quick", bowls = true, kind = eBowlerKind.Fast });
        t.players.Add(new SquadPlayer { name = "Swing", bowls = true, kind = eBowlerKind.Medium });
        t.players.Add(new SquadPlayer { name = "Leggie", bowls = true, kind = eBowlerKind.LegSpin });
        t.players.Add(new SquadPlayer { name = "Bat", bowls = false });
        List<string> b = CareerProgressMath.PickBowlers(t);
        CollectionAssert.AreEqual(new[] { "Quick", "Swing", "Swing", "Quick", "Leggie", "Leggie", "Leggie", "Leggie" }, b);
    }
}
