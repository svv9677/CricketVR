using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quick play's Test match (MatchFormat.Test; 2026-11-06: "add a test match mode in quick play").
/// Two innings a side, no over limit, five days (MatchFormat.TestMatchOvers). The player's innings
/// are played here, each as an innings of MatchFormat.TestInningsOvers (no limit reached); the
/// AI's are simulated at Test tempo (AIInnings.Tempo.Test) and can declare. Who bats first is Match
/// Setup's choice ("Bat first" / "Chase": the AI first).
///
///   The player's innings ends all out, by declaring (Declare on the next-ball card), when the
///   fourth-innings target is reached, or when the five days run out.
///   Between innings: a card with every innings so far and what comes next; Continue (A) starts
///   the player's next innings (HUD.Reset), the AI's being simulated before it.
///   The result: the side batting last reaching its target wins by its wickets in hand; bowled
///   out short, it loses by the runs (level: a tie); a side bowled out twice still behind the
///   other's first innings loses by an innings; the five days out, a draw. No follow-on.
/// </summary>
public partial class MatchFlow
{
    /// This match is a Test (quick play only).
    public static bool Test { get; private set; }

    private sealed class Innings
    {
        public bool yours, declared;
        public int runs, wickets, balls;
        public string Text => declared ? $"{runs}/{wickets} dec" : wickets >= 10 ? $"{runs}" : $"{runs}/{wickets}";
    }

    private readonly List<Innings> innings = new List<Innings>();
    private bool youFirst, declarePending, betweenInnings;
    /// Legal balls in the match before the innings being played now.
    private int matchBalls;

    /// The player's live innings can be declared (Declare on the next-ball card).
    public static bool CanDeclare => Test && instance != null && !Over && !instance.betweenInnings;

    /// Declare the player's innings now (between balls).
    public static void Declare()
    {
        if (!CanDeclare)
            return;
        instance.declarePending = true;
        instance.Check();
    }

    private static bool IsTestMatch() =>
        !CareerMatchSession.Active && MatchFormat.IsTest;

    private void TestStart()
    {
        Test = true;
        innings.Clear();
        matchBalls = 0;
        youFirst = MatchSetup.Type != eMatchType.Chase;
        if (!youFirst)
            SimulateAI();
        ApplyTestOvers();
    }

    /// Each of the player's innings is a Test innings: HUD's own overs are the limited-overs ones.
    private static void ApplyTestOvers()
    {
        HUD hud = HUD.Instance;
        if (hud != null && hud.TotalOvers != MatchFormat.TestInningsOvers)
            hud.TotalOvers = MatchFormat.TestInningsOvers;
    }

    private bool Yours(int n) => youFirst ? n % 2 == 0 : n % 2 == 1;

    private int Total(bool yours)
    {
        int t = 0;
        foreach (Innings i in innings)
            if (i.yours == yours) t += i.runs;
        return t;
    }

    /// What the side batting fourth needs to win.
    private int FourthTarget()
    {
        bool chaser = Yours(3);
        return Total(!chaser) - Total(chaser) + 1;
    }

    private void TestCheck(HUD hud)
    {
        if (betweenInnings)
            return;
        ApplyTestOvers();
        UpdateTestStatus(hud);
        int live = hud.Overs * 6 + hud.Balls;
        bool fourth = innings.Count == 3;
        bool done = hud.Wickets >= 10 || declarePending || (fourth && hud.Runs >= FourthTarget()) ||
                    matchBalls + live >= MatchFormat.TestMatchOvers * 6;
        if (done)
            EndLiveInnings(hud);
    }

    private void EndLiveInnings(HUD hud)
    {
        innings.Add(new Innings { yours = true, runs = hud.Runs, wickets = hud.Wickets, balls = hud.Overs * 6 + hud.Balls,
                                  declared = declarePending && hud.Wickets < 10 });
        matchBalls += hud.Overs * 6 + hud.Balls;
        declarePending = false;
        if (Decided(out string result, out bool won, out bool tied, out bool drawn))
        {
            FinishTest(hud, result, won, tied, drawn);
            return;
        }
        if (innings.Count < 4 && !Yours(innings.Count))
            SimulateAI();
        if (Decided(out result, out won, out tied, out drawn))
        {
            FinishTest(hud, result, won, tied, drawn);
            return;
        }
        ShowBetweenInnings();
    }

    /// The AI's next innings at Test tempo: declaring on a big first-innings total, or once its
    /// lead batting third is out of reach; chasing in the fourth.
    private void SimulateAI()
    {
        int n = innings.Count;
        int oversLeft = Mathf.Max(0, MatchFormat.TestMatchOvers - matchBalls / 6);
        int target = n == 3 ? FourthTarget() : 0;
        int declareAt = 0;
        if (n == 0)
            declareAt = 520 + rng.Next(0, 80);
        else if (n == 2)
            declareAt = Mathf.Max(80, Total(true) - Total(false) + 300 + rng.Next(0, 80));
        AIInnings.Result r = AIInnings.Play(oversLeft, target, MatchSetup.Bowling, rng, AIInnings.Tempo.Test, declareAt);
        innings.Add(new Innings { yours = false, runs = r.runs, wickets = r.wickets, balls = r.balls, declared = r.declared });
        matchBalls += r.balls;
        Debug.Log($"[MatchFlow] Test: the AI's innings {n + 1}: {innings[n].Text} in {r.OversText} overs");
    }

