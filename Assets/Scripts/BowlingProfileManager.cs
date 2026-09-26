using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//public class BowlingParams
//{
//    public eSwingType swingType;
//    public float torqueX;
//    public float speedX;
//    public float speedY;
//    public float speedZ;
//    public float swing;
//    public float pitchTurn;
//    public bool applySwing;
//    public bool applyPitchTurn;

//    public BowlingParams()
//    {
//        swingType = eSwingType.None;
//        torqueX = 50f;
//        speedX = 7f;
//        speedY = -1f;
//        speedZ = 0f;
//        swing = 0f;
//        pitchTurn = 0f;
//        applySwing = false;
//        applyPitchTurn = false;
//    }

//    public override string ToString()
//    {
//        return swingType.ToString() + ", " + torqueX.ToString() + ", (" +
//                speedX.ToString() + "," + speedY.ToString() + "," + speedZ.ToString() + "), " +
//                swing.ToString() + ", " + pitchTurn.ToString() + ", (" +
//                applySwing.ToString() + "/" + applyPitchTurn.ToString() + ")";
//    }
//}

//public class BowlingProfile
//{
//    public eSwingType swingType;

//    public float minX;
//    public float maxX;
//    public float minY;
//    public float maxY;
//    public float minZ;
//    public float maxZ;

//    public float minSwing;
//    public float maxSwing;
//    public float minPitchTurn;
//    public float maxPitchTurn;

//    public BowlingProfile(eSwingType _swingType = eSwingType.Pace,
//                          float _minX = 0f, float _maxX = 10f,
//                          float _minY = -2f, float _maxY = 2f,
//                          float _minZ = -0.5f, float _maxZ = 0.5f,
//                          float _minSwing = 0f, float _maxSwing = 1f,
//                          float _minPitchTurn = 0f, float _maxPitchTurn = 1f
//                          )
//    {
//        swingType = _swingType;

//        minX = _minX;
//        maxX = _maxX;
//        minY = _minY;
//        maxY = _maxY;
//        minZ = _minZ;
//        maxZ = _maxZ;

//        minSwing = _minSwing;
//        maxSwing = _maxSwing;
//        minPitchTurn = _minPitchTurn;
//        maxPitchTurn = _maxPitchTurn;
//    }

//    public BowlingParams GetRandomDelivery()
//    {
//        BowlingParams param = new BowlingParams();
//        float director = (swingType == eSwingType.InSwing || swingType == eSwingType.OutSwing) ? -1f : 11f;

//        param.swingType = swingType;
//        param.torqueX = director * 50f;
//        param.speedX = Random.Range(minX, maxX);
//        param.speedY = Random.Range(minY, maxY);
//        param.speedZ = Random.Range(minZ, maxZ);
//        param.swing = Random.Range(minSwing, maxSwing);
//        param.pitchTurn = Random.Range(minPitchTurn, maxPitchTurn);
//        param.applySwing = Random.Range(0f, 1f) > 0.05f || swingType == eSwingType.InSwing || swingType == eSwingType.OutSwing;
//        param.applyPitchTurn = Random.Range(0f, 1f) > 0.15f || swingType == eSwingType.LegSpin || swingType == eSwingType.OffSpin;

//        // For spin bowling, if we are looping (Y is greater than half of maxY), then limit the x-speed to not throw a no-ball
//        if (swingType == eSwingType.LegSpin || swingType == eSwingType.OffSpin)
//        {
//            if (param.speedY > ((minY+maxY)/2f))
//                param.speedX = Mathf.Clamp(param.speedX, minX, (minX + maxX) / 2f);
//        }
//        // For in-swing, if swing amount is large, make sure we start from way off-side
//        if(swingType == eSwingType.InSwing)
//        {
//            if (param.swing > ((minSwing + maxSwing) / 2f))
//                param.speedZ = Mathf.Clamp(param.speedZ, (minZ + maxZ) / 2f, maxZ);
//        }
//        // Same applies for out swing
//        if (swingType == eSwingType.OutSwing)
//        {
//            if (param.swing > ((minSwing + maxSwing) / 2f))
//                param.speedZ = Mathf.Clamp(param.speedZ, minZ, (minZ + maxZ) / 2f);
//        }
//        return param;
//    }
//}

public class BowlingParams
{
    public eSwingType swingType;
    public float torqueX;
    public float speedX;
    public float length;
    /// Which length band `length` was drawn from - for the debug overlay and logs.
    public eLengthBand lengthBand;
    public float speedZ;
    public float swing;
    public float pitchTurn;
    public bool applySwing;
    public bool applyPitchTurn;

