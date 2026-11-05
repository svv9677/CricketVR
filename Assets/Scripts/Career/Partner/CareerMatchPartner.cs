/// <summary>
/// The career match while the player watches his partner bat (phase 3, CareerProfile.watchPartner).
/// Kept apart from CareerMatch.cs, which is at the size limit.
///
/// With watching on, a partner's balls while the player is at the crease at the other end are
/// bowled for real (PartnerDirector plays them), not simulated: the batch of simulated balls stops as
/// soon as he is at the crease at either end (YourTurn), not only when he is on strike, and no ball is
/// held for a batch while he is watching (PartnerWatch.Live in Update and OnGameState). The partner's
/// balls before he comes in, and the rest of the innings once he is out, are simulated as in phase 2.
/// A watched ball's wicket is found as any real ball's (DetectRealWicket); its four or six is counted
/// here (PartnerBoundary), since BallFeed never carries a partner's ball. With watching off nothing
/// here changes anything: YourTurn is "on strike", and Live is always false.
/// </summary>
public partial class CareerMatch
{
    /// The batch stops for the player's turn: on strike, or (watching) at the crease at either end.
    private static bool YourTurn(HUD hud) => PartnerWatch.YourTurn(StrikerIsYou(hud), YouAtCrease(hud), PartnerWatch.Enabled);

    /// A watched partner's four or six (PartnerDirector, from the scorer's call): to his name on the
    /// career's scorecard. Never one of the player's (his come from BallFeed, OnFaced).
    public void PartnerBoundary(Batsman b, int runs)
    {
        if (ended || b == null || b.IsHuman)
            return;
        CountBoundary(b, runs);
    }
}
