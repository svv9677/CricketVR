using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The coach's goals (phase 7): made once the trial is done, seeded from the career (never the
/// league's rng), measured from the record book, latched once done with one message each, and
/// counted in the season review. CareerTestPlay.Career never sets trialDone, so each test does.
/// </summary>
public class CoachGoalsTests
{
    private static CareerProfile Career(int seed, eRung rung = eRung.Club, float ratings = -1f)
    {
        CareerProfile p = CareerTestPlay.Career(seed, rung, ratings);
        p.trialDone = true;
        return p;
    }

    private static SeasonGoal Goal(CareerProfile p, eGoalKind kind) => p.season.goals.Find(g => g.kind == kind);

    private static int Messages(CareerProfile p, eMessageKind kind) => p.inbox.FindAll(m => m.kind == kind).Count;

    private static void Batted(CareerProfile p, int runs, int balls, int fours = 0, int sixes = 0, bool scouted = false) =>
        p.AddInnings(new InningsRecord
        {
            season = p.season.number, rung = p.season.rung, batted = true, runs = runs, balls = balls,
            fours = fours, sixes = sixes, scouted = scouted,
        });

    [Test]
    public void NoGoalsBeforeTheTrial()
    {
        CareerProfile p = CareerTestPlay.Career(1, eRung.Club, 50f);
        Assert.IsFalse(CoachGoals.Ensure(p));
        Assert.AreEqual(0, p.season.goals.Count);
        Assert.IsEmpty(CoachGoals.Update(p));
    }

    [Test]
    public void TheRunsGoalIsSixtyPercentOfParOverTheLeague()
    {
        CareerProfile club = Career(2, eRung.Club, 50f);
        Assert.IsTrue(CoachGoals.Ensure(club));
        SeasonGoal runs = club.season.goals[0];
        Assert.AreEqual(eGoalKind.Runs, runs.kind, "the runs goal comes first");
        Assert.AreEqual(60f, runs.target);
        Assert.AreEqual("Score 60 runs this season", runs.text);

        var expected = new Dictionary<eRung, float> { { eRung.State, 85f }, { eRung.ProLeague, 120f } };
        foreach (KeyValuePair<eRung, float> e in expected)
        {
            CareerProfile p = Career(3, e.Key, 50f);
            CoachGoals.Ensure(p);
            Assert.AreEqual(7, CoachGoals.LeagueGames(p.season), "seven league games");
            Assert.AreEqual(e.Value, Goal(p, eGoalKind.Runs).target, e.Key.ToString());
        }
    }

    [Test]
    public void TheRunsGoalAsksForMoreThanLastSeasonAtTheRung()
    {
        CareerProfile p = Career(4, eRung.Club, 50f);
        p.season.number = 2;
        p.AddInnings(new InningsRecord { season = 1, rung = eRung.Club, batted = true, runs = 100 });
        p.AddInnings(new InningsRecord { season = 1, rung = eRung.State, batted = true, runs = 400 });   // another rung
        p.AddInnings(new InningsRecord { season = 1, rung = eRung.Club, leftOut = true, runs = 300 });  // sat out
        CoachGoals.Ensure(p);
        Assert.AreEqual(115f, Goal(p, eGoalKind.Runs).target, "100 x 1.15 beats 60");
    }

    [Test]
    public void TheWeakestRatingIsTheSecondGoal()
    {
        CareerProfile p = Career(5, eRung.Club, 60f);
        p.ratings.judgement = 43f;
        CoachGoals.Ensure(p);
        SeasonGoal g = p.season.goals[1];
        Assert.AreEqual(eGoalKind.Rating, g.kind);
        Assert.AreEqual(5, g.stat, "Judgement");
        Assert.AreEqual(50f, g.target, "43 + 5, up to a 5");
        Assert.AreEqual(43f, g.baseline);
        Assert.AreEqual("Get Judgement to 50", g.text);
        Assert.AreEqual(3, p.season.goals.Count);
    }

