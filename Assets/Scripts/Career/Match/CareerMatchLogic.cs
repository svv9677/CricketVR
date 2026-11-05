using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// One batter of the career player's side in a career match, as the career keeps him: what the
/// scoreboard says (HUD) plus what it does not (how he was out, his boundaries, that he came in).
public class MatchBatter
{
    public string name = "";
    public float rating = 40f;
    public int runs, balls, fours, sixes;
    /// Came to the crease: faced a ball, or stood at either end.
    public bool batted;
    public bool isOut, isYou, retired;
    public string how = "";
}

/// <summary>
/// The career player's side's innings as it stands: the batters in order, who is at which end, the
/// score, and the runs and wickets of every over done. CareerMatch keeps it in step with HUD every
/// frame, so it is there to finish from if the match is left half way (the scene is going away and
/// HUD with it), and CareerMatchLogic plays the rest of an innings on it with no scene at all.
/// </summary>
public class MatchState
{
    public readonly List<MatchBatter> batters = new List<MatchBatter>();
    /// Indexes into batters (-1: nobody left to come in).
    public int striker, runner = 1;
    public int runs, wickets, balls, totalBalls, target;
    public readonly List<int> overRuns = new List<int>(), overWickets = new List<int>();
    /// The score when the over now being bowled began.
    public int overStartRuns, overStartWickets;

    public MatchBatter Striker => At(striker);
    public MatchBatter Runner => At(runner);
    public int YouIndex => batters.FindIndex(b => b != null && b.isYou);
    public MatchBatter You => At(YouIndex);

    public MatchBatter At(int i) => i >= 0 && i < batters.Count ? batters[i] : null;
}

/// <summary>
/// The parts of a career match that need no scene, so they are tested in EditMode (CareerMatchTests):
/// the batting side's state from the plan, scoring a simulated ball on it the way HUD scores one,
/// when a run of simulated balls stops, finishing an innings the career player left half way
/// (retired out - it counts as out), the match result from the two innings, and every line of text
/// the career match shows. The balls themselves come from CareerSim; nothing here makes them easier
/// or harder.
/// </summary>
public static class CareerMatchLogic
{
    public const string NotOut = "not out", RetiredOut = "retired out", DidNotBat = "did not bat";

    /// More simulated balls than this in one go and something is wrong (the innings' legal balls
    /// left, with room for wides): the batch stops rather than spin.
    public static int BatchCap(int totalBalls, int ballsBowled) => Math.Max(0, totalBalls - ballsBowled) * 2 + 30;

    // ---- The state ------------------------------------------------------------------------------

    /// The side's innings before a ball: the plan's batting order (the career player at his slot), the
    /// first two at the crease.
    public static MatchState NewState(MatchPlan plan, int totalBalls, int target)
    {
        var s = new MatchState { totalBalls = totalBalls, target = target, striker = 0, runner = 1 };
        if (plan?.order == null)
            return s;
        for (int i = 0; i < plan.order.Count; i++)
        {
            float rating = plan.orderRatings != null && i < plan.orderRatings.Count ? plan.orderRatings[i] : 40f;
            s.batters.Add(new MatchBatter { name = plan.order[i] ?? "", rating = rating, isYou = i == plan.slot });
        }
        if (s.batters.Count > 0) s.batters[0].batted = true;
        if (s.batters.Count > 1) s.batters[1].batted = true;
        if (s.batters.Count < 2) s.runner = -1;
        if (s.batters.Count < 1) s.striker = -1;
        return s;
    }

    /// The innings is over: all out, the overs bowled, or the target reached.
    public static bool InningsDone(int runs, int wickets, int balls, int totalBalls, int target) =>
        wickets >= 10 || (totalBalls > 0 && balls >= totalBalls) || (target > 0 && runs >= target);

    public static bool InningsDone(MatchState s) =>
        s == null || s.striker < 0 || s.runner < 0 || InningsDone(s.runs, s.wickets, s.balls, s.totalBalls, s.target);

    /// A run of simulated balls stops when the career player is on strike (his ball is played, not
    /// simulated - unless he is out, and then the rest of the innings is simulated), when the innings
    /// is over, or at the cap.
    public static bool StopBatch(bool youOnStrike, bool youOut, bool inningsOver, int simulated, int cap) =>
        inningsOver || simulated >= cap || (youOnStrike && !youOut);

