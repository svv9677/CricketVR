using UnityEngine;
using P = FieldSets.Position;
using K = FieldSets.Kind;

/// <summary>
/// The plans a bowler bowls to, and the field each is bowled to: "bowl to your field". Each plan
/// is a way of attacking (or containing) a batter - where it pitches, what line, any change of
/// pace - and the nine places that make that line pay: catchers where its false shots go, the
/// ring and the rope where its good shots go.
///
/// Pace:
///   Off-stump line  the stock plan: the corridor just outside off, good length to back of a
///                   length; a slip, the off-side ring, third man and fine leg.
///   Attack stumps   straight and full: bowled and lbw; ring heavy on the leg side where the
///                   flick goes.
///   Off theory      wide of off stump, inviting the drive: 7-2 off-side field, gully and slip
///                   for the edge, a deep cover for the drive that beats the ring.
///   Short ball      back of a length and short at the body: men out for the hook and the pull
///                   (deep square, deep midwicket, fine leg), a leg gully for the fend-off.
///   Yorkers         at the toes, with slower balls: the straight hits and the scoops covered on
///                   the rope.
///   Wide off        full and wide outside off, slower balls: off side sealed on the rope (third
///                   man, deep point, deep cover, long-off).
/// Spin:
///   Flight it       slower, fuller, outside off to the stumps: slip, short leg and silly point.
///   Darts           quicker, flatter, at the stumps: no room to free the arms; all ring.
///   Wide of off     outside off, away from the hitting arc: a sweeper on each side.
///
/// Delivery weights multiply DeliveryPicker's: one per line channel (corridor, stumps, leg stump,
/// leave, down leg, wide off) and one per length band (yorker, full, good, back of a length,
/// short). Every field keeps FieldSets.Valid (tested).
/// </summary>
public class BowlingPlan
{
    public string name;
    public bool spin;
    public float[] lineWeights;
    public float[] lengthWeights;
    /// Chance of a slower ball (pace off by SlowerBy), and the plan's own pace (darts quicker,
    /// flight slower).
    public float slowerChance, speedScale = 1f;
    public P[] field;

    public const float SlowerBy = 0.82f;
}

public static class BowlingPlans
{
    /// The plan being bowled now (FieldCaptain sets it between balls), or null: plain bowling.
    public static BowlingPlan Active;

    static P Slip(int n) => new P(n == 1 ? "First slip" : "Second slip", K.Slip, 170f, n);
    /// A ring fielder a few metres inside the circle at his angle (FieldSets.RingPlace), or at `d`.
    static P Ring(string n, float a, float d = 0f) => new P(n, K.Ring, a, d);
    static P Close(string n, float a, float d) => new P(n, K.Close, a, d);
    static P Deep(string n, float a) => new P(n, K.Deep, a);

    //                                    corridor stumps legStump leave downLeg wideOff
    //                                    yorker full good backOfLength short
    public static readonly BowlingPlan OffStump = new BowlingPlan
    {
        name = "Off-stump line",
        lineWeights = new[] { 1.4f, 0.9f, 0.6f, 1f, 0.8f, 1f },
        lengthWeights = new[] { 0.6f, 0.9f, 1.4f, 1.2f, 0.7f },
        field = new[] { Slip(1), Ring("Point", 95f), Ring("Cover", 55f), Ring("Mid-off", 12f),
                        Ring("Mid-on", -12f), Ring("Midwicket", -55f), Deep("Third man", 150f),
                        Deep("Fine leg", -155f), Deep("Deep square leg", -88f) },
    };

    public static readonly BowlingPlan AttackStumps = new BowlingPlan
    {
        name = "Attack the stumps",
        lineWeights = new[] { 0.5f, 2.2f, 1.6f, 0.2f, 0.8f, 0.4f },
        lengthWeights = new[] { 1.4f, 1.5f, 1.1f, 0.6f, 0.4f },
        field = new[] { Slip(1), Ring("Cover", 50f), Ring("Mid-off", 12f), Ring("Mid-on", -12f),
                        Ring("Midwicket", -55f), Ring("Square leg", -88f), Ring("Short fine leg", -140f),
                        Deep("Deep midwicket", -60f), Deep("Long-on", -10f) },
    };

