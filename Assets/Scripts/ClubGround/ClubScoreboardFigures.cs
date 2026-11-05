using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// What the club's score hut shows, as a value: compared each poll so the board's text is only
/// rebuilt when a figure changes (ClubScoreboard), and tested without a scene.
///
/// Batter A is the one with the lower batting order index, so the two groups keep their places as
/// the strike changes and only the star moves, as on a real board. A batting number is index + 1;
/// 0 means nobody (the group is blank). lastWkt is the total when the last wicket fell; it shows
/// only once a wicket is down.
/// </summary>
public struct BoardState : System.IEquatable<BoardState>
{
    public int runs, wkts, overs, balls, target, batANo, batARuns, batBNo, batBRuns, lastWkt;
    public bool aOnStrike;

    public bool Equals(BoardState o) =>
        runs == o.runs && wkts == o.wkts && overs == o.overs && balls == o.balls && target == o.target &&
        batANo == o.batANo && batARuns == o.batARuns && batBNo == o.batBNo && batBRuns == o.batBRuns &&
        lastWkt == o.lastWkt && aOnStrike == o.aOnStrike;

    public override bool Equals(object obj) => obj is BoardState o && Equals(o);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = runs;
            h = h * 31 + wkts; h = h * 31 + overs; h = h * 31 + balls; h = h * 31 + target;
            h = h * 31 + batANo; h = h * 31 + batARuns; h = h * 31 + batBNo; h = h * 31 + batBRuns;
            h = h * 31 + lastWkt;
            return h * 2 + (aOnStrike ? 1 : 0);
        }
    }

    public static bool operator ==(BoardState a, BoardState b) => a.Equals(b);
    public static bool operator !=(BoardState a, BoardState b) => !a.Equals(b);
}

/// <summary>
/// The score hut's labels and figures as one TextMeshPro rich-text string, every figure on its tin
/// (ClubScoreboardLayout): TOTAL, WICKETS, OVERS ("15" and a dot and "2" during an over, the dot
/// and the ball tin blank at the end of one); each batter's number and runs, the star on the
/// striker; LAST WKT; TARGET and NEED (blank unless chasing). One string on one TMP, so the
/// whole board but its header is a single draw however many figures change.
/// </summary>
public static class ClubScoreboardFigures
{
    public const int MaxWickets = 10;
    /// The board's paint: cream figures and labels on the dark tins and green plate, and the
    /// batters' numbers in near-black on their light tins.
    public const string Cream = "#F2EEDC", Ink = "#1E1C18";

    public static BoardState From(int runs, int wkts, int overs, int balls, int target, int strikerIndex, int strikerRuns,
                                  int otherIndex, int otherRuns) =>
        From(runs, wkts, overs, balls, target, strikerIndex, strikerRuns, otherIndex, otherRuns, 0);

    /// strikerIndex / otherIndex index HUD.Batsmen (-1 = none); the runs are each batter's;
    /// lastWicket is the total at the fall of the last wicket (LastWicket).
    public static BoardState From(int runs, int wkts, int overs, int balls, int target, int strikerIndex, int strikerRuns,
                                  int otherIndex, int otherRuns, int lastWicket)
    {
        var s = new BoardState
        {
            runs = runs, wkts = wkts < 0 ? 0 : wkts > MaxWickets ? MaxWickets : wkts, overs = overs, balls = balls,
            target = target, lastWkt = lastWicket,
        };
        bool striker = strikerIndex >= 0, other = otherIndex >= 0 && otherIndex != strikerIndex;
        if (striker && other)
        {
            bool strikerFirst = strikerIndex < otherIndex;
            s.batANo = (strikerFirst ? strikerIndex : otherIndex) + 1;
            s.batARuns = strikerFirst ? strikerRuns : otherRuns;
            s.batBNo = (strikerFirst ? otherIndex : strikerIndex) + 1;
            s.batBRuns = strikerFirst ? otherRuns : strikerRuns;
            s.aOnStrike = strikerFirst;
        }
        else if (striker)
        {
            s.batANo = strikerIndex + 1; s.batARuns = strikerRuns; s.aOnStrike = true;
        }
        else if (other)
        {
            s.batANo = otherIndex + 1; s.batARuns = otherRuns;
        }
        return s;
    }

    /// The total at the fall of the last wicket, read from the scorebook's ball list (HUD.BowledBalls:
    /// "*", "4", "W", "wd", "3wd", "nb", "5nb", "2b"): the total now less the runs of every ball after
    /// the last "W". Worked out from the book rather than from the total when the board saw the
    /// wickets go up, because the career plays a batch of simulated balls in one frame (the
    /// partner's balls, the rest of the innings once the player is out): a poll after the batch
    /// would chalk the runs scored after the wicket as well. Counting back from the total, not up
    /// from the first ball, means only the balls since the wicket have to be read right.
    /// 0 with no wicket down; the total now if the book has no "W" to count back to.
    public static int LastWicket(IList<string> book, int runs, int wkts)
    {
        if (wkts <= 0)
            return 0;
        int after = 0;
        for (int i = book != null ? book.Count - 1 : -1; i >= 0; i--)
        {
            if (book[i] == "W")
                return System.Math.Max(0, runs - after);
            after += BallRuns(book[i]);
        }
        return runs;
    }

    /// The runs one scorebook entry put on the total: "*" and "W" none, "wd" and "nb" one, "3wd",
    /// "5nb" and "2b" their number (a no-ball's or wide's includes its one), "4" its runs.
    public static int BallRuns(string entry)
    {
        if (string.IsNullOrEmpty(entry) || entry == "*" || entry == "W")
            return 0;
        if (entry == "wd" || entry == "nb")
            return 1;
        int n = 0;
        foreach (char c in entry)
        {
            if (c < '0' || c > '9') break;
            n = n * 10 + (c - '0');
        }
        return n;
    }

