using UnityEngine;

/// What the umpire signals for a ball (Umpire).
public enum UmpireSignal { None, Wide, NoBall, Bye, LegBye, Four, Six, Out }

/// <summary>What happened on one ball, once it is scored: the words for the floating result
/// (BallResultText) and the umpire's signal (Umpire).</summary>
public struct BallOutcome
{
    public string Label;
    public UmpireSignal Signal;
    public Color Color;
}

/// <summary>
/// The scorebook's detail beyond the team total: balls each batter has faced (for the strike
/// rate), each bowler's balls, runs conceded and wickets, and an event for every ball scored.
/// </summary>
public partial class HUD
{
    /// Raised once a ball is in the book.
    public static event System.Action<BallOutcome> BallScored;

    private static readonly Color RunsColor = Color.white;
    private static readonly Color BoundaryColor = new Color(1f, 0.82f, 0.25f);
    private static readonly Color OutColor = new Color(1f, 0.35f, 0.3f);
    private static readonly Color ExtraColor = new Color(0.55f, 0.75f, 1f);

    private int announcedCount;
    private string announcedLast;

    /// A ball scored by AddRuns (runs, -1 for a wicket, 10 for a wide): before the striker and
    /// the bowler change hands.
    void Credit(int runs)
    {
        if (runs == 10)
        {
            CurrentBowler.Conceded += 1;
            return;
        }
        CurrentBatsman.Balls++;
        CurrentBowler.Balls++;
        if (runs < 0)
            CurrentBowler.Wickets++;
        else
            CurrentBowler.Conceded += runs;
    }

    /// Byes count as a ball faced and bowled, but are not the bowler's; runs off a wide are.
    void CreditExtras(int runs)
    {
        if (Extras.OnWide)
        {
            CurrentBowler.Conceded += runs;
            return;
        }
        CurrentBatsman.Balls++;
        CurrentBowler.Balls++;
    }

    public static string RunsAndBalls(Batsman b) => $"{b.Runs} ({b.Balls})";

    public static string StrikeRate(Batsman b) => b.Balls > 0 ? (100f * b.Runs / b.Balls).ToString("F1") : "-";

    /// Overs-maidens style figures without maidens: "2.3-12-1" (overs - runs - wickets).
    public static string Figures(Bowler b) => $"{b.Balls / 6}.{b.Balls % 6}-{b.Conceded}-{b.Wickets}";

    private void AnnounceBall()
    {
        int count = BowledBalls != null ? BowledBalls.Count : 0;
        string last = count > 0 ? BowledBalls[count - 1] : null;
        if (count == announcedCount && last == announcedLast)
            return;
        announcedCount = count;
        announcedLast = last;
        if (last != null)
            BallScored?.Invoke(Describe(last, Main.Instance.gameState));
    }

    /// The scorebook entry ("*", "1", "4", "W", "wd", "3wd", "2b") as the words and the signal.
    public static BallOutcome Describe(string entry, eGameState state)
    {
        var o = new BallOutcome { Label = entry, Signal = UmpireSignal.None, Color = RunsColor };
        if (entry == "*") o.Label = "0";
        else if (entry == "W")
        {
            o.Label = state == eGameState.InGame_Bowled ? "BOWLED" : "OUT";
            o.Signal = UmpireSignal.Out;
            o.Color = OutColor;
        }
        else if (entry.EndsWith("wd"))
        {
            int n = entry == "wd" ? 1 : int.Parse(entry.Substring(0, entry.Length - 2));
            o.Label = n == 1 ? "WIDE" : $"WIDE +{n - 1}";
            o.Signal = UmpireSignal.Wide;
            o.Color = ExtraColor;
        }
        else if (entry.EndsWith("b"))
        {
            int n = int.Parse(entry.Substring(0, entry.Length - 1));
            o.Label = n == 1 ? "1 BYE" : $"{n} BYES";
            o.Signal = UmpireSignal.Bye;
            o.Color = ExtraColor;
        }
        else if (state == eGameState.InGame_BallPastBoundary && (entry == "4" || entry == "6"))
        {
            o.Label = entry == "6" ? "SIX" : "FOUR";
            o.Signal = entry == "6" ? UmpireSignal.Six : UmpireSignal.Four;
            o.Color = BoundaryColor;
        }
        return o;
    }
}