    public BowlingParams()
    {
        swingType = eSwingType.None;
        torqueX = 50f;
        speedX = 7f;
        length = 1f;
        lengthBand = eLengthBand.GoodLength;
        speedZ = 0f;
        swing = 0f;
        pitchTurn = 0f;
        applySwing = false;
        applyPitchTurn = false;
    }

    public override string ToString()
    {
        return swingType.ToString() + " " + lengthBand.ToString() + ", " + torqueX.ToString() + ", (" +
                speedX.ToString() + "," + length.ToString() + "," + speedZ.ToString() + "), " +
                swing.ToString() + ", " + pitchTurn.ToString() + ", (" +
                applySwing.ToString() + "/" + applyPitchTurn.ToString() + ")";
    }

    /// <summary>
    /// Swing and turn for this delivery, in world terms. offSign is +1 when the off side is +Z
    /// (right-hander) and -1 for a left-hander.
    ///   in-swing   curves toward the batter's pads (leg side); out-swing away (off side).
    ///   leg-spin   drifts in, then turns away to the off side; off-spin drifts away, turns in.
    ///   seamers    a small random seam movement either way (pitchTurn is signed for them).
    /// </summary>
    public BallFlight.DeliveryEffects Effects(float offSign)
    {
        // Toward the off side is +offSign in Z. BallFlight's swing pushes toward -Z for a positive
        // sign (travel is +X), and its turn is a rotation about +Y, which also moves +X toward -Z.
        float swingToward = 0f, turnToward = 0f;
        switch (swingType)
        {
            case eSwingType.InSwing: swingToward = -offSign; turnToward = offSign; break;
            case eSwingType.OutSwing: swingToward = offSign; turnToward = offSign; break;
            case eSwingType.LegSpin: swingToward = -offSign; turnToward = offSign; break;
            case eSwingType.OffSpin: swingToward = offSign; turnToward = -offSign; break;
            default: turnToward = offSign; break;
        }
        return new BallFlight.DeliveryEffects
        {
            swingAccelPerV2 = applySwing ? BallFlight.SwingAccelPerV2(swing) : 0f,
            swingSign = -swingToward,
            turnDegrees = applyPitchTurn ? -turnToward * pitchTurn * BallFlight.MaxTurnDegrees : 0f,
        };
    }
}

/// Where a ball pitches, by the names a commentator would use.
public enum eLengthBand { Yorker, Full, GoodLength, BackOfALength, Short }

/// <summary>
/// Picks the line and length of a delivery. Pure (randomness is passed in), so it can be tested
/// without a scene.
///
/// Length is a weighted choice of band, then uniform within it. Distances are metres from the
/// batsman's stumps back to where the ball pitches. Pace bowlers mostly hit a good length - the
/// batter can't tell whether to go forward or back - with enough full balls, yorkers, and short
/// ones to keep them honest. Spinners pitch it up: a spinner dropping short gets cut away.
///
/// Every pick is legal: it pitches before the popping crease (never a full toss, so never a
/// beamer), and its line at the stumps is inside both wide lines by a wide margin.
/// </summary>
public static class DeliveryPicker
{
    public struct Band
    {
        public eLengthBand band;
        public float minDistance, maxDistance, weight;
        public Band(eLengthBand b, float min, float max, float w) { band = b; minDistance = min; maxDistance = max; weight = w; }
    }

    /// Line window at the stumps, metres from middle stump, off side positive.
    public struct Channel
    {
        public float minLine, maxLine, weight;
        public Channel(float min, float max, float w) { minLine = min; maxLine = max; weight = w; }
    }

    public struct Pick
    {
        public eLengthBand band;
        /// Metres from the batsman's stumps to the pitching point.
        public float distance;
        /// World X the ball pitches at - what BowlingParams.length and BallDelivery take.
        public float pitchX;
        /// Line at the stumps, off side positive.
        public float line;
    }

    /// Yorkers pitch at the batter's toes, 1.3 m out - just ahead of the popping crease (1.22 m),
    /// so even the fullest ball bounces before it reaches the batter.
    public static readonly Band[] PaceBands =
    {
        new Band(eLengthBand.Yorker,        1.3f,  2f,  0.08f),
        new Band(eLengthBand.Full,          2f,    4f,  0.22f),
        new Band(eLengthBand.GoodLength,    4f,    7f,  0.40f),
        new Band(eLengthBand.BackOfALength, 7f,    9f,  0.18f),
        new Band(eLengthBand.Short,         9f,   12f,  0.12f),
    };
    public static readonly Band[] SpinBands =
    {
        new Band(eLengthBand.Full,          1.5f, 3.5f, 0.30f),
        new Band(eLengthBand.GoodLength,    3.5f, 5.5f, 0.55f),
        new Band(eLengthBand.Short,         5.5f, 7f,   0.15f),
    };

