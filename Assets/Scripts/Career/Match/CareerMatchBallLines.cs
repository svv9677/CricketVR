using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The ball-by-ball list of a partner's simulated balls (CareerMatchBallByBall): one line a ball,
/// "6.3  Ravi  1 run", from the scorer's own entry for it (HUD.BowledBalls: "*", "1", "4", "W",
/// "wd", "3wd", "2b"). Pure: CareerMatchBallByBallTests.
/// </summary>
public static partial class CareerMatchLogic
{
    /// The balls the card shows at once, newest last.
    public const int ShownBalls = 6;

    /// "6.3": the ball just bowled, from the legal balls bowled after it. A wide is not one of the
    /// six, so it carries the number of the ball still to come.
    public static string BallNumber(int legalAfter, bool legal)
    {
        int n = Math.Max(0, legalAfter);
        if (legal)
        {
            int i = Math.Max(0, n - 1);
            return $"{i / 6}.{i % 6 + 1}";
        }
        return $"{n / 6}.{n % 6 + 1}";
    }

    /// Whether the scorer's entry is one of the six (not a wide: "wd", "3wd").
    public static bool IsLegal(string entry) => entry == null || !entry.EndsWith("wd", StringComparison.Ordinal);

    /// What the ball was: "dot", "1 run", "FOUR", "SIX", "OUT - c Khan b Lee", "wide", "2 byes".
    public static string BallResult(string entry, string how)
    {
        switch (entry)
        {
            case null:
            case "":
            case "*": return "dot";
            case "W": return string.IsNullOrEmpty(how) ? "OUT" : $"OUT - {how}";
            case "wd": return "wide";
            case "4": return "FOUR";
            case "6": return "SIX";
        }
        if (entry.EndsWith("wd", StringComparison.Ordinal))
            return $"{entry.Substring(0, entry.Length - 2)} wides";
        if (entry.EndsWith("b", StringComparison.Ordinal))
        {
            string n = entry.Substring(0, entry.Length - 1);
            return n == "1" ? "1 bye" : $"{n} byes";
        }
        return entry == "1" ? "1 run" : $"{entry} runs";
    }

    /// "6.3  Ravi  1 run", the result coloured for a wicket or a boundary.
    public static string BallLine(string number, string batter, string entry, string how)
    {
        string result = BallResult(entry, how);
        if (entry == "W")
            result = $"<color=#F2A65A><b>{result}</b></color>";
        else if (entry == "4" || entry == "6")
            result = $"<color=#8AB8FF><b>{result}</b></color>";
        return $"<color=#98A2B3>{number}</color>  {batter}  {result}";
    }

    /// The card's body: the newest ShownBalls lines (an ellipsis over them when there were more),
    /// any closing lines, then the score.
    public static string BallByBallBody(IList<string> balls, IList<string> closing, int runs, int wickets, int legalBalls)
    {
        var sb = new StringBuilder();
        int from = balls == null ? 0 : Math.Max(0, balls.Count - ShownBalls);
        if (from > 0)
            sb.Append("<color=#98A2B3>...</color>\n");
        if (balls != null)
            for (int i = from; i < balls.Count; i++)
                sb.Append(balls[i]).Append('\n');
        if (closing != null)
            foreach (string l in closing)
                if (!string.IsNullOrEmpty(l))
                    sb.Append(l).Append('\n');
        sb.Append("<b>").Append(ScoreAfter(runs, wickets, legalBalls)).Append("</b>");
        return sb.ToString();
    }
}
