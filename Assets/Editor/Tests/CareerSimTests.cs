using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The career simulation (CareerSim): one ball's odds are calibrated (a 50-rated batter against
/// Medium bowling averages 20-32 at a strike rate of 105-130; an 80 against Easy far more and
/// faster; a 20 against Pro under 10; wides 2-4 in 100; more intent means more runs and more
/// wickets), intent follows the state of the innings, dismissals come in a realistic mix, and a
/// simulated innings adds up (every run is a batter's or a wide, wickets, balls and the chase all
/// end where cricket ends them), rotates its bowlers within their quota and is the same for the
/// same seed. Standalone: seeded, no scene.
/// </summary>
public class CareerSimTests
{
    private struct Figures
    {
        public float average, strikeRate, widePct, outRate;
    }

    /// Many seeded balls to one batter: his average, strike rate, wides and wickets per ball.
    private static Figures Measure(float bat, eBowlingLevel level, float aggression, int seed = 7, int balls = 200000)
    {
        var rng = new Random(seed);
        long runs = 0, legal = 0, outs = 0, wides = 0;
        for (int i = 0; i < balls; i++)
        {
            int r = CareerSim.Ball(bat, level, aggression, rng);
            if (r == CareerSim.Wide) { wides++; continue; }
            legal++;
            if (r == CareerSim.Wicket) outs++;
            else
            {
                Assert.That(r == 0 || r == 1 || r == 2 || r == 3 || r == 4 || r == 6, "a ball is a scorebook code: " + r);
                runs += r;
            }
        }
        return new Figures
        {
            average = outs > 0 ? runs / (float)outs : runs,
            strikeRate = legal > 0 ? runs * 100f / legal : 0f,
            widePct = wides * 100f / balls,
            outRate = legal > 0 ? outs / (float)legal : 0f,
        };
    }

    [Test]
    public void AFiftyAgainstMediumBowlingBatsLikeAClubOpener()
    {
        Figures f = Measure(50f, eBowlingLevel.Medium, 0.5f);
        Assert.That(f.average, Is.InRange(20f, 32f), "average");
        Assert.That(f.strikeRate, Is.InRange(105f, 130f), "strike rate");
    }

    [Test]
    public void AnEightyAgainstEasyBowlingAveragesFarMoreAndScoresFaster()
    {
        Figures mid = Measure(50f, eBowlingLevel.Medium, 0.5f);
        Figures star = Measure(80f, eBowlingLevel.Easy, 0.5f);
        Assert.Greater(star.average, mid.average * 1.6f, "average");
        Assert.Greater(star.strikeRate, mid.strikeRate + 20f, "strike rate");
    }

    [Test]
    public void ATwentyAgainstProBowlingAveragesUnderTen()
    {
        Figures f = Measure(20f, eBowlingLevel.Pro, 0.5f);
        Assert.Less(f.average, 10f);
        Assert.Greater(f.average, 1f, "still scores something");
    }

    [Test]
    public void BetterBattersAverageMoreAndHarderBowlingCostsRuns()
    {
        float last = 0f;
        foreach (float bat in new[] { 20f, 40f, 60f, 80f })
        {
            float avg = Measure(bat, eBowlingLevel.Medium, 0.5f).average;
            Assert.Greater(avg, last, "bat " + bat);
            last = avg;
        }
        float easier = float.MaxValue;
        foreach (eBowlingLevel l in new[] { eBowlingLevel.Easy, eBowlingLevel.Medium, eBowlingLevel.Hard, eBowlingLevel.Pro })
        {
            float avg = Measure(60f, l, 0.5f).average;
            Assert.Less(avg, easier, l.ToString());
            easier = avg;
        }
    }

    [Test]
    public void MoreIntentMeansMoreRunsAndMoreWickets()
    {
        Figures calm = Measure(50f, eBowlingLevel.Medium, 0.2f);
        Figures normal = Measure(50f, eBowlingLevel.Medium, 0.5f);
        Figures slog = Measure(50f, eBowlingLevel.Medium, 0.9f);
        Assert.Less(calm.strikeRate, normal.strikeRate);
        Assert.Less(normal.strikeRate, slog.strikeRate);
        Assert.Less(calm.outRate, normal.outRate);
        Assert.Less(normal.outRate, slog.outRate);
    }

    [Test]
    public void WidesAreTwoToFourInAHundred()
    {
        foreach (eBowlingLevel l in new[] { eBowlingLevel.Easy, eBowlingLevel.Medium, eBowlingLevel.Hard, eBowlingLevel.Pro })
            Assert.That(Measure(50f, l, 0.5f).widePct, Is.InRange(2f, 4f), l.ToString());
    }

