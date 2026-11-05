using System;
using NUnit.Framework;

/// <summary>
/// Selection (Selection, CareerProgress.LeftOutNext / PlayLeftOut): never left out at Club; three
/// poor innings at State or above and the captain leaves him out of the next match, which is then
/// played without him (a left-out line in the record book, not in the stats; the round and the
/// season go on), and he is back for the one after with a fresh run of innings; good form keeps
/// him in; and the same seed drops him the same way.
/// </summary>
public class SelectionTests
{
    private static InningsRecord Inn(int season, eRung rung, int runs, int balls, bool isOut) =>
        new InningsRecord { season = season, rung = rung, runs = runs, balls = balls, isOut = isOut, batted = true };

    [Test]
    public void NeverLeftOutAtClub()
    {
        CareerProfile p = CareerTestPlay.Career(50, eRung.Club, 20f);
        var rng = new Random(51);
        for (int i = 0; i < 6; i++)
        {
            CareerTestPlay.Play(p, rng, 0, 3, true, false);
            Assert.IsFalse(Selection.ShouldDrop(p));
            Assert.AreEqual(0, p.benchedFor, "a club always needs its players");
        }
    }

    [Test]
    public void ThreePoorInningsLeaveHimOut()
    {
        CareerProfile p = CareerTestPlay.Career(52, eRung.State, 40f);
        var rng = new Random(53);
        CareerTestPlay.Play(p, rng, 0, 3, true);
        CareerTestPlay.Play(p, rng, 1, 6, true);
        Assert.AreEqual(0, p.benchedFor, "two poor innings are not a run of them");
        CareerTestPlay.Play(p, rng, 2, 5, true);
        Assert.AreEqual(1, p.benchedFor);

        Fixture next = CareerProgress.NextFixture(p);
        Assert.IsNotNull(next, "the fixture is still named");
        Assert.IsTrue(CareerProgress.LeftOutNext(p));
        Assert.IsNull(CareerProgress.PlanNext(p, rng), "no plan for a match he sits out");
        CareerMessage m = CareerTestPlay.Last(p, eMessageKind.Selection);
        Assert.AreEqual("Captain", m.from);
        Assert.AreEqual("You're left out for the match against " + p.season.Team(next.Opponent(p.season.yourTeam)).name, m.title);
        Assert.IsTrue(NameBank.IsLatin1(m.title + m.body));
    }

    [Test]
    public void TheMatchHeSitsOutIsPlayedWithoutHim()
    {
        CareerProfile p = CareerTestPlay.Career(54, eRung.State, 40f);
        var rng = new Random(55);
        for (int i = 0; i < Selection.DropInnings; i++)
            CareerTestPlay.Play(p, rng, 0, 3, true);
        Fixture missed = CareerProgress.NextFixture(p);
        string them = p.season.Team(missed.Opponent(p.season.yourTeam)).name;
        int matches = p.stats.matches, innings = p.innings.Count;

        CareerProgress.PlayLeftOut(p, rng);
        Assert.IsTrue(missed.played);
        Assert.IsNotEmpty(missed.result);
        Assert.IsNotEmpty(missed.homeScore);
        Assert.AreEqual(matches, p.stats.matches, "a match he did not play is not in his stats");
        Assert.AreEqual(innings + 1, p.innings.Count);
        InningsRecord rec = p.innings[p.innings.Count - 1];
        Assert.IsTrue(rec.leftOut);
        Assert.IsFalse(rec.batted);
        Assert.AreEqual(them, rec.opponent);
        Assert.AreEqual(missed.result, rec.result);
        Assert.AreEqual(missed.winner == p.season.yourTeam, rec.won);
        Assert.AreEqual(1, p.season.Yours.players.FindAll(x => x.isYou).Count, "back in the squad");
        foreach (Fixture f in p.season.fixtures)
            if (f.round == missed.round && !f.final)
                Assert.IsTrue(f.played, "the rest of the round was played");
        foreach (Team t in p.season.teams)
            Assert.AreEqual(Selection.DropInnings + 1, t.played, t.name + " has played every round so far");

        Assert.AreEqual(0, p.benchedFor);
        Assert.IsFalse(CareerProgress.LeftOutNext(p));
        Assert.AreEqual("Back in the side", CareerTestPlay.Last(p, eMessageKind.Selection).title);
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        Assert.IsNotNull(plan);
        Assert.AreNotSame(missed, p.season.fixtures[plan.fixture]);

        CareerTestPlay.Play(p, rng, 0, 3, true);
        Assert.AreEqual(0, p.benchedFor, "back in the side, he gets his innings before he is judged again");
        CareerProgress.PlayLeftOut(p, rng);
        Assert.AreEqual(innings + 2, p.innings.Count, "PlayLeftOut does nothing when he is in the side");
    }

    [Test]
    public void GoodFormKeepsHimIn()
    {
        CareerProfile p = CareerTestPlay.Career(56, eRung.ProLeague, 60f);
        var rng = new Random(57);
        for (int i = 0; i < 5; i++)
            CareerTestPlay.Play(p, rng, 30, 22, true);
        Assert.AreEqual(0, p.benchedFor);
        Assert.IsFalse(p.inbox.Exists(m => m.kind == eMessageKind.Selection));
    }

    [Test]
    public void ShouldDropReadsThisSeasonsLastThreeInnings()
    {
        var p = new CareerProfile { name = "Sam Carter", rung = eRung.State, season = new Season { number = 2, rung = eRung.State } };
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        Assert.IsFalse(Selection.ShouldDrop(p), "two innings");
        p.AddInnings(Inn(2, eRung.State, 0, 0, false));        // not out without facing: no innings
        Assert.IsFalse(Selection.ShouldDrop(p));
        p.AddInnings(Inn(2, eRung.State, 1, 4, true));
        Assert.IsTrue(Selection.ShouldDrop(p), "three poor innings");

        p.innings[0].season = 1;                                // last season's duck does not count
        Assert.IsFalse(Selection.ShouldDrop(p));

        p.innings.Clear();
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        p.AddInnings(Inn(2, eRung.State, 40, 25, true));
        Assert.IsFalse(Selection.ShouldDrop(p), "one good innings in three is enough");

        p.innings.Clear();
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        p.AddInnings(new InningsRecord { season = 2, rung = eRung.State, leftOut = true });
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        p.AddInnings(Inn(2, eRung.State, 0, 3, true));
        Assert.IsFalse(Selection.ShouldDrop(p), "only the innings since he was last left out");

        Assert.AreEqual(-1f, Selection.DropForm(eRung.Club));
        Assert.Less(Selection.DropForm(eRung.State), Selection.DropForm(eRung.International));
    }

    [Test]
    public void TheSameSeedLeavesHimOutTheSameWay()
    {
        string Run()
        {
            CareerProfile p = CareerTestPlay.Career(58, eRung.State, 35f);
            var rng = new Random(59);
            CareerTestPlay.Season(p, rng, 1, 5, true);
            return string.Join("|", p.innings.ConvertAll(r => r.leftOut + r.opponent + r.result));
        }
        string a = Run();
        Assert.AreEqual(a, Run());
        StringAssert.Contains("True", a, "he was left out at least once");
    }
}
