using System.Collections.Generic;
using System.Text;

/// Where a batter stands on the card: still to come in, in (or in at the end), or gone.
public enum eBatterState { YetToBat, NotOut, Out }

/// <summary>One batter's line on the batting card, ready to draw: every string is final text.</summary>
public struct BattingCardRow
{
    /// "A. Shah", "You", with " *" after the striker's name.
    public string Name;
    /// "c Jones b Khan", "b Khan", "lbw b Khan", "run out", "not out", "yet to bat".
    public string HowOut;
    /// "27", or "" for a batter yet to bat (a nought he never had a chance at is not a 0).
    public string Runs;
    /// "(19)", or "" for a batter yet to bat.
    public string Balls;
    public eBatterState State;
    public bool Striker;
    /// The career player's own line (drawn in the accent colour).
    public bool You;
}

/// <summary>
/// The batting side's scorecard, as the match panel (ScorecardPanel) draws it:
///
///   WESTFIELD CC        42/2  6.3 ov
///   A. Shah   c Jones b Khan   4 (6)
///   You *      not out        27 (19)
///   R. Patel  not out          9 (11)
///   B. Lee    yet to bat
///   Extras 2      Total 42/2
///
/// Pure: built from the scorebook HUD keeps (its Batsmen, the striker and his partner, the total)
/// and the one thing HUD does not keep, how each batter got out (BattingCardFeed collects that
/// from HUD.BatterOut). Every name and dismissal is shortened to fit its cell and made Latin-1,
/// so the panel only copies strings into its texts.
/// </summary>
public class BattingCard
{
    /// Longest name (after "A. Shah" shortening) and dismissal before they are cut with "...". The
    /// panel's cells also cut with an ellipsis; these keep the striker's star from being the part
    /// that is cut, and keep a long dismissal from pushing into the runs.
    public const int NameChars = 14, HowChars = 24;
    public const string YouName = "You", NotOut = "not out", YetToBat = "yet to bat", Out = "out";

    public string Team = "";
    /// "42/2".
    public string Score = "0/0";
    /// "6.3 ov".
    public string Overs = "0.0 ov";
    /// "Extras 2".
    public string Extras = "Extras 0";
    /// "Total 42/2".
    public string Total = "Total 0/0";
    public readonly List<BattingCardRow> Rows = new List<BattingCardRow>();

    /// The card for `order` (HUD.Batsmen, the order they bat in). `outs` holds how each dismissed
    /// batter went (BattingCardFeed); one missing from it who has plainly been in and is not at
    /// the crease any more (the first 2 + wickets have batted) is shown as plain "out", so a card
    /// opened late never shows a gone batter as not out. Out wins over at-the-crease: after the
    /// tenth wicket HUD's striker wraps back to the first opener, who is long gone.
    public static BattingCard Build(string team, IList<Batsman> order, Batsman striker, Batsman runner,
                                    IDictionary<Batsman, string> outs, int runs, int wickets, int overs, int balls)
    {
        var card = new BattingCard
        {
            Team = Latin1(team),
            Score = $"{runs}/{wickets}",
            Overs = $"{overs}.{balls} ov",
            Total = $"Total {runs}/{wickets}",
        };
        int batterRuns = 0;
        int count = order != null ? order.Count : 0;
        int batted = System.Math.Min(count, 2 + System.Math.Max(0, wickets));
        for (int i = 0; i < count; i++)
        {
            Batsman b = order[i];
            if (b == null)
                continue;
            batterRuns += b.Runs;
            string how = null;
            bool isOut = outs != null && outs.TryGetValue(b, out how);
            bool atCrease = b == striker || b == runner;
            eBatterState state = isOut ? eBatterState.Out
                               : atCrease ? eBatterState.NotOut
                               : i < batted ? eBatterState.Out
                               : eBatterState.YetToBat;
            card.Rows.Add(Row(b, state, how, state == eBatterState.NotOut && b == striker));
        }
        card.Extras = $"Extras {System.Math.Max(0, runs - batterRuns)}";
        return card;
    }

    private static BattingCardRow Row(Batsman b, eBatterState state, string how, bool striker)
    {
        string name = b.IsHuman ? YouName : Truncate(ShortName(Latin1(b.Name)), NameChars);
        bool batted = state != eBatterState.YetToBat;
        return new BattingCardRow
        {
            Name = striker ? name + " *" : name,
            HowOut = state == eBatterState.Out ? Truncate(CompactHowOut(Latin1(how)), HowChars)
                   : state == eBatterState.NotOut ? NotOut : YetToBat,
            Runs = batted ? b.Runs.ToString() : "",
            Balls = batted ? $"({b.Balls})" : "",
            State = state,
            Striker = striker,
            You = b.IsHuman,
        };
    }

