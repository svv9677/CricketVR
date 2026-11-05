using System;

/// Where a measured ball was faced.
public enum eBallSource { Trial, Nets, Drill, Match }

/// What the batter did with it: played at it (and touched it), left it alone, or swung and missed
/// (beaten). A ball that hits the bat only because the bat shield widened it - the real blade would
/// have missed - is Missed (BallRecord.realOutcome).
public enum eBallAction { Played, Left, Missed }

/// How the ball ended for the batter.
public enum eBallEnd { Dot, Runs, Four, Six, Out, Wide, NoBall, Dead }

public enum eHowOut { None, Bowled, Caught, CaughtBehind, CaughtAndBowled }

/// How the contact would have been on a real-width bat (the bat shield widens the blade for finding
/// a hit; the career measures against the real one, so a wider shield never inflates a rating).
public enum eRealContact { None, Clean, Edge, WouldHaveMissed }

/// <summary>
/// One ball the career player faced, as measured (BallLedger, in matches and in the nets). This is
/// everything the ratings are computed from (CareerRatings): the delivery as bowled, what the batter
/// did, how well he hit it, what came of it, and the situation. Stored in the save (CareerProfile.balls)
/// so ratings can be recomputed if the formula changes. JsonUtility: public fields only, enums as ints.
/// </summary>
[Serializable]
public class BallRecord
{
    public eBallSource source;
    /// eBowlingLevel the ball was bowled at; the bat shield (eDifficulty) the player had on.
    public int level, shield;

    // ---- The delivery
    public eBowlerKind bowlerKind;
    public eDelivery delivery;
    public eLengthBand lengthBand;
    public float speedKph;
    /// Where it passed the batter's stumps, metres from middle stump in the batter's frame
    /// (+ = off side), and how high (m). Actual if the ball got there, else as aimed.
    public float lineAtStumps, heightAtStumps;
    /// It would have hit the stumps (line within the stumps plus a ball's radius, under the bails).
    public bool wouldHitStumps;
    public bool noBall, freeHit, wide;

    // ---- What the batter did
    public eBallAction action;
    /// BatContact.ContactKind as an int, -1 when not touched.
    public int contact = -1;
    /// BatContact quality 0..1 on the (possibly widened) bat; the same contact on the real blade.
    public float quality, realQuality;
    public eRealContact realContact;
    /// Exit speed (m/s), launch angle (deg, + up), projected carry (m) for a struck ball.
    public float exitSpeed, launchDeg, carry;
    /// Peak bat speed (m/s) while the ball was arriving; closest bat-to-ball distance (m).
    public float swingSpeed, closestApproach;

    // ---- What came of it
    public eBallEnd end;
    /// Runs to the batter (matches); in the nets, a runs-equivalent from the carry and direction.
    public int runs;
    public eHowOut howOut;

    // ---- The situation (matches; false/0 in the nets)
    public bool pressure, wicketJustFell, chasing;
    public float requiredRate;

    public bool Touched => contact >= 0;
    public bool Out => end == eBallEnd.Out;
    public bool Spin => bowlerKind == eBowlerKind.OffSpin || bowlerKind == eBowlerKind.LegSpin || bowlerKind == eBowlerKind.Mystery;
    /// Counts toward the ratings: a real delivery the batter could play (not a wide, not a dead ball).
    public bool Counts => !wide && end != eBallEnd.Dead && end != eBallEnd.Wide;
}

/// <summary>
/// Every measured ball, as it is settled (BallLedger raises it once per delivery, in every scene
/// with a Main). Career code listens here; nothing in the game itself depends on it.
/// </summary>
public static class BallFeed
{
    public static event Action<BallRecord> Faced;

    public static void Raise(BallRecord r) => Faced?.Invoke(r);

    /// Tests: drop every listener.
    public static void ClearForTests() => Faced = null;
}
