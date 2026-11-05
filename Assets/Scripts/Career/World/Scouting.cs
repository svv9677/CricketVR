using System;
using System.Collections.Generic;

/// <summary>
/// The scouts (phase 4): which of a season's matches a scout from the rung above watches, what he
/// makes of an innings (his impression, 0..1, and the short note he sends), whether the rung above
/// wants the career player after a season, and the offer it makes. At International there are no
/// scouts: the selectors watch every match. The scouts only read what was measured - the innings,
/// the ratings and form; nothing here changes how a match is played.
/// </summary>
public static class Scouting
{
    /// League games of the career player's side a scout watches each season (the final as well).
    public const int ScoutedGames = 3;

    /// What the rung above wants before it offers: form, reports, their average impression.
    public const float MinForm = 45f;
    public const int MinReports = 2;
    public const float MinImpression = 0.5f;

    /// How much of an impression is the innings he saw, the measured ratings, and form.
    private const float InningsWeight = 0.55f, RatingsWeight = 0.3f, FormWeight = 0.15f;

    /// The rating a scout measures the player against: the rung above's (Ladder.PromotionRating);
    /// at the top, the selectors' bar for keeping a place.
    public static float Bar(eRung rung) => Ladder.Top(rung) ? Ladder.RelegationRating(rung) + 10f : Ladder.PromotionRating(rung);

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// Mark the matches a scout watches in a new season: ScoutedGames of the career player's
    /// side's league games (seeded), and at International every one of them (the selectors). The
    /// final is marked when it is made (LeagueBuilder.AddFinal).
    public static void MarkScouts(Season s, Random rng)
    {
        if (s == null || s.fixtures == null || rng == null)
            return;
        var mine = new List<Fixture>();
        foreach (Fixture f in s.fixtures)
            if (f != null && !f.final && f.Involves(s.yourTeam))
                mine.Add(f);
        bool all = Ladder.Top(s.rung);
        var pick = new Random(rng.Next());
        for (int left = mine.Count, need = Math.Min(ScoutedGames, mine.Count); left > 0; left--)
        {
            Fixture f = mine[mine.Count - left];
            // Each game in turn is picked with need/left odds: exactly `need` games, any of them.
            bool take = all || pick.Next(left) < need;
            f.scout = take;
            if (take && !all)
                need--;
        }
    }

    /// What a scout made of an innings at `rung` (the rung it was played at), 0..1: mostly the
    /// innings he saw (runs against the rung's par, the strike rate, not out), then the measured
    /// ratings against Bar(rung), then form. An innings he never got to bat in shows him nothing.
    public static float Impression(InningsRecord r, eRung rung, RatingSet ratings, float form)
    {
        float innings = 0f;
        if (r != null && r.batted)
        {
            float runs = Clamp01(r.runs / (1.5f * CareerProgressMath.ParRuns(rung))) * 0.7f;
            float rate = Clamp01(r.StrikeRate / CareerProgressMath.ParStrikeRate(rung)) * 0.2f;
            innings = runs + rate + (r.isOut ? 0f : 0.1f);
        }
        else if (r != null && !r.leftOut)
            innings = 0.3f;               // did not bat: neither good nor bad

        float overall = ratings != null ? ratings.Overall : RatingSet.Unknown;
        float rated = RatingSet.Known(overall) ? Clamp01(0.5f + (overall - Bar(rung)) / 30f) : 0.3f;
        float formed = RatingSet.Known(form) ? Clamp01(form / 99f) : 0.4f;
        return Clamp01(InningsWeight * innings + RatingsWeight * rated + FormWeight * formed);
    }

    /// "34* off 22 balls against Old Mill CC" / "no innings against Old Mill CC".
    public static string ScoreText(InningsRecord r)
    {
        if (r == null)
            return "the match";
        string against = string.IsNullOrEmpty(r.opponent) ? "" : " against " + r.opponent;
        return r.batted ? $"{r.runs}{(r.isOut ? "" : "*")} off {r.balls} ball{(r.balls == 1 ? "" : "s")}{against}"
                        : "no innings" + against;
    }

    /// The scout's note after watching an innings (`from`: the rung he is from; at the top, the
    /// selectors). Latin-1.
    public static string Report(InningsRecord r, eRung from, float impression)
    {
        string score = ScoreText(r);
        if (Ladder.Top(from))
        {
            string say = impression >= 0.7f ? "That is an innings worthy of the national side."
                       : impression >= 0.5f ? "Solid. Keep that up."
                       : impression >= 0.3f ? "They want more runs from you."
                       : "They need to see a score from you.";
            return $"The selectors watched your {score}. {say}";
        }
        string rung = Ladder.Short(from);
        string verdict = impression >= 0.7f ? $"Very impressed. You looked a level above - I will be telling the {rung} sides about you."
                       : impression >= 0.5f ? $"Promising. Keep scoring like that and the {rung} will come calling."
                       : impression >= 0.3f ? "Not yet. Some good moments, but I need to see bigger scores."
                       : "A quiet day. I will come and watch again.";
        return $"I watched your {score}. {verdict}";
    }

