using UnityEngine;

/// <summary>
/// How each delivery is held, for BowlerGrip. All in the bowling hand's frame: x along the fingers,
/// y out of the palm, z across the knuckles toward the index finger (right hand).
///
///   * Seam up (stock ball, arm ball): first two fingers close together either side of the seam,
///     the seam upright between them, the thumb under it on the seam, the other two tucked.
///   * Out/inswinger: the same grip with the seam angled toward the slips / fine leg (the wrist
///     cocked through the release so the seam points there).
///   * Off/leg-cutter: the fingers across the seam on the diagonal, the wrist rolling over to
///     drag them down the right / left side of the ball.
///   * Slower ball: deeper in the hand, fingers spread wide, the wrist held back.
///   * Off-break: index and middle wide apart across the seam, the ball in the crook of the index
///     finger, the wrist turning clockwise (from behind) and the index finger flicking. Flighted:
///     more of the same. Top-spinner: the wrist snapping forward, the fingers over the top.
///   * Leg-break: the ball in the fingers, the third finger bent along the seam, the palm turned
///     toward the leg side, the wrist flicking anticlockwise. Googly: the hand turned right over,
///     the back of it to the batter. Flipper: squeezed out from under the hand.
///   * Carrom ball: between the thumb and the bent middle finger, which flicks it out.
/// </summary>
public static class BowlerGrips
{
    public enum SpinKind { Back, Top, Side }

    public class Grip
    {
        public string name;
        public Vector3 ball;              // ball centre: (along fingers, off palm, toward index), m
        public Vector3 seamAxis;          // the seam's axis (normal to its plane), same frame
        public Vector3 thumbAt;           // where on the ball the thumb goes, direction from centre
        public float[] spread;            // per finger (thumb unused), degrees toward the index side
        public bool[] onBall;             // curled until the tip meets the ball
        public float[] tuck;              // curl of the fingers off the ball (and the most for those on it)
        public float seamAngle;           // degrees toward the off side (swing)
        public float palmTurn;            // degrees about up (+: palm toward the bowler's right)
        public float roll;                // degrees about the line of travel (+: clockwise from behind)
        public float snap;                // degrees about the bowler's right (+: fingers forward over the ball)
        public int flickFinger = -1;      // the finger that flicks straight at the release
        public SpinKind spin = SpinKind.Back;
        public float spinRate = 75f;      // rad/s (12 revolutions a second)
        /// The seam is set by the line of the delivery into the release, not by the hand: upright
        /// and pointing down the pitch (or angled seamAngle for swing) - what makes it swing.
        public bool seamDownThePitch;

        public Grip Clone(string newName)
        {
            var g = (Grip)MemberwiseClone();
            g.name = newName;
            g.spread = (float[])spread.Clone();
            g.onBall = (bool[])onBall.Clone();
            g.tuck = (float[])tuck.Clone();
            return g;
        }
    }

    private static readonly float Diag = Mathf.Sin(35f * Mathf.Deg2Rad), DiagC = Mathf.Cos(35f * Mathf.Deg2Rad);

