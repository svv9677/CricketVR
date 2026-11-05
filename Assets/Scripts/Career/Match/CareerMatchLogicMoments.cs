/// <summary>
/// The record moments of a career match, kept on its MatchState with no scene (phase 7): how many
/// balls he took to his fifty and hundred, and the longest six he hit off the real blade.
/// </summary>
public static partial class CareerMatchLogic
{
    /// The first time You.runs reaches 50 / 100, keep You.balls in ballsToFifty / ballsToHundred.
    public static void ObserveMoments(MatchState s) { }

    /// A six off the real blade (Clean or Edge only): true when it is the match's longest (stamp kept).
    public static bool NoteSix(MatchState s, float carry, eRealContact contact, string stamp) => false;
}
