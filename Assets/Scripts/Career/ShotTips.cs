using UnityEngine;

/// <summary>
/// The coach's one line about a single ball in the nets (ShotReading's fourth line): what went
/// right or wrong with this ball, judged on the real blade, with a few phrasings of each so a
/// session does not read the same line twice running. Pure: ShotReadingTests.
///
///   Out            bowled, caught, caught behind: what beat him.
///   Left           a good leave (by how much it missed), a brave one, or one that was hitting.
///   Beaten         outside off (could have left it), by swing, by turn, by pace, on the stumps.
///   Mishit         the edges, the toe, the shoulder, the handle: what each says about the shot.
///   Clean          a six, a boundary through the field, in the air, across the line, a block,
///                  a timed shot, a good one not quite out of the middle.
/// </summary>
public static class ShotTips
{
    public static string For(BallRecord r)
    {
        if (r == null || !r.Counts)
            return "";
        if (r.Out) return Out(r);
        if (r.action == eBallAction.Left) return Left(r);
        if (r.action == eBallAction.Missed) return Beaten(r);
        if (!ShotReading.IsClean(r.realContact, r.realKind)) return Mishit(r);
        return Clean(r);
    }

    private static string One(BallRecord r, params string[] lines) => lines[ShotReading.Pick(r, lines.Length)];

    private static bool Outside(BallRecord r) => r.lineAtStumps > ShotReading.StumpReachForTips + 0.1f && !r.wouldHitStumps;
    private static bool Swinging(BallRecord r) =>
        r.delivery == eDelivery.InSwinger || r.delivery == eDelivery.OutSwinger || r.delivery == eDelivery.OffCutter || r.delivery == eDelivery.LegCutter;

    private static string Out(BallRecord r)
    {
        if (r.howOut == eHowOut.Bowled)
            return r.lengthBand == eLengthBand.Yorker
                ? One(r, "The yorker got under the bat. Get it down earlier.", "Too slow getting the bat down to the full one.")
                : BowlerRepertoire.IsSpin(r.bowlerKind)
                    ? One(r, "Played for turn that was not there - watch the hand.", "Through the gate. Bat and pad together against spin.")
                    : One(r, "Bowled. Watch it right onto the bat.", "Gate open - keep the bat close to the pad.");
        if (r.howOut == eHowOut.CaughtBehind)
            return One(r, "Feeling for it outside off. Let it go.", "Hard hands away from the body - that finds the edge.");
        return r.launchDeg >= ShotReading.LoftLaunch
            ? One(r, "Up in the air and not far enough. Commit to the big shot or keep it down.", "Skied it. Get under it fully, or play along the ground.")
            : One(r, "Picked out a fielder in the air. Keep it down.", "Caught - played it too early, the ball popped up.");
    }

    private static string Left(BallRecord r)
    {
        if (r.wouldHitStumps)
            return One(r, "That was hitting the stumps! Never leave the straight one.", "You left a ball that would have bowled you.");
        float margin = Mathf.Abs(r.lineAtStumps) - ShotReading.StumpReachForTips;
        if (margin < 0.08f)
            return One(r, $"Brave leave - it missed by {Mathf.Max(1, Mathf.RoundToInt(margin * 100f))} cm.", "Close. You know where your off stump is.");
        if (r.lengthBand == eLengthBand.Short && r.heightAtStumps > 1.2f)
            return One(r, "Good judgement - let the bouncer go.", "Sensible. No need to take that one on.");
        return One(r, "Good leave - make the bowler come to you.", "Well left. Nothing to play at.", "Disciplined - that was going nowhere.");
    }

    private static string Beaten(BallRecord r)
    {
        if (Outside(r))
            return One(r, "Beaten outside off - you could have left it.", "Chasing it wide. Let it go.", "Playing away from your body - stay in line.");
        if (BowlerRepertoire.IsSpin(r.bowlerKind))
            return One(r, "Beaten by the turn. Read it from the hand.", "Done in the flight - get to the pitch or stay back.");
        if (Swinging(r))
            return One(r, "Beaten by the movement. Play it later, under your eyes.", "It moved late. Soft hands and play it late.");
        if (r.speedKph >= 138f)
            return One(r, "Hurried by the pace. Start your movement earlier.", "Too quick for you there - trigger early, then still.");
        return One(r, "Beaten. Watch it all the way onto the bat.", "Missed it - keep your head over the ball.");
    }

    private static string Mishit(BallRecord r)
    {
        switch ((BatContact.ContactKind)Mathf.Max(0, r.realKind))
        {
            case BatContact.ContactKind.ThinEdge:
                return Outside(r) ? One(r, "Thin edge - you were chasing it.", "Nicked it. That one could have been left.")
                                  : One(r, "Thin edge - you were early on it.", "Just got a nick. Play it later.");
            case BatContact.ContactKind.ThickEdge:
                return One(r, "Thick edge - the face turned as you hit it.", "Off the outside half. Stay side-on through the shot.");
            case BatContact.ContactKind.Toe:
                return One(r, "Off the toe - you were reaching for it. Get to the pitch of it.", "Toe-ended. Bat came down a fraction early.");
            case BatContact.ContactKind.Shoulder:
                return One(r, "Off the shoulder - it climbed on you. Get on top of the bounce.", "High on the bat - get your hands above it.");
            case BatContact.ContactKind.Handle:
                return One(r, "Off the handle - cramped for room.", "Too close to the body. Give yourself room to swing.");
            case BatContact.ContactKind.Back:
                return "Off the back of the bat - the face was closed.";
            default:
                return r.action == eBallAction.Missed ? "" : One(r, "Not out of the middle. Head over the ball.", "Mistimed.");
        }
    }

    private static string Clean(BallRecord r)
    {
        bool middled = r.realKind == (int)BatContact.ContactKind.Middled || r.realQuality >= 0.8f;
        if (r.end == eBallEnd.Six)
            return One(r, $"Huge! {Mathf.RoundToInt(r.carry)} m and right out of the middle.", "Clean strike. That has gone a long way.");
        if (r.end == eBallEnd.Four && r.launchDeg < ShotReading.AirLaunch)
            return One(r, "Pierced the field along the ground.", "Placed perfectly. No chance for the fielder.", "Timed it - just bisected them.");
        if (r.lineAtStumps > 0.25f && r.shotAngle < -45f)
            return One(r, "Hit across the line from outside off. It came off, but it is a risk.", "Dragged it to leg from wide - the straight bat is safer.");
        if (r.exitSpeed < ShotReading.SoftHit)
            return One(r, "Solid defence - soft hands, dead at your feet.", "Good block. Bat and pad together.", "Played with soft hands.");
        if (r.launchDeg >= ShotReading.LoftLaunch && r.end != eBallEnd.Four)
            return One(r, "Lofted, but it would hang up for the fielder. Hit through it.", "In the air - needs more on it to clear the ring.");
        if (r.exitSpeed >= ShotReading.BigHit && middled)
            return One(r, $"Rocket! {Mathf.RoundToInt(r.exitSpeed * 3.6f)} km/h off the bat.", "Middled it hard.");
        if (middled)
            return One(r, "Out of the middle. Lovely timing.", "Sweet spot. Well played.", "Beautifully timed.");
        return One(r, "Decent contact, not quite the middle. Let it come to you.", "Good shot - a touch more timing and it races away.");
    }
}
