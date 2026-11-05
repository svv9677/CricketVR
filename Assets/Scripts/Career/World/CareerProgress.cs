using System;
using System.Collections.Generic;

/// <summary>
/// The career moving on: making the first season, planning the next match, taking a finished
/// match into the record book (innings, stats, form, the batting order, the table, the rest of the
/// round simulated), ending a season, and (phase 4) scouts, offers, selection and promotion.
/// Pure over the profile: no scene, so it is tested in EditMode. The rules' arithmetic (form par,
/// the slot rule, the bowlers) is in CareerProgressMath.
/// </summary>
public static class CareerProgress
{
    /// Innings that make up form.
    public const int FormInnings = 5;

    /// Chance a State-or-higher fixture not under lights starts in the evening.
    public const float EveningChance = 0.2f;

    private const string Coach = "Coach";

    /// Make the first season if the career has none (a new career, or a version-1 save), and end
    /// a finished one (the review, then the next season) so the hub always has a season to show.
    public static void EnsureSeason(CareerProfile p, Random rng)
    {
        if (p == null)
            return;
        // A save can hold an empty Season where JsonUtility had no field: that is no season.
        if (p.season != null && p.season.teams != null && p.season.teams.Count > 0)
        {
            if (p.season.finished)
                EndSeason(p, rng);
            return;
        }
        if (p.stats.matches == 0 || p.battingSlot > Ladder.LowestSlot(p.rung))
            p.battingSlot = Ladder.LowestSlot(p.rung);
        p.season = LeagueBuilder.NewSeason(p.rung, p.seasonsPlayed + 1, p.nation, null, You(p), rng);
    }

    /// The career player as his side's squad holds him.
    private static SquadPlayer You(CareerProfile p) =>
        new SquadPlayer { name = p.name, bat = BattingRating(p), isYou = true };

    /// Order of play: the league rounds in turn, the final after them all.
    private static int PlayOrder(Fixture f) => f.final ? int.MaxValue : f.round;