    /// Stumps span +-0.114. The corridor - off stump to a bat's width outside it - is where good
    /// bowlers live; some attack the stumps; a few drift onto leg stump but never past it.
    public static readonly Channel[] Channels =
    {
        new Channel( 0.10f,  0.50f, 0.55f),   // corridor of uncertainty
        new Channel(-0.03f,  0.10f, 0.33f),   // at the stumps
        new Channel(-0.10f, -0.03f, 0.12f),   // on leg stump
    };

    /// Hard limits on the line. Off-side wide guideline is 0.889 m; the leg-side wide line is the
    /// outside of leg stump, 0.114 m. A ball centred at -0.10 still has its edge on the stump.
    public const float MaxOffLine = 0.60f;
    public const float MaxLegLine = -0.10f;

    public static bool IsSpin(eSwingType t) => t == eSwingType.LegSpin || t == eSwingType.OffSpin;

    public static Band[] BandsFor(eSwingType t) => IsSpin(t) ? SpinBands : PaceBands;

    /// <param name="minLine">The profile's line range (the Z sliders), which narrows the channels.
    /// The legal window always wins over it.</param>
    /// <param name="rand01">Uniform random in [0, 1].</param>
    public static Pick PickDelivery(eSwingType type, float minLine, float maxLine, System.Func<float> rand01)
    {
        Band[] bands = BandsFor(type);
        Band b = bands[PickIndex(bands.Length, i => bands[i].weight, rand01())];
        float distance = Mathf.Lerp(b.minDistance, b.maxDistance, rand01());
        // Keep inside what BallDelivery will accept, so it never has to clamp (and move) the ball.
        float pitchX = Mathf.Clamp(BallDelivery.BatsmanStumpsX - distance, BallDelivery.MinPitchX,
                                   BallDelivery.BatsmanStumpsX - BallDelivery.PoppingCreaseDistance);
        return new Pick
        {
            band = b.band,
            distance = BallDelivery.BatsmanStumpsX - pitchX,
            pitchX = pitchX,
            line = PickLine(minLine, maxLine, rand01),
        };
    }

    public static float PickLine(float minLine, float maxLine, System.Func<float> rand01)
    {
        float lo = Mathf.Max(Mathf.Min(minLine, maxLine), MaxLegLine);
        float hi = Mathf.Min(Mathf.Max(minLine, maxLine), MaxOffLine);
        // Sliders set entirely outside the legal window: bowl the nearest legal line.
        if (hi < lo)
            return Mathf.Clamp(0.5f * (minLine + maxLine), MaxLegLine, MaxOffLine);
        if (hi - lo < 1e-4f)
            return lo;

        // Only channels the slider range overlaps can be chosen, each clipped to that range.
        int c = PickIndex(Channels.Length, i => Overlap(Channels[i], lo, hi) > 0f ? Channels[i].weight : 0f, rand01());
        if (c < 0)
            return Mathf.Lerp(lo, hi, rand01());
        float from = Mathf.Max(Channels[c].minLine, lo), to = Mathf.Min(Channels[c].maxLine, hi);
        return Mathf.Lerp(from, to, rand01());
    }

    static float Overlap(Channel ch, float lo, float hi) =>
        Mathf.Min(ch.maxLine, hi) - Mathf.Max(ch.minLine, lo);

    /// Weighted choice; r in [0, 1]. Returns -1 when every weight is zero.
    static int PickIndex(int count, System.Func<int, float> weight, float r)
    {
        float total = 0f;
        int last = -1;
        for (int i = 0; i < count; i++)
        {
            float w = weight(i);
            if (w <= 0f) continue;
            total += w;
            last = i;
        }
        if (last < 0) return -1;
        float target = Mathf.Clamp01(r) * total;
        for (int i = 0; i < count; i++)
        {
            float w = weight(i);
            if (w <= 0f) continue;
            if (target < w) return i;
            target -= w;
        }
        return last;   // r == 1, or float rounding
    }
}

public class BowlingProfile
{
    public eSwingType swingType;

    public float minX;
    public float maxX;
    public float minLen;
    public float maxLen;
    public float minZ;
    public float maxZ;

    public float minSwing;
    public float maxSwing;
    public float minPitchTurn;
    public float maxPitchTurn;

    public BowlingProfile(eSwingType _swingType = eSwingType.Pace,
                          float _minX = 0f, float _maxX = 10f,
                          float _minLen = -2f, float _maxLen = 2f,
                          float _minZ = -0.5f, float _maxZ = 0.5f,
                          float _minSwing = 0f, float _maxSwing = 1f,
                          float _minPitchTurn = 0f, float _maxPitchTurn = 1f
                          )
    {
        swingType = _swingType;

        minX = _minX;
        maxX = _maxX;
        minLen = _minLen;
        maxLen = _maxLen;
        minZ = _minZ;
        maxZ = _maxZ;

        minSwing = _minSwing;
        maxSwing = _maxSwing;
        minPitchTurn = _minPitchTurn;
        maxPitchTurn = _maxPitchTurn;
    }

