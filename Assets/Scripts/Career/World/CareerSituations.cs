using System;
using System.Collections.Generic;

/// <summary>
/// The match situations of a season (phase 7): which of his side's fixtures ask for a chase, a
/// rain-shortened game or a collapse to rescue, and the final. Dealt once a season, applied to a
/// match's plan as it is made, and stamped on his record-book line. Normal asks nothing.
/// Situations only change the match around him (the other side's innings, the overs, where he comes
/// in and at what score); his own balls are always played and measured as in any match.
/// </summary>
public static class CareerSituations
{
    public const int Rescues = 1, Chases = 1, RainMin = 1, RainMax = 2, RescueWickets = 3, RescueIndex = 4;

    /// Mixed into the plan's seed for Apply's own draws, so the plan's other draws are left as they were.
    private const int ApplySalt = 0x7B5A1C;

    /// What `f` asks of the side: the final is always the final.
    public static eSituation Of(Fixture f) => f == null ? eSituation.Normal : f.final ? eSituation.Final : f.situation;

    // ---- Dealing ----------------------------------------------------------------------------------

    /// The season's situations dealt to its fixtures (not `skipFixture`, a match already begun).
    /// Seeded from the season itself, never from a caller's rng, so every other draw a caller makes
    /// is the same as before phase 7. Only his side's unplayed league fixtures from round 2 on get
    /// one (round 1 is always a normal match); when there are too few, Rain comes first, then the
    /// Rescue, then the Chase, then the second Rain. Sets situationsDealt.
    public static void Deal(Season s, int skipFixture = -1)
    {
        if (s?.fixtures == null)
            return;
        List<Fixture> open = Candidates(s, skipFixture);
        foreach (Fixture f in open)
            f.situation = eSituation.Normal;
        var rng = new Random(StableHash.Fnv1a(DealKey(s)));
        List<eSituation> deal = Wanted(rng);
        for (int i = open.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (open[i], open[j]) = (open[j], open[i]);
        }
        for (int i = 0; i < deal.Count && i < open.Count; i++)
            open[i].situation = deal[i];
        s.situationsDealt = true;
    }

    /// His side's league fixtures still to play from round 2 on, in fixture order.
    private static List<Fixture> Candidates(Season s, int skipFixture)
    {
        var open = new List<Fixture>();
        for (int i = 0; i < s.fixtures.Count; i++)
        {
            Fixture f = s.fixtures[i];
            if (f != null && i != skipFixture && !f.final && !f.played && f.round >= 2 && f.Involves(s.yourTeam))
                open.Add(f);
        }
        return open;
    }

    /// The situations a season asks for, most wanted first: Rain, the Rescue, the Chase, and half
    /// the time a second Rain.
    private static List<eSituation> Wanted(Random rng)
    {
        var deal = new List<eSituation>();
        for (int i = 0; i < RainMin; i++) deal.Add(eSituation.RainShortened);
        for (int i = 0; i < Rescues; i++) deal.Add(eSituation.Rescue);
        for (int i = 0; i < Chases; i++) deal.Add(eSituation.Chase);
        for (int i = RainMin; i < RainMax; i++)
            if (rng.NextDouble() < 0.5) deal.Add(eSituation.RainShortened);
        return deal;
    }

    /// What the deal is seeded from: the season, its sides by name (team ids are only "t0".."t7")
    /// and every fixture's round, sides, lights and pitch.
    private static string DealKey(Season s)
    {
        var key = new System.Text.StringBuilder();
        key.Append(s.number).Append('|').Append((int)s.rung).Append('|').Append(s.yourTeam);
        if (s.teams != null)
            foreach (Team t in s.teams)
                key.Append('|').Append(t?.name);
        foreach (Fixture f in s.fixtures)
            if (f != null)
                key.Append('|').Append(f.round).Append(' ').Append(f.home).Append(' ').Append(f.away)
                   .Append(' ').Append(f.night ? 1 : 0).Append(' ').Append((int)f.pitch);
        return key.ToString();
    }

    // ---- Applying ---------------------------------------------------------------------------------

    /// The plan made to fit its situation, from its own seeded draws (a Normal plan is untouched):
    /// a big chase, overs cut by rain (and the chaser's target revised), a collapse he comes in to
    /// rescue, a packed ground for the final.
    public static void Apply(MatchPlan plan, Team yours, Team them)
    {
        if (plan == null || plan.situation == eSituation.Normal)
            return;
        var rng = new Random(plan.seed ^ ApplySalt);
        switch (plan.situation)
        {
            case eSituation.Chase: BigChase(plan, yours, them, rng); break;
            case eSituation.RainShortened: Rain(plan, rng); break;
            case eSituation.Rescue: Rescue(plan, rng); break;
            case eSituation.Final: plan.crowd = eCrowd.Packed; break;
        }
    }