    /// Index of the career player's side's next unplayed fixture (round order; the final only if
    /// his side is in it), or -1.
    private static int NextIndex(Season s)
    {
        if (s == null || s.fixtures == null)
            return -1;
        int best = -1;
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            Fixture f = s.fixtures[i];
            if (f == null || f.played || !f.Involves(s.yourTeam))
                continue;
            if (best < 0 || PlayOrder(f) < PlayOrder(s.fixtures[best]))
                best = i;
        }
        return best;
    }

    /// The next fixture of the career player's side this season, or null (season over).
    public static Fixture NextFixture(CareerProfile p)
    {
        if (p?.season == null || p.season.finished)
            return null;
        int i = NextIndex(p.season);
        return i < 0 ? null : p.season.fixtures[i];
    }

    /// Everything the next match needs (null when there is none).
    public static MatchPlan PlanNext(CareerProfile p, Random rng)
    {
        Fixture f = NextFixture(p);
        Season s = p?.season;
        Team yours = s?.Yours, them = f == null ? null : s.Team(f.Opponent(s.yourTeam));
        if (f == null || yours == null || them == null)
            return null;
        eRung rung = s.rung;
        var plan = new MatchPlan
        {
            fixture = s.fixtures.IndexOf(f),
            rung = rung,
            yourTeam = yours.id, opponent = them.id,
            yourTeamName = yours.name, opponentName = them.name,
            yourShort = yours.shortName, opponentShort = them.shortName,
            overs = Ladder.Overs(rung),
            bowling = Ladder.Bowling(rung),
            crowd = Ladder.Crowd(rung),
            venue = Ladder.Venue(rung),
            pitch = f.pitch,
            situation = f.situation,
            scout = f.scout,
            scoutFrom = Ladder.Above(rung),
        };
        plan.time = f.night ? eTimeOfDay.Night
                  : rung >= eRung.State && rng.NextDouble() < EveningChance ? eTimeOfDay.Evening
                  : eTimeOfDay.Afternoon;
        plan.batFirst = rng.NextDouble() < 0.5;
        if (!plan.batFirst)
            plan.theirs = CareerSim.Innings(them, yours, plan.overs, 0, plan.bowling, rng);
        FillOrder(p, yours, plan);
        plan.bowlers = CareerProgressMath.PickBowlers(them);
        plan.seed = rng.Next();
        return plan;
    }

    /// The side's batting order with the career player moved to his slot (the others keep their
    /// order around him), and the ratings the partners' simulated balls use.
    private static void FillOrder(CareerProfile p, Team yours, MatchPlan plan)
    {
        var others = new List<SquadPlayer>();
        foreach (SquadPlayer s in yours.players)
            if (s != null && !s.isYou)
                others.Add(s);
        while (others.Count > 10)         // the order is 11 names, whatever size the squad is
            others.RemoveAt(others.Count - 1);
        int slot = Math.Max(0, Math.Min(Math.Min(others.Count, Ladder.LowestSlot(p.rung) - 1), p.battingSlot - 1));
        plan.order.Clear();
        plan.orderRatings.Clear();
        for (int i = 0; i <= others.Count; i++)
        {
            if (i == slot)
            {
                plan.order.Add(p.name);
                plan.orderRatings.Add(BattingRating(p));
            }
            if (i < others.Count)
            {
                plan.order.Add(others[i].name);
                plan.orderRatings.Add(others[i].bat);
            }
        }
        plan.slot = slot;
    }

    /// Take a finished match into the career: the innings into the record book and stats, form
    /// and the batting slot updated, the fixture's result into the table, the round's other
    /// fixtures simulated, the final added after the last league round (and simulated if his side
    /// is not in it), and the season marked finished once the final is played (EnsureSeason or
    /// EndSeason then reviews it and starts the next).
    public static void RecordMatch(CareerProfile p, MatchPlan plan, CareerMatchResult result, Random rng)
    {
        Season s = p?.season;
        if (s == null || plan == null || result == null || s.finished)
            return;
        Fixture f = plan.fixture >= 0 && plan.fixture < s.fixtures.Count ? s.fixtures[plan.fixture] : null;
        if (f == null || f.played || !f.Involves(s.yourTeam)
            || plan.yourTeam != s.yourTeam || f.Opponent(s.yourTeam) != plan.opponent)
            return;                       // not this season's, or already recorded: count it once

        InningsRecord rec = RecordInnings(p, s, plan, result);
        RecordFixture(s, f, plan, result, rec);
        PlayOutRounds(s, rng);

        SquadPlayer you = s.Yours?.You;
        if (you != null)
            you.bat = BattingRating(p);
        int target = CareerProgressMath.TargetSlot(BattingRating(p), Form(p), s.Yours?.players ?? new List<SquadPlayer>());
        p.battingSlot = CareerProgressMath.NextSlot(p.battingSlot, target, Ladder.LowestSlot(p.rung));
        Milestones(p, rec);
    }

    /// The line in the record book, and the career figures.
    private static InningsRecord RecordInnings(CareerProfile p, Season s, MatchPlan plan, CareerMatchResult r)
    {
        // Leaving the match early counts as retired out (MatchTypes: abandoned).
        bool retired = r.retired || (r.abandoned && r.batted);
        bool isOut = r.batted && (r.isOut || r.abandoned);
        var rec = new InningsRecord
        {
            season = s.number, rung = plan.rung,
            team = plan.yourTeamName, opponent = plan.opponentName,
            position = plan.slot + 1,
            runs = r.runs, balls = r.balls, fours = r.fours, sixes = r.sixes,
            batted = r.batted, isOut = isOut, retired = retired,
            how = !string.IsNullOrEmpty(r.how) ? r.how : !r.batted ? "did not bat" : retired ? "retired out" : isOut ? "out" : "not out",
            teamScore = CareerProgressMath.ScoreLine(r.yours),
            won = r.won, tied = r.tied,
            date = CareerProgressMath.Today(),
        };
        rec.result = !string.IsNullOrEmpty(r.result) ? r.result
            : CareerProgressMath.ResultText(r.won, r.tied, plan.batFirst, plan.yourTeamName, plan.opponentName, r.yours, r.theirs ?? plan.theirs);
        p.AddInnings(rec);
        AddToStats(p.stats, rec);
        return rec;
    }

    private static void AddToStats(CareerStats st, InningsRecord r)
    {
        st.matches++;
        if (!r.batted)
            return;
        st.innings++;
        if (!r.isOut) st.notOuts++;
        st.runs += r.runs;
        st.balls += r.balls;
        st.fours += r.fours;
        st.sixes += r.sixes;
        if (r.runs >= 100) st.hundreds++;
        else if (r.runs >= 50) st.fifties++;
        if (r.runs == 0 && r.isOut) st.ducks++;
        // A higher score, or the same score not out where the best was out.
        if (r.runs > st.highScore || (r.runs == st.highScore && !r.isOut && !st.highScoreNotOut))
        {
            st.highScore = r.runs;
            st.highScoreNotOut = !r.isOut;
        }
    }

    /// The fixture played: both scores, the result and the winner, and the table rows.
    private static void RecordFixture(Season s, Fixture f, MatchPlan plan, CareerMatchResult r, InningsRecord rec)
    {
        InningsCard yours = r.yours ?? new InningsCard { team = plan.yourTeamName, overs = plan.overs };
        InningsCard theirs = r.theirs ?? plan.theirs ?? new InningsCard { team = plan.opponentName, overs = plan.overs };
        bool home = f.home == s.yourTeam;
        f.played = true;
        f.homeScore = CareerProgressMath.ScoreLine(home ? yours : theirs);
        f.awayScore = CareerProgressMath.ScoreLine(home ? theirs : yours);
        f.result = rec.result;
        f.winner = r.tied ? "" : r.won ? s.yourTeam : f.Opponent(s.yourTeam);
        LeagueBuilder.Record(s, f, home ? yours : theirs, home ? theirs : yours);
    }

    /// Everything the other sides play before the career player's next match: the rest of each
    /// round he has played, the final once the league rounds are all done (simulated when his
    /// side is not in it), and the season finished when the final has been played.
    private static void PlayOutRounds(Season s, Random rng)
    {
        int next = NextIndex(s);
        int upTo = next < 0 ? int.MaxValue : PlayOrder(s.fixtures[next]);
        bool leagueLeft = false;
        foreach (Fixture f in s.fixtures.ToArray())
        {
            if (f == null || f.played || f.final)
                continue;
            if (!f.Involves(s.yourTeam) && f.round < upTo)
            {
                LeagueBuilder.Simulate(s, f, rng);
                f.played = true;          // simulated is played, whatever the simulation marked
            }
            leagueLeft |= !f.played;
        }
        if (leagueLeft)
            return;

        LeagueBuilder.AddFinal(s);
        Fixture final = s.fixtures.Find(x => x != null && x.final);
        if (final == null)
        {
            s.finished = true;            // no final to play (too few sides): the league is the season
            return;
        }
        if (!final.played && !final.Involves(s.yourTeam))
        {
            LeagueBuilder.Simulate(s, final, rng);
            final.played = true;
        }
        s.finished = final.played;
    }

    /// A short word in the inbox after a milestone: the first fifty, a hundred, ducks in a row.
    private static void Milestones(CareerProfile p, InningsRecord r)
    {
        if (!r.batted)
            return;
        string score = $"{r.runs}{(r.isOut ? "" : "*")} off {r.balls} balls against {r.opponent}";
        if (r.runs >= 100)
            Tell(p, eMessageKind.Milestone, p.stats.hundreds == 1 ? "Your first hundred" : "Another hundred",
                 score + ". Raise the bat - that is a day you will remember.");
        else if (r.runs >= 50 && p.stats.fifties == 1)
            Tell(p, eMessageKind.Milestone, "Your first fifty", score + ". The side will remember it at selection.");

        int ducks = CareerProgressMath.DuckStreak(p.innings);
        if (ducks >= 2)
            Tell(p, eMessageKind.Coach, ducks == 2 ? "Two ducks in a row" : ducks + " ducks in a row",
                 "It happens to everyone. Next time watch the ball onto the bat and look for a single to get off the mark.");
    }

    private static void Tell(CareerProfile p, eMessageKind kind, string title, string body) =>
        p.AddMessage(new CareerMessage { kind = kind, from = Coach, title = title, body = body, date = CareerProgressMath.Today() });

    /// Form 0..99 over the last FormInnings innings (Unknown with none): runs and strike rate for
    /// the rung, survival.
    public static float Form(CareerProfile p) =>
        p == null ? RatingSet.Unknown : CareerProgressMath.Form(p.innings, FormInnings);

    /// The career player's batting rating as his side sees it (his measured overall; a beginner's
    /// prior when nothing is known yet).
    public static float BattingRating(CareerProfile p)
    {
        float o = p?.ratings != null ? p.ratings.Overall : RatingSet.Unknown;
        return RatingSet.Known(o) ? o : CareerProgressMath.BeginnerRating;
    }

    /// Act on a message option (an offer's "join", "decline"); phase 4.
    public static void Choose(CareerProfile p, CareerMessage m, MessageOption o, Random rng) { }

    /// The season is over: review it, then start the next at the same rung with the same side
    /// (phase 4 adds moving up or down, the scouts' offers and selection).
    public static void EndSeason(CareerProfile p, Random rng)
    {
        if (p == null)
            return;
        Season s = p.season;
        if (s == null || s.teams == null || s.teams.Count == 0)
        {
            p.season = null;
            EnsureSeason(p, rng);
            return;
        }
        p.seasonsPlayed++;
        Tell(p, eMessageKind.Info, $"Season {s.number} review", Review(p, s));

        Team keep = s.Yours;
        if (keep != null)
        {
            keep.played = keep.won = keep.lost = keep.tied = keep.points = 0;
            keep.runsFor = keep.ballsFaced = keep.runsAgainst = keep.ballsBowled = 0;
            SquadPlayer you = keep.You;
            if (you != null) { you.name = p.name; you.bat = BattingRating(p); }
        }
        p.season = LeagueBuilder.NewSeason(p.rung, p.seasonsPlayed + 1, p.nation, keep, You(p), rng);
    }

    /// "Club cricket, season 1: 143 runs at 23.8, strike rate 121. Harbour CC finished 3rd of 8."
    private static string Review(CareerProfile p, Season s)
    {
        int runs = 0, balls = 0, outs = 0, inns = 0;
        foreach (InningsRecord r in p.innings)
        {
            if (r == null || r.season != s.number || r.rung != s.rung || !r.batted)
                continue;
            inns++; runs += r.runs; balls += r.balls;
            if (r.isOut) outs++;
        }
        float avg = outs > 0 ? runs / (float)outs : runs;
        float sr = balls > 0 ? runs * 100f / balls : 0f;
        string you = inns == 0 ? "You did not bat."
            : $"{runs} runs in {inns} innings at {avg:0.0}, strike rate {sr:0}.";

        Team yours = s.Yours;
        List<Team> table = LeagueBuilder.Table(s);
        int pos = yours == null ? 0 : table.FindIndex(t => t != null && t.id == s.yourTeam) + 1;
        string side = pos > 0 ? $" {yours.name} finished {CareerProgressMath.Ordinal(pos)} of {table.Count}." : "";
        Fixture final = s.fixtures.Find(f => f != null && f.final);
        if (final != null && final.played && yours != null && final.Involves(s.yourTeam))
            side += final.winner == yours.id || final.winner == yours.name ? " Champions - the final was won." : " The final was lost.";
        return $"{Ladder.Name(s.rung)}, season {s.number}: {you}{side}";
    }
}
