using System;

/// <summary>
/// The career moving on: making the first season, planning the next match, taking a finished
/// match into the record book (innings, stats, form, the batting order, the table, the rest of the
/// round simulated), ending a season, and (phase 4) scouts, offers, selection and promotion.
/// Pure over the profile: no scene, so it is tested in EditMode.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these signatures.
/// </summary>
public static class CareerProgress
{
    /// Innings that make up form.
    public const int FormInnings = 5;

    /// Make the first season if the career has none (a new career, or a version-1 save).
    public static void EnsureSeason(CareerProfile p, Random rng) { }

    /// The next fixture of the career player's side this season, or null (season over).
    public static Fixture NextFixture(CareerProfile p) => null;

    /// Everything the next match needs (null when there is none).
    public static MatchPlan PlanNext(CareerProfile p, Random rng) => null;

    /// Take a finished match into the career: the innings into the record book and stats, form
    /// and the batting slot updated, the fixture's result into the table, the round's other
    /// fixtures simulated, and the season ended if that was its last match.
    public static void RecordMatch(CareerProfile p, MatchPlan plan, CareerMatchResult result, Random rng) { }

    /// Form 0..99 over the last FormInnings innings (Unknown with none): runs and strike rate for
    /// the rung, survival.
    public static float Form(CareerProfile p) => RatingSet.Unknown;

    /// The career player's batting rating as his side sees it (his measured overall; a beginner's
    /// prior when nothing is known yet).
    public static float BattingRating(CareerProfile p) => 35f;

    /// Act on a message option (an offer's "join", "decline"); phase 4.
    public static void Choose(CareerProfile p, CareerMessage m, MessageOption o, Random rng) { }

    /// The season is over: review it, then start the next (same rung, up, or down); phase 4 adds
    /// the scouts' offers and selection.
    public static void EndSeason(CareerProfile p, Random rng) { }
}