    // Wayward deliveries (the old 5% off-side / 2% leg-side strays) are gone: the player wants
    // every ball legal, so line and length now come only from DeliveryPicker's legal channels.
    // minLen / maxLen are kept for the constructor's column order but no longer drive the length:
    // the config ranges (pace x 0..9, spin x 4..10) cut off the bouncer and short-spinner bands,
    // and the length sliders that once edited them are disabled in Main.

    public BowlingParams GetRandomDelivery()
    {
        BowlingParams param = new BowlingParams();

        param.swingType = swingType;
        param.torqueX = 0f;
        param.speedX = Random.Range(minX, maxX);
        // length is the world X it pitches at; speedZ is the LINE at the batsman's stumps, off side
        // positive (Main mirrors it for a left-hander). minZ / maxZ are the line sliders.
        DeliveryPicker.Pick pick = DeliveryPicker.PickDelivery(swingType, minZ, maxZ, () => Random.value);
        param.length = pick.pitchX;
        param.lengthBand = pick.band;
        param.speedZ = pick.line;
        param.swing = Random.Range(minSwing, maxSwing);
        param.pitchTurn = Random.Range(minPitchTurn, maxPitchTurn);
        param.applySwing = true;
        param.applyPitchTurn = true;
        return param;
    }
}

public class BowlingProfileManager
{
    public BowlingProfile paceProfile;
    public BowlingProfile inSwingProfile;
    public BowlingProfile outSwingProfile;
    public BowlingProfile legSpinProfile;
    public BowlingProfile offSpinProfile;

    public BowlingProfileManager()
    {
        InitProfilesFromParams();
    }

    public void InitProfilesFromParams()
    {
        Main inst = Main.Instance;
        paceProfile = new BowlingProfile(eSwingType.Pace, Constants.paceCfg[0], Constants.paceCfg[1], Constants.paceCfg[2], Constants.paceCfg[3], Constants.paceCfg[4], Constants.paceCfg[5], Constants.paceCfg[6], Constants.paceCfg[7], Constants.paceCfg[8], Constants.paceCfg[9]);
        inSwingProfile = new BowlingProfile(eSwingType.InSwing, Constants.inSwingCfg[0], Constants.inSwingCfg[1], Constants.inSwingCfg[2], Constants.inSwingCfg[3], Constants.inSwingCfg[4], Constants.inSwingCfg[5], Constants.inSwingCfg[6], Constants.inSwingCfg[7], Constants.inSwingCfg[8], Constants.inSwingCfg[9]);
        outSwingProfile = new BowlingProfile(eSwingType.OutSwing, Constants.outSwingCfg[0], Constants.outSwingCfg[1], Constants.outSwingCfg[2], Constants.outSwingCfg[3], Constants.outSwingCfg[4], Constants.outSwingCfg[5], Constants.outSwingCfg[6], Constants.outSwingCfg[7], Constants.outSwingCfg[8], Constants.outSwingCfg[9]);
        legSpinProfile = new BowlingProfile(eSwingType.LegSpin, Constants.legSpinCfg[0], Constants.legSpinCfg[1], Constants.legSpinCfg[2], Constants.legSpinCfg[3], Constants.legSpinCfg[4], Constants.legSpinCfg[5], Constants.legSpinCfg[6], Constants.legSpinCfg[7], Constants.legSpinCfg[8], Constants.legSpinCfg[9]);
        offSpinProfile = new BowlingProfile(eSwingType.OffSpin, Constants.offSpinCfg[0], Constants.offSpinCfg[1], Constants.offSpinCfg[2], Constants.offSpinCfg[3], Constants.offSpinCfg[4], Constants.offSpinCfg[5], Constants.offSpinCfg[6], Constants.offSpinCfg[7], Constants.offSpinCfg[8], Constants.offSpinCfg[9]);
    }

    public BowlingProfile GetProfile(eSwingType swing)
    {
        float r = -1f;
        if(swing == eSwingType.Random)
            r = Random.Range(0f, 5f);

        if (swing == eSwingType.Pace || (r >= 0 && r < 1f))
            return paceProfile;
        if (swing == eSwingType.InSwing || (r >= 1f && r < 2f))
            return inSwingProfile;
        if (swing == eSwingType.OutSwing || (r >= 2f && r < 3f))
            return outSwingProfile;
        if (swing == eSwingType.LegSpin || (r >= 3f && r < 4f))
            return legSpinProfile;
        if (swing == eSwingType.OffSpin || r >= 4f)
            return offSpinProfile;

        // default
        return paceProfile;
    }
}
