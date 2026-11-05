using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// The career player's season ("careerSeason", from the hub or a match result): Table | Fixtures.
/// The table is the league's sides in order (points, then net run rate; LeagueBuilder.Table) with
/// played, won, lost, points and net run rate in fixed columns, his side picked out; the fixtures
/// are his side's, round by round, with how each went from his side's point of view, the next one
/// marked, and the final once there is one. There is no scroll view: a league is
/// Ladder.TeamsInLeague sides, so both lists are that many rows. Built by Tools > CricketVR > Build
/// Menu Prefabs.
/// </summary>
public class SeasonScreen : MenuScreen
{
    /// Rows in each list: one per side in the table; seven rounds and the final in the fixtures.
    public const int Rows = Ladder.TeamsInLeague;
    /// Team names longer than this are cut short so the columns stay in line.
    public const int MaxTeamName = 20;
    private const string Muted = "#98A2B3", Good = "#22C55E", Bad = "#FF6B4A", Accent = "#8AB8FF", Blue = "#3B82F6";

    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private SegmentedControl view;
    [SerializeField] private GameObject tablePanel;
    [SerializeField] private GameObject fixturesPanel;
    [SerializeField] private TextMeshProUGUI[] tableRows = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI[] fixtureRows = new TextMeshProUGUI[0];

    private int shown;

    public override void OnShow()
    {
        shown = 0;
        if (view != null) view.SetValueWithoutNotify(0);
        Refresh();
    }

    private void Refresh()
    {
        if (tablePanel != null) tablePanel.SetActive(shown == 0);
        if (fixturesPanel != null) fixturesPanel.SetActive(shown == 1);
        CareerProfile p = CareerSaves.Current;
        Season s = p?.season;
        Set(subtitle, s == null ? "No season yet." : $"{Ladder.Name(s.rung)}  ·  Season {s.number}  ·  {s.Yours?.name}");
        List<Team> table = s != null ? LeagueBuilder.Table(s) : new List<Team>();
        for (int i = 0; i < tableRows.Length; i++)
            Set(tableRows[i], i < table.Count ? TableRow(i + 1, table[i], table[i].id == s.yourTeam) : "");
        List<Fixture> fixtures = s != null ? YourFixtures(s) : new List<Fixture>();
        Fixture next = s != null ? FirstUnplayed(fixtures, s.yourTeam) : null;
        for (int i = 0; i < fixtureRows.Length; i++)
            Set(fixtureRows[i], i < fixtures.Count ? FixtureLine(s, fixtures[i], fixtures[i] == next)
                              : i == 0 ? $"<color={Muted}>No fixtures yet.</color>" : "");
    }

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    /// Wired to the Table | Fixtures segmented control.
    public void OnView(int i)
    {
        shown = Mathf.Clamp(i, 0, 1);
        Refresh();
    }

    // ---- Formatting (pure; CareerScreensTests) -----------------------------------------------------

    // Column stops, in percent of the row: position, team, P, W, L, Pts, NRR.
    private const string CPos = "<pos=0%>", CTeam = "<pos=7%>", CP = "<pos=57%>", CW = "<pos=64%>", CL = "<pos=71%>",
                         CPts = "<pos=78%>", CNrr = "<pos=87%>";
    // Fixture stops: round, opponent, outcome.
    private const string FRound = "<pos=0%>", FTeam = "<pos=12%>", FOutcome = "<pos=58%>";

    /// The table's column headings, at the rows' stops (built into the prefab as they are).
    public static string TableHeader() => $"{CPos}#{CTeam}Team{CP}P{CW}W{CL}L{CPts}Pts{CNrr}NRR";

    /// The fixtures' column headings.
    public static string FixturesHeader() => $"{FRound}Round{FTeam}Opponent{FOutcome}Result";

    /// "1  Harbour CC  7  5  2  10  +0.84", columns fixed by <pos>; the player's side bold in the accent.
    public static string TableRow(int position, Team t, bool yours)
    {
        string row = $"{CPos}{position}{CTeam}{Clip(t.name)}{CP}{t.played}{CW}{t.won}{CL}{t.lost}{CPts}{t.points}" +
                     $"{CNrr}{Nrr(t.NetRunRate)}";
        return yours ? $"<color={Accent}><b>{row}</b></color>" : row;
    }

