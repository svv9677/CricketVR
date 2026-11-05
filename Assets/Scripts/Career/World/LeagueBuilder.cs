using System;
using System.Collections.Generic;

/// <summary>
/// Makes the world the career is played in: fictional sides with fictional squads at each rung
/// (names from NameBank - never real teams, leagues or players), a season's fixtures (each side
/// plays the others once, then the top two meet in a final), the league table, and the results of
/// the fixtures the career player is not in. Pure and seeded: the same seed makes the same season.
/// </summary>
public static class LeagueBuilder
{
    /// Players in a squad. The last five or six in the order are the bowlers (positions 6..11).
    public const int SquadSize = 11;
    /// Where a newcomer bats in his side's order (index 6 = number 7, CareerProfile.battingSlot's default).
    public const int NewcomerIndex = 6;

    // ---- Seasons and sides ----------------------------------------------------------------------

    /// A new season at `rung`: Ladder.TeamsInLeague sides. The career player's side keeps its name
    /// and squad when `keep` is given (the same side another season), else a new side is made;
    /// `you` goes into its squad (isYou). At International, "your side" is your nation's.
    public static Season NewSeason(eRung rung, int number, string nation, Team keep, SquadPlayer you, Random rng)
    {
        var s = new Season { rung = rung, number = number };
        var used = new HashSet<string>();
        if (you != null && !string.IsNullOrEmpty(you.name)) used.Add(you.name);

        Team yours = YourSide(rung, nation, keep, used, rng, out bool kept);
        PutYouIn(yours, you);
        RenameClashes(yours, you, nation, used, rng);
        s.teams.Add(yours);

        foreach (NameBank.TeamName n in OtherNames(rung, nation, yours.name, rng))
        {
            Team t = NewTeam(rung, n, NationFor(rung, nation, n), used, rng);
            s.teams.Add(t);
        }
        GiveIds(s.teams);
        s.yourTeam = yours.id;
        GiveKits(s.teams, kept, rng);
        s.fixtures = RoundRobin(s.teams, rung, rng);
        Scouting.MarkScouts(s, rng);      // the games a scout (at International, the selectors) watches
        return s;
    }

    private static Team YourSide(eRung rung, string nation, Team keep, HashSet<string> used, Random rng, out bool kept)
    {
        // At International your side is your nation's: a kept domestic side does not carry over.
        kept = keep != null && (rung != eRung.International || keep.name == Nations.Name(nation));
        if (kept)
        {
            Team t = Clone(keep);
            foreach (SquadPlayer p in t.players) used.Add(p.name);
            Pad(t, rung, nation, used, rng);
            return t;
        }
        NameBank.TeamName n = rung == eRung.International
            ? NameBank.NationTeam(nation)
            : NameBank.Teams(rung)[rng.Next(NameBank.Teams(rung).Length)];
        return NewTeam(rung, n, nation, used, rng);
    }

    /// The other sides' names: at International the other nations, else the rung's pool less
    /// your side's name, in a seeded random order.
    private static List<NameBank.TeamName> OtherNames(eRung rung, string nation, string yourName, Random rng)
    {
        var pool = new List<NameBank.TeamName>();
        if (rung == eRung.International)
        {
            string code = Nations.All[Nations.IndexOf(nation)].code;
            foreach (Nations.Nation n in Nations.All)
                if (n.code != code && n.name != yourName) pool.Add(NameBank.NationTeam(n.code));
        }
        else
            foreach (NameBank.TeamName n in NameBank.Teams(rung))
                if (n.name != yourName) pool.Add(n);
        Shuffle(pool, rng);
        int others = Ladder.TeamsInLeague - 1;
        if (pool.Count > others) pool.RemoveRange(others, pool.Count - others);
        return pool;
    }

    /// The nation a side's players come from: its own at International, else the career player's
    /// (a domestic league).
    private static string NationFor(eRung rung, string nation, NameBank.TeamName n)
    {
        if (rung != eRung.International) return nation;
        foreach (Nations.Nation x in Nations.All)
            if (x.name == n.name) return x.code;
        return nation;
    }