    [Test]
    public void ARatingGoalIsCappedAndNotSetAtNinety()
    {
        CareerProfile high = Career(6, eRung.Club, 92f);
        CoachGoals.Ensure(high);
        Assert.IsNull(Goal(high, eGoalKind.Rating), "nothing under 90 to work on");
        Assert.AreEqual(2, high.season.goals.Count);

        CareerProfile near = Career(6, eRung.Club, 89.5f);
        CoachGoals.Ensure(near);
        Assert.AreEqual(95f, Goal(near, eGoalKind.Rating).target);
    }

    [Test]
    public void TwoGoalsWhenNothingIsMeasured()
    {
        CareerProfile p = Career(7);
        Assert.IsTrue(CoachGoals.Ensure(p));
        Assert.AreEqual(2, p.season.goals.Count);
        Assert.AreEqual(eGoalKind.Runs, p.season.goals[0].kind);
        Assert.AreEqual(eGoalKind.Drills, p.season.goals[1].kind);
        Assert.IsFalse(CoachGoals.Ensure(p), "once a season");
        Assert.AreEqual(2, p.season.goals.Count);
    }

    [Test]
    public void TheSameCareerGetsTheSameGoals()
    {
        CareerProfile a = Career(8, eRung.State, 55f), b = Career(8, eRung.State, 55f);
        a.ratings.power = b.ratings.power = 40f;
        CoachGoals.Ensure(a);
        CoachGoals.Ensure(b);
        Assert.AreEqual(a.season.goals.Count, b.season.goals.Count);
        for (int i = 0; i < a.season.goals.Count; i++)
        {
            Assert.AreEqual(a.season.goals[i].id, b.season.goals[i].id);
            Assert.AreEqual(a.season.goals[i].target, b.season.goals[i].target);
            Assert.AreEqual(a.season.goals[i].text, b.season.goals[i].text);
        }
    }

    [Test]
    public void GoalsNeverDrawOnTheLeaguesRng()
    {
        // The same career with and without goals: the season and the one after are identical.
        CareerProfile with = Career(9, eRung.Club, 50f), without = CareerTestPlay.Career(9, eRung.Club, 50f);
        CoachGoals.Ensure(with);
        CareerTestPlay.Season(with, new Random(10), 20, 16, true);
        CareerTestPlay.Season(without, new Random(10), 20, 16, true);
        CareerProgress.EndSeason(with, new Random(11));
        CareerProgress.EndSeason(without, new Random(11));
        Assert.Greater(with.season.goals.Count, 0, "the new season has its goals");
        Assert.AreEqual(0, without.season.goals.Count);
        Assert.AreEqual(without.season.fixtures.Count, with.season.fixtures.Count);
        for (int i = 0; i < with.season.fixtures.Count; i++)
        {
            Assert.AreEqual(without.season.fixtures[i].home, with.season.fixtures[i].home);
            Assert.AreEqual(without.season.fixtures[i].away, with.season.fixtures[i].away);
            Assert.AreEqual(without.season.fixtures[i].night, with.season.fixtures[i].night);
        }
        Assert.AreEqual(without.rung, with.rung);
    }

    [Test]
    public void MatchesMoveTheRunsGoal_ItIsDoneOnceWithOneMessage()
    {
        CareerProfile p = Career(12, eRung.Club, 50f);
        CoachGoals.Ensure(p);
        var rng = new Random(13);
        CareerTestPlay.Play(p, rng, 30, 20, false);
        SeasonGoal runs = Goal(p, eGoalKind.Runs);
        Assert.AreEqual(30f, runs.progress);
        Assert.IsFalse(runs.done);

        CareerTestPlay.Play(p, rng, 35, 25, true);
        Assert.IsTrue(runs.done, "65 of 60");
        Assert.IsFalse(string.IsNullOrEmpty(runs.doneDate));
        Assert.AreEqual(65f, runs.progress);
        Assert.AreEqual(CoachGoals.Done(p.season), p.goalsDone, "every goal done is counted");
        Assert.AreEqual(p.goalsDone, Messages(p, eMessageKind.Goal), "one message a goal");
        Assert.AreEqual(1, p.inbox.FindAll(m => m.kind == eMessageKind.Goal && m.body.StartsWith(runs.text)).Count);
        CareerMessage m0 = p.inbox.Find(m => m.kind == eMessageKind.Goal && m.body.StartsWith(runs.text));
        Assert.AreEqual("Coach", m0.from);

        int done = p.goalsDone, messages = p.inbox.Count;
        Assert.IsEmpty(CoachGoals.Update(p), "idempotent");
        Assert.AreEqual(done, p.goalsDone);
        Assert.AreEqual(messages, p.inbox.Count);
    }

