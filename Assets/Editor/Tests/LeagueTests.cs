using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The career's world (LeagueBuilder, NameBank): a season has Ladder.TeamsInLeague sides with the
/// career player in his side exactly once and every squad eleven strong; a side's ratings, order
/// and attack look like a cricket side; the round robin has every pair once and each side once a
/// round; results go into the table (net run rate counting all out as the full overs) and the
/// table sorts by points, then net run rate, then name; the final is added once, between the top
/// two, only when the league is done; the same seed makes the same season; and every name is
/// Latin-1, never a famous cricketer's and never a real side's. Standalone: seeded, no scene.
/// </summary>
public class LeagueTests
{
    private static SquadPlayer You() => new SquadPlayer { name = "Test Batter", bat = 50f };

    private static Season NewSeason(eRung rung = eRung.ProLeague, string nation = "AUS", int seed = 3, Team keep = null) =>
        LeagueBuilder.NewSeason(rung, 1, nation, keep, You(), new Random(seed));

    // ---- Seasons and squads ---------------------------------------------------------------------

    [Test]
    public void ASeasonHasTheLeaguesSidesWithYouInYoursOnce()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
        {
            Season s = NewSeason(rung, "IND", 5);
            Assert.AreEqual(Ladder.TeamsInLeague, s.teams.Count, rung.ToString());
            Assert.AreEqual(rung, s.rung);
            Assert.IsNotNull(s.Yours);
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            int yous = 0;
            foreach (Team t in s.teams)
            {
                Assert.IsTrue(ids.Add(t.id), "unique id " + t.id);
                Assert.IsTrue(names.Add(t.name), "unique name " + t.name);
                Assert.AreEqual(LeagueBuilder.SquadSize, t.players.Count, t.name);
                foreach (SquadPlayer p in t.players)
                    if (p.isYou) { yous++; Assert.AreSame(s.Yours, t); Assert.AreEqual("Test Batter", p.name); }
            }
            Assert.AreEqual(1, yous, "you are in one side, once");
            Assert.AreEqual(LeagueBuilder.NewcomerIndex, s.Yours.players.FindIndex(p => p.isYou), "a newcomer bats at 7");
        }
    }

    [Test]
    public void AtInternationalYourSideIsYourNationAndTheOthersAreOtherNations()
    {
        Season s = NewSeason(eRung.International, "NZ", 8);
        Assert.AreEqual("New Zealand", s.Yours.name);
        Assert.AreEqual("NZL", s.Yours.shortName);
        foreach (Team t in s.teams)
        {
            bool isNation = false;
            foreach (Nations.Nation n in Nations.All) if (n.name == t.name) isNation = true;
            Assert.IsTrue(isNation, t.name);
        }
        // A Pro League side does not carry into the international game.
        Team franchise = NewSeason(eRung.ProLeague, "NZ", 8).Yours;
        Assert.AreEqual("New Zealand", NewSeason(eRung.International, "NZ", 9, franchise).Yours.name);
    }

    [Test]
    public void KeepingYourSideKeepsItsNameAndSquadAndResetsItsTable()
    {
        Season first = NewSeason(eRung.State, "ENG", 4);
        Team mine = first.Yours;
        mine.played = 7; mine.won = 5; mine.points = 10; mine.runsFor = 600; mine.ballsFaced = 400;
        var you = new SquadPlayer { name = "Test Batter", bat = 61f };
        Season next = LeagueBuilder.NewSeason(eRung.State, 2, "ENG", mine, you, new Random(77));
        Team kept = next.Yours;
        Assert.AreNotSame(mine, kept, "a copy, not the old season's side");
        Assert.AreEqual(mine.name, kept.name);
        Assert.AreEqual(mine.shortName, kept.shortName);
        Assert.AreEqual(mine.kit, kept.kit);
        Assert.AreEqual(mine.id, kept.id);
        Assert.AreEqual(0, kept.played + kept.won + kept.points + kept.runsFor + kept.ballsFaced);
        for (int i = 0; i < mine.players.Count; i++)
            Assert.AreEqual(mine.players[i].name, kept.players[i].name, "same squad, same order");
        Assert.AreSame(you, kept.You, "the new you replaces the old in his place");
        Assert.AreEqual(61f, kept.You.bat);
        foreach (Team t in next.teams)
            if (t != kept) Assert.AreNotEqual(kept.name, t.name);
    }

    [Test]
    public void EverySideHasItsOwnKit()
    {
        var kits = new HashSet<int>();
        foreach (Team t in NewSeason(eRung.Club, "IND", 12).teams)
        {
            Assert.That(t.kit, Is.InRange(0, Appearance.KitColours.Length - 1));
            kits.Add(t.kit);
        }
        Assert.AreEqual(Ladder.TeamsInLeague, kits.Count);
    }

    [Test]
    public void ASideBatsStrongestFirstAndBowlsWithAMixedAttack()
    {
        var rng = new Random(2);
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
        {
            double sum = 0; int n = 0;
            for (int i = 0; i < 200; i++)
            {
                Team t = LeagueBuilder.NewTeam(rung, "Harbour CC", "IND", rng);
                Assert.AreEqual(11, t.players.Count);
                int bowlers = 0, pace = 0, spin = 0;
                for (int k = 0; k < t.players.Count; k++)
                {
                    SquadPlayer p = t.players[k];
                    Assert.That(p.bat, Is.InRange(5f, 95f));
                    if (k > 0) Assert.LessOrEqual(p.bat, t.players[k - 1].bat, "strongest first");
                    if (p.bowls) { bowlers++; Assert.GreaterOrEqual(k, 5, "bowlers bat 6 to 11"); }
                    if (p.bowls && p.kind <= eBowlerKind.Medium) pace++;
                    if (p.bowls && p.kind >= eBowlerKind.OffSpin) spin++;
                    sum += p.bat; n++;
                }
                Assert.That(bowlers, Is.InRange(5, 6));
                Assert.GreaterOrEqual(pace, 2);
                Assert.GreaterOrEqual(spin, 1);
                Assert.That(t.kit, Is.InRange(0, 7));
                Assert.AreEqual("HBR", t.shortName);
            }
            Assert.AreEqual(Ladder.SquadMean(rung), sum / n, 2.0, "ratings around the rung's mean");
        }
    }

    // ---- Fixtures -------------------------------------------------------------------------------

    [Test]
    public void TheRoundRobinHasEveryPairOnceAndEachSideOnceARound()
    {
        Season s = NewSeason();
        List<Fixture> fx = s.fixtures;
        int n = s.teams.Count;
        Assert.AreEqual(n * (n - 1) / 2, fx.Count);
        var pairs = new HashSet<string>();
        var home = new Dictionary<string, int>();
        foreach (Fixture f in fx)
        {
            Assert.AreNotEqual(f.home, f.away);
            Assert.That(f.round, Is.InRange(1, n - 1));
            string key = string.CompareOrdinal(f.home, f.away) < 0 ? f.home + "|" + f.away : f.away + "|" + f.home;
            Assert.IsTrue(pairs.Add(key), "every pair once: " + key);
            home[f.home] = home.TryGetValue(f.home, out int h) ? h + 1 : 1;
        }
        for (int r = 1; r <= n - 1; r++)
        {
            var seen = new HashSet<string>();
            foreach (Fixture f in fx.FindAll(x => x.round == r))
            {
                Assert.IsTrue(seen.Add(f.home), "once a round");
                Assert.IsTrue(seen.Add(f.away), "once a round");
            }
            Assert.AreEqual(n, seen.Count, "every side plays each round");
        }
        foreach (Team t in s.teams)
            Assert.That(home.TryGetValue(t.id, out int c) ? c : 0, Is.InRange(3, 4), "home and away shared: " + t.name);
    }

    [Test]
    public void AnOddLeagueGetsByesAndStillMeetsEveryone()
    {
        var teams = new List<Team>();
        for (int i = 0; i < 7; i++) teams.Add(new Team { id = "x" + i, name = "Side " + i });
        List<Fixture> fx = LeagueBuilder.RoundRobin(teams, new Random(1));
        Assert.AreEqual(21, fx.Count);
        for (int r = 1; r <= 7; r++)
        {
            var seen = new HashSet<string>();
            foreach (Fixture f in fx.FindAll(x => x.round == r)) { Assert.IsTrue(seen.Add(f.home)); Assert.IsTrue(seen.Add(f.away)); }
        }
    }

    [Test]
    public void FixturesAreUnderLightsByTheRungAndMostlyOnFlatPitches()
    {
        int night = 0, all = 0, flat = 0;
        for (int seed = 0; seed < 20; seed++)
            foreach (Fixture f in NewSeason(eRung.ProLeague, "IND", seed).fixtures)
            {
                all++;
                if (f.night) night++;
                if (f.pitch == ePitch.Flat) flat++;
            }
        Assert.AreEqual(Ladder.NightChance(eRung.ProLeague), night / (float)all, 0.08f);
        Assert.That(flat / (float)all, Is.InRange(0.6f, 0.8f));
        foreach (Fixture f in NewSeason(eRung.Club, "IND", 1).fixtures) Assert.IsFalse(f.night, "no lights at the club");
    }

    // ---- Results, table, final ------------------------------------------------------------------

    private static InningsCard Card(int runs, int wickets, int balls, int overs = 20, int target = 0) =>
        new InningsCard { runs = runs, wickets = wickets, balls = balls, overs = overs, target = target };

    [Test]
    public void ARecordedResultGoesIntoTheFixtureAndTheTable()
    {
        Season s = NewSeason();
        Fixture f = s.fixtures[0];
        // Home made 150/6 in 20; away were all out for 120 in 15.
        LeagueBuilder.Record(s, f, Card(150, 6, 120), Card(120, 10, 90, 20, 151));
        Team h = s.Team(f.home), a = s.Team(f.away);
        Assert.IsTrue(f.played);
        Assert.AreEqual(f.home, f.winner);
        Assert.AreEqual(h.name + " won by 30 runs", f.result);
        Assert.AreEqual("150/6 (20)", f.homeScore);
        Assert.AreEqual("120 all out (15)", f.awayScore);
        Assert.AreEqual(1, h.played); Assert.AreEqual(1, h.won); Assert.AreEqual(2, h.points);
        Assert.AreEqual(1, a.played); Assert.AreEqual(1, a.lost); Assert.AreEqual(0, a.points);
        Assert.AreEqual(120, a.ballsFaced, "all out counts as the full 20 overs");
        Assert.AreEqual(120, h.ballsBowled);
        Assert.AreEqual(150 * 6f / 120 - 120 * 6f / 120, h.NetRunRate, 1e-4);

        Fixture g = s.fixtures[1];
        LeagueBuilder.Record(s, g, Card(99, 7, 120), Card(100, 4, 87, 20, 100));
        Assert.AreEqual(s.Team(g.away).name + " won by 6 wickets (33 balls left)", g.result);
        Assert.AreEqual("100/4 (14.3)", g.awayScore);
        Assert.AreEqual(87, s.Team(g.away).ballsFaced, "a chase won counts the balls it took");

        Fixture t = s.fixtures[2];
        LeagueBuilder.Record(s, t, Card(130, 8, 120), Card(130, 9, 120, 20, 131));
        Assert.AreEqual("Match tied", t.result);
        Assert.AreEqual("", t.winner);
        Assert.AreEqual(1, s.Team(t.home).points);
        Assert.AreEqual(1, s.Team(t.away).tied);
    }

    [Test]
    public void TheTableSortsByPointsThenNetRunRateThenName()
    {
        var s = new Season();
        s.teams.Add(new Team { id = "a", name = "Bravo", points = 4, runsFor = 100, ballsFaced = 120 });
        s.teams.Add(new Team { id = "b", name = "Alpha", points = 4, runsFor = 100, ballsFaced = 120 });
        s.teams.Add(new Team { id = "c", name = "Charlie", points = 4, runsFor = 200, ballsFaced = 120 });
        s.teams.Add(new Team { id = "d", name = "Delta", points = 6 });
        s.teams.Add(new Team { id = "e", name = "Echo", points = 0, runsFor = 500, ballsFaced = 60 });
        List<Team> t = LeagueBuilder.Table(s);
        CollectionAssert.AreEqual(new[] { "Delta", "Charlie", "Alpha", "Bravo", "Echo" }, t.ConvertAll(x => x.name));
        Assert.AreEqual("Bravo", s.teams[0].name, "the season's own list is not reordered");
    }

    [Test]
    public void TheFinalIsAddedOnceBetweenTheTopTwoWhenTheLeagueIsDone()
    {
        Season s = NewSeason(eRung.ProLeague, "IND", 6);
        var rng = new Random(10);
        for (int i = 0; i < s.fixtures.Count - 1; i++) LeagueBuilder.Simulate(s, s.fixtures[i], rng);
        Assert.IsFalse(LeagueBuilder.AddFinal(s), "a fixture is still to play");
        LeagueBuilder.Simulate(s, s.fixtures[s.fixtures.Count - 1], rng);
        int points = 0;
        foreach (Team t in s.teams) { Assert.AreEqual(Ladder.TeamsInLeague - 1, t.played); points += t.points; }
        Assert.AreEqual(2 * s.fixtures.Count, points, "2 points a match, shared when tied");

        List<Team> table = LeagueBuilder.Table(s);
        Assert.IsTrue(LeagueBuilder.AddFinal(s));
        Fixture fin = s.fixtures[s.fixtures.Count - 1];
        Assert.IsTrue(fin.final);
        Assert.AreEqual(table[0].id, fin.home);
        Assert.AreEqual(table[1].id, fin.away);
        Assert.AreEqual(Ladder.TeamsInLeague, fin.round, "the round after the last");
        Assert.IsFalse(LeagueBuilder.AddFinal(s), "only once");

        LeagueBuilder.Simulate(s, fin, rng);
        Assert.IsTrue(fin.played);
        Assert.That(fin.winner, Is.EqualTo(fin.home).Or.EqualTo(fin.away), "a final always has a winner");
        Assert.AreEqual(Ladder.TeamsInLeague - 1, s.Team(fin.home).played, "the final is not in the table");
    }

    [Test]
    public void ATiedFinalGoesToTheSideThatLostFewerWickets()
    {
        Season s = NewSeason();
        var fin = new Fixture { home = s.teams[0].id, away = s.teams[1].id, final = true };
        LeagueBuilder.Record(s, fin, Card(140, 9, 120), Card(140, 6, 120, 20, 141));
        Assert.AreEqual(fin.away, fin.winner);
        StringAssert.StartsWith("Match tied", fin.result);
    }

    [Test]
    public void ASimulatedFixtureHasAResult()
    {
        Season s = NewSeason(eRung.Club, "WI", 2);
        var rng = new Random(4);
        foreach (Fixture f in s.fixtures)
        {
            LeagueBuilder.Simulate(s, f, rng);
            Assert.IsTrue(f.played);
            Assert.IsNotEmpty(f.homeScore);
            Assert.IsNotEmpty(f.awayScore);
            Assert.IsNotEmpty(f.result);
            Assert.IsTrue(f.winner == "" || f.winner == f.home || f.winner == f.away);
            Assert.IsTrue(NameBank.IsLatin1(f.result));
        }
    }

    [Test]
    public void TheSameSeedMakesTheSameSeason()
    {
        Season x = NewSeason(eRung.State, "SL", 42), y = NewSeason(eRung.State, "SL", 42);
        for (int i = 0; i < x.teams.Count; i++)
        {
            Assert.AreEqual(x.teams[i].name, y.teams[i].name);
            Assert.AreEqual(x.teams[i].kit, y.teams[i].kit);
            for (int k = 0; k < x.teams[i].players.Count; k++)
            {
                Assert.AreEqual(x.teams[i].players[k].name, y.teams[i].players[k].name);
                Assert.AreEqual(x.teams[i].players[k].bat, y.teams[i].players[k].bat);
            }
        }
        for (int i = 0; i < x.fixtures.Count; i++)
        {
            Assert.AreEqual(x.fixtures[i].home, y.fixtures[i].home);
            Assert.AreEqual(x.fixtures[i].night, y.fixtures[i].night);
        }
        LeagueBuilder.Simulate(x, x.fixtures[0], new Random(5));
        LeagueBuilder.Simulate(y, y.fixtures[0], new Random(5));
        Assert.AreEqual(x.fixtures[0].result, y.fixtures[0].result);
        Assert.AreEqual(x.fixtures[0].homeScore, y.fixtures[0].homeScore);
    }

    // ---- Names ----------------------------------------------------------------------------------

    [Test]
    public void PlayerNamesAreLatin1AndNeverAFamousCricketer()
    {
        var rng = new Random(1);
        foreach (Nations.Nation n in Nations.All)
            for (int i = 0; i < 3000; i++)
            {
                string name = NameBank.Player(n.code, rng);
                Assert.IsTrue(NameBank.IsLatin1(name), name);
                Assert.IsFalse(NameBank.IsFamous(name), name);
                Assert.IsTrue(name.Contains(" "), "first name and surname: " + name);
            }
        Assert.IsTrue(NameBank.IsFamous("rohit sharma"), "the check is case-insensitive");
        Assert.IsTrue(NameBank.IsFamous(" Ryan Harris "));
        Assert.GreaterOrEqual(NameLists.Famous.Length, 60);
    }

    [Test]
    public void TheFamousPairsTheListsCanMakeAreNeverMade()
    {
        // Every famous name the region lists can put together is caught (a mutation check: the
        // generator must see these pairs and step around them).
        int reachable = 0;
        for (int r = 0; r < NameLists.First.Length; r++)
            foreach (string f in NameLists.First[r])
                foreach (string l in NameLists.Last[r])
                    if (NameBank.IsFamous(f + " " + l)) reachable++;
        Assert.Greater(reachable, 10, "the famous list covers pairs the lists really make");
    }

    [Test]
    public void ASquadNeverRepeatsANameOrASurname()
    {
        Season s = NewSeason(eRung.ProLeague, "SL", 13);
        var all = new HashSet<string>();
        foreach (Team t in s.teams)
        {
            var surnames = new HashSet<string>();
            foreach (SquadPlayer p in t.players)
            {
                Assert.IsTrue(all.Add(p.name), "unique in the league: " + p.name);
                if (!p.isYou) Assert.IsTrue(surnames.Add(NameBank.Surname(p.name)), "unique surname in " + t.name + ": " + p.name);
                Assert.IsTrue(NameBank.IsLatin1(p.name));
            }
        }
    }

    [Test]
    public void SideNamesAreFictionalLatin1WithShortCodes()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague })
        {
            NameBank.TeamName[] pool = NameBank.Teams(rung);
            Assert.GreaterOrEqual(pool.Length, Ladder.TeamsInLeague + 1, "room for your side and seven others");
            var shorts = new HashSet<string>();
            foreach (NameBank.TeamName n in pool)
            {
                Assert.IsFalse(NameBank.ImitatesReal(n.name), n.name);
                Assert.IsTrue(NameBank.IsLatin1(n.name), n.name);
                Assert.IsTrue(shorts.Add(n.shortName), "unique code " + n.shortName);
                CheckShort(n.shortName);
            }
        }
        foreach (Nations.Nation n in Nations.All) CheckShort(NameBank.NationTeam(n.code).shortName);
        // The check itself bites: real names and real nicknames are caught.
        foreach (string real in NameLists.RealTeams) Assert.IsTrue(NameBank.ImitatesReal(real), real);
        Assert.IsTrue(NameBank.ImitatesReal("Port Calder Super Kings"));
        Assert.IsTrue(NameBank.ImitatesReal("Sydney Lamplighters"));
        Assert.IsFalse(NameBank.ImitatesReal("Kingsbridge CC"), "a nickname is matched as a word");
    }

    private static void CheckShort(string code)
    {
        Assert.That(code.Length, Is.InRange(3, 4), code);
        foreach (char c in code) Assert.IsTrue(c >= 'A' && c <= 'Z', code);
    }

    [Test]
    public void ASurnameIsEverythingAfterTheFirstName()
    {
        Assert.AreEqual("de Silva", NameBank.Surname("Kasun de Silva"));
        Assert.AreEqual("Sharma", NameBank.Surname("Arjun Sharma"));
        Assert.AreEqual("Madonna", NameBank.Surname("Madonna"));
        Assert.AreEqual("", NameBank.Surname(null));
    }
}
