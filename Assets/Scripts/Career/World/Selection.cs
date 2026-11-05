using System;
using System.Collections.Generic;

/// <summary>
/// Selection (phase 4): poor form gets the career player left out of the next match (never at
/// Club, where every club needs its players), and a poor season below the rung's standard gets him
/// released, back down a rung. Both read only form and the measured ratings. Also the match he
/// sits out, simulated with a reserve in his place.
/// </summary>
public static class Selection
{
    /// Innings that make up the form a side picks on: this many poor ones in a row and he is left out.
    public const int DropInnings = 3;

    /// Matches a dropped player sits out.
    public const int DropMatches = 1;

    /// Form for the season below this (with the overall below Ladder.RelegationRating) and he is
    /// released at the end of it.
    public const float PoorSeasonForm = 40f;

    /// Form over the last DropInnings below this and he is left out (never at Club). A State
    /// innings of about 5 off 10 balls is form 25.
    public static float DropForm(eRung r) => r switch
    {
        eRung.Club => -1f,
        eRung.State => 25f,
        eRung.ProLeague => 28f,
        _ => 30f,
    };

    /// An innings that tells the side something: he batted and faced a ball, or was out.
    private static bool Counts(InningsRecord r) =>
        r != null && r.batted && !r.leftOut && (r.balls > 0 || r.isOut);

    /// The innings of the season he is in now at his rung, newest last.
    private static List<InningsRecord> ThisSeason(CareerProfile p)
    {
        var l = new List<InningsRecord>();
        if (p?.season == null || p.innings == null)
            return l;
        foreach (InningsRecord r in p.innings)
            if (r != null && r.season == p.season.number && r.rung == p.season.rung)
                l.Add(r);
        return l;
    }

    /// Left out of the next match: not at Club; his last DropInnings innings this season at this
    /// rung since he was last left out (a man back in the side gets his innings) average form
    /// below DropForm.
    public static bool ShouldDrop(CareerProfile p)
    {
        if (p?.season == null || p.season.rung == eRung.Club || p.rung == eRung.Club)
            return false;
        List<InningsRecord> season = ThisSeason(p);
        var recent = new List<InningsRecord>();
        for (int i = season.Count - 1; i >= 0 && recent.Count < DropInnings; i--)
        {
            if (season[i].leftOut)
                break;
            if (Counts(season[i]))
                recent.Insert(0, season[i]);
        }
        if (recent.Count < DropInnings)
            return false;
        return CareerProgressMath.Form(recent, DropInnings) < DropForm(p.season.rung);
    }

    /// Form over the whole season (Unknown if he never batted).
    public static float SeasonForm(CareerProfile p) => CareerProgressMath.Form(ThisSeason(p), int.MaxValue);

    /// Released at the end of the season: the measured overall below Ladder.RelegationRating(rung)
    /// and the season's form poor (or none: he never made a score). Never at Club.
    public static bool ShouldRelegate(CareerProfile p)
    {
        if (p == null || p.rung == eRung.Club)
            return false;
        float overall = CareerProgress.BattingRating(p);
        if (overall >= Ladder.RelegationRating(p.rung))
            return false;
        float form = SeasonForm(p);
        return !RatingSet.Known(form) || form < PoorSeasonForm;
    }

    /// After a match he played: left out of the next one if his form says so (and the captain
    /// says so in the inbox).
    internal static void AfterMatch(CareerProfile p)
    {
        if (p == null || p.benchedFor > 0 || !ShouldDrop(p))
            return;
        Season s = p.season;
        Fixture next = CareerProgress.NextFixture(p);
        if (next == null)
            return;                       // the season is over: the review decides
        p.benchedFor = DropMatches;
        string them = s.Team(next.Opponent(s.yourTeam))?.name ?? "the next side";
        CareerProgress.Post(p, eMessageKind.Selection, "Captain", $"You're left out for the match against {them}",
            $"Your last {DropInnings} innings have not brought the runs we need. Sit this one out, work in the nets, "
            + "and you're straight back in after it.");
    }

    /// After the match he sat out: back in the side, and how it went without him.
    internal static void BackIn(CareerProfile p, InningsRecord missed)
    {
        Season s = p.season;
        Fixture next = CareerProgress.NextFixture(p);
        string them = next == null ? null : s.Team(next.Opponent(s.yourTeam))?.name;
        string body = missed.result + " while you sat out.";
        body += them != null && p.benchedFor == 0 ? $" You're back in the side for the match against {them}." : "";
        CareerProgress.Post(p, eMessageKind.Selection, "Captain",
            them != null && p.benchedFor == 0 ? "Back in the side" : "The match you missed", body);
    }

    /// The season's release: who let him go and where he plays next.
    internal static void Released(CareerProfile p, string oldSide, eRung from)
    {
        string next = p.season?.Yours?.name ?? "a new side";
        string by = Ladder.Top(from) ? $"Dropped by {oldSide}" : $"Released by {oldSide}";
        CareerProgress.Post(p, eMessageKind.Selection, Ladder.Top(from) ? "Selector" : "Captain", by,
            $"A season without the runs at {Ladder.Name(from)}. You'll play {Ladder.Name(p.rung)} next season with {next} - "
            + "score there and the scouts will be back.");
    }

    /// Play the fixture he is left out of by simulation, a reserve in his place (he is back in the
    /// squad after it).
    internal static void SimulateWithout(Season s, Fixture f, string nation, Random rng)
    {
        Team yours = s?.Yours;
        int i = yours == null ? -1 : yours.players.FindIndex(x => x != null && x.isYou);
        SquadPlayer you = i >= 0 ? yours.players[i] : null;
        if (you != null)
            yours.players[i] = Reserve(s, nation, rng);
        try
        {
            LeagueBuilder.Simulate(s, f, rng);
        }
        finally
        {
            if (you != null)
                yours.players[i] = you;
        }
    }

    /// The twelfth man: a name new to the league, a batter a little below the rung's typical.
    private static SquadPlayer Reserve(Season s, string nation, Random rng)
    {
        var used = new HashSet<string>();
        foreach (Team t in s.teams)
            foreach (SquadPlayer x in t.players)
                if (x != null) used.Add(x.name);
        return new SquadPlayer
        {
            name = NameBank.Player(nation, rng, used),
            bat = Ladder.SquadMean(s.rung) - Ladder.SquadSpread * 0.5f,
        };
    }
}