    /// The national selectors do the watching from the Pro League up, not a scout.
    private static bool Selectors(eRung playedAt) => Ladder.Top(Ladder.Above(playedAt));

    /// Who signs a scout's note: "Scout, State", or "Selector" from the Pro League up.
    public static string From(eRung playedAt) =>
        Selectors(playedAt) ? "Selector" : "Scout, " + Ladder.Short(Ladder.Above(playedAt));

    /// A scouted match recorded: the innings marked scouted with the impression, the report kept
    /// for the season, and the note in the inbox. Form includes this innings.
    internal static void Watch(CareerProfile p, InningsRecord rec, eRung rung)
    {
        if (p == null || rec == null)
            return;
        rec.scouted = true;
        rec.impression = Impression(rec, rung, p.ratings, CareerProgress.Form(p));
        p.scoutReports ??= new List<float>();
        p.scoutReports.Add(rec.impression);
        string title = Selectors(rung) ? "The selectors' view" : $"A {Ladder.Short(Ladder.Above(rung))} scout's report";
        CareerProgress.Post(p, eMessageKind.Scout, From(rung), title, Report(rec, Ladder.Above(rung), rec.impression));
    }

    /// The mean of the season's reports (0 with none).
    public static float AverageReport(List<float> reports)
    {
        if (reports == null || reports.Count == 0)
            return 0f;
        float sum = 0f;
        foreach (float r in reports)
            sum += r;
        return sum / reports.Count;
    }

    /// Enough for the rung above to offer: the measured overall at Ladder.PromotionRating(rung),
    /// form at MinForm, and MinReports scouts' reports averaging MinImpression. Never at the top.
    public static bool Interested(CareerProfile p)
    {
        if (p == null || Ladder.Top(p.rung) || p.ratings == null)
            return false;
        float overall = p.ratings.Overall;
        float form = CareerProgress.Form(p);
        return RatingSet.Known(overall) && overall >= Ladder.PromotionRating(p.rung)
            && RatingSet.Known(form) && form >= MinForm
            && p.scoutReports != null && p.scoutReports.Count >= MinReports
            && AverageReport(p.scoutReports) >= MinImpression;
    }

    /// One line for the season review: what the scouts (or the selectors) thought.
    public static string Verdict(List<float> reports, eRung rung)
    {
        int n = reports?.Count ?? 0;
        if (n == 0)
            return Ladder.Top(rung) ? "" : "No scout saw you play this season.";
        float avg = AverageReport(reports);
        string who = Selectors(rung) ? "The selectors" : $"{Ladder.Short(Ladder.Above(rung))} scouts";
        string matches = n == 1 ? "one match" : n + " matches";
        string view = avg >= 0.7f ? "liked what they saw" : avg >= MinImpression ? "saw promise"
                    : avg >= 0.3f ? "want to see more runs" : "were not convinced";
        return $"{who} watched {matches} and {view}.";
    }

    /// The rung above's offer after a season the scouts liked: one to three of its sides (more
    /// the better the reports), each a "join:<rung>:<side>" option, and "stay". From the Pro
    /// League up it is the national selectors' call-up instead ("accept-callup" or "stay").
    internal static CareerMessage Offer(CareerProfile p, Random rng)
    {
        eRung above = Ladder.Above(p.rung);
        string side = p.season?.Yours?.name ?? "your side";
        var m = new CareerMessage { kind = eMessageKind.Offer, date = CareerProgressMath.Today() };
        if (Ladder.Top(above))
        {
            string nation = Nations.Name(p.nation);
            m.from = "Selector";
            m.title = "A national call-up";
            m.body = $"The {nation} selectors have watched your season with {side}. They want you in the squad for the next international season.";
            m.options.Add(new MessageOption { label = $"Play for {nation}", action = "accept-callup" });
            m.options.Add(new MessageOption { label = $"Stay with {side}", action = "stay" });
            return m;
        }
        float avg = AverageReport(p.scoutReports);
        int count = avg >= 0.7f ? 3 : avg >= 0.6f ? 2 : 1;
        var names = new List<NameBank.TeamName>(NameBank.Teams(above));
        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }
        m.from = "Scout, " + Ladder.Short(above);
        string rung = Ladder.Short(above);
        string sides = count == 1 ? $"A {rung} side" : $"{count} {rung} sides";
        m.title = count == 1 ? $"An offer from a {rung} side" : $"Offers from {count} {rung} sides";
        m.body = $"Your season with {side} was noticed. {sides} would like you for next season. "
               + "Join one, or stay where you are for another season.";
        for (int i = 0; i < count && i < names.Count; i++)
            m.options.Add(new MessageOption { label = "Join " + names[i].name, action = $"join:{(int)above}:{names[i].name}" });
        m.options.Add(new MessageOption { label = $"Stay with {side}", action = "stay" });
        return m;
    }
}