    public static readonly BowlingPlan OffTheory = new BowlingPlan
    {
        name = "Off theory",
        lineWeights = new[] { 1.2f, 0.3f, 0.1f, 2.4f, 0.4f, 1.6f },
        lengthWeights = new[] { 0.5f, 1.5f, 1.3f, 0.8f, 0.5f },
        field = new[] { Slip(1), Slip(2), Close("Gully", 130f, 14f), Ring("Point", 95f), Ring("Cover", 60f),
                        Ring("Extra cover", 35f), Ring("Mid-off", 12f), Ring("Mid-on", -12f),
                        Deep("Fine leg", -155f) },
    };

    public static readonly BowlingPlan ShortBall = new BowlingPlan
    {
        name = "Short-ball plan",
        lineWeights = new[] { 0.8f, 1.6f, 1.6f, 0.4f, 1.4f, 0.6f },
        lengthWeights = new[] { 0.3f, 0.4f, 0.7f, 1.6f, 2.6f },
        field = new[] { Slip(1), Ring("Point", 95f), Ring("Cover", 55f), Ring("Mid-off", 12f), Ring("Mid-on", -12f),
                        Close("Short leg", -80f, 4.5f), Close("Leg gully", -135f, 12f),
                        Deep("Deep square leg", -85f), Deep("Fine leg", -160f) },
    };

    public static readonly BowlingPlan Yorkers = new BowlingPlan
    {
        name = "Yorkers",
        slowerChance = 0.25f,
        lineWeights = new[] { 0.7f, 1.8f, 1.4f, 0.6f, 0.9f, 1.3f },
        lengthWeights = new[] { 3.0f, 1.2f, 0.5f, 0.6f, 0.5f },
        field = new[] { Ring("Point", 95f), Ring("Cover", 55f), Ring("Mid-off", 12f), Ring("Short fine leg", -140f),
                        Deep("Long-off", 10f), Deep("Long-on", -10f), Deep("Deep midwicket", -55f),
                        Deep("Deep square leg", -85f), Deep("Third man", 150f) },
    };

    public static readonly BowlingPlan WideOff = new BowlingPlan
    {
        name = "Wide outside off",
        slowerChance = 0.3f,
        lineWeights = new[] { 0.9f, 0.2f, 0.1f, 2.6f, 0.3f, 2.0f },
        lengthWeights = new[] { 1.6f, 1.5f, 1.0f, 0.7f, 0.5f },
        field = new[] { Ring("Cover", 50f), Ring("Mid-off", 12f), Ring("Midwicket", -55f), Ring("Short fine leg", -140f),
                        Deep("Deep point", 95f), Deep("Deep cover", 60f), Deep("Long-off", 10f),
                        Deep("Third man", 145f), Deep("Long-on", -10f) },
    };

    public static readonly BowlingPlan Flight = new BowlingPlan
    {
        name = "Flight it",
        spin = true,
        speedScale = 0.92f,
        lineWeights = new[] { 1.5f, 1.2f, 0.5f, 0.7f, 0.6f, 0.6f },
        lengthWeights = new[] { 0.4f, 1.8f, 1.3f, 0.5f, 0.3f },
        field = new[] { Slip(1), Close("Silly point", 80f, 4f), Close("Short leg", -95f, 4.5f),
                        Ring("Point", 92f), Ring("Cover", 55f), Ring("Mid-off", 12f),
                        Ring("Mid-on", -12f), Ring("Midwicket", -55f), Deep("Long-on", -10f) },
    };

