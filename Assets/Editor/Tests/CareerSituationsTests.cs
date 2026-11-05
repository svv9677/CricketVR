using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// The match situations' world rules (phase 7, CareerSituations): which fixtures a season deals a
/// chase, rain or a rescue to (seeded from the season alone, never in round 1, only his side's),
/// what each does to the match plan (a Normal plan untouched), the rescue's scripted collapse, the
/// results and the table judged against a rain-revised target, form scaled for a shortened game, and
/// an old mid-season save given its situations when it loads.
/// </summary>
public class CareerSituationsTests
{
    private static readonly eRung[] Rungs = { eRung.Club, eRung.State, eRung.ProLeague, eRung.International };

    private static Season NewSeason(int seed, eRung rung = eRung.Club) =>
        LeagueBuilder.NewSeason(rung, 1, "AUS", null, new SquadPlayer { name = "Sam Carter" }, new Random(seed));

    private static List<eSituation> Situations(Season s) => s.fixtures.ConvertAll(f => f.situation);
    private static int Count(Season s, eSituation x) => s.fixtures.FindAll(f => f.situation == x).Count;

    /// Every public field of an object, walked the way JsonUtility writes a [Serializable] class
    /// (JsonUtility itself needs the engine; this runs anywhere).
    private static string Dump(object o)
    {
        if (o == null || o is string || o.GetType().IsPrimitive || o.GetType().IsEnum) return (o ?? "null") + ";";
        var b = new StringBuilder();
        if (o is IList l) { foreach (object x in l) b.Append(Dump(x)); return "[" + b + "]"; }
        foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            b.Append(f.Name).Append('=').Append(Dump(f.GetValue(o)));
        return b.ToString();
    }

    // ---- Dealing ----------------------------------------------------------------------------------

