using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// How the career match just played went ("careerResult"; MainMenuExtras pushes it over the hub
/// when CareerMatchSession.ReturnToHub brings the player home): won, lost or tied and by how much,
/// his own innings big ("34 (22)", the boundaries and how he got out), both sides' totals, his
/// overall before and after the match with his form, and where his side now sits in the league.
/// Continue goes back to the hub; Season opens the table. It reads CareerMatchSession.LastResult and
/// LastPlan and the saved career, and changes none of them. Built by Tools > CricketVR > Build Menu
/// Prefabs.
/// </summary>
public class CareerResultScreen : MenuScreen
{
    public const int Wickets = 10;
    private const string Muted = "#98A2B3", Good = "#22C55E", Bad = "#FF6B4A", Blue = "#3B82F6";

    [SerializeField] private TextMeshProUGUI headline;
    [SerializeField] private TextMeshProUGUI innings;
    [SerializeField] private TextMeshProUGUI inningsDetail;
    [SerializeField] private TextMeshProUGUI scores;
    [SerializeField] private TextMeshProUGUI ratings;
    [SerializeField] private TextMeshProUGUI position;

    // The overall as the player walked out (statics survive the scene loads, like NetsSession's),
    // so the result can say what the match did to it. Kept with the slot it belongs to.
    private static float overallBefore = RatingSet.Unknown;
    private static int beforeSlot = -1;
    // The season the match was played in: if recording it ended that season and started the next,
    // the new season's empty table says nothing about this match, so no position is shown.
    private static int beforeSeason = -1;

    /// The hub calls this as a match begins.
    public static void RememberBefore(CareerProfile p)
    {
        overallBefore = p?.ratings != null ? p.ratings.Overall : RatingSet.Unknown;
        beforeSlot = CareerSaves.Active;
        beforeSeason = p?.season != null ? p.season.number : -1;
    }

    /// The season the last match of `slot` was played in, if it is still the current one; null
    /// when the career has moved on to a new season since (or nothing was remembered for it).
    public static Season SeasonPlayed(CareerProfile p, int slot)
    {
        Season s = p?.season;
        if (s == null || slot != beforeSlot || beforeSeason < 0)
            return s;
        return s.number == beforeSeason ? s : null;
    }

    /// The overall remembered for `slot` as its last match began (Unknown if none was).
    public static float OverallBefore(int slot) => slot >= 0 && slot == beforeSlot ? overallBefore : RatingSet.Unknown;

    public override void OnShow()
    {
        CareerMatchResult r = CareerMatchSession.LastResult;
        MatchPlan plan = CareerMatchSession.LastPlan;
        CareerProfile p = CareerSaves.Current;
        if (r == null)
        {
            Set(headline, "No match yet");
            Set(innings, "");
            Set(inningsDetail, "Play a career match and how it went will be here.");
            Set(scores, "");
            Set(ratings, "");
            Set(position, "");
            return;
        }
        bool batFirst = BattedFirst(r, plan);
        int margin = Margin(r, batFirst, out bool byRuns);
        string words = margin > 0 ? Headline(r.won, r.tied, margin, byRuns) : Headline(r.won, r.tied, r.result);
        Set(headline, $"<color={(r.tied ? Blue : r.won ? Good : Bad)}>{words}</color>");
        Set(innings, r.batted ? $"{r.runs} <color={Muted}>({r.balls})</color>" : $"<color={Muted}>Did not bat</color>");
        Set(inningsDetail, r.batted ? InningsDetail(r.fours, r.sixes, r.isOut, r.retired, r.how) : "");
        Set(scores, ScoresLine(r, plan, batFirst));
        Set(ratings, p == null ? "" : RatingsLine(OverallBefore(CareerSaves.Active), Overall(p), CareerProgress.Form(p)));
        Set(position, PositionLine(SeasonPlayed(p, CareerSaves.Active)));
    }

    private static float Overall(CareerProfile p) => p.ratings != null ? p.ratings.Overall : RatingSet.Unknown;

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Formatting (pure; CareerScreensTests) -----------------------------------------------------

    /// "WON by 12 runs", "LOST by 3 wickets", "WON by 1 wicket", "TIED" (no margin: "WON" / "LOST").
    public static string Headline(bool won, bool tied, int margin, bool byRuns)
    {
        if (tied)
            return "TIED";
        string word = won ? "WON" : "LOST";
        if (margin <= 0)
            return word;
        string unit = byRuns ? (margin == 1 ? "run" : "runs") : (margin == 1 ? "wicket" : "wickets");
        return $"{word} by {margin} {unit}";
    }