    /// His side chases, and the other side has made a big total: the higher of two simulated innings
    /// over the full overs (only the other side's innings changes; never a ball of his).
    private static void BigChase(MatchPlan plan, Team yours, Team them, Random rng)
    {
        if (yours == null || them == null)
            return;
        InningsCard a = CareerSim.Innings(them, yours, plan.overs, 0, plan.bowling, rng);
        InningsCard b = CareerSim.Innings(them, yours, plan.overs, 0, plan.bowling, rng);
        plan.batFirst = false;
        plan.theirs = b.runs > a.runs ? b : a;
    }

    /// Rain takes overs: batting first, both sides have the overs left; chasing, the other side batted
    /// its full overs and the chase is cut, with the target revised (CareerSituationMath.RevisedTarget).
    private static void Rain(MatchPlan plan, Random rng)
    {
        int full = plan.overs;
        int left = CareerSituationMath.RainOvers(full, rng);
        if (!plan.batFirst && plan.theirs != null)
            plan.revisedTarget = CareerSituationMath.RevisedTarget(plan.theirs.runs, full, left);
        plan.overs = left;
    }

    /// Three wickets go down before he comes in: he bats at number 5 for this match (moved with his
    /// rating; his own batting slot is untouched), and the top order's collapse is worked out now and
    /// kept in the plan, so the hub, the match and a recovered match all see the same score.
    private static void Rescue(MatchPlan plan, Random rng)
    {
        if (plan.order == null || plan.order.Count <= RescueIndex || plan.slot < 0 || plan.slot >= plan.order.Count)
            return;
        MoveTo(plan, RescueIndex);
        int budget = (int)Math.Round(CareerSituationMath.CollapseShare * Ladder.Overs(plan.rung) * 6);
        plan.collapse = CareerSituationMath.Collapse(plan, RescueWickets, budget, rng, out MatchState end);
        plan.startRuns = end.runs;
        plan.startWickets = end.wickets;
    }

    /// His name and rating moved to `index` in the order; the others keep their order around him.
    private static void MoveTo(MatchPlan plan, int index)
    {
        if (plan.slot == index)
            return;
        bool rated = plan.orderRatings != null && plan.orderRatings.Count == plan.order.Count;
        string name = plan.order[plan.slot];
        plan.order.RemoveAt(plan.slot);
        plan.order.Insert(index, name);
        if (rated)
        {
            float rating = plan.orderRatings[plan.slot];
            plan.orderRatings.RemoveAt(plan.slot);
            plan.orderRatings.Insert(index, rating);
        }
        plan.slot = index;
    }

    // ---- After the match --------------------------------------------------------------------------

    /// The situation, his side's overs and the target, onto his record-book line.
    public static void Stamp(InningsRecord rec, MatchPlan plan)
    {
        if (rec == null || plan == null)
            return;
        rec.situation = plan.situation;
        rec.overs = plan.overs;
        rec.target = plan.Target;
    }

    /// How much of a full innings `r` had (rain): par runs scale by it for form.
    public static float ParScale(InningsRecord r) => ParScale(r, r?.rung ?? eRung.Club);

    /// ParScale against the format of `rung`: his side's overs over the full overs when rain cut
    /// them, else 1 (records from before phase 7 have no overs, and count as full).
    public static float ParScale(InningsRecord r, eRung rung)
    {
        int full = Ladder.Overs(rung);
        return r != null && r.overs > 0 && r.overs < full ? r.overs / (float)full : 1f;
    }

    /// The rng the next match's plan is made with when it has a situation: the same every time it is
    /// asked for (the hub's preview and the match agree), from the career and the fixture alone.
    public static Random PlanRng(CareerProfile p)
    {
        Season s = p?.season;
        int next = s?.fixtures != null ? s.fixtures.IndexOf(CareerProgress.NextFixture(p)) : -1;
        int rung = (int)(s != null ? s.rung : p != null ? p.rung : eRung.Club);
        return new Random(StableHash.Fnv1a($"{p?.name}|{p?.created}|{s?.number}|{rung}|{next}|{s?.yourTeam}"));
    }
}
