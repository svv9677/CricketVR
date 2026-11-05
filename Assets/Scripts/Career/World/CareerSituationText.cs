/// <summary>
/// Every line the match situations show (phase 7): the hub's next-match line, the card before the
/// first ball, and the tag on the result. "" when the match has no situation.
/// </summary>
public static class CareerSituationText
{
    /// The hub's next-match line for a situation ("" for none).
    public static string HubLine(MatchPlan plan, int slot) => "";

    /// The card's heading before the first ball ("" for none).
    public static string IntroHeading(MatchPlan plan) => "";

    /// The card's body before the first ball.
    public static string IntroBody(MatchPlan plan) => "";

    /// The short tag after the result's headline ("" for none).
    public static string ResultTag(MatchPlan plan) => "";
}
