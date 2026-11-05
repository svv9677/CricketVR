using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Phase 7 situation text (CareerSituationText and the hub's tile): the copy for each situation; a
/// Normal match says nothing new; every line is Latin-1 with no em dash; and every line fits where it
/// goes, for the longest side names the game can make - the hub's tile (52 characters), the card's
/// heading (31) and body (4 lines of 40), and the result's tag (24). The tile's detail line keeps its
/// output, with rain's overs when rain cut them.
/// </summary>
public class CareerSituationTextTests
{
    private static string Plain(string s) => Regex.Replace(s ?? "", "<[^>]*>", "");

    private static MatchPlan Plan(eSituation situation, bool batFirst, eRung rung = eRung.ProLeague,
                                  string yours = "Harbour CC", string them = "Old Mill CC")
    {
        var p = new MatchPlan
        {
            rung = rung, situation = situation, batFirst = batFirst, overs = Ladder.Overs(rung),
            yourTeamName = yours, opponentName = them, slot = 1,
        };
        if (!batFirst)
            p.theirs = new InningsCard { team = them, runs = 164, wickets = 6, balls = 120, overs = Ladder.Overs(rung) };
        return p;
    }

    private static MatchPlan RainChase(eRung rung = eRung.ProLeague, string yours = "Harbour CC", string them = "Old Mill CC")
    {
        MatchPlan p = Plan(eSituation.RainShortened, false, rung, yours, them);
        int full = Ladder.Overs(rung);
        p.overs = full - (full >= 20 ? 6 : 2);
        p.revisedTarget = CareerSituationMath.RevisedTarget(p.theirs.runs, full, p.overs);
        return p;
    }

    private static MatchPlan Rescue(int runs = 28, int wickets = 3)
    {
        MatchPlan p = Plan(eSituation.Rescue, true);
        p.slot = CareerSituations.RescueIndex;
        p.startRuns = runs; p.startWickets = wickets;
        p.collapse.AddRange(new[] { 0, CareerSim.Wicket, 1, CareerSim.Wicket, 0, CareerSim.Wicket });
        return p;
    }

    // ---- The copy -------------------------------------------------------------------------------

    [Test]
    public void ARainChase_SaysItsRevisedTargetAndOvers()
    {
        MatchPlan p = RainChase();
        Assert.AreEqual(124, p.Target);
        Assert.AreEqual("RAIN  ·  124 to win off 14  ·  You bat at 2", Plain(CareerSituationText.HubLine(p, 2)));
        Assert.AreEqual("Rain: 124 to win off 14", CareerSituationText.IntroHeading(p));
        Assert.AreEqual("Old Mill CC made 164/6 in 20 overs.\nThe target is revised for overs lost.",
                        CareerSituationText.IntroBody(p));
        Assert.AreEqual("rain, target 124", CareerSituationText.ResultTag(p));
    }

    [Test]
    public void RainBattingFirst_SaysTheOversWereCut()
    {
        MatchPlan p = Plan(eSituation.RainShortened, true);
        p.overs = 14;
        Assert.AreEqual("RAIN  ·  Overs cut from 20  ·  You bat at 2", Plain(CareerSituationText.HubLine(p, 2)));
        Assert.AreEqual("Rain: 14 overs a side", CareerSituationText.IntroHeading(p));
        Assert.AreEqual("Rain before the start cut the match from\n20 overs to 14 a side.", CareerSituationText.IntroBody(p));
        Assert.AreEqual("rain, 14 overs a side", CareerSituationText.ResultTag(p));
    }

    [Test]
    public void AChase_SaysWhatItTakes()
    {
        MatchPlan p = Plan(eSituation.Chase, false);
        Assert.AreEqual("CHASE  ·  165 to win  ·  You bat at 2", Plain(CareerSituationText.HubLine(p, 2)));
        Assert.AreEqual("Chase: 165 to win", CareerSituationText.IntroHeading(p));
        Assert.AreEqual("Old Mill CC made 164/6 in 20 overs.", CareerSituationText.IntroBody(p));
        Assert.AreEqual("chase of 165", CareerSituationText.ResultTag(p));
        p.theirs.wickets = 10; p.theirs.balls = 105;
        Assert.AreEqual("Old Mill CC made 164 all out in 17.3\novers.", CareerSituationText.IntroBody(p));
    }

    [Test]
    public void ACollapse_SaysTheScoreHeComesInAt_AndKeepsTheComeInCard()
    {
        MatchPlan p = Rescue();
        Assert.AreEqual("COLLAPSE  ·  You come in at 28/3", Plain(CareerSituationText.HubLine(p, 2)));
        Assert.AreEqual("", CareerSituationText.IntroHeading(p), "the come-in card after the wickets says it");
        Assert.AreEqual("", CareerSituationText.IntroBody(p));
        Assert.AreEqual("came in at 28/3", CareerSituationText.ResultTag(p));
        Assert.AreEqual("You come in at 28/3 (1.5 overs)", CareerMatchLogic.ComeIn(28, 3, 11), "the existing heading");
    }

    [Test]
    public void TheFinal_KeepsItsBadgeOnTheHub_AndGetsACard()
    {
        MatchPlan p = Plan(eSituation.Final, true);
        Assert.AreEqual("", CareerSituationText.HubLine(p, 3));
        Assert.AreEqual("FINAL  ·  You bat at 3", Plain(CareerHubScreen.NextSlotLine(p, 3, true)));
        Assert.AreEqual("The final", CareerSituationText.IntroHeading(p));
        Assert.AreEqual("Harbour CC against Old Mill CC.\nThe winner takes the title.", CareerSituationText.IntroBody(p));
        Assert.AreEqual("the final", CareerSituationText.ResultTag(p));
        MatchPlan chase = Plan(eSituation.Final, false);
        StringAssert.EndsWith("\n165 to win.", CareerSituationText.IntroBody(chase));
    }

    [Test]
    public void TheHubsSituationLine_SaysTheSlotThePlanBatsHimAt()
    {
        MatchPlan p = Plan(eSituation.Chase, false);
        p.slot = 4;   // PlanNext clamped him to #5, whatever the saved slot says
        Assert.AreEqual("CHASE  ·  165 to win  ·  You bat at 5", Plain(CareerHubScreen.NextSlotLine(p, 2, false)));
        Assert.AreEqual(CareerHubScreen.SlotLine(2, false), CareerHubScreen.NextSlotLine(Plan(eSituation.Normal, false), 2, false),
                        "no situation: the saved slot, as before");
    }

    [Test]
    public void ANormalMatch_SaysNothingNew()
    {
        foreach (bool batFirst in new[] { true, false })
        {
            MatchPlan p = Plan(eSituation.Normal, batFirst);
            Assert.AreEqual("", CareerSituationText.HubLine(p, 2));
            Assert.AreEqual("", CareerSituationText.IntroHeading(p));
            Assert.AreEqual("", CareerSituationText.IntroBody(p));
            Assert.AreEqual("", CareerSituationText.ResultTag(p));
            Assert.AreEqual(CareerHubScreen.SlotLine(2, false), CareerHubScreen.NextSlotLine(p, 2, false));
        }
        Assert.AreEqual(CareerHubScreen.SlotLine(4, false), CareerHubScreen.NextSlotLine(null, 4, false), "no preview");
        Assert.AreEqual("", CareerSituationText.HubLine(null, 2));
        Assert.AreEqual("", CareerSituationText.IntroBody(null));
        Assert.AreEqual("", CareerSituationText.ResultTag(null));
    }

    [Test]
    public void ASituationThatDidNotHappen_SaysNothing()
    {
        MatchPlan rain = Plan(eSituation.RainShortened, true);   // no overs lost
        Assert.AreEqual("", CareerSituationText.HubLine(rain, 2));
        Assert.AreEqual("", CareerSituationText.IntroHeading(rain));
        Assert.AreEqual("", CareerSituationText.ResultTag(rain));
        MatchPlan chase = Plan(eSituation.Chase, true);          // batting first after all
        Assert.AreEqual("", CareerSituationText.HubLine(chase, 2));
        Assert.AreEqual("", CareerSituationText.IntroHeading(chase));
        MatchPlan rescue = Rescue(0, 0);                          // no collapse scripted
        Assert.AreEqual("", CareerSituationText.HubLine(rescue, 2));
        Assert.AreEqual("", CareerSituationText.ResultTag(rescue));
    }

    // ---- It fits, and the font has it -----------------------------------------------------------

    /// The longest side name of every pool the game names sides from.
    private static List<string> LongestNames()
    {
        var names = new List<string>();
        foreach (eRung r in new[] { eRung.Club, eRung.State, eRung.ProLeague })
        {
            string longest = "";
            foreach (NameBank.TeamName t in NameBank.Teams(r))
                if (t.name.Length > longest.Length) longest = t.name;
            names.Add(longest);
        }
        string nation = "";
        foreach (Nations.Nation n in Nations.All)
            if (n.name.Length > nation.Length) nation = n.name;
        names.Add(nation);
        return names;
    }

    /// Every situation's plan, at every rung, between the longest names, with big numbers.
    private static List<MatchPlan> EveryPlan()
    {
        var plans = new List<MatchPlan>();
        List<string> names = LongestNames();
        foreach (eRung r in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
            foreach (string a in names)
                foreach (string b in names)
                {
                    MatchPlan rain = RainChase(r, a, b);
                    rain.theirs.runs = 288; rain.theirs.wickets = 10; rain.theirs.balls = Ladder.Overs(r) * 6 - 1;
                    rain.revisedTarget = CareerSituationMath.RevisedTarget(288, Ladder.Overs(r), rain.overs);
                    plans.Add(rain);
                    MatchPlan dry = Plan(eSituation.RainShortened, true, r, a, b);
                    dry.overs = Ladder.Overs(r) - 1;
                    plans.Add(dry);
                    MatchPlan chase = Plan(eSituation.Chase, false, r, a, b);
                    chase.theirs.runs = 288; chase.theirs.wickets = 10; chase.theirs.balls = 119;
                    plans.Add(chase);
                    plans.Add(Plan(eSituation.Final, false, r, a, b));
                    plans.Add(Plan(eSituation.Final, true, r, a, b));
                }
        plans.Add(Rescue(188, 3));
        return plans;
    }

    [Test]
    public void EveryLine_IsLatin1_WithNoEmDash()
    {
        foreach (MatchPlan p in EveryPlan())
            foreach (string s in new[] { CareerSituationText.HubLine(p, 11), CareerSituationText.IntroHeading(p),
                                         CareerSituationText.IntroBody(p), CareerSituationText.ResultTag(p),
                                         CareerResultScreen.HeadlineTag(CareerSituationText.ResultTag(p)) })
            {
                Assert.IsTrue(NameBank.IsLatin1(s), s);
                Assert.IsFalse(s.Contains("\u2014") || s.Contains("\u2013") || s.Contains("\u203A"), s);
            }
    }

    [Test]
    public void EveryLine_FitsWhereItGoes()
    {
        foreach (MatchPlan p in EveryPlan())
        {
            string hub = Plain(CareerSituationText.HubLine(p, 11));
            Assert.LessOrEqual(hub.Length, 52, hub);
            string heading = CareerSituationText.IntroHeading(p);
            Assert.LessOrEqual(heading.Length, 31, heading);
            string body = CareerSituationText.IntroBody(p);
            string[] lines = body.Split('\n');
            Assert.LessOrEqual(lines.Length, 4, body);
            foreach (string l in lines)
                Assert.LessOrEqual(l.Length, CareerSituationText.BodyWidth, body);
            string tag = CareerSituationText.ResultTag(p);
            Assert.LessOrEqual(tag.Length, 24, tag);
        }
    }

    [Test]
    public void TheWrap_BreaksAtSpacesOnly()
    {
        CollectionAssert.AreEqual(new[] { "one two", "three" }, CareerSituationText.Wrap("one two three", 8));
        CollectionAssert.AreEqual(new[] { "a", "longerword", "b" }, CareerSituationText.Wrap("a longerword b", 5));
        CollectionAssert.AreEqual(new string[0], CareerSituationText.Wrap("", 40));
        CollectionAssert.AreEqual(new string[0], CareerSituationText.Wrap(null, 40));
    }

    // ---- The hub's detail line ------------------------------------------------------------------

    [Test]
    public void TheDetailLine_KeepsItsOutput_AndShowsRainsOvers()
    {
        var f = new Fixture { round = 3 };
        Assert.AreEqual("Round 3 of 7 · 6 overs · Club ground · Afternoon", CareerHubScreen.NextDetail(f, 7, eRung.Club));
        Assert.AreEqual(CareerHubScreen.NextDetail(f, 7, eRung.ProLeague), CareerHubScreen.NextDetail(f, 7, eRung.ProLeague, 0));
        Assert.AreEqual("Round 3 of 7 · 14 overs · Stadium · Afternoon", CareerHubScreen.NextDetail(f, 7, eRung.ProLeague, 14));
        var final = new Fixture { round = 8, final = true, night = true };
        Assert.AreEqual("Final · 20 overs · Stadium · Night", CareerHubScreen.NextDetail(final, 7, eRung.ProLeague));
    }
}
