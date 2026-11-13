using System;

/// <summary>
/// A career match that never finished: the app quit from the Quest's menu, the battery died, the
/// headset slept and the OS killed the game. OnDestroy is not promised on Android, so the match
/// cannot rely on its own "left early" path; instead the career save says a match is in progress
/// (CareerProfile.pending, written as it begins and kept in step every ball he faces), and the next
/// time the career is opened it is recorded as he left it - retired out, the rest simulated - just
/// as leaving through the pause menu would. Without this, quitting after a cheap dismissal would
/// wipe the match: a fresh toss, a fresh target, no duck in the book.
/// Since 2026-11-09 a match is continued instead (PendingMatch.resumable, the hub's Continue match):
/// this records only a save from before then.
/// </summary>
public static class CareerMatchRecovery
{
    /// A match is starting: write it down before its scene loads.
    public static void Begin(CareerProfile p, MatchPlan plan)
    {
        if (p == null || plan == null)
            return;
        p.pending = new PendingMatch { active = true, plan = plan, resumable = true };
    }

    /// The innings as it stands (CareerMatch, every frame; saved with the next ball).
    public static void Note(CareerProfile p, MatchState s)
    {
        PendingMatch m = p?.pending;
        if (m == null || !m.active || s == null)
            return;
        m.teamRuns = s.runs; m.teamWickets = s.wickets; m.teamBalls = s.balls;
        // The record moments (phase 7), so a match finished without him keeps his fifty and his six.
        m.ballsToFifty = s.ballsToFifty; m.ballsToHundred = s.ballsToHundred;
        m.longestSix = s.longestSix; m.sixStamp = s.sixStamp ?? "";
        MatchBatter you = s.You;
        if (you == null)
            return;
        m.runs = you.runs; m.balls = you.balls; m.fours = you.fours; m.sixes = you.sixes;
        m.batted = you.batted; m.isOut = you.isOut; m.how = you.how ?? "";
    }

    /// Recorded (or abandoned on purpose): nothing pending.
    public static void Clear(CareerProfile p)
    {
        if (p?.pending != null)
            p.pending.active = false;
    }

    /// The match left unfinished last time, recorded now. True when there was one.
    public static bool Resolve(CareerProfile p)
    {
        PendingMatch m = p?.pending;
        // A match that can be continued is the hub's Continue match, not recorded here.
        if (m == null || !m.active || m.plan == null || m.resumable || CareerMatchSession.Active)
            return false;
        MatchPlan plan = m.plan;
        m.active = false;
        MatchState s = FromSnapshot(plan, m);
        var rng = new Random(plan.seed ^ 0x51C0DE);
        if (!CareerMatchLogic.InningsDone(s))
        {
            CareerMatchLogic.Retire(s);
            CareerMatchLogic.Complete(s, plan.bowling, plan.bowlers, Fielders(plan), rng);
        }
        InningsCard theirs = plan.batFirst
            ? CareerSim.Innings(CareerMatchSession.TeamOf(plan, false), CareerMatchSession.TeamOf(plan, true),
                                plan.overs, s.runs + 1, plan.bowling, new Random(plan.seed ^ 0x6C1E2D))
            : plan.theirs;
        CareerMatchResult result = CareerMatchLogic.Build(s, plan, theirs, true);
        CareerProgress.RecordMatch(p, plan, result, new Random(plan.seed ^ 0x2F6B5A1));
        p.AddMessage(new CareerMessage
        {
            kind = eMessageKind.Info, from = "Captain", title = "The match you left",
            body = $"The match against {plan.opponentName} was finished without you: {result.result}. " +
                   "Leaving a match counts as retiring out.",
            date = CareerProgressMath.Today(),
        });
        return true;
    }

    /// The batting side as it was when the match stopped: the score, the batters out before him,
    /// his own innings, and who is at the crease.
    public static MatchState FromSnapshot(MatchPlan plan, PendingMatch m)
    {
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        s.runs = m.teamRuns; s.wickets = Math.Min(10, m.teamWickets); s.balls = m.teamBalls;
        s.ballsToFifty = m.ballsToFifty; s.ballsToHundred = m.ballsToHundred;
        s.longestSix = m.longestSix; s.sixStamp = m.sixStamp ?? "";
        int you = s.YouIndex;
        foreach (MatchBatter b in s.batters)
            b.batted = false;
        MatchBatter y = s.At(you);
        if (y != null)
        {
            y.runs = m.runs; y.balls = m.balls; y.fours = m.fours; y.sixes = m.sixes;
            y.batted = m.batted; y.isOut = m.isOut; y.how = m.how ?? "";
        }
        // The others out in order, then the two at the crease: him if he is in, and the next in order.
        int othersOut = s.wickets - (m.isOut ? 1 : 0);
        int first = -1, second = -1;
        for (int i = 0; i < s.batters.Count; i++)
        {
            if (i == you) continue;
            if (othersOut > 0) { s.batters[i].batted = s.batters[i].isOut = true; s.batters[i].how = "out"; othersOut--; continue; }
            if (first < 0) first = i; else if (second < 0) second = i;
        }
        bool youIn = y != null && y.batted && !y.isOut;
        s.striker = youIn ? you : first;
        s.runner = youIn ? first : second;
        if (s.striker >= 0) s.batters[s.striker].batted = true;
        if (s.runner >= 0) s.batters[s.runner].batted = true;
        return s;
    }

    private static System.Collections.Generic.List<string> Fielders(MatchPlan plan)
    {
        var names = new System.Collections.Generic.List<string>();
        Team t = CareerMatchSession.TeamOf(plan, false);
        if (t?.players != null)
            foreach (SquadPlayer sp in t.players)
                if (sp != null && !string.IsNullOrEmpty(sp.name)) names.Add(sp.name);
        return names;
    }
}
