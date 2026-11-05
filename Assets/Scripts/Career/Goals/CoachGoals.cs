using System;
using System.Collections.Generic;

/// <summary>
/// The coach's goals for a season (phase 7): two or three targets set as the season begins, and
/// marked done from the record book as he reaches them. They only measure what he did - reaching
/// one changes nothing about how the game plays, and nothing about selection or the scouts. A goal
/// once done stays done (a rating goal's rating moves with the ball window, so it is latched).
/// </summary>
public static class CoachGoals
{
    /// A rating at or above this is not set as a goal; a rating goal never asks for more than RatingMax.
    public const float RatingCeiling = 90f, RatingMax = 95f;
    /// Drills to finish for a Drills goal; balls a strike-rate goal must be over.
    public const int DrillsTarget = 3, StrikeRateBalls = 30;

    public const string RunsId = "runs", RatingId = "rating", DrillsId = "drills", ScoutedId = "scouted",
                        StrikeRateId = "strikerate", BoundariesId = "boundaries";

    private const string Coach = "Coach";

    /// The season's goals made, if it has none yet: only once the nets trial is done, in a season
    /// still being played. Seeded from the career and the season, never from a caller's rng (so the
    /// league's own draws are unchanged). True when it changed the career.
    public static bool Ensure(CareerProfile p)
    {
        Season s = p?.season;
        if (p == null || !p.trialDone || s == null || s.finished || s.teams == null || s.teams.Count == 0)
            return false;
        s.goals ??= new List<SeasonGoal>();
        if (s.goals.Count > 0)
            return false;
        var r = new Random(StableHash.Fnv1a($"{p.name}|{s.number}|{(int)s.rung}"));
        s.goals.AddRange(Make(p, s, r));
        return s.goals.Count > 0;
    }

    /// The goals for `s`, from `r`: runs for the season; his weakest measured rating below
    /// RatingCeiling (or, with nothing measured, drills); and a third from `r` among those that fit.
    public static List<SeasonGoal> Make(CareerProfile p, Season s, Random r)
    {
        var goals = new List<SeasonGoal>();
        if (p == null || s == null)
            return goals;
        r ??= new Random(0);
        goals.Add(RunsGoal(p, s));
        int weak = CareerRatings.Weakest(p.ratings);
        bool measured = weak >= 0;
        if (measured && p.ratings[weak] < RatingCeiling)
            goals.Add(RatingGoal(weak, p.ratings[weak]));
        else if (!measured)
        {
            goals.Add(DrillsGoal(p));
            return goals;   // nothing measured yet: runs and drills only
        }
        SeasonGoal third = ThirdGoal(p, s, weak, r);
        if (third != null)
            goals.Add(third);
        return goals;
    }

    /// The goals brought up to date from the record book; returns the ones newly done (each told
    /// in the inbox by the coach, and counted in p.goalsDone). Idempotent: a goal is done once.
    public static List<SeasonGoal> Update(CareerProfile p)
    {
        var newly = new List<SeasonGoal>();
        Season s = p?.season;
        if (s?.goals == null || s.goals.Count == 0)
            return newly;
        Tally t = Count(p, s);
        foreach (SeasonGoal g in s.goals)
        {
            if (g == null || g.done)
                continue;
            g.progress = Progress(p, g, t);
            if (!Met(p, g, t))
                continue;
            g.done = true;
            g.doneDate = CareerProgressMath.Today();
            p.goalsDone++;
            newly.Add(g);
            CareerProgress.Post(p, eMessageKind.Goal, Coach, "Goal reached", ReachedBody(g));
        }
        return newly;
    }

    /// How many of `s`'s goals are done.
    public static int Done(Season s) => s?.goals == null ? 0 : s.goals.FindAll(g => g != null && g.done).Count;

    /// The ids of `s`'s goals that are done.
    public static List<string> DoneIds(Season s)
    {
        var ids = new List<string>();
        if (s?.goals != null)
            foreach (SeasonGoal g in s.goals)
                if (g != null && g.done)
                    ids.Add(g.id ?? "");
        return ids;
    }

    /// The season review's line about the goals ("Coach's goals: 2 of 3."; "" when there were none).
    public static string ReviewLine(Season s)
    {
        int n = s?.goals == null ? 0 : s.goals.FindAll(g => g != null).Count;
        return n == 0 ? "" : $"Coach's goals: {Done(s)} of {n}.";
    }

    /// The inbox message's body when `g` is reached.
    public static string ReachedBody(SeasonGoal g) =>
        $"{g?.text} - reached. Well done. Goals you reach unlock kit in your Locker.";

    // ---- Making the goals ---------------------------------------------------------------------

    /// Runs over the season: 60% of a good innings (ParRuns) in every league match, or 15% more
    /// than he made at this rung last season, whichever is more.
    private static SeasonGoal RunsGoal(CareerProfile p, Season s)
    {
        int games = LeagueGames(s);
        float par = CareerProgressMath.ParRuns(s.rung) * games * 0.6f;
        float target = Round5(Math.Max(par, LastSeasonRuns(p, s) * 1.15f));
        return new SeasonGoal { id = RunsId, kind = eGoalKind.Runs, target = target, text = $"Score {target:0} runs this season" };
    }

    private static SeasonGoal RatingGoal(int stat, float rating)
    {
        float target = Math.Min(RatingMax, RoundUp5(rating + 5f));
        return new SeasonGoal
        {
            id = RatingId, kind = eGoalKind.Rating, stat = stat, baseline = rating, target = target, progress = rating,
            text = $"Get {RatingSet.Names[stat]} to {target:0}",
        };
    }

