using System.Collections.Generic;

/// <summary>
/// The career ratings, computed from the measured balls (BallRecord) - never the other way round:
/// a rating describes how the player actually bats, and nothing in the game reads it to help them.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these signatures.
/// </summary>
public static class CareerRatings
{
    /// The ratings are over the most recent Window counting balls (slow, earned skill).
    public const int Window = 300;

    public static RatingSet Compute(IList<BallRecord> balls) => new RatingSet { sample = balls?.Count ?? 0 };

    /// "--" (unknown), "Raw", "Developing", "Solid", "Strong", "Elite".
    public static string Grade(float rating) => RatingSet.Known(rating) ? "Solid" : "--";

    /// Index (RatingSet order) of the strongest / weakest known rating, -1 when none is known.
    public static int Strongest(RatingSet r) => -1;
    public static int Weakest(RatingSet r) => -1;

    /// One line from the coach on the ratings ("Lovely timing; your judgement outside off needs work.").
    public static string Verdict(RatingSet r) => "";
}