    /// Overs completed since the last look go into the over-by-over runs and wickets.
    public static void ObserveOvers(MatchState s)
    {
        while (s.overRuns.Count < s.balls / 6)
        {
            s.overRuns.Add(s.runs - s.overStartRuns);
            s.overWickets.Add(s.wickets - s.overStartWickets);
            s.overStartRuns = s.runs;
            s.overStartWickets = s.wickets;
        }
    }

    /// The innings is over: the over it ended in, if it ended part way through one, goes in too.
    public static void CloseOvers(MatchState s)
    {
        ObserveOvers(s);
        bool partOver = s.balls > s.overRuns.Count * 6 || s.runs != s.overStartRuns || s.wickets != s.overStartWickets;
        if (!partOver)
            return;
        s.overRuns.Add(s.runs - s.overStartRuns);
        s.overWickets.Add(s.wickets - s.overStartWickets);
        s.overStartRuns = s.runs;
        s.overStartWickets = s.wickets;
    }

    // ---- Scoring a simulated ball (as HUD.AddRuns does) -----------------------------------------

    /// One ball to the striker: CareerSim.Wicket, 0..6, or CareerSim.Wide. A wide is the team's run
    /// and not a ball of the over; runs are the striker's; odd runs and the end of an over change ends;
    /// a wicket brings the next batter in at the striker's end.
    public static void Score(MatchState s, int code, string how)
    {
        MatchBatter b = s?.Striker;
        if (b == null)
            return;
        if (code == CareerSim.Wide)
        {
            s.runs++;
            return;
        }
        b.balls++;
        b.batted = true;
        if (code == CareerSim.Wicket)
        {
            b.isOut = true;
            b.how = string.IsNullOrEmpty(how) ? "out" : how;
            s.wickets++;
            s.striker = NextIn(s);
            LegalBall(s);
            return;
        }
        int runs = Math.Max(0, Math.Min(6, code));
        b.runs += runs;
        s.runs += runs;
        if (runs == 4) b.fours++;
        if (runs == 6) b.sixes++;
        if (runs % 2 == 1)
            Swap(s);
        LegalBall(s);
    }

    private static void LegalBall(MatchState s)
    {
        s.balls++;
        if (s.balls % 6 == 0)
        {
            Swap(s);
            ObserveOvers(s);
        }
    }

    private static void Swap(MatchState s)
    {
        int t = s.striker;
        s.striker = s.runner;
        s.runner = t;
    }

    /// The next in the order who has not batted (the career player too, at his slot), or -1.
    public static int NextIn(MatchState s)
    {
        for (int i = 0; i < s.batters.Count; i++)
        {
            MatchBatter b = s.batters[i];
            if (b != null && !b.batted && !b.isOut && i != s.striker && i != s.runner)
            {
                b.batted = true;
                return i;
            }
        }
        return -1;
    }

    // ---- Leaving early --------------------------------------------------------------------------

    /// The career player walked off before the innings ended: retired out, which counts as out (a
    /// way out of a bad match must cost what getting out costs). Not yet in, he retires at once - he
    /// is not there to bat. The next batter takes his end. Nothing happens if he is already out.
    public static void Retire(MatchState s)
    {
        int y = s?.YouIndex ?? -1;
        MatchBatter you = s?.At(y);
        if (you == null || you.isOut)
            return;
        you.isOut = true;
        you.retired = true;
        you.batted = true;
        you.how = RetiredOut;
        s.wickets++;
        if (y == s.striker)
            s.striker = NextIn(s);
        else if (y == s.runner)
            s.runner = NextIn(s);
    }

    /// Play the rest of the innings by simulation (the bowlers in turn, an over each; catchers from
    /// `fielders`). Capped, so a broken simulation cannot hang the match.
    public static void Complete(MatchState s, eBowlingLevel level, IList<string> bowlers, IList<string> fielders, Random rng)
    {
        if (s == null)
            return;
        int cap = BatchCap(s.totalBalls, s.balls);
        for (int n = 0; n < cap && !InningsDone(s); n++)
        {
            float aggression = CareerSim.Aggression(s.runs, s.wickets, s.balls, s.totalBalls, s.target);
            int code = CareerSim.Ball(s.Striker.rating, level, aggression, rng);
            string how = null;
            if (code == CareerSim.Wicket)
            {
                string bowler = BowlerFor(bowlers, s.balls);
                how = CareerSim.HowOut(bowler, PickFielder(fielders, bowler, rng), rng);
            }
            Score(s, code, how);
        }
    }