    [Test]
    public void IntentRisesAtTheEndWithWicketsInHandAndFallsAfterACollapse()
    {
        // Setting a total of 20 overs (120 balls).
        float middle = CareerSim.Aggression(70, 2, 60, 120, 0);
        float death = CareerSim.Aggression(140, 2, 112, 120, 0);
        float deathLate = CareerSim.Aggression(140, 7, 112, 120, 0);
        Assert.Greater(death, middle, "the last overs");
        Assert.Greater(death, deathLate, "wickets in hand");
        float steady = CareerSim.Aggression(30, 1, 30, 120, 0);
        float collapse = CareerSim.Aggression(30, 5, 30, 120, 0);
        Assert.Less(collapse, steady, "a collapse");
        for (int b = 0; b <= 120; b += 6)
            for (int w = 0; w <= 10; w++)
                Assert.That(CareerSim.Aggression(b, w, b, 120, b % 12 == 0 ? 160 : 0), Is.InRange(0f, 1f));
    }

    [Test]
    public void AChaseFollowsTheAskingRate()
    {
        float easy = CareerSim.Aggression(100, 3, 90, 120, 120);   // 20 off 30
        float hard = CareerSim.Aggression(100, 3, 90, 120, 160);   // 60 off 30
        Assert.Greater(hard, easy);
        Assert.Greater(hard, 0.8f);
        Assert.Less(easy, 0.5f);
    }

    [Test]
    public void DismissalsComeInARealisticMixAndRead()
    {
        var rng = new Random(3);
        int n = 20000, caught = 0, bowled = 0, lbw = 0, runOut = 0, stumped = 0;
        for (int i = 0; i < n; i++)
        {
            string h = CareerSim.HowOut("Khan", "Sharma", "Nair", i % 2 == 0, rng);
            if (h.StartsWith("c ")) caught++;
            else if (h.StartsWith("lbw b ")) lbw++;
            else if (h.StartsWith("b ")) bowled++;
            else if (h == "run out") runOut++;
            else if (h.StartsWith("st ")) stumped++;
            else Assert.Fail("unexpected: " + h);
            Assert.IsTrue(h == "run out" || h.EndsWith("b Khan"), h);
        }
        Assert.That(caught * 100f / n, Is.InRange(50f, 62f), "caught");
        Assert.That(bowled * 100f / n, Is.InRange(17f, 23f), "bowled");
        Assert.That(lbw * 100f / n, Is.InRange(12f, 18f), "lbw");
        Assert.That(runOut * 100f / n, Is.InRange(3f, 7f), "run out");
        Assert.That(stumped * 100f / n, Is.InRange(1f, 4f), "stumped (spin only, half the balls)");

        var seen = new HashSet<string>();
        for (int i = 0; i < 2000; i++) seen.Add(CareerSim.HowOut("Khan", "Sharma", "Nair", true, rng));
        foreach (string s in new[] { "c Sharma b Khan", "c Nair b Khan", "b Khan", "lbw b Khan", "st Nair b Khan", "c & b Khan", "run out" })
            Assert.IsTrue(seen.Contains(s), s);
        for (int i = 0; i < 2000; i++)
            Assert.IsFalse(CareerSim.HowOut("Khan", "Sharma", rng).StartsWith("st "), "no stumping without a spinner");
    }

    // ---- Innings --------------------------------------------------------------------------------

    private static Team Side(eRung rung, string name, int seed) => LeagueBuilder.NewTeam(rung, name, "IND", new Random(seed));

    [Test]
    public void AnInningsAddsUp()
    {
        var rng = new Random(21);
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
            for (int i = 0; i < 150; i++)
            {
                Team a = Side(rung, "A", i), b = Side(rung, "B", 1000 + i);
                int overs = Ladder.Overs(rung);
                int target = i % 2 == 0 ? 0 : 30 + i;
                InningsCard c = CareerSim.Innings(a, b, overs, target, Ladder.Bowling(rung), rng);
                CheckCard(c, a, overs, target);
            }
    }

    private static void CheckCard(InningsCard c, Team batting, int overs, int target)
    {
        Assert.AreEqual(batting.name, c.team);
        Assert.AreEqual(overs, c.overs);
        Assert.AreEqual(target, c.target);
        Assert.LessOrEqual(c.wickets, 10);
        Assert.LessOrEqual(c.balls, overs * 6);
        int batRuns = 0, batBalls = 0, outs = 0, notOut = 0;
        for (int i = 0; i < c.batting.Count; i++)
        {
            BatterLine l = c.batting[i];
            Assert.AreEqual(batting.players[i].name, l.name, "lines in batting order");
            Assert.IsTrue(l.batted);
            Assert.AreEqual(l.runs >= 4 * l.fours + 6 * l.sixes, true, "boundaries within runs");
            batRuns += l.runs;
            batBalls += l.balls;
            if (l.isOut) outs++;
            else { notOut++; Assert.AreEqual("not out", l.how); }
        }
        int wides = c.runs - batRuns;
        Assert.That(wides, Is.InRange(0, c.balls / 8 + 4), "every other run is a wide");
        Assert.AreEqual(c.balls, batBalls, "every legal ball was faced by a batter");
        Assert.AreEqual(c.wickets, outs);
        Assert.AreEqual(c.wickets >= 10 ? 1 : 2, notOut);
        Assert.AreEqual(c.wickets >= 10 ? 11 : c.wickets + 2, c.batting.Count, "everyone who batted, nobody else");
        int overRuns = 0, overWickets = 0;
        foreach (int r in c.overRuns) overRuns += r;
        foreach (int w in c.overWickets) overWickets += w;
        Assert.AreEqual(c.runs, overRuns);
        Assert.AreEqual(c.wickets, overWickets);
        // One entry per over begun (a chase can end on a wide before the over's first legal ball).
        Assert.That(c.overRuns.Count, Is.InRange((c.balls + 5) / 6, Math.Min(overs, (c.balls + 5) / 6 + 1)), "one entry per over begun");
        Assert.AreEqual(c.overRuns.Count, c.overWickets.Count);
        bool ended = c.wickets >= 10 || c.balls == overs * 6 || (target > 0 && c.runs >= target);
        Assert.IsTrue(ended, "an innings ends all out, out of overs or with the target reached");
        if (target > 0) Assert.LessOrEqual(c.runs, target - 1 + 6, "a chase stops at the target");
    }

