using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scoreboard into and out of a MatchSnapshot (a quick-play match saved between balls and
/// continued later: QuickPlayAutosave). Restore runs after HUD.Start's Reset, like the career's
/// roster (HUDCareer.ApplyCareerRoster): the same eleven and eight by index, with their figures,
/// the batters in, the bowler on and his over, and the captain's record of the overs.
/// </summary>
public partial class HUD
{
    public void Capture(MatchSnapshot s, IDictionary<Batsman, string> outs)
    {
        s.runs = Runs; s.wickets = Wickets; s.overs = Overs; s.balls = Balls; s.totalOvers = TotalOvers;
        s.bowledBalls = new List<string>(BowledBalls);
        s.batters.Clear();
        foreach (Batsman b in Batsmen)
            s.batters.Add(new MatchSnapshot.BatterLine
            {
                name = b.Name, runs = b.Runs, balls = b.Balls,
                howOut = outs != null && outs.TryGetValue(b, out string how) ? how : "",
            });
        s.striker = Batsmen.IndexOf(CurrentBatsman);
        s.runner = Batsmen.IndexOf(CurrentRunner);
        s.nextBatter = NextBatsmanIndex;
        s.bowlers.Clear();
        foreach (Bowler b in Bowlers)
            s.bowlers.Add(new MatchSnapshot.BowlerLine { name = b.Name, balls = b.Balls, conceded = b.Conceded, wickets = b.Wickets });
        s.bowler = Bowlers.IndexOf(CurrentBowler);
        s.nextBowler = NextBowlerIndex;
        s.overBowlers.Clear();
        foreach (Bowler b in BowlingAttack.OversSoFar)
            s.overBowlers.Add(Bowlers.IndexOf(b));
        s.nextFreeHit = NoBalls.NextIsFreeHit;
        s.lastDismissal = LastDismissal;
    }

    /// The saved innings back on the board. False (and nothing changed) if it does not fit this
    /// side (a save from another version of the lists).
    public bool Restore(MatchSnapshot s)
    {
        if (s == null || s.batters.Count != Batsmen.Count || s.bowlers.Count != Bowlers.Count ||
            s.striker < 0 || s.striker >= Batsmen.Count || s.runner < 0 || s.runner >= Batsmen.Count ||
            s.bowler < 0 || s.bowler >= Bowlers.Count)
            return false;
        Runs = s.runs; Wickets = s.wickets; Overs = s.overs; Balls = s.balls; TotalOvers = s.totalOvers;
        BowledBalls = new List<string>(s.bowledBalls);
        for (int i = 0; i < Batsmen.Count; i++)
        {
            Batsmen[i].Runs = s.batters[i].runs;
            Batsmen[i].Balls = s.batters[i].balls;
        }
        CurrentBatsman = Batsmen[s.striker];
        CurrentRunner = Batsmen[s.runner];
        NextBatsmanIndex = s.nextBatter;
        for (int i = 0; i < Bowlers.Count; i++)
        {
            Bowlers[i].Balls = s.bowlers[i].balls;
            Bowlers[i].Conceded = s.bowlers[i].conceded;
            Bowlers[i].Wickets = s.bowlers[i].wickets;
        }
        BowlingAttack.NewInnings();
        foreach (int i in s.overBowlers)
            if (i >= 0 && i < Bowlers.Count)
                BowlingAttack.OverDone(Bowlers[i]);
        NoBalls.NewInnings();
        NoBalls.OweFreeHit(s.nextFreeHit);
        NextBowlerIndex = s.nextBowler;
        SetBowler(s.bowler);
        UpdateKeeperPosition();
        keeperMovePending = false;
        LastDismissal = s.lastDismissal;
        // The scorecard's dismissals (BattingCardFeed listens to nothing else for them).
        for (int i = 0; i < Batsmen.Count; i++)
            if (!string.IsNullOrEmpty(s.batters[i].howOut))
                BatterOut?.Invoke(Batsmen[i], s.batters[i].howOut);
        StrikeChanged?.Invoke(CurrentBatsman);
        UpdateUI();
        return true;
    }
}
