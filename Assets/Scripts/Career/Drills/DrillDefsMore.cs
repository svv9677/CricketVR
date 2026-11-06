using B = eLengthBand;
using D = eDelivery;
using K = eBowlerKind;

/// <summary>
/// The second set of drills (2026-11-06: "add more training modes"), as data like DrillDefs' first
/// five: twelve balls each, every one a ball that bowler really bowls, inside the legal line window.
///
///   Straight hitting   Timing, Power. Full and good-length balls on and just outside off from the
///                      seamers and the off-spinner: hit them back down the ground, in the V.
///   Swing              vs Pace, Defence, Judgement. In- and out-swingers at a good length: play the
///                      straight ones late and soft, leave the ones swinging away.
///   Power hitting      Power, Timing. Full and in the slot from the medium-pacer and the spinners:
///                      clear the rope.
///   Death overs        Power, vs Pace, Judgement. Yorkers, wide yorkers, slower balls and bouncers
///                      from the quick: score off every ball.
///   Soft hands         Defence, vs Spin. Good-length balls on the stumps from seam and spin: kill it
///                      dead at your feet, no edge carrying.
/// </summary>
public static class DrillDefsMore
{
    private const int Timing = 0, Power = 1, Pace = 2, Spin = 3, Defence = 4, Judgement = 5;

    private static DrillBall Ball(K k, D d, B b, float min, float max) => new DrillBall(k, d, b, min, max);

    public static readonly DrillDef Straight = new DrillDef
    {
        id = eDrill.StraightHitting, name = "Straight hitting", title = "STRAIGHT-HITTING DRILL",
        blurb = "Full balls on off stump. Head still, full face of the bat: hit them back past the bowler, in the V.",
        feeds = new[] { Timing, Power },
        balls = new[]
        {
            Ball(K.Medium, D.Stock, B.Full, -0.05f, 0.15f),
            Ball(K.Medium, D.Stock, B.Full, 0.05f, 0.25f),
            Ball(K.Medium, D.InSwinger, B.Full, -0.05f, 0.10f),
            Ball(K.Medium, D.Stock, B.GoodLength, -0.05f, 0.15f),
            Ball(K.Fast, D.Stock, B.Full, -0.05f, 0.15f),
            Ball(K.Fast, D.OutSwinger, B.Full, 0f, 0.20f),
            Ball(K.Fast, D.Stock, B.GoodLength, 0f, 0.20f),
            Ball(K.Medium, D.SlowerBall, B.Full, -0.05f, 0.15f),
            Ball(K.OffSpin, D.OffBreak, B.Full, 0f, 0.20f),
            Ball(K.OffSpin, D.Flighted, B.Full, 0.05f, 0.25f),
            Ball(K.OffSpin, D.TopSpinner, B.Full, -0.05f, 0.10f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, 0f, 0.15f),
        },
    };

