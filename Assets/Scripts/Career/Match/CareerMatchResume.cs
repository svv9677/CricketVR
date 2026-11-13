using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A career match kept so it can be continued (2026-11-09: "Career manager, I should also be able
/// to resume. If I'm leaving the game, if I just close the game, then it should save"): after every
/// ball, between balls, the scoreboard and the match (MatchSnapshot) and what the career keeps
/// beside them go into the career's pending match and are saved; so do they when the headset is
/// taken off or the app is closed (OnApplicationPause / OnApplicationQuit - OnDestroy is not
/// promised on Android). Leaving through the pause menu no longer retires him: the match stays
/// pending (CareerMatchSession.Suspend) and the hub's Continue match puts it all back here once
/// the career's roster is on the scoreboard (Restore, from ApplyRoster).
/// </summary>
public partial class CareerMatch
{
    private int savedAtBall = -1;

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveResume(true);
    }

    private void OnApplicationQuit() => SaveResume(true);

    /// The match as it stands into the career's pending match, and saved - once per ball unless
    /// `force` (the app going away).
    private void SaveResume(bool force)
    {
        HUD hud = Hud();
        CareerProfile p = CareerSaves.Current;
        PendingMatch m = p?.pending;
        if (ended || !rosterApplied || hud == null || hud.Batsmen == null || m == null || !m.active)
            return;
        int bowled = hud.BowledBalls != null ? hud.BowledBalls.Count : Bowled(hud);
        if (!force && bowled == savedAtBall)
            return;
        savedAtBall = bowled;
        m.resumable = true;
        m.resume = Capture(hud);
        CareerSaves.SaveCurrent();
    }

    private CareerResumeState Capture(HUD hud)
    {
        var r = new CareerResumeState { hasMatch = true };
        hud.Capture(r.match, outs);
        MatchFlow.Capture(r.match);
        foreach (Batsman b in hud.Batsmen)
        {
            r.batterFours.Add(b != null && fours.TryGetValue(b, out int f) ? f : 0);
            r.batterSixes.Add(b != null && sixes.TryGetValue(b, out int x) ? x : 0);
        }
        r.youOut = youOut; r.restDone = restDone; r.introShown = introShown; r.scriptAt = scriptAt;
        if (state != null)
        {
            r.overRuns.AddRange(state.overRuns);
            r.overWickets.AddRange(state.overWickets);
            r.overStartRuns = state.overStartRuns;
            r.overStartWickets = state.overStartWickets;
        }
        return r;
    }

    /// The continued match back: the scoreboard, the match, and the career's own. Called once the
    /// roster is in (HUD.ApplyCareerRoster renames the same eleven and attack it was saved with).
    private void Restore(HUD hud)
    {
        CareerResumeState r = CareerMatchSession.Resuming;
        CareerMatchSession.Resuming = null;
        if (r == null || !r.hasMatch)
            return;
        if (!hud.Restore(r.match) || !MatchFlow.Restore(r.match))
        {
            Debug.LogWarning("[CareerMatch] the saved match did not fit the scoreboard: played from the first ball");
            return;
        }
        for (int i = 0; i < hud.Batsmen.Count; i++)
        {
            Batsman b = hud.Batsmen[i];
            if (i < r.match.batters.Count && !string.IsNullOrEmpty(r.match.batters[i].howOut))
                outs[b] = r.match.batters[i].howOut;
            if (i < r.batterFours.Count && r.batterFours[i] > 0) fours[b] = r.batterFours[i];
            if (i < r.batterSixes.Count && r.batterSixes[i] > 0) sixes[b] = r.batterSixes[i];
        }
        youOut = r.youOut; restDone = r.restDone; introShown = r.introShown; scriptAt = r.scriptAt;
        if (state != null)
        {
            state.overRuns.Clear(); state.overRuns.AddRange(r.overRuns);
            state.overWickets.Clear(); state.overWickets.AddRange(r.overWickets);
            state.overStartRuns = r.overStartRuns;
            state.overStartWickets = r.overStartWickets;
            PendingMatch m = CareerSaves.Current?.pending;
            if (m != null)
            {
                state.ballsToFifty = m.ballsToFifty; state.ballsToHundred = m.ballsToHundred;
                state.longestSix = m.longestSix; state.sixStamp = m.sixStamp ?? "";
            }
        }
        savedAtBall = hud.BowledBalls != null ? hud.BowledBalls.Count : Bowled(hud);
        Debug.Log($"[CareerMatch] continued at {hud.Runs}/{hud.Wickets} after {CareerMatchLogic.OversText(Bowled(hud))}");
    }

    /// Between balls with nothing waiting (his ball next, or the innings done): kept.
    private void KeepBetweenBalls(HUD hud)
    {
        if (main != null && main.gameState == eGameState.InGame_Ready && !MatchFlow.Over)
            SaveResume(false);
    }
}
