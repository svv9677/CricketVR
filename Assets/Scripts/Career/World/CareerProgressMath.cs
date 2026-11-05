using System;
using System.Collections.Generic;

/// <summary>
/// The arithmetic behind CareerProgress, kept apart so each rule can be read (and tested) on its
/// own: what a decent innings is at each rung (form), where the side bats the career player
/// (the slot rule), which of the other side's bowlers take HUD's eight places, and the short lines
/// of text the record book and the inbox show. Pure: no profile is changed here.
/// </summary>
public static class CareerProgressMath
{
    /// A beginner's batting rating before anything is measured: a club tail-ender's.
    public const float BeginnerRating = 35f;

    /// The deepest slot the slot rule ranks for (the top order and the all-rounder at 7).
    public const int TopOrderSlots = 7;

    /// Places the slot can move after one match, up or down: one innings is not a career.
    public const int MaxSlotMove = 2;

    /// How much of the slot score is the measured overall (the rest is form).
    public const float OverallWeight = 0.7f;

    /// HUD's attack, in its order (HUD.cs, the boKinds list): two quicks, two medium pacers, an
    /// off-spinner, two leg-spinners and a mystery spinner. The plan's bowler names follow it.
    public static readonly eBowlerKind[] AttackKinds =
    {
        eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.Medium, eBowlerKind.Fast,
        eBowlerKind.OffSpin, eBowlerKind.LegSpin, eBowlerKind.Mystery, eBowlerKind.LegSpin,
    };

    /// A good innings at the rung: runs that win a match of that length.
    public static float ParRuns(eRung r) => r switch
    {
        eRung.Club => 14f,
        eRung.State => 20f,
        eRung.ProLeague => 28f,
        _ => 30f,
    };

    /// A good strike rate at the rung (T20 at Pro League goes hardest).
    public static float ParStrikeRate(eRung r) => r switch
    {
        eRung.Club => 100f,
        eRung.State => 110f,
        eRung.ProLeague => 130f,
        _ => 125f,
    };

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// One innings toward form, 0..1: mostly the runs against the rung's par, some for the strike
    /// rate, and a little for still being there at the end. A game rain cut asks for fewer runs, in
    /// proportion to its overs (CareerSituations.ParScale; 1 for a full game).
    public static float InningsScore(InningsRecord r)
    {
        float runs = Clamp01(r.runs / (ParRuns(r.rung) * CareerSituations.ParScale(r))) * 0.75f;
        float rate = Clamp01(r.StrikeRate / ParStrikeRate(r.rung)) * 0.15f;
        return runs + rate + (r.isOut ? 0f : 0.1f);
    }

    /// Form 0..99 over the last `count` innings he batted in (Unknown when there are none).
    public static float Form(List<InningsRecord> innings, int count)
    {
        float sum = 0f;
        int n = 0;
        for (int i = innings.Count - 1; i >= 0 && n < count; i--)
        {
            InningsRecord r = innings[i];
            // Not out without facing a ball (the end came at the other end) says nothing about form.
            if (r == null || !r.batted || r.leftOut || (r.balls == 0 && !r.isOut))
                continue;
            sum += InningsScore(r);
            n++;
        }
        return n > 0 ? sum / n * 99f : RatingSet.Unknown;
    }

    /// Where the side would like him (1..TopOrderSlots): one place below every top-order team-mate
    /// who bats better than his blend of overall and form. Team-mates are the six best of the rest.
    public static int TargetSlot(float overall, float form, List<SquadPlayer> squad)
    {
        float score = RatingSet.Known(form) ? OverallWeight * overall + (1f - OverallWeight) * form : overall;
        var others = new List<float>();
        foreach (SquadPlayer s in squad)
            if (s != null && !s.isYou)
                others.Add(s.bat);
        others.Sort((a, b) => b.CompareTo(a));
        int better = 0;
        for (int i = 0; i < others.Count && i < TopOrderSlots - 1; i++)
            if (others[i] > score)
                better++;
        return 1 + better;
    }

    /// The slot for the next match: toward the target by at most MaxSlotMove, never higher than
    /// the rung's lowest slot (Ladder.LowestSlot), and never above 1.
    public static int NextSlot(int current, int target, int lowest)
    {
        int move = Math.Max(-MaxSlotMove, Math.Min(MaxSlotMove, target - current));
        return Math.Max(1, Math.Min(Math.Max(1, lowest), current + move));
    }

    /// Pace or spin, for a near match when the other side has none of a kind.
    private static bool Spin(eBowlerKind k) => k == eBowlerKind.OffSpin || k == eBowlerKind.LegSpin || k == eBowlerKind.Mystery;

    private static int KindDistance(eBowlerKind want, eBowlerKind have) =>
        want == have ? 0 : Spin(want) == Spin(have) ? 1 : 2;

