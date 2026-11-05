using System;

/// <summary>
/// The match situations of a season (phase 7): which of his side's fixtures ask for a chase, a
/// rain-shortened game or a collapse to rescue, and the final. Dealt once a season, applied to a
/// match's plan as it is made, and stamped on his record-book line. Normal asks nothing.
/// </summary>
public static class CareerSituations
{
    public const int Rescues = 1, Chases = 1, RainMin = 1, RainMax = 2, RescueWickets = 3, RescueIndex = 4;

    /// What `f` asks of the side: the final is always the final.
    public static eSituation Of(Fixture f) => f == null ? eSituation.Normal : f.final ? eSituation.Final : f.situation;

    /// The season's situations dealt to its fixtures (not `skipFixture`, a match already begun).
    public static void Deal(Season s, int skipFixture = -1) { }

    /// The plan made to fit its situation.
    public static void Apply(MatchPlan plan, Team yours, Team them) { }

    /// The situation, his side's overs and the target, onto his record-book line.
    public static void Stamp(InningsRecord rec, MatchPlan plan) { }

    /// How much of a full innings `r` had (rain): par runs scale by it for form.
    public static float ParScale(InningsRecord r) => 1f;

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