    public static readonly DrillDef Swing = new DrillDef
    {
        id = eDrill.Swing, name = "Swing", title = "SWING DRILL",
        blurb = "The new ball is moving. Play the straight ones late and soft; leave the ones swinging away.",
        feeds = new[] { Pace, Defence, Judgement },
        balls = new[]
        {
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0f, 0.15f),
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0.35f, 0.65f),
            Ball(K.Medium, D.InSwinger, B.GoodLength, 0f, 0.20f),
            Ball(K.Medium, D.InSwinger, B.Full, -0.05f, 0.10f),
            Ball(K.Medium, D.OutSwinger, B.Full, 0.10f, 0.30f),
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0.45f, 0.75f),
            Ball(K.Fast, D.OutSwinger, B.GoodLength, 0f, 0.15f),
            Ball(K.Fast, D.InSwinger, B.GoodLength, 0.05f, 0.25f),
            Ball(K.Fast, D.OutSwinger, B.GoodLength, 0.40f, 0.70f),
            Ball(K.Fast, D.InSwinger, B.Full, -0.05f, 0.10f),
            Ball(K.Fast, D.OutSwinger, B.Full, 0.05f, 0.20f),
            Ball(K.Fast, D.OffCutter, B.GoodLength, 0f, 0.15f),
        },
    };

    public static readonly DrillDef PowerHitting = new DrillDef
    {
        id = eDrill.PowerHitting, name = "Power hitting", title = "POWER-HITTING DRILL",
        blurb = "Balls in the slot. Base set, swing through the line: clear the rope.",
        feeds = new[] { Power, Timing },
        balls = new[]
        {
            Ball(K.Medium, D.Stock, B.Full, -0.05f, 0.15f),
            Ball(K.Medium, D.Stock, B.Full, 0.05f, 0.25f),
            Ball(K.Medium, D.SlowerBall, B.Full, -0.05f, 0.15f),
            Ball(K.Medium, D.Stock, B.GoodLength, 0f, 0.20f),
            Ball(K.Medium, D.InSwinger, B.Full, -0.05f, 0.10f),
            Ball(K.Medium, D.Stock, B.Full, 0.10f, 0.30f),
            Ball(K.OffSpin, D.Flighted, B.Full, 0f, 0.20f),
            Ball(K.OffSpin, D.OffBreak, B.Full, 0.05f, 0.25f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, -0.05f, 0.15f),
            Ball(K.LegSpin, D.LegBreak, B.Full, 0.05f, 0.25f),
            Ball(K.LegSpin, D.Flighted, B.Full, 0f, 0.20f),
            Ball(K.LegSpin, D.Googly, B.Full, -0.05f, 0.15f),
        },
    };

    public static readonly DrillDef Death = new DrillDef
    {
        id = eDrill.DeathOvers, name = "Death overs", title = "DEATH-OVERS DRILL",
        blurb = "The last two overs. Yorkers, wide ones, slower balls and bouncers: score off every ball.",
        feeds = new[] { Power, Pace, Judgement },
        balls = new[]
        {
            Ball(K.Fast, D.Stock, B.Yorker, -0.05f, 0.10f),
            Ball(K.Fast, D.Stock, B.Yorker, 0.40f, 0.65f),
            Ball(K.Fast, D.SlowerBall, B.Full, -0.05f, 0.15f),
            Ball(K.Fast, D.Stock, B.Short, -0.05f, 0.15f),
            Ball(K.Fast, D.OffCutter, B.GoodLength, 0f, 0.20f),
            Ball(K.Fast, D.Stock, B.Full, 0.05f, 0.25f),
            Ball(K.Fast, D.Stock, B.Yorker, -0.05f, 0.10f),
            Ball(K.Fast, D.SlowerBall, B.BackOfALength, 0f, 0.20f),
            Ball(K.Fast, D.Stock, B.Yorker, 0.45f, 0.70f),
            Ball(K.Fast, D.LegCutter, B.Full, 0.05f, 0.25f),
            Ball(K.Fast, D.Stock, B.Short, 0.30f, 0.55f),
            Ball(K.Fast, D.SlowerBall, B.Yorker, -0.05f, 0.10f),
        },
    };

    public static readonly DrillDef SoftHands = new DrillDef
    {
        id = eDrill.SoftHands, name = "Soft hands", title = "SOFT-HANDS DRILL",
        blurb = "Good-length balls on the stumps. Defend with soft hands: kill it dead and let nothing carry.",
        feeds = new[] { Defence, Spin },
        balls = new[]
        {
            Ball(K.Medium, D.Stock, B.GoodLength, -0.05f, 0.10f),
            Ball(K.Medium, D.OutSwinger, B.GoodLength, 0f, 0.12f),
            Ball(K.Fast, D.Stock, B.GoodLength, -0.05f, 0.10f),
            Ball(K.Fast, D.OffCutter, B.BackOfALength, -0.05f, 0.10f),
            Ball(K.Fast, D.InSwinger, B.GoodLength, 0f, 0.10f),
            Ball(K.Medium, D.LegCutter, B.GoodLength, 0f, 0.12f),
            Ball(K.OffSpin, D.OffBreak, B.GoodLength, 0f, 0.15f),
            Ball(K.OffSpin, D.Slider, B.GoodLength, -0.05f, 0.10f),
            Ball(K.OffSpin, D.Flighted, B.GoodLength, 0f, 0.15f),
            Ball(K.LegSpin, D.LegBreak, B.GoodLength, -0.05f, 0.10f),
            Ball(K.LegSpin, D.Googly, B.GoodLength, 0f, 0.12f),
            Ball(K.LegSpin, D.TopSpinner, B.GoodLength, -0.05f, 0.10f),
        },
    };
}
