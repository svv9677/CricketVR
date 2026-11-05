using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How good the attack is (MatchSetup.Bowling), applied to every delivery on top of the bowler's
/// own ranges (the settings sliders) and the captain's plan:
///
///   Easy    club bowlers: 15% slower, half the swing and turn, many more half-volleys, long
///           hops and balls on the pads; and no plans - the captain just sets a field.
///   Medium  county: a little slower, most of the movement, a few more loose balls; plans picked
///           loosely (a captain who does not always see what is working).
///   Hard    international: the bowlers as configured, and plans picked as FieldCaptain picks
///           them.
///   Pro     the best: quicker, more movement, very few loose balls (tighter to the corridor and
///           a good length, fewer wides), and the sharpest plans.
///
/// Weights multiply DeliveryPicker's, in BowlingPlan's order (channels: corridor, stumps, leg
/// stump, leave, down leg, wide off; bands: yorker, full, good, back of a length, short).
/// </summary>
public static class BowlingLevels
{
    public class Level
    {
        public float speed, movement;
        public float[] lineWeights, lengthWeights;
        public bool plans;
        /// FieldCaptain's softmax temperature: higher picks plans more at random.
        public float temperature;
    }

    public static readonly Level Easy = new Level
    {
        speed = 0.85f, movement = 0.5f, plans = false, temperature = 1f,
        lineWeights = new[] { 0.75f, 1f, 1.5f, 1f, 1.5f, 1.3f },
        lengthWeights = new[] { 0.5f, 1.7f, 0.65f, 0.8f, 1.6f },
    };
    public static readonly Level Medium = new Level
    {
        speed = 0.93f, movement = 0.8f, plans = true, temperature = 0.35f,
        lineWeights = new[] { 0.95f, 1f, 1.15f, 1f, 1.2f, 1.1f },
        lengthWeights = new[] { 0.85f, 1.2f, 0.95f, 1f, 1.15f },
    };
    public static readonly Level Hard = new Level
    {
        speed = 1f, movement = 1f, plans = true, temperature = 0.12f,
        lineWeights = new[] { 1f, 1f, 1f, 1f, 1f, 1f },
        lengthWeights = new[] { 1f, 1f, 1f, 1f, 1f },
    };
    public static readonly Level Pro = new Level
    {
        speed = 1.05f, movement = 1.15f, plans = true, temperature = 0.08f,
        lineWeights = new[] { 1.3f, 1.1f, 0.8f, 0.9f, 0.7f, 0.7f },
        lengthWeights = new[] { 1.2f, 0.85f, 1.3f, 1.1f, 0.8f },
    };

    public static Level For(eBowlingLevel l) =>
        l == eBowlingLevel.Easy ? Easy : l == eBowlingLevel.Medium ? Medium : l == eBowlingLevel.Hard ? Hard : Pro;

    public static Level Current => For(MatchSetup.Bowling);

    private static readonly Dictionary<(BowlingPlan, Level), BowlingPlan> combined = new Dictionary<(BowlingPlan, Level), BowlingPlan>();

    /// The plan as this attack bowls it: its weights times the level's. Plan null is plain bowling.
    public static BowlingPlan Apply(BowlingPlan plan, Level level)
    {
        if (combined.TryGetValue((plan, level), out BowlingPlan c))
            return c;
        c = new BowlingPlan
        {
            name = plan != null ? plan.name : "Plain",
            spin = plan != null && plan.spin,
            slowerChance = plan != null ? plan.slowerChance : 0f,
            speedScale = plan != null ? plan.speedScale : 1f,
            field = plan?.field,
            lineWeights = Multiply(plan?.lineWeights, level.lineWeights),
            lengthWeights = Multiply(plan?.lengthWeights, level.lengthWeights),
        };
        combined[(plan, level)] = c;
        return c;
    }

    private static float[] Multiply(float[] a, float[] b)
    {
        var r = new float[b.Length];
        for (int i = 0; i < r.Length; i++)
            r[i] = (a != null && i < a.Length ? a[i] : 1f) * b[i];
        return r;
    }
}