    [Test]
    public void AChaseOfASmallTargetStopsAtIt()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            InningsCard c = CareerSim.Innings(Side(eRung.State, "A", seed), Side(eRung.State, "B", seed + 1), 10, 5,
                eBowlingLevel.Medium, new Random(seed));
            if (c.runs >= 5) Assert.LessOrEqual(c.runs, 10);
            Assert.IsTrue(c.runs >= 5 || c.wickets >= 10 || c.balls == 60);
        }
    }

    [Test]
    public void BowlersShareTheOversWithinTheirQuota()
    {
        foreach (int overs in new[] { 6, 10, 20 })
            for (int seed = 0; seed < 50; seed++)
            {
                Team t = Side(eRung.ProLeague, "B", seed);
                List<SquadPlayer> order = CareerSim.BowlingOrder(t, overs);
                Assert.AreEqual(overs, order.Count);
                var count = new Dictionary<SquadPlayer, int>();
                for (int o = 0; o < overs; o++)
                {
                    Assert.IsTrue(order[o].bowls, "a specialist bowler");
                    if (o > 0) Assert.AreNotSame(order[o - 1], order[o], "no bowler bowls two overs running");
                    count[order[o]] = count.TryGetValue(order[o], out int n) ? n + 1 : 1;
                }
                foreach (int n in count.Values) Assert.LessOrEqual(n, (overs + 4) / 5, "quota");
                Assert.That(order[0].kind, Is.EqualTo(eBowlerKind.Fast).Or.EqualTo(eBowlerKind.Medium), "pace takes the new ball");
            }
    }

    [Test]
    public void TheSameSeedPlaysTheSameInnings()
    {
        Team a = Side(eRung.ProLeague, "A", 1), b = Side(eRung.ProLeague, "B", 2);
        InningsCard x = CareerSim.Innings(a, b, 20, 0, eBowlingLevel.Hard, new Random(99));
        InningsCard y = CareerSim.Innings(a, b, 20, 0, eBowlingLevel.Hard, new Random(99));
        Assert.AreEqual(x.Score, y.Score);
        Assert.AreEqual(x.balls, y.balls);
        CollectionAssert.AreEqual(x.overRuns, y.overRuns);
        for (int i = 0; i < x.batting.Count; i++)
        {
            Assert.AreEqual(x.batting[i].runs, y.batting[i].runs);
            Assert.AreEqual(x.batting[i].how, y.batting[i].how);
        }
    }

    [Test]
    public void TheCareerPlayerIsMarkedOnTheCardAndNeverKeeps()
    {
        Team a = Side(eRung.Club, "A", 5), b = Side(eRung.Club, "B", 6);
        a.players[0].isYou = true;
        b.players[4].isYou = true;
        InningsCard c = CareerSim.Innings(a, b, 6, 0, eBowlingLevel.Easy, new Random(4));
        Assert.IsTrue(c.batting[0].isYou);
        string keeperSurname = NameBank.Surname(b.players[4].name);
        for (int seed = 0; seed < 300; seed++)
            foreach (BatterLine l in CareerSim.Innings(a, b, 6, 0, eBowlingLevel.Easy, new Random(seed)).batting)
                StringAssert.DoesNotStartWith("st " + keeperSurname + " ", l.how);
    }

    [Test]
    public void ACardBecomesTheMatchFlowResult()
    {
        InningsCard c = CareerSim.Innings(Side(eRung.State, "A", 1), Side(eRung.State, "B", 2), 10, 70, eBowlingLevel.Medium, new Random(8));
        AIInnings.Result r = CareerSim.ToAIResult(c);
        Assert.AreEqual(c.runs, r.runs);
        Assert.AreEqual(c.wickets, r.wickets);
        Assert.AreEqual(c.balls, r.balls);
        Assert.AreEqual(c.overs, r.overs);
        Assert.AreEqual(70, r.target);
        CollectionAssert.AreEqual(c.overRuns, r.overRuns);
        CollectionAssert.AreEqual(c.overWickets, r.overWickets);
        Assert.AreEqual(c.Score, r.Score);
        Assert.AreEqual(c.OversText, r.OversText);
    }
}
