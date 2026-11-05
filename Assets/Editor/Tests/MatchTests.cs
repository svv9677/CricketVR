using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The match rules that need no scene: the AI's innings, the bowling levels on top of plans, and
/// the match history kept in PlayerPrefs (restored after each test).
/// </summary>
public class MatchTests
{
    private string savedHistory;
    private int savedPending;

    [SetUp]
    public void Save()
    {
        savedHistory = PlayerPrefs.GetString("match_history", "");
        savedPending = PlayerPrefs.GetInt("match_result_pending", 0);
        PlayerPrefs.DeleteKey("match_history");
    }

    [TearDown]
    public void Restore()
    {
        PlayerPrefs.SetString("match_history", savedHistory);
        PlayerPrefs.SetInt("match_result_pending", savedPending);
    }

    [Test]
    public void LevelsMultiplyThePlan()
    {
        BowlingPlan easyOffStump = BowlingLevels.Apply(BowlingPlans.OffStump, BowlingLevels.Easy);
        for (int i = 0; i < BowlingPlans.OffStump.lineWeights.Length; i++)
            Assert.AreEqual(BowlingPlans.OffStump.lineWeights[i] * BowlingLevels.Easy.lineWeights[i], easyOffStump.lineWeights[i], 1e-5f);
        // Hard is the plan as written; no plan is plain bowling under the level.
        BowlingPlan hard = BowlingLevels.Apply(BowlingPlans.ShortBall, BowlingLevels.Hard);
        Assert.AreEqual(BowlingPlans.ShortBall.lengthWeights, hard.lengthWeights);
        BowlingPlan plain = BowlingLevels.Apply(null, BowlingLevels.Pro);
        Assert.AreEqual(BowlingLevels.Pro.lineWeights, plain.lineWeights);
        Assert.AreSame(plain, BowlingLevels.Apply(null, BowlingLevels.Pro), "cached, not rebuilt every ball");
    }

    [Test]
    public void EasyBowlsMoreLooseBallsThanPro()
    {
        float Loose(BowlingLevels.Level l)
        {
            var s = BowlingPlans.Share(BowlingLevels.Apply(null, l), eSwingType.Pace);
            float x = 0f;
            for (int z = 0; z < BatterBook.LineZones; z++) x += s[z, 1] + s[z, 4];   // half-volleys and long hops
            return x;
        }
        Assert.Greater(Loose(BowlingLevels.Easy), Loose(BowlingLevels.Hard));
        Assert.Greater(Loose(BowlingLevels.Hard), Loose(BowlingLevels.Pro));
    }

    [Test]
    public void HistoryKeepsTheNewestFirst()
    {
        for (int i = 0; i < MatchHistory.MaxKept + 3; i++)
            MatchHistory.Add(new MatchHistory.Record { runs = i, wickets = 1, balls = 30, overs = 5, result = "Innings complete" });
        Assert.AreEqual(MatchHistory.MaxKept, MatchHistory.All.Count);
        Assert.AreEqual(MatchHistory.MaxKept + 2, MatchHistory.Latest.runs);
        Assert.IsTrue(MatchHistory.ResultPending);
        Assert.AreEqual($"{MatchHistory.MaxKept + 2}/1 (5.0 ov) · Innings complete", MatchHistory.Line(MatchHistory.Latest));
    }

    [Test]
    public void AIScoresLikeARealSide()
    {
        float Average(eBowlingLevel l)
        {
            var rng = new System.Random(7);
            float sum = 0f;
            for (int i = 0; i < 400; i++) sum += AIInnings.Play(20, 0, l, rng).runs;
            return sum / 400f;
        }
        float easy = Average(eBowlingLevel.Easy), pro = Average(eBowlingLevel.Pro);
        // T20-style totals: ~7 an over for a club side, ~9 for a professional one.
        Assert.That(easy, Is.InRange(120f, 160f), "club side, 20 overs");
        Assert.That(pro, Is.InRange(160f, 200f), "professional side, 20 overs");
        Assert.Greater(Average(eBowlingLevel.Hard), Average(eBowlingLevel.Medium));
    }

    [Test]
    public void AIChaseStopsAtTheTargetAndIsReplayable()
    {
        var a = AIInnings.Play(5, 40, eBowlingLevel.Medium, new System.Random(3));
        var b = AIInnings.Play(5, 40, eBowlingLevel.Medium, new System.Random(3));
        Assert.AreEqual(a.runs, b.runs, "same seed, same innings");
        Assert.AreEqual(a.balls, b.balls);
        Assert.LessOrEqual(a.balls, 30);
        int sum = 0; foreach (int r in a.overRuns) sum += r;
        Assert.AreEqual(a.runs, sum, "the overs add up to the total");
        if (a.ReachedTarget) Assert.Less(a.runs, 40 + 6, "stops as soon as it is reached");
        // A big chase is lost more often than a small one.
        int smallWon = 0, bigWon = 0;
        var rng = new System.Random(11);
        for (int i = 0; i < 300; i++)
        {
            if (AIInnings.Play(5, 30, eBowlingLevel.Medium, rng).ReachedTarget) smallWon++;
            if (AIInnings.Play(5, 70, eBowlingLevel.Medium, rng).ReachedTarget) bigWon++;
        }
        Assert.Greater(smallWon, bigWon + 60);
        string res = AIInnings.ChaseResult(new AIInnings.Result { target = 50, runs = 40, overs = 5, balls = 30 }, out bool won, out bool tied);
        Assert.IsTrue(won); Assert.IsFalse(tied);
        Assert.AreEqual("You won by 9 runs", res);
    }

    [Test]
    public void AChaseIsWonOnlyByReachingTheTarget()
    {
        Assert.IsTrue(new MatchHistory.Record { runs = 50, target = 50 }.Won);
        Assert.IsFalse(new MatchHistory.Record { runs = 49, target = 50 }.Won);
        Assert.IsFalse(new MatchHistory.Record { runs = 80, target = 0 }.Won, "setting a total is not won or lost");
    }
}
