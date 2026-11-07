using System.Text;
using UnityEngine;

/// <summary>
/// What one ball measured, in words, for the career's nets (2026-11-06: the shot card said "good
/// contact" while the nets text said "edged"). One judge for both: the contact on the real-width
/// blade (RealBlade), which is what every rating and drill score reads. The shot card shows the
/// same word in the career (ShotCard), adding what the bat shield made of it when that differs.
///
/// Read(r) is the nets' left-hand text under the progress: up to four short lines -
///   1. the verdict: the contact and what came of it ("Middled · FOUR");
///   2. the shot: how hard, where and how far ("112 km/h · along the ground through the covers · 41 m");
///   3. the ball: what it was and where ("Outswinger · good length · 20 cm outside off");
///   4. the coach: one thing about this ball, phrased one of a few ways so it does not repeat.
/// Pure: ShotReadingTests.
/// </summary>
public static class ShotReading
{
    private const string Dot = " · ";
    private const string Good = "#8AB8FF", Bad = "#F2A65A", Grey = "#98A2B3", White = "#E5E7EB";

    /// Stump half-width plus a ball's radius (m): a line inside this, at stump height, hits.
    private const float StumpReach = 0.15f;
    /// Exit speeds (m/s): a block below the first, a firm shot above the second, a rocket above the third.
    private const float Soft = 9f, Firm = 22f, Hit = 30f;
    /// Launch angle (deg) above which a shot is in the air, and well in the air.
    private const float Airborne = 12f, Lofted = 25f;

    /// The contact as the career judges it, on the real blade.
    public static string ContactWord(eRealContact real, int realKind, float realQuality)
    {
        if (real == eRealContact.WouldHaveMissed)
            return "Missed it";
        if (realKind < 0)
            return real == eRealContact.Edge ? "Edge" : realQuality >= 0.8f ? "Middled" : "Good contact";
        return ((BatContact.ContactKind)realKind) switch
        {
            BatContact.ContactKind.Middled => "Middled",
            BatContact.ContactKind.Good => "Good contact",
            BatContact.ContactKind.Toe => "Off the toe",
            BatContact.ContactKind.Shoulder => "Off the shoulder",
            BatContact.ContactKind.Handle => "Off the handle",
            BatContact.ContactKind.ThickEdge => "Thick edge",
            BatContact.ContactKind.ThinEdge => "Thin edge",
            BatContact.ContactKind.Back => "Off the back of the bat",
            _ => "Played",
        };
    }

    /// Clean off the real blade: middled or good contact.
    public static bool IsClean(eRealContact real, int realKind) =>
        real == eRealContact.Clean && (realKind < 0 || realKind == (int)BatContact.ContactKind.Middled ||
                                       realKind == (int)BatContact.ContactKind.Good);

    /// Where a shot went (FieldSets.AngleOf: 0 straight back, + off side, 180 behind).
    public static string Direction(float angle)
    {
        float a = Mathf.Abs(angle);
        bool off = angle >= 0f;
        if (a < 10f) return "straight back past the bowler";
        if (a < 30f) return off ? "past mid-off" : "past mid-on";
        if (a < 70f) return off ? "through the covers" : "through midwicket";
        if (a < 110f) return off ? "square through point" : "square to leg";
        if (a < 150f) return off ? "down to third man" : "fine to leg";
        return "behind the wicket";
    }

    public static string Length(eLengthBand b) => b switch
    {
        eLengthBand.Yorker => "yorker",
        eLengthBand.Full => "full",
        eLengthBand.GoodLength => "good length",
        eLengthBand.BackOfALength => "back of a length",
        _ => "short",
    };

    /// "on the stumps", "20 cm outside off", "down leg".
    public static string Line(float lineAtStumps, bool wouldHit)
    {
        if (wouldHit || Mathf.Abs(lineAtStumps) < StumpReach)
            return "on the stumps";
        int cm = Mathf.RoundToInt((Mathf.Abs(lineAtStumps) - StumpReach + 0.05f) * 20f) * 5;
        if (lineAtStumps > 0f)
            return cm <= 10 ? "just outside off" : cm + " cm outside off";
        return cm <= 10 ? "just down leg" : "down leg";
    }