    /// Who bowls the over containing ball `balls` (0-based) in a simulated innings: each in turn.
    public static string BowlerFor(IList<string> bowlers, int balls)
    {
        if (bowlers == null || bowlers.Count == 0)
            return "the bowler";
        return bowlers[(balls / 6) % bowlers.Count] ?? "the bowler";
    }

    /// A catcher for a simulated wicket: one of the fielding side, not the bowler himself.
    public static string PickFielder(IList<string> names, string bowler, Random rng)
    {
        if (names == null || names.Count == 0)
            return "a fielder";
        for (int tries = 0; tries < 6; tries++)
        {
            string n = names[rng.Next(names.Count)];
            if (!string.IsNullOrEmpty(n) && n != bowler)
                return n;
        }
        return names[0] ?? "a fielder";
    }

    // ---- The result -----------------------------------------------------------------------------

    /// The side's innings as a card: the total, the overs, and a line for everyone who batted.
    public static InningsCard Card(MatchState s, string team)
    {
        CloseOvers(s);
        var c = new InningsCard
        {
            team = team ?? "", runs = s.runs, wickets = s.wickets, balls = s.balls,
            overs = s.totalBalls / 6, target = s.target,
        };
        c.overRuns.AddRange(s.overRuns);
        c.overWickets.AddRange(s.overWickets);
        foreach (MatchBatter b in s.batters)
        {
            if (b == null || !b.batted)
                continue;
            c.batting.Add(new BatterLine
            {
                name = b.name, runs = b.runs, balls = b.balls, fours = b.fours, sixes = b.sixes,
                batted = true, isOut = b.isOut, isYou = b.isYou, how = b.isOut ? b.how : NotOut,
            });
        }
        return c;
    }

    /// The match as the career records it: his innings, both cards, who won.
    public static CareerMatchResult Build(MatchState s, MatchPlan plan, InningsCard theirs, bool abandoned)
    {
        InningsCard yours = Card(s, plan?.yourTeamName);
        var r = new CareerMatchResult { yours = yours, theirs = theirs, abandoned = abandoned };
        MatchBatter you = s.You;
        if (you != null)
        {
            r.runs = you.runs; r.balls = you.balls; r.fours = you.fours; r.sixes = you.sixes;
            r.batted = you.batted; r.isOut = you.isOut; r.retired = you.retired;
            r.how = you.isOut ? you.how : you.batted ? NotOut : DidNotBat;
        }
        r.result = ResultText(yours, theirs, plan == null || plan.batFirst, plan?.yourTeamName, plan?.opponentName,
                              out r.won, out r.tied);
        return r;
    }

    /// "Harbour CC won by 12 runs" / "Rivertown won by 4 wickets (7 balls left)" / "Match tied".
    public static string ResultText(InningsCard yours, InningsCard theirs, bool batFirst, string yourName, string theirName,
                                    out bool won, out bool tied)
    {
        won = tied = false;
        if (yours == null || theirs == null)
            return "";
        InningsCard first = batFirst ? yours : theirs, second = batFirst ? theirs : yours;
        string firstName = Name(batFirst ? yourName : theirName, first), secondName = Name(batFirst ? theirName : yourName, second);
        if (second.runs == first.runs)
        {
            tied = true;
            return "Match tied";
        }
        bool secondWon = second.runs > first.runs;
        won = secondWon != batFirst;
        if (secondWon)
        {
            int wkts = Math.Max(0, 10 - second.wickets), left = Math.Max(0, Math.Max(second.overs, first.overs) * 6 - second.balls);
            return $"{secondName} won by {wkts} wicket{(wkts == 1 ? "" : "s")} ({left} ball{(left == 1 ? "" : "s")} left)";
        }
        int by = first.runs - second.runs;
        return $"{firstName} won by {by} run{(by == 1 ? "" : "s")}";
    }

    private static string Name(string name, InningsCard c) => !string.IsNullOrEmpty(name) ? name : c?.team ?? "";