    private static SeasonGoal DrillsGoal(CareerProfile p) => new SeasonGoal
    {
        id = DrillsId, kind = eGoalKind.Drills, target = DrillsTarget, baseline = DrillsFinished(p),
        text = $"Complete {DrillsTarget} drills",
    };

    /// One of Drills, a score in a scouted match (not at the top, with a scouted match still to
    /// play), strike rate (State and up) or boundaries (when Power is his weakest), from `r`;
    /// boundaries count twice when Power is weakest, since it is what he most needs.
    private static SeasonGoal ThirdGoal(CareerProfile p, Season s, int weak, Random r)
    {
        var pool = new List<SeasonGoal> { DrillsGoal(p) };
        if (!Ladder.Top(s.rung) && ScoutedLeft(s))
        {
            float t = Round5(CareerProgressMath.ParRuns(s.rung) * 1.5f);
            pool.Add(new SeasonGoal { id = ScoutedId, kind = eGoalKind.ScoutedScore, target = t, text = $"Score {t:0} in a scouted match" });
        }
        if (s.rung >= eRung.State)
        {
            float t = Round10(CareerProgressMath.ParStrikeRate(s.rung));
            pool.Add(new SeasonGoal
            {
                id = StrikeRateId, kind = eGoalKind.StrikeRate, target = t, minBalls = StrikeRateBalls,
                text = $"Strike rate {t:0}+ ({StrikeRateBalls} balls)",
            });
        }
        if (weak == 1)   // RatingSet index 1 is Power
        {
            float t = BoundaryTarget(s.rung);
            var boundaries = new SeasonGoal { id = BoundariesId, kind = eGoalKind.Boundaries, target = t, text = $"Hit {t:0} boundaries this season" };
            pool.Add(boundaries);
            pool.Add(boundaries);
        }
        return pool[r.Next(pool.Count)];
    }

    public static float BoundaryTarget(eRung rung) => rung switch
    {
        eRung.Club => 6f,
        eRung.State => 10f,
        _ => 15f,
    };

    /// His side's league matches this season (the final not counted); a league's worth when the
    /// fixtures are not there.
    public static int LeagueGames(Season s)
    {
        int n = 0;
        if (s?.fixtures != null)
            foreach (Fixture f in s.fixtures)
                if (f != null && !f.final && f.Involves(s.yourTeam))
                    n++;
        return n > 0 ? n : Ladder.TeamsInLeague - 1;
    }

    /// His runs in the latest earlier season he played at this rung (0 when there was none).
    private static int LastSeasonRuns(CareerProfile p, Season s)
    {
        int last = -1, runs = 0;
        foreach (InningsRecord r in p.innings ?? new List<InningsRecord>())
            if (r != null && r.rung == s.rung && r.season < s.number && r.season > last && Counts(r))
                last = r.season;
        foreach (InningsRecord r in p.innings ?? new List<InningsRecord>())
            if (r != null && r.rung == s.rung && r.season == last && Counts(r))
                runs += r.runs;
        return runs;
    }

    private static bool ScoutedLeft(Season s) =>
        s.fixtures != null && s.fixtures.Exists(f => f != null && f.scout && !f.played && f.Involves(s.yourTeam));

    private static int DrillsFinished(CareerProfile p)
    {
        int n = 0;
        if (p?.drills?.records != null)
            foreach (DrillRecord d in p.drills.records)
                if (d != null)
                    n += d.attempts;
        return n;
    }

    public static float Round5(float v) => Math.Max(5f, (float)Math.Floor(v / 5f + 0.5f) * 5f);
    public static float RoundUp5(float v) => (float)Math.Ceiling(v / 5f - 1e-4f) * 5f;
    private static float Round10(float v) => (float)Math.Floor(v / 10f + 0.5f) * 10f;

    // ---- Progress ------------------------------------------------------------------------------

    /// This season's figures at this rung: the innings he batted in, not the ones he sat out.
    private struct Tally
    {
        public int runs, balls, boundaries, bestScouted;
    }

    private static bool Counts(InningsRecord r) => r.batted && !r.leftOut;

    private static Tally Count(CareerProfile p, Season s)
    {
        var t = new Tally();
        foreach (InningsRecord r in p.innings ?? new List<InningsRecord>())
        {
            if (r == null || r.season != s.number || r.rung != s.rung || !Counts(r))
                continue;
            t.runs += r.runs;
            t.balls += r.balls;
            t.boundaries += r.fours + r.sixes;
            if (r.scouted)
                t.bestScouted = Math.Max(t.bestScouted, r.runs);
        }
        return t;
    }

    private static float Progress(CareerProfile p, SeasonGoal g, Tally t) => g.kind switch
    {
        eGoalKind.Runs => t.runs,
        eGoalKind.StrikeRate => t.balls > 0 ? t.runs * 100f / t.balls : 0f,
        eGoalKind.Rating => RatingOf(p, g.stat),
        eGoalKind.Drills => Math.Max(0f, DrillsFinished(p) - g.baseline),
        eGoalKind.ScoutedScore => t.bestScouted,
        _ => t.boundaries,
    };

    private static bool Met(CareerProfile p, SeasonGoal g, Tally t) => g.kind switch
    {
        eGoalKind.StrikeRate => t.balls >= Math.Max(1, g.minBalls) && g.progress >= g.target,
        // As the screen shows it: a rating of 49.6 reads 50, and so does the goal.
        eGoalKind.Rating => RatingSet.Known(g.progress) && Math.Round(g.progress, MidpointRounding.AwayFromZero) >= g.target,
        _ => g.progress >= g.target,
    };

    private static float RatingOf(CareerProfile p, int stat) =>
        p?.ratings == null || stat < 0 || stat >= RatingSet.Count ? RatingSet.Unknown : p.ratings[stat];
}