    /// The four lines, rich text, Latin-1. Empty for no ball.
    public static string Read(BallRecord r)
    {
        if (r == null)
            return "";
        var sb = new StringBuilder();
        sb.Append(Verdict(r));
        string shot = Shot(r);
        if (shot.Length > 0)
            sb.Append('\n').Append("<color=").Append(White).Append('>').Append(shot).Append("</color>");
        sb.Append('\n').Append("<color=").Append(Grey).Append('>').Append(Ball(r)).Append("</color>");
        string tip = ShotTips.For(r);
        if (tip.Length > 0)
            sb.Append('\n').Append("<i>").Append(tip).Append("</i>");
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// The nets' short reading (2026-11-06: "too much text on the left side"): the verdict with
    /// how hard it was hit, and the coach's one line. Read keeps all four for the record.
    public static string Brief(BallRecord r)
    {
        if (r == null)
            return "";
        var sb = new StringBuilder(Verdict(r));
        if (r.action == eBallAction.Played && r.exitSpeed > 0f)
            sb.Append(Dot).Append(Mathf.RoundToInt(r.exitSpeed * 3.6f)).Append(" km/h");
        string tip = ShotTips.For(r);
        if (tip.Length > 0)
            sb.Append('\n').Append("<color=").Append(Grey).Append("><i>").Append(tip).Append("</i></color>");
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// "Middled · FOUR", coloured by how it went.
    public static string Verdict(BallRecord r)
    {
        string how = r.action == eBallAction.Left ? "Left alone"
                   : r.action == eBallAction.Missed ? (r.Touched ? "Missed it (the wide bat found it)" : "Beaten")
                   : ContactWord(r.realContact, r.realKind, r.realQuality);
        bool good = r.action == eBallAction.Left ? !r.wouldHitStumps && !r.Out
                  : r.action == eBallAction.Played && IsClean(r.realContact, r.realKind) && !r.Out;
        return "<b><color=" + (good ? Good : Bad) + ">" + how + "</color></b>" + Dot + End(r);
    }

    private static string End(BallRecord r) => r.end switch
    {
        eBallEnd.Four => "<b>FOUR</b>",
        eBallEnd.Six => "<b>SIX</b>",
        eBallEnd.Out => "<b>OUT</b>" + (r.howOut == eHowOut.Bowled ? " - bowled" : r.howOut == eHowOut.CaughtBehind ? " - caught behind"
                                       : r.howOut == eHowOut.Caught ? " - caught" : ""),
        eBallEnd.Runs => r.runs == 1 ? "1 run" : r.runs + " runs",
        eBallEnd.Wide => "wide",
        eBallEnd.NoBall => "no ball",
        _ => "no run",
    };

    /// "112 km/h · along the ground through the covers · 41 m"; empty when it was not struck.
    public static string Shot(BallRecord r)
    {
        if (r.action != eBallAction.Played || r.exitSpeed <= 0f)
            return "";
        var sb = new StringBuilder();
        sb.Append(Mathf.RoundToInt(r.exitSpeed * 3.6f)).Append(" km/h");
        string air = r.exitSpeed < Soft ? "dead bat" : r.launchDeg >= Lofted ? "lofted" : r.launchDeg >= Airborne ? "in the air" : "along the ground";
        sb.Append(Dot).Append(air).Append(' ').Append(Direction(r.shotAngle));
        if (r.carry >= 3f && r.launchDeg >= Airborne)
            sb.Append(Dot).Append(Mathf.RoundToInt(r.carry)).Append(" m carry");
        return sb.ToString();
    }

    /// "Outswinger · good length · 20 cm outside off · 134 km/h".
    public static string Ball(BallRecord r)
    {
        string what = BowlerRepertoire.Name(r.delivery, r.bowlerKind);
        return what + Dot + Length(r.lengthBand) + Dot + Line(r.lineAtStumps, r.wouldHitStumps) + Dot +
               Mathf.RoundToInt(r.speedKph) + " km/h";
    }

    /// A stable pick among `n` phrasings for this ball (no randomness: tests and replays agree).
    internal static int Pick(BallRecord r, int n)
    {
        if (n <= 1)
            return 0;
        unchecked
        {
            int h = Mathf.RoundToInt(r.speedKph * 10f) * 31 + Mathf.RoundToInt(r.lineAtStumps * 1000f) * 17 +
                    Mathf.RoundToInt(r.exitSpeed * 100f) * 7 + r.drillAttempt * 3 + (int)r.delivery;
            return (h & 0x7fffffff) % n;
        }
    }

    internal const float StumpReachForTips = StumpReach;
    internal const float SoftHit = Soft, FirmHit = Firm, BigHit = Hit, AirLaunch = Airborne, LoftLaunch = Lofted;
}
