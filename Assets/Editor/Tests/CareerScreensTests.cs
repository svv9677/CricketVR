using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The career match screens' wording, which needs no scene: the hub's chips and next-match line,
/// the season table's fixed columns and the fixtures' outcomes from the player's side, the result
/// headline and innings, and ordinals - and that every string stays in Latin-1, the only characters
/// the TMP default font has. Standalone; nothing here reads or writes a save.
/// Run: Window > General > Test Runner > EditMode > CareerScreensTests.
/// </summary>
public class CareerScreensTests
{
    /// The text a player sees: rich-text tags taken out.
    private static string Plain(string rich) => Regex.Replace(rich ?? "", "<[^>]*>", "");

    private static void AssertLatin1(string s)
    {
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    private static Season League()
    {
        var s = new Season { number = 2, rung = eRung.Club, yourTeam = "hcc" };
        s.teams.Add(new Team { id = "hcc", name = "Harbour CC", shortName = "HCC", played = 2, won = 1, lost = 1, points = 2 });
        s.teams.Add(new Team { id = "omc", name = "Old Mill CC", shortName = "OMC", played = 2, won = 2, points = 4 });
        s.teams.Add(new Team { id = "rvr", name = "Riverside Rovers", shortName = "RVR" });
        s.fixtures.Add(new Fixture { round = 2, home = "omc", away = "hcc", played = true, winner = "omc",
                                     result = "Old Mill CC won by 3 wickets" });
        s.fixtures.Add(new Fixture { round = 1, home = "hcc", away = "rvr", played = true, winner = "hcc",
                                     result = "Harbour CC won by 12 runs" });
        s.fixtures.Add(new Fixture { round = 1, home = "omc", away = "xyz", played = true, winner = "omc" });
        s.fixtures.Add(new Fixture { round = 3, home = "rvr", away = "hcc" });
        s.fixtures.Add(new Fixture { round = 7, home = "hcc", away = "omc" });
        return s;
    }

    // ---- Hub ----------------------------------------------------------------------------------------

    [Test]
    public void TheChipsShowTheNumberAndGradeOrDashes()
    {
        Assert.AreEqual("Overall  54  Solid", Plain(CareerHubScreen.OverallChip(54.4f)));
        Assert.AreEqual("Overall  --", Plain(CareerHubScreen.OverallChip(RatingSet.Unknown)));
        Assert.AreEqual("Form  61", Plain(CareerHubScreen.FormChip(60.6f)));
        Assert.AreEqual("Form  --", Plain(CareerHubScreen.FormChip(RatingSet.Unknown)));
    }

    [Test]
    public void TheNextMatchLineGivesTheRoundTheFormatTheGroundAndTheTime()
    {
        var f = new Fixture { round = 3 };
        Assert.AreEqual("Round 3 of 7 · 6 overs · Club ground · Afternoon", CareerHubScreen.NextDetail(f, 7, eRung.Club));
        var final = new Fixture { round = 8, final = true, night = true };
        Assert.AreEqual("Final · 20 overs · Stadium · Night", CareerHubScreen.NextDetail(final, 7, eRung.ProLeague));
        Assert.AreEqual("Round 3 · 10 overs · Club ground · Afternoon", CareerHubScreen.NextDetail(f, 0, eRung.State));
    }

    [Test]
    public void TheSlotLineSaysWhereHeBatsAndMarksTheFinal()
    {
        Assert.AreEqual("You bat at 7", Plain(CareerHubScreen.SlotLine(7, false)));
        Assert.AreEqual("FINAL  ·  You bat at 3", Plain(CareerHubScreen.SlotLine(3, true)));
        Assert.AreEqual("You bat at 11", Plain(CareerHubScreen.SlotLine(14, false)), "kept to the eleven");
    }

    [Test]
    public void TheRoundsAreTheLeagueRoundsWithoutTheFinal()
    {
        Season s = League();
        s.fixtures.Add(new Fixture { round = 8, final = true });
        Assert.AreEqual(7, CareerHubScreen.Rounds(s));
        Assert.AreEqual(0, CareerHubScreen.Rounds(null));
        Assert.AreEqual("Old Mill CC", CareerHubScreen.TeamName(s, "omc"));
        Assert.AreEqual("xyz", CareerHubScreen.TeamName(s, "xyz"), "a side not in the season shows its id");
    }

    // ---- Season -------------------------------------------------------------------------------------

    [Test]
    public void TableRowsPutEveryFigureAtItsColumn()
    {
        Team t = League().teams[1];
        t.runsFor = 100; t.ballsFaced = 60; t.runsAgainst = 80; t.ballsBowled = 60;   // 10.0 - 8.0
        string row = SeasonScreen.TableRow(2, t, false);
        Assert.AreEqual("2Old Mill CC2204+2.00", Plain(row));
        StringAssert.Contains("<pos=7%>Old Mill CC<pos=57%>2<pos=64%>2<pos=71%>0<pos=78%>4<pos=87%>+2.00", row);
        Assert.AreEqual(Regex.Matches(SeasonScreen.TableHeader(), "<pos=").Count, Regex.Matches(row, "<pos=").Count,
                        "a stop for every heading");
        StringAssert.DoesNotContain("<b>", row);
        StringAssert.Contains("<b>", SeasonScreen.TableRow(1, t, true), "the player's side stands out");
    }

    [Test]
    public void NetRunRateHasItsSignAndTwoPlaces()
    {
        Assert.AreEqual("+0.84", SeasonScreen.Nrr(0.8449f));
        Assert.AreEqual("-1.20", SeasonScreen.Nrr(-1.2f));
        Assert.AreEqual("0.00", SeasonScreen.Nrr(0f));
        Assert.AreEqual("0.00", SeasonScreen.Nrr(-0.001f), "no minus on a rate that rounds to nothing");
    }

    [Test]
    public void LongTeamNamesAreCutToKeepTheColumns()
    {
        Assert.AreEqual("Old Mill CC", SeasonScreen.Clip("Old Mill CC"));
        string cut = SeasonScreen.Clip("Riverside Rovers Cricket Club");
        Assert.LessOrEqual(cut.Length, SeasonScreen.MaxTeamName);
        StringAssert.EndsWith("..", cut);
        Assert.AreEqual("", SeasonScreen.Clip(null));
    }

    [Test]
    public void FixturesAreTheSidesOwnInRoundOrderFromItsPointOfView()
    {
        Season s = League();
        List<Fixture> mine = SeasonScreen.YourFixtures(s);
        Assert.AreEqual(4, mine.Count, "the other sides' match is left out");
        Assert.AreEqual(new[] { 1, 2, 3, 7 }, mine.ConvertAll(f => f.round).ToArray());
        Assert.AreEqual("R1vs Riverside RoversWon by 12 runs", Plain(SeasonScreen.FixtureLine(s, mine[0], false)));
        Assert.AreEqual("R2vs Old Mill CCLost by 3 wickets", Plain(SeasonScreen.FixtureLine(s, mine[1], false)));
        Assert.AreEqual("R3vs Riverside Roversnext", Plain(SeasonScreen.FixtureLine(s, mine[2], true)));
        Assert.AreEqual("R7vs Old Mill CCto play", Plain(SeasonScreen.FixtureLine(s, mine[3], false)));
    }

    [Test]
    public void TheFinalComesLastAndATieIsATie()
    {
        Season s = League();
        s.fixtures.Insert(0, new Fixture { round = 8, final = true, home = "omc", away = "rvr", played = true, winner = "omc",
                                           result = "Old Mill CC won by 5 runs" });
        List<Fixture> mine = SeasonScreen.YourFixtures(s);
        Assert.IsTrue(mine[mine.Count - 1].final);
        Assert.AreEqual("FinalOMC v RVROMC won by 5 runs", Plain(SeasonScreen.FixtureLine(s, mine[mine.Count - 1], false)));
        var tie = new Fixture { round = 4, home = "hcc", away = "omc", played = true, winner = "" };
        Assert.AreEqual("Tied", Plain(SeasonScreen.Outcome(s, tie, false)));
        Assert.AreEqual(" by 12 runs", SeasonScreen.Margin("Harbour CC won by 12 runs"));
        Assert.AreEqual("", SeasonScreen.Margin("Match tied"));
    }

    // ---- Result -------------------------------------------------------------------------------------

    [Test]
    public void TheHeadlineSaysWonLostOrTiedAndByHowMuch()
    {
        Assert.AreEqual("WON by 12 runs", CareerResultScreen.Headline(true, false, 12, true));
        Assert.AreEqual("LOST by 3 wickets", CareerResultScreen.Headline(false, false, 3, false));
        Assert.AreEqual("WON by 1 wicket", CareerResultScreen.Headline(true, false, 1, false));
        Assert.AreEqual("LOST by 1 run", CareerResultScreen.Headline(false, false, 1, true));
        Assert.AreEqual("TIED", CareerResultScreen.Headline(false, true, 0, true));
        Assert.AreEqual("WON", CareerResultScreen.Headline(true, false, 0, true));
        Assert.AreEqual("WON by 4 wickets", CareerResultScreen.Headline(true, false, "Harbour CC won by 4 wickets"));
    }

    [Test]
    public void TheMarginComesFromTheTwoInnings()
    {
        var r = new CareerMatchResult { won = true, yours = new InningsCard { runs = 145, wickets = 6 },
                                        theirs = new InningsCard { runs = 133, wickets = 9 } };
        Assert.AreEqual(12, CareerResultScreen.Margin(r, true, out bool byRuns));
        Assert.IsTrue(byRuns, "batting first, won by runs");
        r = new CareerMatchResult { won = true, yours = new InningsCard { runs = 134, wickets = 3 },
                                    theirs = new InningsCard { runs = 133, wickets = 9 } };
        Assert.AreEqual(7, CareerResultScreen.Margin(r, false, out byRuns));
        Assert.IsFalse(byRuns, "chasing, won by wickets left");
        r.won = false;
        r.yours = new InningsCard { runs = 120, wickets = 10 };
        Assert.AreEqual(13, CareerResultScreen.Margin(r, false, out byRuns));
        Assert.IsTrue(byRuns, "they set it, they won by runs");
        Assert.AreEqual(0, CareerResultScreen.Margin(new CareerMatchResult { tied = true }, true, out _));
        Assert.IsFalse(CareerResultScreen.BattedFirst(new CareerMatchResult { yours = new InningsCard { target = 134 } }, null));
        Assert.IsTrue(CareerResultScreen.BattedFirst(new CareerMatchResult(), new MatchPlan { batFirst = true }));
    }

    [Test]
    public void TheInningsLineGivesTheBoundariesAndHowHeWasOut()
    {
        Assert.AreEqual("4 fours · 2 sixes · c Khan b Lee", CareerResultScreen.InningsDetail(4, 2, true, false, "c Khan b Lee"));
        Assert.AreEqual("1 four · 1 six · not out", CareerResultScreen.InningsDetail(1, 1, false, false, ""));
        Assert.AreEqual("not out", CareerResultScreen.InningsDetail(0, 0, false, false, null));
        Assert.AreEqual("retired", CareerResultScreen.InningsDetail(0, 0, false, true, ""));
        Assert.AreEqual("out", CareerResultScreen.InningsDetail(0, 0, true, false, ""));
    }

    [Test]
    public void BothScoresReadInBattingOrder()
    {
        var r = new CareerMatchResult { yours = new InningsCard { team = "hcc", runs = 145, wickets = 6, balls = 120 },
                                        theirs = new InningsCard { team = "omc", runs = 133, wickets = 9, balls = 118 } };
        var plan = new MatchPlan { yourTeamName = "Harbour CC", opponentName = "Old Mill CC" };
        Assert.AreEqual("Harbour CC 145/6 (20) · Old Mill CC 133/9 (19.4)", Plain(CareerResultScreen.ScoresLine(r, plan, true)));
        Assert.AreEqual("omc 133/9 (19.4) · hcc 145/6 (20)", Plain(CareerResultScreen.ScoresLine(r, null, false)));
    }

    [Test]
    public void TheRatingsLineShowsTheOverallMoving()
    {
        Assert.AreEqual("Overall 54 -> 56  ·  Form 61", Plain(CareerResultScreen.RatingsLine(54f, 56f, 61f)));
        Assert.AreEqual("Overall 56  ·  Form --", Plain(CareerResultScreen.RatingsLine(RatingSet.Unknown, 56f, RatingSet.Unknown)));
        Assert.AreEqual("Overall 56  ·  Form 40", Plain(CareerResultScreen.RatingsLine(55.8f, 56.2f, 40f)), "no arrow when it did not move");
    }

    [Test]
    public void OrdinalsHaveTheRightEndings()
    {
        string[] want = { "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th" };
        for (int i = 0; i < want.Length; i++)
            Assert.AreEqual(want[i], CareerResultScreen.Ordinal(i + 1));
        Assert.AreEqual("11th", CareerResultScreen.Ordinal(11));
        Assert.AreEqual("12th", CareerResultScreen.Ordinal(12));
        Assert.AreEqual("13th", CareerResultScreen.Ordinal(13));
        Assert.AreEqual("21st", CareerResultScreen.Ordinal(21));
    }

    [Test]
    public void TheOverallBeforeIsRememberedForItsSlotOnly()
    {
        var p = new CareerProfile();
        p.ratings.timing = 50f;
        CareerResultScreen.RememberBefore(p);   // no slot active in a test: -1
        Assert.IsFalse(RatingSet.Known(CareerResultScreen.OverallBefore(-1)), "nothing for no slot");
        Assert.IsFalse(RatingSet.Known(CareerResultScreen.OverallBefore(0)), "nothing for another slot");
    }

    // ---- Every string -------------------------------------------------------------------------------

    [Test]
    public void EveryLineIsLatin1()
    {
        Season s = League();
        var lines = new List<string>
        {
            CareerHubScreen.OverallChip(54f), CareerHubScreen.FormChip(61f), CareerHubScreen.SlotLine(7, true),
            CareerHubScreen.NextDetail(new Fixture { round = 3 }, 7, eRung.International),
            SeasonScreen.TableHeader(), SeasonScreen.FixturesHeader(), SeasonScreen.TableRow(1, s.teams[0], true),
            CareerResultScreen.Headline(true, false, 12, true), CareerResultScreen.InningsDetail(4, 2, true, false, "b Lee"),
            CareerResultScreen.RatingsLine(54f, 56f, 61f), CareerResultScreen.PositionLine(s),
            CareerRatingsScreen.Value(54f), CareerRatingsScreen.TrialLine(new CareerProfile()),
        };
        foreach (Fixture f in SeasonScreen.YourFixtures(s))
            lines.Add(SeasonScreen.FixtureLine(s, f, true));
        foreach (string line in lines)
            AssertLatin1(line);
    }

    [Test]
    public void TheTrialLineSaysWhatTheTrialMeasured()
    {
        var p = new CareerProfile { trialDone = true };
        p.trialRatings.timing = 30f;
        p.trialRatings.power = 34f;
        p.trialRatings.sample = 30;
        Assert.AreEqual("Trial ratings: Overall 32 from 30 balls", CareerRatingsScreen.TrialLine(p));
        Assert.AreEqual("Trial ratings: too few balls to say.", CareerRatingsScreen.TrialLine(new CareerProfile { trialDone = true }));
    }
}