    /// The other side's bowlers matched to HUD's eight places (AttackKinds): for each place the
    /// closest kind (exact, then pace for pace or spin for spin, then anything), the least used
    /// first, so names repeat only when the side is short of that kind. A side with no bowlers
    /// marked lends its tail (the last players in the order).
    public static List<string> PickBowlers(Team t)
    {
        var pool = new List<SquadPlayer>();
        if (t != null)
            foreach (SquadPlayer s in t.players)
                if (s != null && s.bowls && !s.isYou)
                    pool.Add(s);
        if (pool.Count == 0 && t != null)
            for (int i = t.players.Count - 1; i >= 0 && pool.Count < 5; i--)
                if (t.players[i] != null && !t.players[i].isYou)
                    pool.Add(t.players[i]);

        var names = new List<string>();
        var used = new int[pool.Count];
        foreach (eBowlerKind want in AttackKinds)
        {
            int best = -1, bestScore = int.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                int score = KindDistance(want, pool[i].kind) * 100 + used[i];
                if (score < bestScore) { best = i; bestScore = score; }
            }
            if (best < 0) { names.Add("Bowler " + (names.Count + 1)); continue; }
            used[best]++;
            names.Add(pool[best].name);
        }
        return names;
    }

    /// "145/6 (20.0)": a side's score for the fixtures list and the record book.
    public static string ScoreLine(InningsCard c) => c == null ? "" : $"{c.Score} ({c.OversText})";

    /// "Harbour CC won by 12 runs" / "... won by 4 wickets" / "Match tied", when the match scene
    /// did not say. `yoursFirst`: the career player's side batted first.
    public static string ResultText(bool won, bool tied, bool yoursFirst, string yourName, string theirName,
                                    InningsCard yours, InningsCard theirs)
    {
        if (tied)
            return "Match tied";
        string winner = won ? yourName : theirName;
        InningsCard first = yoursFirst ? yours : theirs, second = yoursFirst ? theirs : yours;
        bool firstWon = won == yoursFirst;
        if (first == null || second == null)
            return winner + " won";
        if (firstWon)
        {
            int by = Math.Max(1, CareerSituationMath.Par(first, second) - second.runs);   // a rain-revised target
            return $"{winner} won by {by} run{(by == 1 ? "" : "s")}";
        }
        int wk = Math.Max(1, 10 - second.wickets);
        return $"{winner} won by {wk} wicket{(wk == 1 ? "" : "s")}";
    }

    /// "1st", "2nd", "3rd", "4th" ... for the season review.
    public static string Ordinal(int n)
    {
        int tens = n % 100;
        if (tens >= 11 && tens <= 13)
            return n + "th";
        return (n % 10) switch { 1 => n + "st", 2 => n + "nd", 3 => n + "rd", _ => n + "th" };
    }

    /// Ducks in a row at the end of the record book (innings he did not bat in are skipped).
    public static int DuckStreak(List<InningsRecord> innings)
    {
        int n = 0;
        for (int i = innings.Count - 1; i >= 0; i--)
        {
            InningsRecord r = innings[i];
            if (r == null || !r.batted || r.leftOut || (r.balls == 0 && !r.isOut))
                continue;
            if (r.runs != 0 || !r.isOut)
                break;
            n++;
        }
        return n;
    }

    /// The season review: "Club cricket, season 1: 143 runs in 7 innings at 23.8, strike rate 121.
    /// Harbour CC finished 3rd of 8. State scouts watched 3 matches and saw promise." Reads the
    /// season's record-book lines and the scouts' reports (CareerProfile.scoutReports).
    public static string SeasonReview(CareerProfile p, Season s)
    {
        if (p == null || s == null)
            return "";
        int runs = 0, balls = 0, outs = 0, inns = 0, missed = 0;
        foreach (InningsRecord r in p.innings ?? new List<InningsRecord>())
        {
            if (r == null || r.season != s.number || r.rung != s.rung)
                continue;
            if (r.leftOut) missed++;
            if (!r.batted)
                continue;
            inns++; runs += r.runs; balls += r.balls;
            if (r.isOut) outs++;
        }
        float sr = balls > 0 ? runs * 100f / balls : 0f;
        string avg = outs > 0 ? $"at {runs / (float)outs:0.0}" : "without being out";
        string you = inns == 0 ? "You did not bat."
            : $"{runs} runs in {inns} innings {avg}, strike rate {sr:0}.";
        if (missed > 0)
            you += missed == 1 ? " Left out of one match." : $" Left out of {missed} matches.";

        Team yours = s.Yours;
        List<Team> table = LeagueBuilder.Table(s);
        int pos = yours == null ? 0 : table.FindIndex(t => t != null && t.id == s.yourTeam) + 1;
        string side = pos > 0 ? $" {yours.name} finished {Ordinal(pos)} of {table.Count}." : "";
        Fixture final = s.fixtures?.Find(f => f != null && f.final);
        if (final != null && final.played && yours != null && final.Involves(s.yourTeam))
            side += final.winner == yours.id || final.winner == yours.name ? " Champions - the final was won." : " The final was lost.";
        string scouts = Scouting.Verdict(p.scoutReports, s.rung);
        string goals = CoachGoals.ReviewLine(s);
        return $"{Ladder.Name(s.rung)}, season {s.number}: {you}{side}{(scouts.Length > 0 ? " " + scouts : "")}{(goals.Length > 0 ? " " + goals : "")}";
    }

    /// Today, as the record book and the inbox date things.
    public static string Today() => DateTime.Now.ToString("yyyy-MM-dd");
}
