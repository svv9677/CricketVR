using UnityEngine;

/// <summary>
/// What the partner means to do with a ball: CareerSim's outcome for his rating, the same odds a
/// simulated partner's ball is scored by, so watching him does not change how well he bats. The
/// planner then picks the shot that makes it happen and the real game decides what it was.
///
///   * A wide is the bowler's, not the batter's: a delivery that will be wide is left alone
///     (LeaveWide) and CareerSim's own Wide is re-rolled - the live ball decides wides.
///   * 5 (never drawn today) is 4: there is no shot that runs five.
///   * No wicket off a ball he cannot be out to (a no-ball's free hit): a dot instead.
/// </summary>
public static class PartnerIntent
{
    /// Leave a wide alone.
    public const int LeaveWide = 100;
    /// Re-rolls of CareerSim's Wide before it is taken as a dot.
    public const int WideRerolls = 4;
    /// How far the batter may drift across before the side wide lines follow him (WideCollider).
    public const float StanceTolerance = 0.12f;
    /// The wide guideline on the off side and the outside of leg stump, from middle stump (WideCollider).
    public const float OffSideWideLine = 0.889f, LegSideWideLine = 0.1143f;

    /// An outcome code: CareerSim.Wicket, 0..4, 6 (a Wide re-rolled; 5 is 4; no wicket off a
    /// no-ball/free hit), or LeaveWide when the delivery is a wide.
    public static int Draw(float rating, eBowlingLevel level, float aggression, System.Random rng, bool deliveryIsWide, bool protectedBall)
    {
        if (deliveryIsWide)
            return LeaveWide;
        int r = CareerSim.Ball(rating, level, aggression, rng);
        for (int i = 0; i < WideRerolls && r == CareerSim.Wide; i++)
            r = CareerSim.Ball(rating, level, aggression, rng);
        if (r == CareerSim.Wide) r = 0;
        if (r == 5) r = 4;
        if (protectedBall && r == CareerSim.Wicket) r = 0;
        return r;
    }

    /// The delivery will be called wide where the batter stands: past the off-side guideline or
    /// outside leg stump with all of the ball (the lines move with a batter who has stepped across,
    /// as WideCollider's boxes do), or over his head at the stumps (Ball.HeadTop). stanceZ is where
    /// he takes guard (world z), offSign +1 for a right-hander, headTop the top of his head (world y).
    public static bool IsWide(in BallDelivery.Solution d, float stanceZ, float offSign, float headTop)
    {
        float o = offSign < 0f ? -1f : 1f;
        float r = BallFlight.Radius;
        float offStep = Mathf.Max(0f, stanceZ * o - StanceTolerance);
        float legStep = Mathf.Max(0f, -stanceZ * o - StanceTolerance);
        if (d.lineZ * o - r > OffSideWideLine + offStep) return true;
        if (-d.lineZ * o - r > LegSideWideLine + legStep) return true;
        return d.heightAtStumps - r > headTop;
    }
}
