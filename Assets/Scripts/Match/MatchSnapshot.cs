using System.Collections.Generic;

/// <summary>
/// A quick-play match part-way through, as saved between balls (QuickPlayAutosave) and put back
/// when the player continues it (2026-11-09: "If I'm midway through a match, I should be able to
/// exit and come back to it and continue"): the scoreboard (HUD), the captain's record of who
/// bowled which over (BowlingAttack), a free hit owed (NoBalls), how each batter got out (the
/// scorecard), and the match around the innings (MatchFlow: the target and the AI's first innings
/// in a chase; a Test's innings so far).
///
/// What is not kept is what a ball in the air holds: a match is saved only between balls, and one
/// left mid-ball continues from the ball before it.
/// </summary>
[System.Serializable]
public class MatchSnapshot
{
    public const int Version = 1;
    public int version = Version;

    // ---- The innings being played (HUD) ----------------------------------------------------
    public int runs, wickets, overs, balls, totalOvers;
    public List<string> bowledBalls = new List<string>();
    public List<BatterLine> batters = new List<BatterLine>();
    public int striker, runner, nextBatter;
    public List<BowlerLine> bowlers = new List<BowlerLine>();
    public int bowler, nextBowler;
    /// Who bowled each over so far, by index in bowlers.
    public List<int> overBowlers = new List<int>();
    public bool nextFreeHit;
    public string lastDismissal;

    // ---- The match around it (MatchFlow) ----------------------------------------------------
    public int target;
    public bool hasAIFirst;
    public AILine aiFirst = new AILine();
    public bool test, youFirst, betweenInnings;
    public int matchBalls;
    public List<InningsLine> innings = new List<InningsLine>();

    [System.Serializable]
    public class BatterLine
    {
        public string name;
        public int runs, balls;
        /// How he got out ("c Point b Khan"), or empty.
        public string howOut;
    }

    [System.Serializable]
    public class BowlerLine
    {
        public string name;
        public int balls, conceded, wickets;
    }

    [System.Serializable]
    public class InningsLine
    {
        public bool yours, declared;
        public int runs, wickets, balls;
    }

    /// AIInnings.Result, whose over lists are read-only to JsonUtility.
    [System.Serializable]
    public class AILine
    {
        public int runs, wickets, balls, overs, target;
        public bool declared;
        public List<int> overRuns = new List<int>(), overWickets = new List<int>();

        public static AILine From(AIInnings.Result r)
        {
            var a = new AILine { runs = r.runs, wickets = r.wickets, balls = r.balls, overs = r.overs, target = r.target, declared = r.declared };
            a.overRuns.AddRange(r.overRuns);
            a.overWickets.AddRange(r.overWickets);
            return a;
        }

        public AIInnings.Result ToResult()
        {
            var r = new AIInnings.Result { runs = runs, wickets = wickets, balls = balls, overs = overs, target = target, declared = declared };
            r.overRuns.AddRange(overRuns);
            r.overWickets.AddRange(overWickets);
            return r;
        }
    }

    /// "45/2 (6.3 ov)".
    public string ScoreText => $"{runs}/{wickets} ({overs}.{balls} ov)";
}
