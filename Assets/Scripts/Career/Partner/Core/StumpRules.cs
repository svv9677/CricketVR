/// <summary>
/// Which stumps can bowl the striker: only his own end's, and only while the delivery or the shot
/// is live. A ball driven back onto the bowler's end stumps is not bowled (it could only ever be a
/// run-out chance, which the game does not play), and the career player standing at the
/// non-striker's end cannot knock them over and be given out either.
/// </summary>
public static class StumpRules
{
    public static bool CountsAsBowled(bool strikersEnd, eGameState s) =>
        strikersEnd && (s == eGameState.InGame_DeliverBallLoop || s == eGameState.InGame_BallHit || s == eGameState.InGame_BallHitLoop);
}
