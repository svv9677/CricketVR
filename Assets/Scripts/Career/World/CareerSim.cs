using System;
using System.Collections.Generic;

/// <summary>
/// The simulation of everything the career player does not bat himself: his partners' balls, the
/// other side's innings, and the matches between the other sides in his league. Pure and seeded.
/// It never touches the player's own balls - those are played and measured.
///
/// One ball is decided by the batter's edge over the attack: his rating (0..99, the same scale as
/// the career player's measured ratings) against how hard the bowling is (Difficulty by level),
/// and by how hard he is going at it (aggression). A bigger edge means fewer wickets and more
/// boundaries; more aggression means more boundaries and more wickets. The odds started from
/// AIInnings' per-ball table and were tuned so that, over many seeded balls (CareerSimTests;
/// measured 2026-11-05 over 300,000 balls each): a 50 against Medium bowling at normal intent
/// averages ~24.5 at a strike rate ~126, an 80 against Easy ~79 at ~172, and a 20 against Pro
/// ~6.5 at ~80. Wides are 2-4 in 100 balls, fewer from a better attack. Whole innings between
/// sides of a rung (LeagueBuilder.NewTeam) come out at about 57/2 in 6 overs at Club, 85/3 in 10
/// at State and 155/6 in 20 at Pro League and International, the chasing side winning about half.
/// </summary>
public static class CareerSim
{
    /// Scorebook codes, as HUD.AddRuns takes them.
    public const int Wicket = -1, Wide = 10;

    /// How hard the bowling is to score off, by level (Easy .. Pro), on the 0..1 scale of a
    /// batter's rating / 100. A batter rated just above it has a small edge (a normal innings).
    private static readonly float[] Difficulty = { 0.15f, 0.42f, 0.58f, 0.69f };
    /// Wides in a ball, by level: a club attack sprays more.
    private static readonly float[] WideChance = { 0.036f, 0.03f, 0.026f, 0.022f };

    // ---- One ball -------------------------------------------------------------------------------

    /// How hard the batting side is going (0 = survival, 0.5 = normal, 1 = all-out attack) given
    /// the score, the balls gone and left, and the target (0 when setting a total).
    /// Setting a total: a little brisk in the first overs, steady through the middle, then harder
    /// over the last 30% of the innings - harder still with wickets in hand. Chasing: it follows
    /// the asking rate. Either way, a collapse (wickets down well ahead of the stage of the
    /// innings) makes them rebuild, and the tail cannot slog for long.
    public static float Aggression(int runs, int wickets, int ballsBowled, int totalBalls, int target)
    {
        if (totalBalls <= 0) return 0.5f;
        int left = Math.Max(0, totalBalls - ballsBowled);
        float through = Clamp01(ballsBowled / (float)totalBalls);
        int inHand = Math.Max(0, 10 - wickets);
        float a = target > 0 ? ChaseIntent(runs, target, left) : SettingIntent(through, inHand);
        // A short game is a slog from the start: wickets are worth less with fewer balls to bat.
        if (target <= 0) a += 0.15f * Math.Max(0f, 1f - totalBalls / 120f);

        // A normal side is about one down early and six or seven down by the end.
        float behind = wickets - (1f + 6.5f * through);
        if (behind > 1f) a -= Math.Min(0.3f, 0.1f * (behind - 1f));
        if (wickets >= 8) a *= 0.75f;
        return Clamp01(a);
    }

    private static float SettingIntent(float through, int inHand)
    {
        if (through < 0.3f) return 0.52f;
        if (through < 0.7f) return 0.45f;
        float late = (through - 0.7f) / 0.3f;
        return 0.45f + late * (0.25f + 0.04f * inHand);
    }

    private static float ChaseIntent(int runs, int target, int left)
    {
        int need = target - runs;
        if (need <= 0) return 0.5f;
        float perBall = need / (float)Math.Max(1, left);
        // About 7.5 an over is par: above it they push, below it they knock it about.
        return 0.5f + (perBall - 1.25f) * 0.8f;
    }

    /// One ball to a batter of `bat` rating (0..99) against an attack of `level`:
    /// Wicket, 0..6, or Wide.
    public static int Ball(float bat, eBowlingLevel level, float aggression, Random rng)
    {
        int l = Math.Max(0, Math.Min(Difficulty.Length - 1, (int)level));
        if (rng.NextDouble() < WideChance[l]) return Wide;

        float e = Math.Max(0f, Math.Min(99f, bat)) / 100f - Difficulty[l];
        float a = Clamp01(aggression) - 0.5f;
        float wicket = Math.Max(0.004f, Math.Min(0.3f, 0.058f * (float)Math.Exp(-1.5f * e) * (1f + 1.2f * a)));
        float six = Math.Max(0.003f, 0.04f + 0.05f * e + 0.08f * a);
        float four = Math.Max(0.015f, 0.10f + 0.10f * e + 0.08f * a);
        float three = 0.01f;
        float two = Math.Max(0.03f, 0.08f + 0.03f * e);
        float one = Math.Max(0.2f, 0.36f + 0.05f * e - 0.06f * a);

        double x = rng.NextDouble();
        if ((x -= wicket) < 0) return Wicket;
        if ((x -= six) < 0) return 6;
        if ((x -= four) < 0) return 4;
        if ((x -= three) < 0) return 3;
        if ((x -= two) < 0) return 2;
        if ((x -= one) < 0) return 1;
        return 0;
    }