    /// What each slot shows ("" for a blank tin), in ClubScoreboardLayout.Slot order. A figure too
    /// long for its tins shows its tins' largest (999 on three).
    public static string[] Figures(BoardState s)
    {
        bool bOnStrike = !s.aOnStrike && s.batBNo > 0;   // both in, and A not on strike: B is
        bool chasing = s.target > 0;
        var f = new string[System.Enum.GetValues(typeof(ClubScoreboardLayout.Slot)).Length];
        Put(f, ClubScoreboardLayout.Slot.Total, s.runs);
        Put(f, ClubScoreboardLayout.Slot.Wickets, s.wkts);
        Put(f, ClubScoreboardLayout.Slot.Overs, s.overs);
        Put(f, ClubScoreboardLayout.Slot.Balls, s.balls, s.balls > 0);
        Put(f, ClubScoreboardLayout.Slot.BatANo, s.batANo, s.batANo > 0);
        Put(f, ClubScoreboardLayout.Slot.BatARuns, s.batARuns, s.batANo > 0);
        f[(int)ClubScoreboardLayout.Slot.BatAStar] = s.batANo > 0 && s.aOnStrike ? "*" : "";
        Put(f, ClubScoreboardLayout.Slot.BatBNo, s.batBNo, s.batBNo > 0);
        Put(f, ClubScoreboardLayout.Slot.BatBRuns, s.batBRuns, s.batBNo > 0);
        f[(int)ClubScoreboardLayout.Slot.BatBStar] = bOnStrike ? "*" : "";
        Put(f, ClubScoreboardLayout.Slot.LastWicket, s.lastWkt, s.wkts > 0);
        Put(f, ClubScoreboardLayout.Slot.Target, s.target, chasing);
        Put(f, ClubScoreboardLayout.Slot.Need, System.Math.Max(0, s.target - s.runs), chasing);
        return f;
    }

    static void Put(string[] f, ClubScoreboardLayout.Slot slot, int value, bool shown = true)
    {
        int cells = ClubScoreboardLayout.Find(slot).cells, max = 1;
        for (int i = 0; i < cells; i++) max *= 10;
        f[(int)slot] = shown ? System.Math.Max(0, System.Math.Min(value, max - 1)).ToString(CultureInfo.InvariantCulture) : "";
    }

    /// A length on the board in TMP's world-space "pixels" (a tag value without a unit is 0.1 m).
    public static string Px(float metres) => Num(metres * 10f);

    /// A tag value as TMP parses it (invariant culture: a comma would end the number).
    public static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// The board's text: per band, a line of labels and a line of figures, each line's baseline
    /// spaced from the last by a line-height tag (fixed, in metres: the font's own spacing never
    /// enters), so line n sits on ClubScoreboardLayout.Baselines()[n] when the text's first
    /// baseline is on the first (the builder anchors it so: BaselineLeft, the rect centred on it).
    public static string Compose(BoardState s)
    {
        string[] figures = Figures(s);
        float[] baselines = ClubScoreboardLayout.Baselines();
        var sb = new StringBuilder(640);
        for (int row = 0; row < ClubScoreboardLayout.Rows.Length; row++)
        {
            LabelLine(sb, row, Step(baselines, 2 * row));
            sb.Append('\n');
            FigureLine(sb, row, figures, s.balls > 0, Step(baselines, 2 * row + 1));
            if (row + 1 < ClubScoreboardLayout.Rows.Length) sb.Append('\n');
        }
        return sb.ToString();
    }

    static float Step(float[] baselines, int line) => line + 1 < baselines.Length ? baselines[line + 1] - baselines[line] : 0f;

    static void LabelLine(StringBuilder sb, int row, float lineHeight)
    {
        sb.Append("<line-height=").Append(Px(lineHeight)).Append("><size=")
          .Append(Num(ClubScoreboardLayout.LabelSize)).Append("><mspace=").Append(Px(ClubScoreboardLayout.LabelCell)).Append('>');
        foreach (ClubScoreboardLayout.Label l in ClubScoreboardLayout.Labels)
            if (l.row == row)
                sb.Append("<pos=").Append(Px(l.centre - ClubScoreboardLayout.LabelWidth(l.text) * 0.5f)).Append('>').Append(l.text);
    }

    static void FigureLine(StringBuilder sb, int row, string[] figures, bool dot, float lineHeight)
    {
        ClubScoreboardLayout.Row r = ClubScoreboardLayout.Rows[row];
        sb.Append("<line-height=").Append(Px(lineHeight)).Append("><size=").Append(Num(r.digitSize)).Append('>');
        foreach (ClubScoreboardLayout.Field f in ClubScoreboardLayout.Fields)
        {
            if (f.row != row) continue;
            string text = figures[(int)f.slot];
            if (text.Length == 0) continue;
            float cell = ClubScoreboardLayout.CellWidth(f);
            if (f.tin == ClubScoreboardLayout.Tin.Light) sb.Append("<color=").Append(Ink).Append('>');
            sb.Append("<mspace=").Append(Px(cell)).Append('>');
            // Right-aligned in the field, each digit placed on its own cell.
            for (int i = 0; i < text.Length; i++)
                sb.Append("<pos=").Append(Px(f.left + (f.cells - text.Length + i) * cell)).Append('>').Append(text[i]);
            if (f.tin == ClubScoreboardLayout.Tin.Light) sb.Append("</color>");
        }
        if (row == 0 && dot)
            sb.Append("<mspace=").Append(Px(ClubScoreboardLayout.DotWidth)).Append("><pos=")
              .Append(Px(ClubScoreboardLayout.DotLeft)).Append(">.");
    }
}