    /// MatchFlow's AI innings as a card (no batting lines), when the career's own card is missing.
    public static InningsCard FromAI(AIInnings.Result r, string team)
    {
        if (r == null)
            return null;
        var c = new InningsCard { team = team ?? "", runs = r.runs, wickets = r.wickets, balls = r.balls, overs = r.overs, target = r.target };
        c.overRuns.AddRange(r.overRuns);
        c.overWickets.AddRange(r.overWickets);
        return c;
    }

    /// A side made from names alone (eleven, padded with numbered players; batting `ratings` or
    /// `mean`), for a plan whose season is not to hand.
    public static Team StandInTeam(string id, string name, IList<string> names, IList<float> ratings, float mean)
    {
        var t = new Team { id = id ?? "", name = name ?? "", shortName = "" };
        for (int i = 0; i < 11; i++)
        {
            string n = names != null && i < names.Count && !string.IsNullOrEmpty(names[i]) ? names[i] : $"{t.name} {i + 1}".Trim();
            float bat = ratings != null && i < ratings.Count ? ratings[i] : mean;
            t.players.Add(new SquadPlayer { name = n, bat = bat, bowls = i >= 6 });
        }
        return t;
    }

    // ---- The words ------------------------------------------------------------------------------

    public static string OversText(int balls) => $"{Math.Max(0, balls) / 6}.{Math.Max(0, balls) % 6}";

    /// "45/4 after 6.2 overs".
    public static string ScoreAfter(int runs, int wickets, int balls) => $"{runs}/{wickets} after {OversText(balls)} overs";

    /// "You come in at 45/4 (6.2 overs)".
    public static string ComeIn(int runs, int wickets, int balls) => $"You come in at {runs}/{wickets} ({OversText(balls)} overs)";

    public const string AtTheOtherEnd = "While you were at the other end";

    /// "Ravi 18 (11) - out c Khan b Lee", "Ravi 4 (3) - run out", "Ravi 22 (15) not out".
    public static string BatterText(string name, int runs, int balls, bool isOut, string how)
    {
        if (!isOut)
            return $"{name} {runs} ({balls}) {NotOut}";
        string h = string.IsNullOrEmpty(how) ? "out" : how;
        return h.Contains("out") ? $"{name} {runs} ({balls}) - {h}" : $"{name} {runs} ({balls}) - out {h}";
    }

    /// The catch-up card's lines: who fell, how the partner stands, then the score.
    public static string CatchUpBody(IList<string> lines, int runs, int wickets, int balls)
    {
        var sb = new StringBuilder();
        if (lines != null)
            foreach (string l in lines)
                if (!string.IsNullOrEmpty(l))
                    sb.Append(l).Append('\n');
        sb.Append(ScoreAfter(runs, wickets, balls));
        return sb.ToString();
    }

    /// "Your innings: 34 (22), c Khan b Lee" / "Your innings: 34 (22), not out".
    public static string YourInnings(int runs, int balls, bool isOut, string how) =>
        $"Your innings: {runs} ({balls}), {(isOut && !string.IsNullOrEmpty(how) ? how : isOut ? "out" : NotOut)}";

    /// MatchFlow's result card line: "You: 34 (22) - c Khan b Lee", "You: did not bat".
    public static string CardNote(CareerMatchResult r)
    {
        if (r == null || !r.batted)
            return "You: " + DidNotBat;
        return $"You: {r.runs} ({r.balls}) - {(r.isOut ? (string.IsNullOrEmpty(r.how) ? "out" : r.how) : NotOut)}";
    }

    private static readonly Regex TrailingScore = new Regex(@"\s+\d+\s*\(\d+\)\s*$");

    /// How a batter was out from the scorebook's dismissal line ("Ravi c Point b Khan  34 (22)"):
    /// without his name in front or his score behind - "c Point b Khan".
    public static string CleanHow(string name, string dismissal)
    {
        if (string.IsNullOrEmpty(dismissal))
            return "out";
        string h = TrailingScore.Replace(dismissal.Trim(), "").Trim();
        if (!string.IsNullOrEmpty(name) && h.StartsWith(name + " "))
            h = h.Substring(name.Length).Trim();
        return h.Length > 0 ? h : "out";
    }
}