    [Test]
    public void TheSameSeason_IsDealtTheSameSituations()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            Season a = NewSeason(seed), b = NewSeason(seed);
            Assert.IsTrue(a.situationsDealt);
            CollectionAssert.AreEqual(Situations(a), Situations(b), $"seed {seed}");
            List<eSituation> first = Situations(a);
            CareerSituations.Deal(a);
            CollectionAssert.AreEqual(first, Situations(a), "dealt again: the same");
        }
    }

    [Test]
    public void Dealing_NeverTakesTheCallersRng_AndTouchesNothingElse()
    {
        foreach (ParameterInfo pi in typeof(CareerSituations).GetMethod("Deal").GetParameters())
            Assert.AreNotEqual(typeof(Random), pi.ParameterType, "Deal takes no rng");
        Season s = NewSeason(7, eRung.ProLeague);
        var before = s.fixtures.ConvertAll(f => $"{f.round}{f.home}{f.away}{f.night}{f.pitch}{f.scout}{f.played}");
        CareerSituations.Deal(s);
        CollectionAssert.AreEqual(before, s.fixtures.ConvertAll(f => $"{f.round}{f.home}{f.away}{f.night}{f.pitch}{f.scout}{f.played}"));
        Assert.DoesNotThrow(() => CareerSituations.Deal(null));
        Assert.DoesNotThrow(() => CareerSituations.Deal(new Season { fixtures = null }));
    }

    [Test]
    public void OverManySeasons_EachGetsOneOrTwoRains_ARescueAndAChase()
    {
        foreach (eRung rung in Rungs)
        {
            int twoRains = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                Season s = NewSeason(seed, rung);
                int rain = Count(s, eSituation.RainShortened);
                Assert.That(rain, Is.InRange(CareerSituations.RainMin, CareerSituations.RainMax), $"{rung} seed {seed}");
                Assert.AreEqual(CareerSituations.Rescues, Count(s, eSituation.Rescue), $"{rung} seed {seed}");
                Assert.AreEqual(CareerSituations.Chases, Count(s, eSituation.Chase), $"{rung} seed {seed}");
                Assert.AreEqual(0, Count(s, eSituation.Final), "a final is made later, by AddFinal");
                if (rain == 2) twoRains++;
                foreach (Fixture f in s.fixtures)
                {
                    if (f.situation == eSituation.Normal) continue;
                    Assert.GreaterOrEqual(f.round, 2, "round 1 is always a normal match");
                    Assert.IsTrue(f.Involves(s.yourTeam), "never a match between two other sides");
                }
            }
            Assert.That(twoRains, Is.InRange(60, 140), $"{rung}: a second rain about half the time");
        }
    }

    [Test]
    public void TheMatchAlreadyBegun_IsSkipped()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            Season s = NewSeason(seed);
            int dealt = s.fixtures.FindIndex(f => f.situation != eSituation.Normal);
            int total = s.fixtures.FindAll(f => f.situation != eSituation.Normal).Count;
            foreach (Fixture f in s.fixtures) f.situation = eSituation.Normal;   // as a phase 6 save has them
            CareerSituations.Deal(s, dealt);
            Assert.AreEqual(eSituation.Normal, s.fixtures[dealt].situation, $"seed {seed}");
            Assert.AreEqual(total, s.fixtures.FindAll(f => f.situation != eSituation.Normal).Count,
                            "the others still get every situation (enough fixtures left)");
        }
    }

    [Test]
    public void TooFewFixturesLeft_RainThenRescueThenChase()
    {
        Season s = NewSeason(11);
        List<Fixture> mine = s.fixtures.FindAll(f => f.Involves(s.yourTeam) && f.round >= 2);
        for (int i = 0; i < mine.Count - 2; i++)
            mine[i].played = true;
        CareerSituations.Deal(s);
        var left = new List<eSituation> { mine[mine.Count - 1].situation, mine[mine.Count - 2].situation };
        CollectionAssert.AreEquivalent(new[] { eSituation.RainShortened, eSituation.Rescue }, left);
        mine[mine.Count - 3].played = false;
        mine[mine.Count - 3].situation = eSituation.Normal;
        CareerSituations.Deal(s);
        CollectionAssert.IsSubsetOf(new[] { eSituation.RainShortened, eSituation.Rescue, eSituation.Chase },
                                    mine.GetRange(mine.Count - 3, 3).ConvertAll(f => f.situation));
    }

    [Test]
    public void TheFinal_IsMadeAsTheFinal()
    {
        Season s = NewSeason(3);
        foreach (Fixture f in s.fixtures) f.played = true;
        Assert.IsTrue(LeagueBuilder.AddFinal(s));
        Fixture final = s.fixtures.Find(f => f.final);
        Assert.AreEqual(eSituation.Final, final.situation);
        Assert.AreEqual(eSituation.Final, CareerSituations.Of(final));
    }

    // ---- Applying ---------------------------------------------------------------------------------

    /// His side's round-1 plan (always a normal match) and the two sides, to apply a situation to.
    private static MatchPlan Plan(int seed, eRung rung, out Team yours, out Team them, int ratings = -1)
    {
        CareerProfile p = CareerTestPlay.Career(seed, rung, ratings);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(seed));
        Assert.AreEqual(eSituation.Normal, plan.situation, "round 1 is normal");
        yours = p.season.Yours;
        them = p.season.Team(plan.opponent);
        return plan;
    }

    [Test]
    public void ANormalPlan_IsLeftExactlyAsItWas()
    {
        foreach (eRung rung in Rungs)
            for (int seed = 0; seed < 10; seed++)
            {
                MatchPlan plan = Plan(seed, rung, out Team yours, out Team them);
                string before = Dump(plan);
                CareerSituations.Apply(plan, yours, them);
                Assert.AreEqual(before, Dump(plan), $"{rung} seed {seed}");
            }
        Assert.DoesNotThrow(() => CareerSituations.Apply(null, null, null));
    }

    [Test]
    public void AChase_IsTheHigherOfTwoOfTheirInnings()
    {
        foreach (eRung rung in Rungs)
            for (int seed = 0; seed < 10; seed++)
            {
                MatchPlan plan = Plan(seed, rung, out Team yours, out Team them);
                plan.situation = eSituation.Chase;
                CareerSituations.Apply(plan, yours, them);
                var rng = new Random(plan.seed ^ 0x7B5A1C);
                int a = CareerSim.Innings(them, yours, plan.overs, 0, plan.bowling, rng).runs;
                int b = CareerSim.Innings(them, yours, plan.overs, 0, plan.bowling, rng).runs;
                Assert.IsFalse(plan.batFirst, "he chases");
                Assert.AreEqual(Math.Max(a, b), plan.theirs.runs);
                Assert.AreEqual(Ladder.Overs(rung), plan.theirs.overs, "over the full overs");
                Assert.AreEqual(plan.theirs.runs + 1, plan.Target);
                Assert.AreEqual(0, plan.revisedTarget);
            }
    }

    [Test]
    public void Rain_CutsTheOvers_AndAChasesTarget()
    {
        int chases = 0, firsts = 0;
        foreach (eRung rung in Rungs)
            for (int seed = 0; seed < 20; seed++)
            {
                MatchPlan plan = Plan(seed, rung, out Team yours, out Team them);
                plan.situation = eSituation.RainShortened;
                int full = plan.overs;
                CareerSituations.Apply(plan, yours, them);
                Assert.That(plan.overs, Is.InRange((int)Math.Floor(full * 0.6), full - 1), $"{rung} seed {seed}");
                if (plan.batFirst)
                {
                    firsts++;
                    Assert.IsNull(plan.theirs);
                    Assert.AreEqual(0, plan.revisedTarget);
                    Assert.AreEqual(0, plan.Target);
                    continue;
                }
                chases++;
                Assert.AreEqual(full, plan.theirs.overs, "they batted their full overs");
                Assert.AreEqual(CareerSituationMath.RevisedTarget(plan.theirs.runs, full, plan.overs), plan.revisedTarget);
                Assert.AreEqual(plan.revisedTarget, plan.Target);
                Assert.LessOrEqual(plan.Target, plan.theirs.runs + 1);
            }
        Assert.Greater(chases, 0);
        Assert.Greater(firsts, 0);
    }

    [Test]
    public void TheFinal_IsPlayedToAPackedGround()
    {
        MatchPlan plan = Plan(4, eRung.Club, out Team yours, out Team them);
        plan.situation = eSituation.Final;
        var before = (plan.batFirst, plan.overs, plan.slot);
        CareerSituations.Apply(plan, yours, them);
        Assert.AreEqual(eCrowd.Packed, plan.crowd);
        Assert.AreEqual(before, (plan.batFirst, plan.overs, plan.slot), "nothing else changes");
    }

    // ---- The rescue -------------------------------------------------------------------------------

    /// The plan's collapse replayed as the match replays it, from a fresh state.
    private static MatchState Replay(MatchPlan plan, out int legal)
    {
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        legal = 0;
        foreach (int code in plan.collapse)
        {
            CareerMatchLogic.Score(s, code, "");
            if (code != CareerSim.Wide) legal++;
        }
        return s;
    }

    [Test]
    public void ARescue_ComesInAtNumberFive_AtTheScoreTheScriptReaches()
    {
        foreach (eRung rung in Rungs)
            for (int seed = 0; seed < 25; seed++)
            {
                MatchPlan plan = Plan(seed, rung, out Team yours, out Team them);
                string you = plan.order[plan.slot];
                float rating = plan.orderRatings[plan.slot];
                plan.situation = eSituation.Rescue;
                CareerSituations.Apply(plan, yours, them);
                string at = $"{rung} seed {seed}";
                Assert.AreEqual(CareerSituations.RescueIndex, plan.slot, at);
                Assert.AreEqual(you, plan.order[plan.slot], "his name moved with him");
                Assert.AreEqual(rating, plan.orderRatings[plan.slot], "and his rating");
                Assert.AreEqual(11, plan.order.Count);

                MatchState s = Replay(plan, out int legal);
                Assert.AreEqual(CareerSituations.RescueWickets, s.wickets, at);
                Assert.AreEqual(plan.startWickets, s.wickets, at);
                Assert.AreEqual(plan.startRuns, s.runs, at);
                int budget = (int)Math.Round(0.3f * Ladder.Overs(rung) * 6);
                Assert.LessOrEqual(legal, Math.Max(3 * CareerSituations.RescueWickets, budget), at);
                Assert.IsTrue(s.striker == plan.slot || s.runner == plan.slot, "he is at the crease");
                Assert.AreEqual(0, s.You.balls, "having faced nothing");
                Assert.IsFalse(CareerMatchLogic.InningsDone(s), "with the match still on");
            }
    }

    [Test]
    public void ACollapse_ScoresSlowerThanANormalStart()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.ProLeague })
        {
            float collapseRuns = 0f, collapseBalls = 0f, normalRuns = 0f, normalBalls = 0f;
            MatchPlan basePlan = Plan(5, rung, out Team yours, out Team them);
            for (int seed = 0; seed < 500; seed++)
            {
                MatchPlan plan = Plan(5, rung, out yours, out them);
                plan.seed = seed;
                plan.situation = eSituation.Rescue;
                CareerSituations.Apply(plan, yours, them);
                MatchState c = Replay(plan, out int legal);
                collapseRuns += c.runs; collapseBalls += legal;
                // The same number of balls from a normal start (CareerMatch's own ball when nothing is scripted).
                MatchState n = CareerMatchLogic.NewState(basePlan, basePlan.overs * 6, basePlan.Target);
                var rng = new Random(seed + 7919);
                for (int balls = 0; balls < legal && !CareerMatchLogic.InningsDone(n);)
                {
                    float a = CareerSim.Aggression(n.runs, n.wickets, n.balls, n.totalBalls, n.target);
                    int code = CareerSim.Ball(n.Striker.rating, basePlan.bowling, a, rng);
                    CareerMatchLogic.Score(n, code, "");
                    if (code != CareerSim.Wide) balls++;
                }
                normalRuns += n.runs; normalBalls += n.balls;
            }
            float ratio = collapseRuns / collapseBalls / (normalRuns / normalBalls);
            Assert.LessOrEqual(ratio, 0.75f, $"{rung}: the collapse scores at {ratio:0.00} of a normal start");
        }
    }

    [Test]
    public void HisRatings_NeverChangeTheCollapse()
    {
        CareerProfile p = CareerTestPlay.Career(9, eRung.State, 40f);
        CareerProgress.NextFixture(p).situation = eSituation.Rescue;
        MatchPlan low = CareerProgress.PlanNext(p, new Random(21));
        for (int i = 0; i < RatingSet.Count; i++)
            p.ratings[i] = 90f;
        MatchPlan high = CareerProgress.PlanNext(p, new Random(21));
        Assert.AreNotEqual(low.orderRatings[low.slot], high.orderRatings[high.slot], "his rating did change");
        Assert.IsNotEmpty(low.collapse);
        CollectionAssert.AreEqual(low.collapse, high.collapse);
        Assert.AreEqual(low.startRuns, high.startRuns);
        Assert.AreEqual(low.startWickets, high.startWickets);
    }

    [Test]
    public void ACollapse_NeverScriptsHisBalls_NorWinsTheChase()
    {
        MatchPlan plan = Plan(2, eRung.Club, out _, out _);
        plan.slot = 1;   // in the top order: no script
        Assert.IsEmpty(CareerSituationMath.Collapse(plan, 3, 11, new Random(1), out MatchState none));
        Assert.AreEqual(0, none.balls);
        Assert.IsEmpty(CareerSituationMath.Collapse(null, 3, 11, new Random(1), out _));

        for (int seed = 0; seed < 200; seed++)
        {
            MatchPlan chase = Plan(2, eRung.Club, out _, out _);
            chase.batFirst = false;
            chase.theirs = new InningsCard { runs = 6, overs = chase.overs };   // 7 to win
            chase.slot = 5;
            List<int> codes = CareerSituationMath.Collapse(chase, 3, 11, new Random(seed), out MatchState end);
            Assert.AreEqual(3, end.wickets, $"seed {seed}");
            Assert.Less(end.runs, chase.Target, "the script never wins it");
            Assert.AreEqual(0, end.You.balls);
        }
    }

    // ---- Results against a revised target ---------------------------------------------------------

    /// A Pro League fixture recorded: the home side made 164/6 in 20, the away side chased a revised
    /// 124 in 14 overs and made `chased` in 13.2.
    private static Fixture RecordRainChase(Season s, int chased)
    {
        var f = new Fixture { round = 2, home = s.teams[0].id, away = s.teams[1].id };
        var home = new InningsCard { runs = 164, wickets = 6, balls = 120, overs = 20 };
        var away = new InningsCard { runs = chased, wickets = 4, balls = 80, overs = 14, target = 124, revised = true };
        LeagueBuilder.Record(s, f, home, away);
        return f;
    }

    [Test]
    public void ARevisedChase_IsWonAtTheTarget_TiedOneShort_AndLostByOneRunBelow()
    {
        Season s = NewSeason(1, eRung.ProLeague);
        Team h = s.teams[0], a = s.teams[1];

        Fixture f = RecordRainChase(s, 124);
        Assert.AreEqual(a.id, f.winner, "124 reaches the revised target");
        Assert.AreEqual($"{a.name} won by 6 wickets (4 balls left)", f.result, "balls left of his 14 overs");
        Assert.AreEqual((2, 0), (a.points, h.points));

        f = RecordRainChase(s, 123);
        Assert.AreEqual("", f.winner);
        Assert.AreEqual("Match tied", f.result);
        Assert.AreEqual((3, 1), (a.points, h.points));

        f = RecordRainChase(s, 122);
        Assert.AreEqual(h.id, f.winner);
        Assert.AreEqual($"{h.name} won by 1 run", f.result);
        Assert.AreEqual((3, 3), (a.points, h.points));
        Assert.AreEqual((1, 1, 1), (a.won, a.tied, a.lost));
    }

    [Test]
    public void AChaseNoRainCut_IsJudgedAsBefore()
    {
        Season s = NewSeason(1, eRung.ProLeague);
        var f = new Fixture { round = 2, home = s.teams[0].id, away = s.teams[1].id };
        var home = new InningsCard { runs = 140, wickets = 9, balls = 120, overs = 20, target = 165 };
        var away = new InningsCard { runs = 164, wickets = 6, balls = 120, overs = 20 };
        LeagueBuilder.Record(s, f, home, away);
        Assert.AreEqual(s.teams[1].id, f.winner);
        Assert.AreEqual($"{s.teams[1].name} won by 24 runs", f.result);

        CareerSituationMath.Judged(new InningsCard { runs = 10 }, new InningsCard { runs = 12 }, out int hr, out int ar);
        Assert.AreEqual((10, 12), (hr, ar), "no target on either card: their own runs");
    }

    [Test]
    public void TheResultLine_CountsTheMarginFromThePar()
    {
        var first = new InningsCard { runs = 164, wickets = 6, balls = 120, overs = 20 };
        var second = new InningsCard { runs = 120, wickets = 7, balls = 84, overs = 14, target = 124, revised = true };
        Assert.AreEqual("Old Mill CC won by 3 runs",
            CareerProgressMath.ResultText(true, false, true, "Old Mill CC", "Harbour CC", first, second));
        Assert.AreEqual("Old Mill CC won by 3 runs",
            CareerProgressMath.ResultText(false, false, false, "Harbour CC", "Old Mill CC", second, first));
        second.revised = false;
        Assert.AreEqual("Old Mill CC won by 44 runs",
            CareerProgressMath.ResultText(true, false, true, "Old Mill CC", "Harbour CC", first, second), "no rain: as before");
    }

    // ---- Form and the record book -----------------------------------------------------------------

    [Test]
    public void AShortenedGame_AsksForFewerRunsTowardForm_OldRecordsUnchanged()
    {
        var full = new InningsRecord { rung = eRung.ProLeague, batted = true, runs = 20, balls = 18, isOut = true };
        float old = Mathf.Clamp01(20 / CareerProgressMath.ParRuns(eRung.ProLeague)) * 0.75f
                  + Mathf.Clamp01(full.StrikeRate / CareerProgressMath.ParStrikeRate(eRung.ProLeague)) * 0.15f;
        Assert.AreEqual(old, CareerProgressMath.InningsScore(full), "overs 0 (before phase 7): as it was");
        full.overs = 20;
        Assert.AreEqual(old, CareerProgressMath.InningsScore(full), "a full game: as it was");
        var rain = new InningsRecord { rung = eRung.ProLeague, batted = true, runs = 20, balls = 18, isOut = true, overs = 14 };
        Assert.Greater(CareerProgressMath.InningsScore(rain), old);
        Assert.AreEqual(0.7f, CareerSituations.ParScale(rain), 1e-6f);
        Assert.AreEqual(1f, CareerSituations.ParScale(full));
        Assert.AreEqual(1f, CareerSituations.ParScale(null));
    }

    [Test]
    public void TheRecordBookLine_IsStampedWithTheSituation()
    {
        var plan = new MatchPlan { situation = eSituation.RainShortened, overs = 14, batFirst = false,
                                   theirs = new InningsCard { runs = 164 }, revisedTarget = 124 };
        var rec = new InningsRecord();
        CareerSituations.Stamp(rec, plan);
        Assert.AreEqual((eSituation.RainShortened, 14, 124), (rec.situation, rec.overs, rec.target));
        Assert.DoesNotThrow(() => CareerSituations.Stamp(null, plan));
        Assert.DoesNotThrow(() => CareerSituations.Stamp(rec, null));
    }

    [Test]
    public void ASeasonWithItsSituations_PlaysThrough_EachMatchStamped()
    {
        foreach (eRung rung in Rungs)
        {
            CareerProfile p = CareerTestPlay.Career(13, rung, 60f);
            var rng = new Random(13);
            var seen = new HashSet<eSituation>();
            for (int guard = 0; !p.season.finished && guard < 30; guard++)
            {
                if (CareerProgress.LeftOutNext(p)) { CareerProgress.PlayLeftOut(p, rng); continue; }
                Fixture f = CareerProgress.NextFixture(p);
                MatchPlan plan = CareerTestPlay.Play(p, rng, 30, 24, false);
                InningsRecord rec = p.innings[p.innings.Count - 1];
                Assert.AreEqual(CareerSituations.Of(f), rec.situation, $"{rung} round {f.round}");
                Assert.AreEqual(plan.overs, rec.overs);
                Assert.AreEqual(plan.Target, rec.target);
                if (rec.situation == eSituation.Rescue) Assert.AreEqual(CareerSituations.RescueIndex + 1, rec.position);
                seen.Add(rec.situation);
            }
            Assert.IsTrue(p.season.finished, rung.ToString());
            Assert.Greater(seen.Count, 1, $"{rung}: the season asked for more than normal matches");
        }
    }

    // ---- An old save ------------------------------------------------------------------------------

    [Test]
    public void AnOldMidSeasonSave_IsDealtItsSituations_SkippingTheMatchBegun()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cvr_situations_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        try
        {
            CareerProfile p = CareerTestPlay.Career(6);
            CareerTestPlay.Play(p, new Random(6), 20, 15, true);
            Season s = p.season;
            int begun = s.fixtures.FindIndex(f => f.situation != eSituation.Normal && !f.played);
            Assert.GreaterOrEqual(begun, 0);
            foreach (Fixture f in s.fixtures) f.situation = eSituation.Normal;   // as phase 6 saved them
            s.situationsDealt = false;
            p.pending = new PendingMatch { active = true, plan = new MatchPlan { fixture = begun } };
            p.version = 2;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "slot1.json"), JsonUtility.ToJson(p));

            CareerProfile back = CareerSaves.Load(0);
            Assert.IsTrue(back.season.situationsDealt);
            Assert.AreEqual(eSituation.Normal, back.season.fixtures[begun].situation, "the match begun is left as it was");
            Assert.Greater(back.season.fixtures.FindAll(f => f.situation != eSituation.Normal).Count, 0, "the rest are dealt");
            foreach (Fixture f in back.season.fixtures)
                if (f.played) Assert.AreEqual(eSituation.Normal, f.situation, "a match played stays as it was");
        }
        finally
        {
            CareerSaves.UseDirectoryForTests(null);
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (IOException) { }
        }
    }
}
