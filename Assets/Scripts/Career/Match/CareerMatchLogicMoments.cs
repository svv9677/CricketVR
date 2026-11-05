/// <summary>
/// The record moments of a career match, kept on its MatchState with no scene (phase 7): how many
/// balls he took to his fifty and hundred, and the longest six he hit off the real blade. Pure, so
/// CareerMomentsTests checks them in EditMode. They only watch the innings; nothing here changes a
/// ball or a score.
/// </summary>
public static partial class CareerMatchLogic
{
    /// The first time You.runs reaches 50 / 100, keep You.balls in ballsToFifty / ballsToHundred.
    /// Called with the scoreboard (CareerMatch.Sync), so a fifty reached with the winning runs is
    /// seen before the result is built. Never lowered or moved once kept; 0 means not reached.
    public static void ObserveMoments(MatchState s)
    {
        MatchBatter you = s?.You;
        if (you == null)
            return;
        // At least one ball: 0 is "not reached", and no score comes without a ball faced.
        if (s.ballsToFifty <= 0 && you.runs >= 50)
            s.ballsToFifty = System.Math.Max(1, you.balls);
        if (s.ballsToHundred <= 0 && you.runs >= 100)
            s.ballsToHundred = System.Math.Max(1, you.balls);
    }

    /// A six off the real blade (Clean or Edge only): true when it is the match's longest (stamp kept).
    /// A six the bat shield's extra width found (WouldHaveMissed) or with no contact never counts, so
    /// the shield cannot flatter the record. A tie keeps the first six.
    public static bool NoteSix(MatchState s, float carry, eRealContact contact, string stamp)
    {
        if (s == null || (contact != eRealContact.Clean && contact != eRealContact.Edge))
            return false;
        if (!(carry > 0f) || carry <= s.longestSix)   // NaN and 0 are no carry
            return false;
        s.longestSix = carry;
        s.sixStamp = stamp ?? "";
        return true;
    }
}