    /// "Arjun Shah" as the scorecard writes him, "A. Shah"; "Abraham de Villiers", "A. de Villiers".
    /// One word ("Ishan", Quick Match's batters) stays as it is.
    public static string ShortName(string full)
    {
        if (string.IsNullOrEmpty(full))
            return "";
        string t = full.Trim();
        int sp = t.IndexOf(' ');
        if (sp <= 0 || sp >= t.Length - 1)
            return t;
        return t[0] + ". " + t.Substring(sp + 1).Trim();
    }

    /// A dismissal with the players by surname, as scorecards print them: "c Arjun Khan b Ravi Lee"
    /// is "c Khan b Lee", "st Dev Rao b Om Iyer" is "st Rao b Iyer". The forms HUD and CareerSim
    /// write ("b X", "lbw b X", "c & b X", "c wk b X", "c Point b X", "st X b Y", "run out") keep
    /// their shape, and a catcher that is a field position keeps all its words ("c Third man b X",
    /// IsFieldPosition); anything else, or nothing, comes back as it is (or "out").
    public static string CompactHowOut(string how)
    {
        if (string.IsNullOrEmpty(how) || string.IsNullOrEmpty(how.Trim()))
            return Out;
        string h = how.Trim();
        if (h.StartsWith("run out"))
            return h;
        foreach (string lead in new[] { "lbw b ", "c & b ", "b " })
            if (h.StartsWith(lead))
                return lead + Surname(h.Substring(lead.Length));
        foreach (string lead in new[] { "c ", "st " })
        {
            if (!h.StartsWith(lead))
                continue;
            int by = h.LastIndexOf(" b ");
            // No fielder between ("c b Khan": HUD could not name the catcher): left as written,
            // rather than read the bowler as the catcher.
            if (by < lead.Length)
                return h;
            string fielder = h.Substring(lead.Length, by - lead.Length);
            string catcher = IsFieldPosition(fielder) ? fielder.Trim() : Surname(fielder);
            return lead + catcher + " b " + Surname(h.Substring(by + 3));
        }
        return h;
    }

    /// Everything after the first name ("Ravi Lee" -> "Lee"); one word stays ("Point", "wk").
    private static string Surname(string full) => NameBank.Surname(full);

    /// True for a catcher that is a place on the field, not a player: a real-ball catch is written
    /// by position (HUD, FieldCaptain.PositionName), and "Third man" cut to a surname would read
    /// "c man". Positions are written in sentence case, every word after the first in lower case
    /// ("Deep square leg", "Short third man", "Cover (up)"), while a player's name always has a
    /// capital after the first word ("Arjun Khan", "Quinton de Kock"). One word is left as it is
    /// either way, so it does not matter which it is.
    public static bool IsFieldPosition(string catcher)
    {
        if (string.IsNullOrEmpty(catcher))
            return false;
        string[] words = catcher.Trim().Split(' ');
        if (words.Length < 2)
            return false;
        for (int i = 1; i < words.Length; i++)
            if (words[i].Length > 0 && char.IsUpper(words[i][0]))
                return false;
        return true;
    }

    /// At most `max` characters: a longer one is cut and ends "...".
    public static string Truncate(string s, int max)
    {
        if (s == null)
            return "";
        if (s.Length <= max || max < 4)
            return s;
        return s.Substring(0, max - 3).TrimEnd() + "...";
    }

    /// The side's name for the heading: the career side's name in a career match, else the name
    /// the replays use (ReplayOverlay.Team), else "Batting".
    public static string TeamName(string careerSide, string fallback)
    {
        if (!string.IsNullOrEmpty(careerSide))
            return careerSide;
        return !string.IsNullOrEmpty(fallback) ? fallback : "Batting";
    }

    /// The TMP font is Latin-1 only (NetsTrialCard.Latin1).
    private static string Latin1(string s) => NetsTrialCard.Latin1(s ?? "");

    /// The card as plain lines, for the log and the tests: "A. Shah | c Jones b Khan | 4 | (6)".
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Team).Append(" | ").Append(Score).Append(" | ").Append(Overs).Append('\n');
        foreach (BattingCardRow r in Rows)
            sb.Append(r.Name).Append(" | ").Append(r.HowOut).Append(" | ").Append(r.Runs).Append(" | ").Append(r.Balls).Append('\n');
        sb.Append(Extras).Append(" | ").Append(Total);
        return sb.ToString();
    }
}