    /// A side at `rung` with a fictional name and eleven players (batting ratings around
    /// Ladder.SquadMean, the order strongest-first for the top six, bowlers at the tail).
    public static Team NewTeam(eRung rung, string name, string nation, Random rng)
    {
        var n = new NameBank.TeamName(name ?? "", ShortFrom(name));
        foreach (eRung r in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
            foreach (NameBank.TeamName t in AllNames(r))
                if (t.name == name) n = t;
        return NewTeam(rung, n, nation, new HashSet<string>(), rng);
    }

    /// NewTeam with the scoreboard code and the names already used in the league (never repeated).
    public static Team NewTeam(eRung rung, NameBank.TeamName n, string nation, HashSet<string> used, Random rng)
    {
        var t = new Team { name = n.name, shortName = n.shortName, kit = rng.Next(Appearance.KitColours.Length) };
        var bats = new List<float>();
        for (int i = 0; i < SquadSize; i++)
            bats.Add(Clamp(Gaussian(rng, Ladder.SquadMean(rung), Ladder.SquadSpread), 5f, 95f));
        bats.Sort((a, b) => b.CompareTo(a));

        int bowlers = rng.NextDouble() < 0.5 ? 5 : 6;
        List<eBowlerKind> kinds = BowlerKinds(bowlers, rng);
        int overseas = rung == eRung.ProLeague ? 2 + rng.Next(2) : 0;
        var surnames = new HashSet<string>();
        for (int i = 0; i < SquadSize; i++)
        {
            bool bowls = i >= SquadSize - bowlers;
            string from = overseas > 0 && rng.Next(SquadSize - i) < overseas ? Overseas(nation, rng) : nation;
            if (from != nation) overseas--;
            t.players.Add(new SquadPlayer
            {
                name = SquadName(from, used, surnames, rng),
                bat = (float)Math.Round(bats[i], 1),
                bowls = bowls,
                kind = bowls ? kinds[i - (SquadSize - bowlers)] : eBowlerKind.Medium,
            });
        }
        return t;
    }

    /// A name new to the league whose surname is new to the side (so a scorecard's "c Perera"
    /// is never ambiguous).
    private static string SquadName(string nation, HashSet<string> used, HashSet<string> surnames, Random rng)
    {
        string name = "";
        for (int tries = 0; tries < 40; tries++)
        {
            name = NameBank.Player(nation, rng, used);
            if (!surnames.Contains(NameBank.Surname(name))) break;
            if (tries < 39) used.Remove(name);
        }
        surnames.Add(NameBank.Surname(name));
        return name;
    }

    /// A Pro League side's overseas signing comes from any other cricket nation.
    private static string Overseas(string nation, Random rng)
    {
        for (int tries = 0; tries < 10; tries++)
        {
            string c = Nations.All[rng.Next(Nations.All.Length)].code;
            if (c != nation) return c;
        }
        return nation;
    }

    /// The attack: two quicks, a medium pacer, an off-spinner and a leg-spinner, and a sixth of any
    /// kind but mystery; one side in twelve has a mystery spinner in place of a spinner. Shuffled,
    /// so the kinds fall anywhere in the tail.
    private static List<eBowlerKind> BowlerKinds(int count, Random rng)
    {
        var k = new List<eBowlerKind> { eBowlerKind.Fast, eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin };
        if (count > 5)
        {
            eBowlerKind[] sixth = { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin };
            k.Add(sixth[rng.Next(sixth.Length)]);
        }
        if (rng.NextDouble() < 1.0 / 12) k[rng.NextDouble() < 0.5 ? 3 : 4] = eBowlerKind.Mystery;
        while (k.Count > count) k.RemoveAt(k.Count - 1);
        Shuffle(k, rng);
        return k;
    }

    /// The career player in his side: in place of the old "you" if the side had one, else in place
    /// of the weakest specialist batter, batting at number 7 (NewcomerIndex). Eleven either way.
    private static void PutYouIn(Team t, SquadPlayer you)
    {
        if (you == null) return;
        you.isYou = true;
        int old = t.players.FindIndex(p => p.isYou);
        if (old >= 0)
        {
            t.players[old] = you;
            for (int i = 0; i < t.players.Count; i++)
                if (i != old) t.players[i].isYou = false;
            return;
        }
        int weakest = -1;
        for (int i = 0; i < t.players.Count; i++)
            if (!t.players[i].bowls && (weakest < 0 || t.players[i].bat < t.players[weakest].bat)) weakest = i;
        if (weakest < 0) weakest = t.players.Count - 1;
        if (weakest >= 0) t.players.RemoveAt(weakest);
        t.players.Insert(Math.Min(NewcomerIndex, t.players.Count), you);
        while (t.players.Count > SquadSize) t.players.RemoveAt(t.players.Count - 1);
    }

    /// No teammate shares the career player's surname, so a card line like "c Sharma b X" always
    /// means one man. A clashing teammate gets a new name (new to the league and to the side).
    private static void RenameClashes(Team t, SquadPlayer you, string nation, HashSet<string> used, Random rng)
    {
        string mine = NameBank.Surname(you?.name);
        if (mine.Length == 0) return;
        var surnames = new HashSet<string>();
        foreach (SquadPlayer p in t.players) surnames.Add(NameBank.Surname(p.name));
        foreach (SquadPlayer p in t.players)
            if (p != you && NameBank.Surname(p.name) == mine) p.name = SquadName(nation, used, surnames, rng);
    }

    /// A kept squad short of eleven (only a damaged save makes one) is made up with new tail-end
    /// bowlers rated for the rung, so every side still fields eleven.
    private static void Pad(Team t, eRung rung, string nation, HashSet<string> used, Random rng)
    {
        if (t.players.Count >= SquadSize) return;
        var surnames = new HashSet<string>();
        foreach (SquadPlayer p in t.players) surnames.Add(NameBank.Surname(p.name));
        while (t.players.Count < SquadSize)
            t.players.Add(new SquadPlayer
            {
                name = SquadName(nation, used, surnames, rng),
                bat = (float)Math.Round(Clamp(Gaussian(rng, Ladder.SquadMean(rung), Ladder.SquadSpread), 5f, 95f), 1),
                bowls = true,
                kind = eBowlerKind.Medium,
            });
    }

    /// A copy of a side for a new season: the same name, code, kit and players; the table reset.
    private static Team Clone(Team k)
    {
        var t = new Team { id = k.id, name = k.name, shortName = k.shortName, kit = k.kit };
        foreach (SquadPlayer p in k.players)
            t.players.Add(new SquadPlayer { name = p.name, bat = p.bat, bowls = p.bowls, kind = p.kind, isYou = p.isYou });
        return t;
    }

    /// Ids: your side keeps its own if it has one; every side's id is unique ("t0".."t7").
    private static void GiveIds(List<Team> teams)
    {
        var taken = new HashSet<string>();
        if (!string.IsNullOrEmpty(teams[0].id)) taken.Add(teams[0].id);
        int n = 0;
        for (int i = 0; i < teams.Count; i++)
        {
            if (i == 0 && !string.IsNullOrEmpty(teams[0].id)) continue;
            string id;
            do id = "t" + n++; while (taken.Contains(id));
            teams[i].id = id;
            taken.Add(id);
        }
    }

    /// Every side in its own kit colour where there are enough colours; a kept side keeps its own.
    private static void GiveKits(List<Team> teams, bool keepFirst, Random rng)
    {
        var free = new List<int>();
        for (int i = 0; i < Appearance.KitColours.Length; i++) free.Add(i);
        Shuffle(free, rng);
        if (keepFirst) free.Remove(teams[0].kit);
        for (int i = keepFirst ? 1 : 0; i < teams.Count; i++)
        {
            if (free.Count == 0) { teams[i].kit = rng.Next(Appearance.KitColours.Length); continue; }
            teams[i].kit = free[0];
            free.RemoveAt(0);
        }
    }

    // ---- Fixtures -------------------------------------------------------------------------------

    /// Round robin: each side plays every other once, spread over rounds, each side once a round.
    /// Without a rung the fixtures are day games (as at Club).
    public static List<Fixture> RoundRobin(List<Team> teams, Random rng) => RoundRobin(teams, eRung.Club, rng);

    /// The circle method: one side fixed, the rest turn one place a round; n-1 rounds for n sides
    /// (a bye each round when n is odd). Home and away alternate round to round as far as a round
    /// robin allows (a side has at most a couple of back-to-back home or away games). Each fixture is
    /// under lights at Ladder.NightChance(rung), on a pitch that is mostly flat, now and then green
    /// or dusty.
    public static List<Fixture> RoundRobin(List<Team> teams, eRung rung, Random rng)
    {
        var fixtures = new List<Fixture>();
        if (teams == null || teams.Count < 2) return fixtures;
        var ids = new List<string>();
        foreach (Team t in teams) ids.Add(t.id);
        Shuffle(ids, rng);
        if (ids.Count % 2 == 1) ids.Add(null);
        int m = ids.Count - 1;
        for (int round = 0; round < m; round++)
        {
            // The fixed side (the last) meets side `round`; the others pair off around it.
            AddFixture(fixtures, round, round % 2 == 1 ? ids[m] : ids[round], round % 2 == 1 ? ids[round] : ids[m], rung, rng);
            for (int k = 1; k <= m / 2; k++)
            {
                string a = ids[(round + k) % m], b = ids[(round - k + m) % m];
                // Alternating the venue by k keeps runs of home or away games short (de Werra).
                if (k % 2 == 1) AddFixture(fixtures, round, a, b, rung, rng);
                else AddFixture(fixtures, round, b, a, rung, rng);
            }
        }
        return fixtures;
    }

    private static void AddFixture(List<Fixture> fixtures, int round, string home, string away, eRung rung, Random rng)
    {
        if (home == null || away == null) return;
        fixtures.Add(new Fixture
        {
            round = round + 1, home = home, away = away,
            night = rng.NextDouble() < Ladder.NightChance(rung),
            pitch = Pitch(rng),
        });
    }

    private static ePitch Pitch(Random rng)
    {
        double x = rng.NextDouble();
        return x < 0.7 ? ePitch.Flat : x < 0.85 ? ePitch.Green : ePitch.Dusty;
    }

    // ---- Results and the table ------------------------------------------------------------------

    /// Play an AI-vs-AI fixture by simulation and put it in the table: a toss for who bats first,
    /// that side's innings, then the other side's chase of it. A fixture already played is left
    /// alone, so a second call never counts the match twice in the table.
    public static void Simulate(Season s, Fixture f, Random rng)
    {
        if (s == null || f == null || f.played) return;
        Team home = s.Team(f.home), away = s.Team(f.away);
        if (home == null || away == null) return;
        int overs = Ladder.Overs(s.rung);
        eBowlingLevel level = Ladder.Bowling(s.rung);
        bool homeFirst = rng.NextDouble() < 0.5;
        Team first = homeFirst ? home : away, second = homeFirst ? away : home;
        InningsCard one = CareerSim.Innings(first, second, overs, 0, level, rng);
        InningsCard two = CareerSim.Innings(second, first, overs, one.runs + 1, level, rng);
        Record(s, f, homeFirst ? one : two, homeFirst ? two : one);
    }

    /// Put a played fixture's result into both sides' table rows (2 points a win, 1 a tie), and
    /// the result into the fixture (played, the scores, the winner's id, the result line). The side
    /// that chased is the one whose card has a target. Net run rate counts an all-out innings as
    /// its full overs, as the real game does. A final goes into the fixture but not the table; a
    /// tied final goes to the side that lost fewer wickets, then to the home side (top of the table).
    /// Call it once for each fixture.
    public static void Record(Season s, Fixture f, InningsCard home, InningsCard away)
    {
        if (s == null || f == null || home == null || away == null) return;
        Team h = s.Team(f.home), a = s.Team(f.away);
        f.played = true;
        f.homeScore = ScoreText(home);
        f.awayScore = ScoreText(away);
        bool tied = home.runs == away.runs;
        bool homeWon = home.runs > away.runs;
        if (tied && f.final)
        {
            homeWon = home.wickets <= away.wickets;
            f.winner = homeWon ? f.home : f.away;
            string tb = home.wickets == away.wickets ? "higher in the table" : "fewer wickets lost";
            f.result = $"Match tied - {(homeWon ? h?.name : a?.name)} take the title ({tb})";
        }
        else
        {
            f.winner = tied ? "" : homeWon ? f.home : f.away;
            f.result = tied ? "Match tied" : homeWon ? WinText(h?.name, home, away) : WinText(a?.name, away, home);
        }
        if (f.final || h == null || a == null) return;
        Row(h, home, away, tied, homeWon);
        Row(a, away, home, tied, !homeWon && !tied);
    }

    private static void Row(Team t, InningsCard mine, InningsCard theirs, bool tied, bool won)
    {
        t.played++;
        if (tied) { t.tied++; t.points += 1; }
        else if (won) { t.won++; t.points += 2; }
        else t.lost++;
        t.runsFor += mine.runs;
        t.ballsFaced += RateBalls(mine);
        t.runsAgainst += theirs.runs;
        t.ballsBowled += RateBalls(theirs);
    }

    /// The balls an innings counts for net run rate: all out counts as the full overs.
    public static int RateBalls(InningsCard c) => c.wickets >= 10 ? c.overs * 6 : c.balls;

    /// "Harbour CC won by 12 runs" / "Old Mill CC won by 4 wickets (7 balls left)".
    private static string WinText(string winner, InningsCard w, InningsCard l)
    {
        if (w.target > 0)
        {
            int left = 10 - w.wickets, balls = Math.Max(0, w.overs * 6 - w.balls);
            return $"{winner} won by {left} wicket{(left == 1 ? "" : "s")} ({balls} ball{(balls == 1 ? "" : "s")} left)";
        }
        int by = w.runs - l.runs;
        return $"{winner} won by {by} run{(by == 1 ? "" : "s")}";
    }

    /// "145/6 (20)", "98 all out (14.2)".
    public static string ScoreText(InningsCard c)
    {
        string overs = c.balls % 6 == 0 ? (c.balls / 6).ToString() : $"{c.balls / 6}.{c.balls % 6}";
        return c.wickets >= 10 ? $"{c.runs} all out ({overs})" : $"{c.runs}/{c.wickets} ({overs})";
    }

    /// The table: points, then net run rate, then name.
    public static List<Team> Table(Season s)
    {
        var t = new List<Team>(s?.teams ?? new List<Team>());
        t.Sort((x, y) =>
        {
            int c = y.points.CompareTo(x.points);
            if (c != 0) return c;
            c = y.NetRunRate.CompareTo(x.NetRunRate);
            return c != 0 ? c : string.CompareOrdinal(x.name, y.name);
        });
        return t;
    }

    /// After the last league round: the final between the top two (added once, false if it exists
    /// or a league fixture is still to play). The top side is at home; a Pro League final is
    /// under lights; a scout watches every final.
    public static bool AddFinal(Season s)
    {
        if (s == null || s.teams.Count < 2 || s.fixtures.Count == 0) return false;
        int last = 0;
        foreach (Fixture f in s.fixtures)
        {
            if (f.final || !f.played) return false;
            last = Math.Max(last, f.round);
        }
        List<Team> table = Table(s);
        s.fixtures.Add(new Fixture
        {
            round = last + 1, home = table[0].id, away = table[1].id, final = true,
            night = Ladder.NightChance(s.rung) >= 0.5f, pitch = ePitch.Flat, scout = true,
        });
        return true;
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static IEnumerable<NameBank.TeamName> AllNames(eRung rung)
    {
        if (rung != eRung.International) return NameBank.Teams(rung);
        var l = new List<NameBank.TeamName>();
        foreach (Nations.Nation n in Nations.All) l.Add(NameBank.NationTeam(n.code));
        return l;
    }

    /// A scoreboard code for a name not in the pools: initials, else the first three letters.
    private static string ShortFrom(string name)
    {
        string code = "";
        foreach (string w in (name ?? "").Split(' '))
            if (w.Length > 0 && char.IsLetter(w[0])) code += char.ToUpperInvariant(w[0]);
        if (code.Length >= 3) return code.Length > 4 ? code.Substring(0, 4) : code;
        string letters = "";
        foreach (char c in name ?? "")
            if (char.IsLetter(c)) letters += char.ToUpperInvariant(c);
        return letters.Length >= 3 ? letters.Substring(0, 3) : (letters + "XXX").Substring(0, 3);
    }

    private static void Shuffle<T>(List<T> l, Random rng)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
    }

    /// A normal draw (Box-Muller).
    private static float Gaussian(Random rng, float mean, float sd)
    {
        double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
        return mean + sd * (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