    [Test]
    public void TheDrillsGoalMovesWithDrillsFinished()
    {
        CareerProfile p = Career(14);
        p.drills.For(eDrill.Spin).attempts = 4;   // before the goal: not counted
        CoachGoals.Ensure(p);
        SeasonGoal drills = Goal(p, eGoalKind.Drills);
        Assert.AreEqual(4f, drills.baseline);
        for (int i = 0; i < 2; i++)
            DrillCoach.Record(p, new DrillResult { drill = eDrill.Yorkers, score = 40 }, "2026-11-05");
        CoachGoals.Update(p);
        Assert.AreEqual(2f, drills.progress);
        Assert.IsFalse(drills.done);
        DrillCoach.Record(p, new DrillResult { drill = eDrill.Gaps, score = 10 }, "2026-11-05");
        Assert.AreEqual(1, CoachGoals.Update(p).Count);
        Assert.IsTrue(drills.done);
    }

    [Test]
    public void ARatingGoalIsMetAsShown_AndStaysDone()
    {
        CareerProfile p = Career(15, eRung.Club, 60f);
        p.ratings.judgement = 43f;
        CoachGoals.Ensure(p);
        SeasonGoal g = Goal(p, eGoalKind.Rating);
        p.ratings.judgement = 49.4f;
        CoachGoals.Update(p);
        Assert.IsFalse(g.done, "reads 49");
        p.ratings.judgement = 49.6f;
        Assert.AreEqual(1, CoachGoals.Update(p).Count, "reads 50");
        p.ratings.judgement = 30f;
        CoachGoals.Update(p);
        Assert.IsTrue(g.done, "latched: the rating window moves");
        Assert.AreEqual(49.6f, g.progress, 1e-4f, "kept as it was when done");
    }

    [Test]
    public void AStrikeRateGoalNeedsItsBalls()
    {
        CareerProfile p = Career(16, eRung.State, 60f);
        p.season.goals.Add(new SeasonGoal { id = "strikerate", kind = eGoalKind.StrikeRate, target = 110f, minBalls = 30 });
        Batted(p, 30, 20);
        CoachGoals.Update(p);
        Assert.AreEqual(150f, p.season.goals[0].progress);
        Assert.IsFalse(p.season.goals[0].done, "150 over 20 balls is not over 30");
        Batted(p, 10, 10);
        CoachGoals.Update(p);
        Assert.IsTrue(p.season.goals[0].done, "133 over 30 balls");
    }

    [Test]
    public void OnlyThisSeasonsInningsAtThisRungCount()
    {
        CareerProfile p = Career(17, eRung.Club, 60f);
        p.season.goals.Add(new SeasonGoal { id = "runs", kind = eGoalKind.Runs, target = 60f });
        p.season.goals.Add(new SeasonGoal { id = "boundaries", kind = eGoalKind.Boundaries, target = 6f });
        p.season.goals.Add(new SeasonGoal { id = "scouted", kind = eGoalKind.ScoutedScore, target = 20f });
        p.AddInnings(new InningsRecord { season = 0, rung = eRung.Club, batted = true, runs = 99, fours = 9 });
        p.AddInnings(new InningsRecord { season = 1, rung = eRung.State, batted = true, runs = 99, fours = 9 });
        p.AddInnings(new InningsRecord { season = 1, rung = eRung.Club, leftOut = true, batted = true, runs = 99 });
        Batted(p, 12, 10, 1, 1);
        Batted(p, 25, 20, 2, 0, true);
        CoachGoals.Update(p);
        Assert.AreEqual(37f, p.season.goals[0].progress);
        Assert.AreEqual(4f, p.season.goals[1].progress);
        Assert.AreEqual(25f, p.season.goals[2].progress, "the best scouted innings");
        Assert.IsTrue(p.season.goals[2].done);
        Assert.AreEqual(1, p.goalsDone);
    }