    /// How a simulated batter was out ("c Sharma b Khan", "b Khan", "lbw b Khan", "run out").
    /// Without a keeper's name or the bowler's kind, a catch behind is credited to `fielder` and
    /// there are no stumpings.
    public static string HowOut(string bowler, string fielder, Random rng) => HowOut(bowler, fielder, fielder, false, rng);

    /// The full mix, out of 100 dismissals: caught in the field ~46, caught behind ~12 (fewer off
    /// spin, which brings stumpings instead), caught and bowled ~2, bowled ~20, lbw ~15, run out ~5.
    /// Names as the scorecard shows them (surnames: NameBank.Surname).
    public static string HowOut(string bowler, string fielder, string keeper, bool spinner, Random rng)
    {
        double x = rng.NextDouble() * 100.0;
        if ((x -= 20) < 0) return "b " + bowler;
        if ((x -= 15) < 0) return "lbw b " + bowler;
        if ((x -= 5) < 0) return "run out";
        if ((x -= 2) < 0) return "c & b " + bowler;
        if (spinner)
        {
            if ((x -= 5) < 0) return "st " + keeper + " b " + bowler;
            if ((x -= 7) < 0) return "c " + keeper + " b " + bowler;
        }
        else if ((x -= 12) < 0) return "c " + keeper + " b " + bowler;
        return "c " + fielder + " b " + bowler;
    }

    // ---- A whole innings ------------------------------------------------------------------------

    /// The innings in progress: the card being filled, who is in, and who bowls.
    private class State
    {
        public InningsCard card;
        public List<SquadPlayer> batters;
        public List<BatterLine> lines = new List<BatterLine>();
        public int striker, nonStriker, next;
        public int allOut;
        public Attack attack;
        public bool Done => card.wickets >= allOut || (card.target > 0 && card.runs >= card.target);
    }

    /// The fielding side: who bowls each over, its keeper and its fielders.
    private class Attack
    {
        public List<SquadPlayer> order = new List<SquadPlayer>();
        public List<SquadPlayer> fielders = new List<SquadPlayer>();
        public SquadPlayer keeper;
        public SquadPlayer current;
    }

    /// A whole innings for `batting` against `bowling`: in order, each batter's balls from Ball(),
    /// strike rotating on odd runs and at the over, ending all out, overs done or target reached.
    /// `target` 0 when batting first. The card names every batter who batted.
    public static InningsCard Innings(Team batting, Team bowling, int overs, int target, eBowlingLevel level, Random rng)
    {
        var card = new InningsCard { team = batting?.name ?? "", overs = Math.Max(0, overs), target = Math.Max(0, target) };
        var batters = batting?.players ?? new List<SquadPlayer>();
        if (batters.Count < 2 || card.overs == 0) return card;

        var s = new State
        {
            card = card, batters = batters, striker = 0, nonStriker = 1, next = 2,
            allOut = Math.Min(10, batters.Count - 1), attack = MakeAttack(bowling, card.overs),
        };
        s.lines.Add(Line(batters[0]));
        s.lines.Add(Line(batters[1]));

        int total = card.overs * 6;
        for (int over = 0; over < card.overs && !s.Done; over++)
        {
            int startRuns = card.runs, startWickets = card.wickets;
            s.attack.current = over < s.attack.order.Count ? s.attack.order[over] : null;
            int legal = 0;
            while (legal < 6 && !s.Done)
            {
                float a = Aggression(card.runs, card.wickets, card.balls, total, card.target);
                int r = Ball(batters[s.striker].bat, level, a, rng);
                if (r == Wide) { card.runs++; continue; }
                legal++;
                card.balls++;
                if (r == Wicket) Dismiss(s, rng);
                else Score(s, r);
            }
            card.overRuns.Add(card.runs - startRuns);
            card.overWickets.Add(card.wickets - startWickets);
            if (legal == 6) Swap(s);
        }
        foreach (BatterLine l in s.lines)
            if (!l.isOut) l.how = "not out";
        card.batting.AddRange(s.lines);
        return card;
    }