    /// The headline from the result's own words when the scores cannot say: "WON by 4 wickets"
    /// out of "Harbour CC won by 4 wickets".
    public static string Headline(bool won, bool tied, string result) =>
        tied ? "TIED" : (won ? "WON" : "LOST") + SeasonScreen.Margin(result);

    /// Did the player's side bat first? The plan says so; without it, whichever side had a target
    /// to chase batted second.
    public static bool BattedFirst(CareerMatchResult r, MatchPlan plan)
    {
        if (plan != null)
            return plan.batFirst;
        if (r?.yours != null && r.yours.target > 0) return false;
        if (r?.theirs != null && r.theirs.target > 0) return true;
        return true;
    }

    /// The winning margin from the two innings: the side batting first wins by runs, the side
    /// chasing by the wickets it had left. 0 when it cannot be told (a tie, a card missing).
    public static int Margin(CareerMatchResult r, bool batFirst, out bool byRuns)
    {
        byRuns = false;
        if (r == null || r.tied || r.yours == null || r.theirs == null)
            return 0;
        InningsCard first = batFirst ? r.yours : r.theirs, second = batFirst ? r.theirs : r.yours;
        bool firstWon = r.won == batFirst;
        byRuns = firstWon;
        int margin = firstWon ? first.runs - second.runs : Wickets - second.wickets;
        return Mathf.Max(0, margin);
    }

    /// "4 fours · 2 sixes · c Khan b Lee", "1 four · not out", "retired".
    public static string InningsDetail(int fours, int sixes, bool isOut, bool retired, string how)
    {
        var parts = new List<string>();
        if (fours > 0) parts.Add(fours == 1 ? "1 four" : $"{fours} fours");
        if (sixes > 0) parts.Add(sixes == 1 ? "1 six" : $"{sixes} sixes");
        if (isOut) parts.Add(string.IsNullOrEmpty(how) ? "out" : how);
        else parts.Add(retired ? "retired" : "not out");
        return string.Join(" · ", parts);
    }

    /// "Harbour CC 145/6 (20) · Old Mill CC 133/9 (20)", the side that batted first first.
    public static string ScoresLine(CareerMatchResult r, MatchPlan plan, bool batFirst)
    {
        string yours = Score(r.yours, plan != null ? plan.yourTeamName : null);
        string theirs = Score(r.theirs, plan != null ? plan.opponentName : null);
        if (yours == "" || theirs == "")
            return yours + theirs;
        return batFirst ? $"{yours} · {theirs}" : $"{theirs} · {yours}";
    }

    /// "Harbour CC 145/6 (20)"; overs whole when the innings ran its course, else "(18.4)".
    public static string Score(InningsCard c, string name)
    {
        if (c == null)
            return "";
        string who = !string.IsNullOrEmpty(name) ? name : c.team;
        string overs = c.balls % 6 == 0 ? (c.balls / 6).ToString(CultureInfo.InvariantCulture) : c.OversText;
        return $"{who} {c.Score} <color={Muted}>({overs})</color>";
    }

    /// "Overall 54 -> 56  ·  Form 61"; just "Overall 56" when the before is not known.
    public static string RatingsLine(float before, float after, float form)
    {
        string now = RatingSet.Known(after) ? Round(after) : "--";
        string overall = RatingSet.Known(before) && RatingSet.Known(after) && Round(before) != now
            ? $"Overall {Round(before)} -> <b>{now}</b>"
            : $"Overall <b>{now}</b>";
        string f = RatingSet.Known(form) ? Round(form) : "--";
        return $"{overall}  <color={Muted}>·</color>  Form <b>{f}</b>";
    }

    /// "League position 3rd of 8" ("" with no season).
    public static string PositionLine(Season s)
    {
        if (s == null || s.teams == null || s.teams.Count == 0)
            return "";
        int pos = LeagueBuilder.Table(s).FindIndex(t => t.id == s.yourTeam) + 1;
        return pos > 0 ? $"<color={Muted}>League position</color>  <b>{Ordinal(pos)}</b> of {s.teams.Count}" : "";
    }

    /// 1st, 2nd, 3rd, 4th ... 11th, 12th, 13th ... 21st.
    public static string Ordinal(int n)
    {
        int tens = n % 100;
        string suffix = tens >= 11 && tens <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private static string Round(float v) => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// Back to the hub, under this screen.
    public void OnContinue() => OnBack();

    /// The table, over the hub (not over this screen), so Back from it goes to the hub.
    public void OnSeason()
    {
        if (Root == null)
            return;
        Root.Back();
        Root.Push("careerSeason");
    }
}