    [Test]
    public void TheThirdGoalFitsTheRungAndThePlayer()
    {
        var seen = new HashSet<eGoalKind>();
        CareerProfile club = Career(18, eRung.Club, 60f);
        CareerProfile intl = Career(19, eRung.International, 70f);
        CareerProfile state = Career(20, eRung.State, 60f);
        CareerProfile power = Career(21, eRung.Club, 60f);
        power.ratings.power = 30f;
        for (int seed = 0; seed < 200; seed++)
        {
            eGoalKind c = CoachGoals.Make(club, club.season, new Random(seed))[2].kind;
            Assert.IsTrue(c == eGoalKind.Drills || c == eGoalKind.ScoutedScore, $"club: {c}");
            Assert.AreNotEqual(eGoalKind.ScoutedScore, CoachGoals.Make(intl, intl.season, new Random(seed))[2].kind, "no scouts at the top");
            SeasonGoal st = CoachGoals.Make(state, state.season, new Random(seed))[2];
            if (st.kind == eGoalKind.StrikeRate)
            {
                Assert.AreEqual(110f, st.target);
                Assert.AreEqual(30, st.minBalls);
            }
            seen.Add(st.kind);
            seen.Add(CoachGoals.Make(power, power.season, new Random(seed))[2].kind);
        }
        Assert.Contains(eGoalKind.StrikeRate, new List<eGoalKind>(seen));
        Assert.Contains(eGoalKind.Boundaries, new List<eGoalKind>(seen), "Power weakest");
    }

    [Test]
    public void EndSeasonSetsNewGoals_AndTheReviewCountsThem()
    {
        CareerProfile p = Career(22, eRung.Club, 50f);
        CoachGoals.Ensure(p);
        var rng = new Random(23);
        CareerTestPlay.Season(p, rng, 25, 20, false);
        int goals = p.season.goals.Count;
        CareerProgress.EndSeason(p, rng);
        CareerMessage review = p.inbox.Find(m => m.title == "Season 1 review");
        Assert.IsNotNull(review);
        StringAssert.Contains("Coach's goals: ", review.body);
        StringAssert.Contains($" of {goals}.", review.body);
        Assert.AreEqual(2, p.season.number);
        Assert.Greater(p.season.goals.Count, 0, "the next season's goals");
        Assert.IsTrue(p.season.goals.TrueForAll(g => !g.done));
    }

    [Test]
    public void JoiningANewSideSetsThatRungsGoals()
    {
        CareerProfile p = Career(24, eRung.Club, 50f);
        CoachGoals.Ensure(p);
        var offer = new CareerMessage { kind = eMessageKind.Offer };
        CareerProgress.Choose(p, offer, new MessageOption { action = "join:1:Test XI" }, new Random(25));
        Assert.AreEqual(eRung.State, p.season.rung);
        Assert.AreEqual(85f, Goal(p, eGoalKind.Runs).target);
    }

    [Test]
    public void TheReviewLine()
    {
        var s = new Season();
        Assert.AreEqual("", CoachGoals.ReviewLine(s));
        s.goals.Add(new SeasonGoal { id = "a", done = true });
        s.goals.Add(new SeasonGoal { id = "b" });
        s.goals.Add(new SeasonGoal { id = "c" });
        Assert.AreEqual("Coach's goals: 1 of 3.", CoachGoals.ReviewLine(s));
        Assert.AreEqual(new List<string> { "a" }, CoachGoals.DoneIds(s));
    }

    [Test]
    public void Rounding()
    {
        Assert.AreEqual(60f, CoachGoals.Round5(58.8f));
        Assert.AreEqual(125f, CoachGoals.Round5(126f));
        Assert.AreEqual(5f, CoachGoals.Round5(0f), "never a goal of nothing");
        Assert.AreEqual(50f, CoachGoals.RoundUp5(48f));
        Assert.AreEqual(50f, CoachGoals.RoundUp5(50f));
        Assert.AreEqual(55f, CoachGoals.RoundUp5(50.2f));
    }
}
