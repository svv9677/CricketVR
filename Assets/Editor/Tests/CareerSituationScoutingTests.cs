using NUnit.Framework;
using UnityEngine;

/// <summary>
/// What a scout makes of an innings in a match situation (phase 7, Scouting): a normal match exactly
/// as before; fewer runs asked in a game rain cut; a rescue rewards still being there; seeing a chase
/// home not out and keeping up with its asking rate count; the final weighs the innings more; and the
/// note says where the innings was played. Only what was measured is read.
/// </summary>
public class CareerSituationScoutingTests
{
    private static InningsRecord Inn(int runs, int balls, bool isOut, eSituation s = eSituation.Normal,
                                     eRung rung = eRung.ProLeague, int overs = 0, int target = 0, bool won = false) =>
        new InningsRecord { runs = runs, balls = balls, isOut = isOut, batted = true, rung = rung, opponent = "Old Mill CC",
                            situation = s, overs = overs, target = target, won = won };

    private static RatingSet Ratings(float v)
    {
        var r = new RatingSet();
        for (int i = 0; i < RatingSet.Count; i++)
            r[i] = v;
        return r;
    }

    private static float Seen(InningsRecord r, eRung rung = eRung.ProLeague) => Scouting.Impression(r, rung, Ratings(50f), 50f);

    /// The phase 4 rule, written out: what a normal match must still give, to the last bit.
    private static float Phase4(InningsRecord r, eRung rung, float overall, float form)
    {
        float runs = Mathf.Clamp01(r.runs / (1.5f * CareerProgressMath.ParRuns(rung))) * 0.7f;
        float rate = Mathf.Clamp01(r.StrikeRate / CareerProgressMath.ParStrikeRate(rung)) * 0.2f;
        float innings = runs + rate + (r.isOut ? 0f : 0.1f);
        float rated = Mathf.Clamp01(0.5f + (overall - Scouting.Bar(rung)) / 30f);
        float formed = Mathf.Clamp01(form / 99f);
        return Mathf.Clamp01(0.55f * innings + 0.3f * rated + 0.15f * formed);
    }

    private static void AssertLatin1(string s)
    {
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    [Test]
    public void ANormalMatch_IsJudgedExactlyAsBefore()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
            foreach (InningsRecord r in new[] { Inn(0, 3, true), Inn(17, 15, true), Inn(45, 30, false), Inn(140, 60, false, overs: 20, target: 150, won: true) })
            {
                r.rung = rung;
                Assert.AreEqual(Phase4(r, rung, 50f, 50f), Seen(r, rung), $"{rung} {r.runs}");
            }
        Assert.AreEqual("34* off 22 balls against Old Mill CC", Scouting.ScoreText(Inn(34, 22, false)));
        Assert.AreEqual("no innings against Old Mill CC", Scouting.ScoreText(new InningsRecord { opponent = "Old Mill CC" }));
    }

    [Test]
    public void TwentyInARainShortenedGame_ImpressesMoreThanTwentyInAFullOne()
    {
        float rain = Seen(Inn(20, 16, true, eSituation.RainShortened, overs: 14));
        float full = Seen(Inn(20, 16, true));
        Assert.Greater(rain, full);
        Assert.Less(Seen(Inn(20, 16, true, eSituation.RainShortened, overs: 0)), full,
                    "no overs on the line: the runs par is not cut, and the rate par still rises");
    }

    [Test]
    public void InARescue_StayingInCountsForMore()
    {
        float rescueGap = Seen(Inn(25, 30, false, eSituation.Rescue)) - Seen(Inn(25, 30, true, eSituation.Rescue));
        float normalGap = Seen(Inn(25, 30, false)) - Seen(Inn(25, 30, true));
        Assert.Greater(rescueGap, normalGap);
        Assert.Greater(Seen(Inn(25, 30, true, eSituation.Rescue)), Seen(Inn(25, 30, true)), "fewer runs asked after a collapse");
    }

    [Test]
    public void SeeingAChaseHome_AndKeepingUpWithItsRate_Count()
    {
        float home = Seen(Inn(30, 24, false, eSituation.Chase, overs: 20, target: 150, won: true));
        float outInWin = Seen(Inn(30, 24, true, eSituation.Chase, overs: 20, target: 150, won: true));
        float notOutLost = Seen(Inn(30, 24, false, eSituation.Chase, overs: 20, target: 150, won: false));
        float normalGap = Seen(Inn(30, 24, false, won: true)) - Seen(Inn(30, 24, true, won: true));
        Assert.Greater(home - outInWin, normalGap, "not out at the end of a won chase");
        Assert.AreEqual(normalGap, notOutLost - outInWin, 1e-5f, "not out in a lost chase: the usual bonus");

        // 28 off 20 is above par at 150 to win (125 a 100 balls) but below it at 230 (about 192).
        float easy = Seen(Inn(28, 20, true, eSituation.Chase, overs: 20, target: 150));
        float steep = Seen(Inn(28, 20, true, eSituation.Chase, overs: 20, target: 230));
        Assert.Greater(easy, steep, "the asking rate is the strike-rate par");
    }

    [Test]
    public void TheSameBigInnings_CountsForMoreInTheFinal()
    {
        Assert.Greater(Seen(Inn(70, 40, false, eSituation.Final)), Seen(Inn(70, 40, false)));
        Assert.Less(Seen(Inn(0, 4, true, eSituation.Final)), Seen(Inn(0, 4, true)), "and a duck for less");
    }

    [Test]
    public void EverySituation_StaysBetweenZeroAndOne()
    {
        foreach (eSituation s in new[] { eSituation.Normal, eSituation.Chase, eSituation.RainShortened, eSituation.Rescue, eSituation.Final })
            foreach (InningsRecord r in new[] { Inn(0, 1, true, s), Inn(200, 60, false, s, overs: 4, target: 20, won: true) })
            {
                float x = Scouting.Impression(r, eRung.Club, Ratings(99f), 99f);
                Assert.That(x, Is.InRange(0f, 1f), s.ToString());
            }
    }

    [Test]
    public void TheNote_SaysWhereTheInningsWasPlayed()
    {
        Assert.AreEqual("34* off 22 balls against Old Mill CC in the chase", Scouting.ScoreText(Inn(34, 22, false, eSituation.Chase)));
        Assert.AreEqual("12 off 9 balls against Old Mill CC in the rain-shortened game", Scouting.ScoreText(Inn(12, 9, true, eSituation.RainShortened)));
        Assert.AreEqual("40* off 35 balls against Old Mill CC after the collapse", Scouting.ScoreText(Inn(40, 35, false, eSituation.Rescue)));
        Assert.AreEqual("1 off 1 ball against Old Mill CC in the final", Scouting.ScoreText(Inn(1, 1, true, eSituation.Final)));
        foreach (eSituation s in new[] { eSituation.Chase, eSituation.RainShortened, eSituation.Rescue, eSituation.Final })
            foreach (eRung from in new[] { eRung.State, eRung.International })
            {
                string note = Scouting.Report(Inn(30, 20, false, s), from, 0.6f);
                StringAssert.Contains(Scouting.ScoreText(Inn(30, 20, false, s)), note);
                AssertLatin1(note);
            }
    }
}
