using UnityEngine;

/// <summary>
/// The pure rules BallLedger measures a ball by, kept apart from the scene so they can be tested
/// on their own (BallLedgerTests): which side of the stumps, would it have hit them, did the batter
/// play, leave or miss, what a nets shot was worth, the chase situation, how he was out.
/// </summary>
public static class BallLedgerMath
{
    /// Stumps are 0.2286 m across the outside (WideCollider), 0.711 m tall (Law 8).
    public const float StumpHalfWidth = 0.1143f, StumpHeight = 0.71f;
    /// A ball that did not touch the bat was swung at (beaten) when the bat came this close to it
    /// (ball centre to the real blade, m) at least this fast (m/s, the controller's speed).
    public const float SwingNear = 0.3f, SwingSpeed = 3f;
    /// Required rate (per over) from which a chase is under pressure, and the balls left (the last
    /// two overs) in which any chase still within reach is.
    public const float PressureRate = 9f;
    public const int PressureBalls = 12;

    /// A world line (Z at the stumps, metres from middle stump) in the batter's frame, off side
    /// positive: world Z is the right-hander's frame, mirrored for a left-hander - exactly as
    /// Main.DeliverBall turns the bowler's line (batter frame) into the world target.
    public static float LineInBatterFrame(float worldZ, bool leftHanded) => leftHanded ? -worldZ : worldZ;

    /// The ball would have hit the stumps passing them at this line and height (ball centre): within
    /// the stumps' width plus a ball's radius either side, and below the top of the stumps plus a
    /// radius (the bails sit on top). Height is the ball centre's world Y; the ground is ~0.
    public static bool WouldHitStumps(float line, float height) =>
        Mathf.Abs(line) <= StumpHalfWidth + BallFlight.Radius && height <= StumpHeight + BallFlight.Radius;

    /// Played (touched the real blade), missed (touched only the shield's extra width, or swung
    /// close and hard enough to have been trying), or left alone.
    public static eBallAction Action(bool touched, eRealContact realContact, float closest, float peakSwingSpeed)
    {
        if (touched)
            return realContact == eRealContact.WouldHaveMissed ? eBallAction.Missed : eBallAction.Played;
        return closest < SwingNear && peakSwingSpeed > SwingSpeed ? eBallAction.Missed : eBallAction.Left;
    }

    /// Launch angle (degrees, + up) of a velocity.
    public static float LaunchDegrees(Vector3 v) =>
        Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;

    /// Direction a struck ball leaves the bat (degrees) in FieldSets.AngleOf's convention, in the
    /// batter's frame: 0 straight back past the bowler (-X), + the off side, +-180 the keeper.
    public static float ShotAngle(Vector3 exitVelocity, bool leftHanded) =>
        Mathf.Atan2(LineInBatterFrame(exitVelocity.z, leftHanded), -exitVelocity.x) * Mathf.Rad2Deg;

    /// Metres the head has moved down the pitch toward the bowler (-X) since the ball was released:
    /// the batter using his feet (negative when he went back).
    public static float HeadStep(Vector3 atRelease, Vector3 now) => atRelease.x - now.x;

    /// The nets have no field and no scoring (every hit is a dot there, Bat.AutomaticReset), so a shot
    /// is worth what it would have been worth in a match, judged from the contact, where it went and
    /// how far it would have carried:
    ///   * the real blade missed it: 0;
    ///   * an edge, or anything straight back at the keeper (+X): 0, or 1 if hard;
    ///   * carrying 65 m or more on the full: 6;
    ///   * along the ground (first bounce inside 20 m) and hard in front of square: 4;
    ///   * otherwise 0-3 by how hard it was struck.
    /// The rules are the same on both sides of the wicket today; leftHanded turns the shot into the
    /// batter's frame so an off/leg split can come in without changing the callers.
    /// Degrees either side of straight back (+X) that the keeper takes.
    public const float KeeperCone = 15f;

    public static int NetsRunsEquivalent(Vector3 exitVelocity, float carry, eRealContact rc, bool leftHanded)
    {
        if (rc == eRealContact.WouldHaveMissed)
            return 0;
        Vector3 v = new Vector3(exitVelocity.x, exitVelocity.y, LineInBatterFrame(exitVelocity.z, leftHanded));
        float ground = new Vector2(v.x, v.z).magnitude, speed = v.magnitude;
        // Back toward the keeper (within KeeperCone of +X) is the keeper's ball whatever the contact;
        // a clean pull, sweep or glance square or fine of square scores like any other shot.
        bool atKeeper = v.x > 0f && Mathf.Abs(v.z) < v.x * Mathf.Tan(KeeperCone * Mathf.Deg2Rad);
        if (rc == eRealContact.Edge || atKeeper)
            return ground >= 15f ? 1 : 0;
        if (carry >= 65f && v.y > 0f)
            return 6;
        if (carry < 20f && ground >= 25f)
            return 4;
        return speed < 8f ? 0 : speed < 15f ? 1 : speed < 22f ? 2 : 3;
    }

    /// Runs needed per over with `left` legal balls to come (0 when none are left).
    public static float RequiredRate(int need, int left) => left > 0 ? Mathf.Max(0, need) * 6f / left : 0f;

    /// Pressure: chasing at a required rate of PressureRate or more, or in the last two overs of a
    /// chase still within reach (no more needed than a six off every ball left), or a wicket in the
    /// previous two balls.
    public static bool UnderPressure(bool chasing, int need, int left, bool wicketJustFell)
    {
        if (wicketJustFell)
            return true;
        if (!chasing || need <= 0 || left <= 0)
            return false;
        return RequiredRate(need, left) >= PressureRate || (left <= PressureBalls && need <= 6 * left);
    }

    /// How the batter was out, from the state the ball died in and who took it (Main.currentFielderName:
    /// "Bowler" for a return catch, the keeper's name contains "Keeper").
    public static eHowOut HowOut(eGameState terminal, string fielder)
    {
        if (terminal == eGameState.InGame_Bowled)
            return eHowOut.Bowled;
        if (terminal != eGameState.InGame_BallFielded)
            return eHowOut.None;
        if (fielder == "Bowler")
            return eHowOut.CaughtAndBowled;
        if (fielder != null && fielder.Contains("Keeper"))
            return eHowOut.CaughtBehind;
        return eHowOut.Caught;
    }

    /// How a ball ended: out first, then a wide, then the runs off the bat (a six however it came,
    /// a four only over the rope - four run is Runs), then a no-ball the batter scored nothing off,
    /// else a dot. A no-ball's runs off the bat keep their own end; noBall stays on the record.
    public static eBallEnd End(bool wicket, bool wide, bool noBall, int runs, bool boundary)
    {
        if (wicket) return eBallEnd.Out;
        if (wide) return eBallEnd.Wide;
        if (runs >= 6) return eBallEnd.Six;
        if (runs == 4 && boundary) return eBallEnd.Four;
        if (runs > 0) return eBallEnd.Runs;
        return noBall ? eBallEnd.NoBall : eBallEnd.Dot;
    }
}