    /// The match is over after the innings so far, and how.
    private bool Decided(out string result, out bool won, out bool tied, out bool drawn)
    {
        result = null; won = tied = drawn = false;
        int n = innings.Count;
        // Two innings done (bowled out, or declared) and still behind the other side's one: an
        // innings defeat - there is nothing for them to chase.
        if (n == 3)
        {
            bool third = innings[2].yours;
            int behind = Total(!third) - Total(third);
            if (behind > 0)
            {
                won = !third;
                result = $"{(won ? YourName : OpponentName)} won by an innings and {behind} run{(behind == 1 ? "" : "s")}";
                return true;
            }
        }
        if (n == 4)
        {
            Innings last = innings[3];
            int target = Total(!last.yours) - (Total(last.yours) - last.runs) + 1;
            if (last.runs >= target)
            {
                won = last.yours;
                int left = 10 - last.wickets;
                result = $"{(won ? YourName : OpponentName)} won by {left} wicket{(left == 1 ? "" : "s")}";
                return true;
            }
            if (last.wickets >= 10)
            {
                if (last.runs == target - 1) { tied = true; result = "Match tied"; return true; }
                won = !last.yours;
                int by = target - 1 - last.runs;
                result = $"{(won ? YourName : OpponentName)} won by {by} run{(by == 1 ? "" : "s")}";
                return true;
            }
        }
        if (matchBalls >= MatchFormat.TestMatchOvers * 6)
        {
            drawn = true;
            result = "Match drawn";
            return true;
        }
        return false;
    }

    /// "You 245 & 180   AI 312 & 114/4".
    private string Scorecard()
    {
        string Side(bool yours)
        {
            var parts = new List<string>();
            foreach (Innings i in innings)
                if (i.yours == yours) parts.Add(i.Text);
            return parts.Count == 0 ? "yet to bat" : string.Join(" & ", parts);
        }
        return $"{YourName} {Side(true)}\n{OpponentName} {Side(false)}";
    }

    private void UpdateTestStatus(HUD hud)
    {
        int live = hud.Overs * 6 + hud.Balls;
        int day = 1 + (matchBalls + live) / (MatchFormat.OversPerDay * 6);
        int n = innings.Count;
        string where;
        if (n == 3)
        {
            Target = FourthTarget();
            where = $"Need {Mathf.Max(0, Target - hud.Runs)} to win";
        }
        else if (n == 0)
            where = "First innings";
        else
        {
            int lead = Total(true) + hud.Runs - Total(false);
            where = lead > 0 ? $"Lead by {lead}" : lead < 0 ? $"Trail by {-lead}" : "Scores level";
        }
        Status = $"Day {Mathf.Min(day, 5)} · {where}";
        hud.UpdateUI();
    }

    private void ShowBetweenInnings()
    {
        betweenInnings = true;
        MatchGate.Blocking = true;
        if (!BuildCard())
        {
            ContinueTest();
            return;
        }
        int next = innings.Count;
        string what = next == 3 ? $"You need {FourthTarget()} to win" : "Your second innings";
        heading.text = "<color=#3B82F6>END OF INNINGS</color>";
        body.text = $"{Scorecard()}\n{what}\n<size=70%><color=#98A2B3>Day {1 + matchBalls / (MatchFormat.OversPerDay * 6)}</color></size>";
        SetCardButton("Continue  <size=75%>(A)</size>");
        overAt = Time.time;
        Place();
        card.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
    }

    /// The player's next innings: a fresh scoreboard (HUD.Reset) as a Test innings.
    private void ContinueTest()
    {
        betweenInnings = false;
        MatchGate.Blocking = false;
        if (card != null)
            card.SetActive(false);
        HUD hud = HUD.Instance;
        if (hud == null)
            return;
        hud.Reset(MatchFormat.TestInningsOvers);
        UpdateTestStatus(hud);
    }

    private void FinishTest(HUD hud, string result, bool won, bool tied, bool drawn)
    {
        Over = true;
        overAt = Time.time;
        betweenInnings = false;
        MatchGate.Blocking = false;
        MatchHistory.Record r = Record(hud, result);
        r.test = true;
        r.drawn = drawn;
        r.won = won;
        r.tied = tied;
        r.summary = Scorecard().Replace("\n", "   ");
        if (KeepHistory)
            MatchHistory.Add(r);
        Status = result;
        hud.UpdateUI();
        Debug.Log($"[MatchFlow] Test: {r.summary}: {result}");
        Ended?.Invoke(r, null, result);
        if (!BuildCard())
            return;
        heading.text = won ? "<color=#22C55E>YOU WON</color>" : tied ? "<color=#3B82F6>MATCH TIED</color>"
                     : drawn ? "<color=#3B82F6>MATCH DRAWN</color>" : "<color=#FF6B4A>MATCH LOST</color>";
        body.text = $"{Scorecard()}\n{result}";
        SetCardButton("Home  <size=75%>(A)</size>");
        Place();
        card.SetActive(true);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
    }
}
