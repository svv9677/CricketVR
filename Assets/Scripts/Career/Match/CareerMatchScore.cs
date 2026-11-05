using System.Collections.Generic;
using UnityEngine;

/// CareerMatch's copy of the scoreboard: the innings kept in step with HUD, and the small questions
/// asked of it (who is on strike, is the innings over, a boundary to someone's name).
public partial class CareerMatch
{
    // ---- The scoreboard -------------------------------------------------------------------------

    /// The career's copy of the innings in step with HUD (what is left to finish from if the scene
    /// goes): the score, each batter's runs and balls, who is in, and what HUD does not keep.
    private void Sync(HUD hud)
    {
        if (!rosterApplied || state == null)
            return;
        state.runs = hud.Runs; state.wickets = hud.Wickets; state.balls = Bowled(hud);
        state.totalBalls = hud.TotalOvers * 6; state.target = MatchFlow.Target;
        int n = Mathf.Min(hud.Batsmen.Count, state.batters.Count);
        for (int i = 0; i < n; i++)
        {
            Batsman b = hud.Batsmen[i];
            MatchBatter m = state.batters[i];
            if (b == null || m == null)
                continue;
            m.name = b.Name; m.runs = b.Runs; m.balls = b.Balls; m.rating = b.Rating;
            m.batted |= b == hud.CurrentBatsman || b == hud.CurrentRunner || b.Balls > 0;
            if (outs.TryGetValue(b, out string how))
            {
                m.isOut = m.batted = true;
                m.how = how;
            }
            m.fours = fours.TryGetValue(b, out int f) ? f : 0;
            m.sixes = sixes.TryGetValue(b, out int x) ? x : 0;
        }
        state.striker = hud.Batsmen.IndexOf(hud.CurrentBatsman);
        state.runner = hud.Batsmen.IndexOf(hud.CurrentRunner);
        CareerMatchLogic.ObserveOvers(state);
        CareerMatchRecovery.Note(CareerSaves.Current, state);   // saved with his next ball
    }

    private static int Bowled(HUD hud) => hud.Overs * 6 + hud.Balls;
    private static bool InningsOver(HUD hud) =>
        CareerMatchLogic.InningsDone(hud.Runs, hud.Wickets, Bowled(hud), hud.TotalOvers * 6, MatchFlow.Target);

    private static bool StrikerIsYou(HUD hud) => hud.CurrentBatsman != null && hud.CurrentBatsman.IsHuman;
    private static bool YouAtCrease(HUD hud) =>
        StrikerIsYou(hud) || (hud.CurrentRunner != null && hud.CurrentRunner.IsHuman);

    /// A four or a six (runs 4 or 6) to his name; anything else is not a boundary.
    private void CountBoundary(Batsman b, int runs)
    {
        Dictionary<Batsman, int> d = runs == 4 ? fours : runs == 6 ? sixes : null;
        if (b != null && d != null)
            d[b] = d.TryGetValue(b, out int n) ? n + 1 : 1;
    }
}