    private static BatterLine Line(SquadPlayer p) => new BatterLine { name = p.name, batted = true, isYou = p.isYou };

    private static void Score(State s, int r)
    {
        BatterLine l = s.lines[s.striker];
        l.balls++;
        l.runs += r;
        if (r == 4) l.fours++;
        if (r == 6) l.sixes++;
        s.card.runs += r;
        if (r % 2 == 1) Swap(s);
    }

    private static void Dismiss(State s, Random rng)
    {
        BatterLine l = s.lines[s.striker];
        l.balls++;
        l.isOut = true;
        l.how = HowOutBy(s.attack, rng);
        s.card.wickets++;
        if (s.card.wickets >= s.allOut || s.next >= s.batters.Count) return;
        // The new batter comes in at the striker's end.
        s.striker = s.next++;
        s.lines.Add(Line(s.batters[s.striker]));
    }

    /// The lines list is in batting order and the striker / non-striker are indexes into it.
    private static void Swap(State s) => (s.striker, s.nonStriker) = (s.nonStriker, s.striker);

    private static string HowOutBy(Attack at, Random rng)
    {
        SquadPlayer bowler = at.current;
        string b = bowler != null ? NameBank.Surname(bowler.name) : "the bowler";
        string k = at.keeper != null ? NameBank.Surname(at.keeper.name) : b;
        string f = k;
        var others = at.fielders.FindAll(p => p != bowler && p != at.keeper);
        if (others.Count > 0) f = NameBank.Surname(others[rng.Next(others.Count)].name);
        bool spin = bowler != null && bowler.kind >= eBowlerKind.OffSpin;
        return HowOut(b, f, k, spin, rng);
    }

    /// The bowling side's attack: who bowls each over (BowlingOrder), its keeper (the last
    /// specialist batter in the order, never the career player) and its fielders.
    private static Attack MakeAttack(Team bowling, int overs)
    {
        var at = new Attack { order = BowlingOrder(bowling, overs) };
        var players = bowling?.players ?? new List<SquadPlayer>();
        at.fielders.AddRange(players);
        for (int i = players.Count - 1; i >= 0; i--)
            if (!players[i].bowls && !players[i].isYou) { at.keeper = players[i]; break; }
        return at;
    }

    /// Who bowls each of `overs` overs for `bowling`: its bowlers, pace first, then spin (part-timers
    /// from the tail up only if there are too few to bowl the overs at no more than a fifth each,
    /// rounded up, and never the career player). Each over goes to the bowler with fewest overs
    /// who did not bowl the last and has overs left; ties go to the earlier, so the quicks share
    /// the new ball. Only a squad too small to have enough bowlers ever bowls back-to-back overs
    /// or past the quota. The scoreboard can show these names; the simulated innings uses them.
    public static List<SquadPlayer> BowlingOrder(Team bowling, int overs)
    {
        var order = new List<SquadPlayer>();
        var players = bowling?.players ?? new List<SquadPlayer>();
        var pool = new List<SquadPlayer>();
        foreach (SquadPlayer p in players)
            if (p.bowls) pool.Add(p);
        // Stable: pace before spin, squad order within a kind.
        var byKind = new List<SquadPlayer>();
        for (int k = (int)eBowlerKind.Fast; k <= (int)eBowlerKind.Mystery; k++)
            byKind.AddRange(pool.FindAll(p => (int)p.kind == k));
        pool = byKind;
        int maxEach = Math.Max(1, (overs + 4) / 5);
        int need = Math.Max(2, (overs + maxEach - 1) / maxEach);
        for (int i = players.Count - 1; i >= 0 && pool.Count < need; i--)
            if (!pool.Contains(players[i]) && !players[i].isYou) pool.Add(players[i]);
        if (pool.Count == 0) return order;

        var oversBy = new int[pool.Count];
        int last = -1;
        for (int over = 0; over < overs; over++)
        {
            int pick = -1;
            for (int pass = 0; pass < 3 && pick < 0; pass++)
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pass == 0 && i == last) continue;
                    if (pass < 2 && oversBy[i] >= maxEach) continue;
                    if (pick < 0 || oversBy[i] < oversBy[pick]) pick = i;
                }
            oversBy[pick]++;
            last = pick;
            order.Add(pool[pick]);
        }
        return order;
    }

    /// The card as MatchFlow's AI innings (the result screen plays these back).
    public static AIInnings.Result ToAIResult(InningsCard c)
    {
        var r = new AIInnings.Result();
        if (c == null) return r;
        r.runs = c.runs;
        r.wickets = c.wickets;
        r.balls = c.balls;
        r.overs = c.overs;
        r.target = c.target;
        r.overRuns.AddRange(c.overRuns);
        r.overWickets.AddRange(c.overWickets);
        return r;
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