    /// Net run rate to two places with its sign: "+0.84", "-1.20", "0.00".
    public static string Nrr(float nrr)
    {
        double r = System.Math.Round(nrr, 2, System.MidpointRounding.AwayFromZero);
        string abs = System.Math.Abs(r).ToString("0.00", CultureInfo.InvariantCulture);
        return r > 0 ? "+" + abs : r < 0 ? "-" + abs : "0.00";
    }

    /// A name cut to MaxTeamName characters, with ".." when cut (Latin-1: there is no ellipsis).
    public static string Clip(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        return name.Length <= MaxTeamName ? name : name.Substring(0, MaxTeamName - 2).TrimEnd() + "..";
    }

    /// "R3  vs Old Mill CC  Won by 12 runs" / "... next" / "Final  HCC v OMC  to play" (a final he is not in).
    public static string FixtureLine(Season s, Fixture f, bool next)
    {
        string round = f.final ? "Final" : $"R{f.round}";
        string against = f.Involves(s.yourTeam)
            ? $"vs {Clip(CareerHubScreen.TeamName(s, f.Opponent(s.yourTeam)))}"
            : $"{ShortName(s, f.home)} v {ShortName(s, f.away)}";
        return $"{FRound}{round}{FTeam}{against}{FOutcome}{Outcome(s, f, next)}";
    }

    /// How a fixture went for the player's side ("Won by 12 runs" green, "Lost by 3 wickets"
    /// orange, "Tied" blue), "next" for the one to play next, a muted "to play" for the rest; a
    /// final he is not in shows its own result.
    public static string Outcome(Season s, Fixture f, bool next)
    {
        if (!f.played)
            return next ? $"<color={Accent}><b>next</b></color>" : $"<color={Muted}>to play</color>";
        if (string.IsNullOrEmpty(f.winner))
            return $"<color={Blue}>Tied</color>";
        if (!f.Involves(s.yourTeam))
            return $"<color={Muted}>{ShortName(s, f.winner)} won{Margin(f.result)}</color>";
        bool won = f.winner == s.yourTeam || f.winner == s.Yours?.name;
        return won ? $"<color={Good}>Won{Margin(f.result)}</color>" : $"<color={Bad}>Lost{Margin(f.result)}</color>";
    }

    /// A side's scoreboard letters ("HCC"), by id or name, for a row with two sides in it; its
    /// name cut short when it has none.
    public static string ShortName(Season s, string idOrName)
    {
        Team t = s?.teams?.Find(x => x.id == idOrName || x.name == idOrName);
        if (t == null)
            return Clip(idOrName);
        return !string.IsNullOrEmpty(t.shortName) ? t.shortName : Clip(t.name);
    }

    /// " by 12 runs" out of "Harbour CC won by 12 runs" ("" when the result gives no margin).
    public static string Margin(string result)
    {
        if (string.IsNullOrEmpty(result))
            return "";
        int at = result.IndexOf(" by ", System.StringComparison.Ordinal);
        return at >= 0 ? result.Substring(at) : "";
    }

    /// The player's side's fixtures, round by round, the final last; at most Rows of them. A final
    /// he is not in is listed too, once it is drawn.
    public static List<Fixture> YourFixtures(Season s)
    {
        var list = new List<Fixture>();
        if (s?.fixtures == null)
            return list;
        foreach (Fixture f in s.fixtures)
            if (f.final || f.Involves(s.yourTeam))
                list.Add(f);
        list.Sort((a, b) => a.final != b.final ? (a.final ? 1 : -1) : a.round.CompareTo(b.round));
        if (list.Count > Rows)
            list.RemoveRange(Rows, list.Count - Rows);
        return list;
    }

    private static Fixture FirstUnplayed(List<Fixture> fixtures, string yourTeam) =>
        fixtures.Find(f => !f.played && f.Involves(yourTeam));
}
