/// <summary>
/// What the club's score hut shows, as a value: compared each poll so the board's text is only
/// rebuilt when a figure changes (ClubScoreboard), and tested without a scene.
///
/// Batter A is the one with the lower batting order index, so the two rows keep their places as
/// the strike changes and only the star moves, as on a real board. A batting number is index + 1;
/// 0 means nobody (the row is blank).
/// </summary>
public struct BoardState : System.IEquatable<BoardState>
{
    public int runs, wkts, overs, balls, target, batANo, batARuns, batBNo, batBRuns;
    public bool aOnStrike;

    public bool Equals(BoardState o) =>
        runs == o.runs && wkts == o.wkts && overs == o.overs && balls == o.balls && target == o.target &&
        batANo == o.batANo && batARuns == o.batARuns && batBNo == o.batBNo && batBRuns == o.batBRuns &&
        aOnStrike == o.aOnStrike;

    public override bool Equals(object obj) => obj is BoardState o && Equals(o);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = runs;
            h = h * 31 + wkts; h = h * 31 + overs; h = h * 31 + balls; h = h * 31 + target;
            h = h * 31 + batANo; h = h * 31 + batARuns; h = h * 31 + batBNo; h = h * 31 + batBRuns;
            return h * 2 + (aOnStrike ? 1 : 0);
        }
    }

    public static bool operator ==(BoardState a, BoardState b) => a.Equals(b);
    public static bool operator !=(BoardState a, BoardState b) => !a.Equals(b);
}

/// <summary>
/// The score hut's figures as one TextMeshPro rich-text string, in rows matching Labels (a second,
/// static TMP object over the same rect): TOTAL, WKTS, OVERS ("6" at the end of an over, "6.3"
/// during one), TARGET (blank when there is none) and the two batters ("4  34*", the star on the
/// striker). One string, so the board costs one text mesh however many figures change.
/// </summary>
public static class ClubScoreboardFigures
{
    /// Where the figures start across the board, and the row spacing both texts share.
    public const string FiguresAt = "56%", LineHeight = "118%";
    public const int MaxWickets = 10;

    /// The board's painted labels, row for row with Compose.
    public const string Labels = "<line-height=" + LineHeight + ">TOTAL\nWKTS\nOVERS\nTARGET\nBAT\nBAT";

    /// strikerIndex / otherIndex index HUD.Batsmen (-1 = none); the runs are each batter's.
    public static BoardState From(int runs, int wkts, int overs, int balls, int target, int strikerIndex, int strikerRuns,
                                  int otherIndex, int otherRuns)
    {
        var s = new BoardState
        {
            runs = runs, wkts = wkts < 0 ? 0 : wkts > MaxWickets ? MaxWickets : wkts, overs = overs, balls = balls,
            target = target,
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

    /// "6" at the end of an over, "6.3" during one.
    public static string Overs(int overs, int balls) => balls > 0 ? overs + "." + balls : overs.ToString();

    /// "4  34*": batting number, runs, the star if on strike. Blank for nobody.
    public static string Batter(int no, int runs, bool onStrike) =>
        no <= 0 ? "" : no + "  " + runs + (onStrike ? "*" : "");

    public static string Compose(BoardState s)
    {
        // B has a batter only when both are in, and then one of them is on strike: no star on A means B.
        bool bOnStrike = !s.aOnStrike && s.batBNo > 0;
        return "<line-height=" + LineHeight + ">" +
               Row(s.runs.ToString()) + "\n" +
               Row(s.wkts.ToString()) + "\n" +
               Row(Overs(s.overs, s.balls)) + "\n" +
               Row(s.target > 0 ? s.target.ToString() : "") + "\n" +
               Row(Batter(s.batANo, s.batARuns, s.aOnStrike)) + "\n" +
               Row(Batter(s.batBNo, s.batBRuns, bOnStrike));
    }

    private static string Row(string figure) => "<pos=" + FiguresAt + ">" + figure;
}
