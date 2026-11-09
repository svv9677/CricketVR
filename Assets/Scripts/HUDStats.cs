using UnityEngine;

/// What the umpire signals for a ball (Umpire).
public enum UmpireSignal { None, Wide, NoBall, Bye, LegBye, Four, Six, Out, FreeHit }

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
            o.Label = state == eGameState.InGame_Bowled ? "BOWLED" : state == eGameState.InGame_BallFielded ? "CAUGHT" : "OUT";
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
        else if (entry.EndsWith("nb"))
        {
            int n = entry == "nb" ? 1 : int.Parse(entry.Substring(0, entry.Length - 2));
            o.Label = n == 1 ? "NO BALL" : n == 5 ? "NO BALL + FOUR" : n == 7 ? "NO BALL + SIX" : $"NO BALL +{n - 1}";
            o.Signal = UmpireSignal.NoBall;
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

    /// The current over, ball by ball: "1  •  4  W  2wd", boundaries gold, wickets red, extras blue.
    private string ThisOver()
    {
        if (BowledBalls == null || BowledBalls.Count == 0)
            return "<color=#98A2B3>THIS OVER</color>";
        // Back to the start of this over: Balls legal deliveries, plus any wides / no-balls among them.
        int legal = 0, from = BowledBalls.Count;
        while (from > 0 && (legal < Balls || IsExtra(BowledBalls[from - 1])))
        {
            from--;
            if (!IsExtra(BowledBalls[from])) legal++;
        }
        var sb = new System.Text.StringBuilder("<color=#98A2B3>THIS OVER</color>   ");
        for (int i = from; i < BowledBalls.Count; i++)
        {
            string b = BowledBalls[i];
            string shown = b == "*" ? "•" : b;
            string col = b == "W" ? "#FF5A4E" : b == "4" || b == "6" ? "#FFD34D" : IsExtra(b) || b.EndsWith("b") ? "#7FB2FF" : "#F3F5F8";
            sb.Append("<color=").Append(col).Append('>').Append(shown).Append("</color>  ");
        }
        return sb.ToString();
    }

    private static bool IsExtra(string b) => b.EndsWith("wd") || b.EndsWith("nb");

    /// The last wicket as the scorecard writes it: "Rohit c Point b Bumrah 34 (22)" (WicketMoment).
    public string LastDismissal;

    private string Dismissal(Batsman b)
    {
        Main inst = Main.Instance;
        string bowler = CurrentBowler != null ? CurrentBowler.Name : "";
        string how;
        if (inst != null && inst.gameState == eGameState.InGame_Bowled)
            how = $"b {bowler}";
        else
        {
            string catcher = inst != null ? inst.currentFielderName : "";
            if (catcher == "Bowler")
                how = $"c & b {bowler}";
            else if (catcher != null && catcher.Contains("Keeper"))
                how = $"c wk b {bowler}";
            else
            {
                string where = null;
                FieldCaptain captain = FindFirstObjectByType<FieldCaptain>();
                foreach (AnimatedFielder f in AnimatedFielder.All)
                    if (f != null && f.name == catcher && captain != null)
                        where = captain.PositionName(f);
                how = where != null ? $"c {where} b {bowler}" : $"c b {bowler}";
            }
        }
        return $"{b.Name} {how}  {b.Runs} ({b.Balls})";
    }

    /// A no-ball (NoBalls): one run to the team (the bowler's), plus runs off the bat to the batter
    /// or byes to the team; a ball faced, not one of the over's six. "nb", "5nb" (four off it).
    /// Byes coming in after the ball was first scored add to the same entry.
    void ScoreNoBall(int runs, bool byes)
    {
        int last = BowledBalls.Count - 1;
        bool already = byes && last >= 0 && BowledBalls[last].EndsWith("nb");
        if (!already)
        {
            Runs += 1;
            CurrentBowler.Conceded += 1;
            CurrentBatsman.Balls++;
        }
        Runs += runs;
        if (!byes)
        {
            CurrentBatsman.Runs += runs;
            CurrentBowler.Conceded += runs;
        }
        if (runs % 2 == 1)
            SwitchBatsmen();
        if (already)
        {
            string e = BowledBalls[last];
            int was = e == "nb" ? 1 : int.Parse(e.Substring(0, e.Length - 2));
            BowledBalls[last] = $"{was + runs}nb";
        }
        else
            BowledBalls.Add(runs == 0 ? "nb" : $"{1 + runs}nb");
        NoBalls.Scored(true);
    }

    /// Every line on the board shrinks to stay inside its box rather than run past the board's edge
    /// (2026-11-08: "0.0 / 450 ov" in a Test, a long bowler's name).
    private void KeepInside()
    {
        foreach (TMPro.TextMeshProUGUI t in new[] { txtScore, txtRunRate, txtBatsman1, txtBatsman2, txtBatsman1Score,
                                                    txtBatsman2Score, txtOvers, txtBowler, txtBowlerStyle, txtBalls })
        {
            if (t == null || t.enableAutoSizing)
                continue;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = t.fontSize * 0.5f;
            t.enableAutoSizing = true;
        }
    }
}