    public static readonly BowlingPlan Darts = new BowlingPlan
    {
        name = "Darts at the stumps",
        spin = true,
        speedScale = 1.1f,
        lineWeights = new[] { 0.6f, 2.2f, 1.5f, 0.2f, 0.6f, 0.3f },
        lengthWeights = new[] { 0.8f, 1.0f, 1.6f, 0.9f, 0.3f },
        field = new[] { Ring("Point", 95f), Ring("Cover", 55f), Ring("Mid-off", 12f), Ring("Mid-on", -12f),
                        Ring("Midwicket", -55f), Ring("Short fine leg", -140f), Deep("Deep point", 100f),
                        Deep("Deep midwicket", -55f), Deep("Deep square leg", -88f) },
    };

    public static readonly BowlingPlan WideOfOffSpin = new BowlingPlan
    {
        name = "Wide of off",
        spin = true,
        lineWeights = new[] { 1.5f, 0.4f, 0.2f, 2.0f, 0.4f, 1.3f },
        lengthWeights = new[] { 0.4f, 1.0f, 1.5f, 1.0f, 0.4f },
        field = new[] { Slip(1), Ring("Point", 95f), Ring("Cover", 55f), Ring("Extra cover", 35f), Ring("Mid-on", -12f),
                        Ring("Midwicket", -55f), Deep("Long-off", 12f), Deep("Deep cover", 60f),
                        Deep("Deep square leg", -88f) },
    };

    public static readonly BowlingPlan[] Pace = { OffStump, AttackStumps, OffTheory, ShortBall, Yorkers, WideOff };
    public static readonly BowlingPlan[] Spin = { Flight, Darts, WideOfOffSpin };

    public static BowlingPlan[] For(eSwingType bowler) => FieldSets.IsSpin(bowler) ? Spin : Pace;

    /// The plan to bowl when nothing is known yet: the stock line.
    public static BowlingPlan Stock(eSwingType bowler) => FieldSets.IsSpin(bowler) ? Flight : OffStump;

    /// The plan applies to this bowler (a pace plan is not bowled by a spinner).
    public static bool Fits(BowlingPlan plan, eSwingType bowler) => plan != null && plan.spin == FieldSets.IsSpin(bowler);

    /// Catchers an attacking field adds, in order, if the plan's field has none there.
    public static P[] Catchers(bool spin) => spin
        ? new[] { Slip(1), Close("Short leg", -95f, 4.5f), Close("Silly point", 80f, 4f) }
        : new[] { Slip(1), Slip(2), Close("Gully", 130f, 14f) };

    // ---- What a plan bowls, by the zones BatterBook keeps -------------------------------------

    /// Share of a plan's balls in each line zone x length band (BatterBook.LineZone), from its
    /// weights on DeliveryPicker's channels and bands; sums to 1.
    public static float[,] Share(BowlingPlan plan, eSwingType bowler)
    {
        var share = new float[BatterBook.LineZones, BatterBook.Bands];
        DeliveryPicker.Band[] bands = DeliveryPicker.BandsFor(bowler);
        float total = 0f;
        for (int c = 0; c < DeliveryPicker.Channels.Length; c++)
        {
            DeliveryPicker.Channel ch = DeliveryPicker.Channels[c];
            float cw = ch.weight * (plan != null ? plan.lineWeights[c] : 1f);
            for (int z = 0; z < BatterBook.LineZones; z++)
            {
                BatterBook.ZoneRange(z, out float lo, out float hi);
                float overlap = Mathf.Max(0f, Mathf.Min(hi, ch.maxLine) - Mathf.Max(lo, ch.minLine));
                float f = overlap / Mathf.Max(1e-4f, ch.maxLine - ch.minLine);
                for (int b = 0; b < bands.Length && b < BatterBook.Bands; b++)
                {
                    float w = cw * f * bands[b].weight * (plan != null ? plan.lengthWeights[b] : 1f);
                    share[z, b] += w;
                    total += w;
                }
            }
        }
        for (int z = 0; z < BatterBook.LineZones; z++)
            for (int b = 0; b < BatterBook.Bands; b++)
                share[z, b] /= Mathf.Max(total, 1e-6f);
        return share;
    }
}