    public static readonly Grip SeamUp = new Grip
    {
        name = "Seam up",
        // Along the fingers and off the palm, so they run up the back of the ball and the tips
        // come over the top (nearer the palm, they curled under it: balanced on the fingertips).
        ball = new Vector3(0.13f, 0.046f, 0.006f),
        seamAxis = new Vector3(0f, 0f, 1f),
        thumbAt = new Vector3(-1f, 0.35f, 0.05f),
        spread = new[] { 0f, 5f, -4f, -10f, -16f },
        onBall = new[] { false, true, true, false, false },
        tuck = new[] { 0f, 0.9f, 0.9f, 0.85f, 0.95f },
        spin = SpinKind.Back, spinRate = 75f,
        seamDownThePitch = true,
    };
    public static readonly Grip OutSwing = With(SeamUp.Clone("Outswinger"), g => g.seamAngle = 20f);
    public static readonly Grip InSwing = With(SeamUp.Clone("Inswinger"), g => g.seamAngle = -20f);
    public static readonly Grip OffCutter = With(SeamUp.Clone("Off-cutter"), g =>
    {
        g.seamAxis = new Vector3(Diag, 0f, DiagC);
        g.spread = new[] { 0f, 9f, -2f, -10f, -16f };
        g.roll = 30f; g.spinRate = 90f; g.seamDownThePitch = false;
    });
    public static readonly Grip LegCutter = With(SeamUp.Clone("Leg-cutter"), g =>
    {
        g.seamAxis = new Vector3(-Diag, 0f, DiagC);
        g.spread = new[] { 0f, 9f, -2f, -10f, -16f };
        g.roll = -30f; g.spinRate = 90f; g.seamDownThePitch = false;
    });
    public static readonly Grip Slower = With(SeamUp.Clone("Slower ball"), g =>
    {
        g.ball = new Vector3(0.112f, 0.047f, 0f);
        g.spread = new[] { 0f, 16f, -14f, -14f, -20f };
        g.onBall = new[] { false, true, true, true, false };
        g.tuck = new[] { 0f, 0.95f, 0.95f, 0.95f, 0.9f };
        g.snap = -15f; g.spinRate = 35f;
    });
    public static readonly Grip OffBreak = new Grip
    {
        name = "Off-break",
        ball = new Vector3(0.12f, 0.040f, 0.004f),
        seamAxis = new Vector3(DiagC, 0f, Diag),
        thumbAt = new Vector3(-0.8f, 0.2f, 0.55f),
        spread = new[] { 0f, 18f, -10f, -14f, -18f },
        onBall = new[] { false, true, true, true, false },
        tuck = new[] { 0f, 0.95f, 0.95f, 0.9f, 0.95f },
        roll = 35f, snap = 10f, flickFinger = 1,
        spin = SpinKind.Side, spinRate = 155f,
    };
    public static readonly Grip Flighted = With(OffBreak.Clone("Flighted"), g => { g.roll = 45f; g.snap = 20f; g.spinRate = 175f; });
    public static readonly Grip OffTopSpinner = With(OffBreak.Clone("Top-spinner"), g => { g.roll = 0f; g.snap = 40f; g.spin = SpinKind.Top; });
    public static readonly Grip ArmBall = With(SeamUp.Clone("Arm ball"), g => g.spinRate = 60f);
    public static readonly Grip LegBreak = new Grip
    {
        name = "Leg-break",
        ball = new Vector3(0.105f, 0.043f, -0.004f),
        seamAxis = new Vector3(DiagC, 0f, -Diag),
        thumbAt = new Vector3(-0.85f, 0.25f, 0.45f),
        spread = new[] { 0f, 10f, -2f, -12f, -16f },
        onBall = new[] { false, true, true, true, false },
        tuck = new[] { 0f, 0.9f, 0.9f, 1f, 0.95f },
        palmTurn = 45f, roll = -40f, flickFinger = 3,
        spin = SpinKind.Side, spinRate = 165f,
    };
    public static readonly Grip Googly = With(LegBreak.Clone("Googly"), g => { g.palmTurn = 130f; g.roll = -20f; });
    public static readonly Grip LegTopSpinner = With(LegBreak.Clone("Top-spinner"), g => { g.roll = 0f; g.snap = 40f; g.spin = SpinKind.Top; });
    public static readonly Grip LegFlighted = With(LegBreak.Clone("Flighted"), g => { g.roll = -50f; g.snap = 15f; g.spinRate = 185f; });
    public static readonly Grip Flipper = With(LegBreak.Clone("Flipper"), g =>
    {
        g.ball = new Vector3(0.115f, 0.040f, 0.006f);
        g.onBall = new[] { false, true, true, false, false };
        g.palmTurn = 20f; g.roll = 0f; g.snap = -35f; g.flickFinger = -1;
        g.spin = SpinKind.Back; g.spinRate = 95f;
    });
    public static readonly Grip Carrom = new Grip
    {
        name = "Carrom ball",
        ball = new Vector3(0.115f, 0.038f, 0f),
        seamAxis = new Vector3(0f, 0f, 1f),
        thumbAt = new Vector3(0.1f, 0.55f, 0.83f),
        spread = new[] { 0f, 10f, 0f, -12f, -16f },
        onBall = new[] { false, true, true, false, false },
        tuck = new[] { 0f, 0.8f, 1f, 0.9f, 0.95f },
        roll = -25f, flickFinger = 2,
        spin = SpinKind.Side, spinRate = 150f,
    };

    private static Grip With(Grip g, System.Action<Grip> edit) { edit(g); return g; }

    /// The grip for this delivery.
    public static Grip For(BowlingParams p)
    {
        if (p == null) return SeamUp;
        bool leg = p.kind == eBowlerKind.LegSpin;
        switch (p.delivery)
        {
            case eDelivery.OutSwinger: return OutSwing;
            case eDelivery.InSwinger: return InSwing;
            case eDelivery.OffCutter: return OffCutter;
            case eDelivery.LegCutter: return LegCutter;
            case eDelivery.SlowerBall: return Slower;
            case eDelivery.OffBreak: return OffBreak;
            case eDelivery.LegBreak: return LegBreak;
            case eDelivery.Googly: return Googly;
            case eDelivery.Carrom: return Carrom;
            case eDelivery.TopSpinner: return leg ? LegTopSpinner : OffTopSpinner;
            case eDelivery.Slider: return leg ? Flipper : ArmBall;
            case eDelivery.Flighted: return leg ? LegFlighted : Flighted;
            default:
                // The stock ball of a spinner is his stock spinning ball.
                return p.kind == eBowlerKind.OffSpin ? OffBreak : leg ? LegBreak : SeamUp;
        }
    }

    /// The spin the ball leaves with (rad/s, world): backspin about the seam for a seamer (the seam
    /// stays upright in flight), over-spin for a top-spinner, and for a spinner side-spin the way
    /// it turns (turnDegrees: + into a right-hander) with some over-spin.
    public static Vector3 Spin(Grip g, Vector3 travel, Vector3 seamAxis, float turnDegrees)
    {
        Vector3 backAxis = Vector3.Cross(travel, Vector3.up);          // backspin: the top comes back
        switch (g.spin)
        {
            case SpinKind.Top:
                return -backAxis * g.spinRate;
            case SpinKind.Side:
                float s = Mathf.Abs(turnDegrees) < 0.05f ? Mathf.Sign(g.roll == 0f ? 1f : g.roll) : Mathf.Sign(turnDegrees);
                return (travel * (0.8f * s) - backAxis * 0.45f).normalized * g.spinRate;
            default:
                Vector3 axis = Vector3.Dot(seamAxis, backAxis) < 0f ? -seamAxis : seamAxis;
                return axis * g.spinRate;
        }
    }
}
