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
    /// A bouncer let go over the batter's head (a wide): the solver does not pull it fuller.
    public bool overHead;
    /// Who bowled it and what it is (BowlerRepertoire): an off-cutter from a quick, a googly...
    /// swingType above is the physics it is bowled with; these say what the bowler meant.
    public eBowlerKind kind = eBowlerKind.Fast;
    public eDelivery delivery = eDelivery.Stock;
    /// Top-spin dip in the air and kick or skid off the pitch (BallFlight.DeliveryEffects).
    public float dip;
    public float bounceScale = 1f;

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
        return kind + " " + delivery + " (" + swingType.ToString() + ") " + lengthBand.ToString() + ", " + torqueX.ToString() + ", (" +
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
            dip = dip,
            bounceScale = bounceScale,
        };
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

    // Line and length come only from DeliveryPicker's channels - which, since 2026-11-03, include
    // the odd ball down leg and wide outside off (the old WaywardChance strays were removed when
    // every ball was meant to be legal; the player found that too tight to be real).
    // minLen / maxLen are kept for the constructor's column order but no longer drive the length:
    // the config ranges (pace x 0..9, spin x 4..10) cut off the bouncer and short-spinner bands,
    // and the length sliders that once edited them are disabled in Main.

    /// <param name="ownVariations">False when the over plan (BowlerRepertoire) picks the
    /// variations: then the old random ones here - the plan's slower ball, a swinger holding its
    /// line, the googly, the arm ball - are left to it. The over-the-head bouncer stays.</param>
    public BowlingParams GetRandomDelivery(bool ownVariations = true)
    {
        BowlingParams param = new BowlingParams();

        param.swingType = swingType;
        param.torqueX = 0f;
        param.speedX = Random.Range(minX, maxX);
        // The captain's plan for this bowler (FieldCaptain), if it is his kind of plan: where it
        // pitches, the line, and the pace - quicker darts, slower flight, the slower ball.
        BowlingPlan plan = BowlingPlans.Fits(BowlingPlans.Active, swingType) ? BowlingPlans.Active : null;
        // ...as this standard of attack bowls it (BowlingLevels: pace, movement, loose balls).
        BowlingLevels.Level level = BowlingLevels.Current;
        plan = BowlingLevels.Apply(plan, level);
        param.speedX *= plan.speedScale * level.speed;
        if (ownVariations && Random.value < plan.slowerChance)
            param.speedX *= BowlingPlan.SlowerBy;
        // length is the world X it pitches at; speedZ is the LINE at the batsman's stumps, off side
        // positive (Main mirrors it for a left-hander). minZ / maxZ are the line sliders.
        DeliveryPicker.Pick pick = DeliveryPicker.PickDelivery(swingType, minZ, maxZ, () => Random.value, plan);
        param.length = pick.pitchX;
        param.lengthBand = pick.band;
        param.speedZ = pick.line;
        param.swing = Random.Range(minSwing, maxSwing) * level.movement;
        // Off the pitch: the level's movement, and the pitch's (StadiumConditions) - a green one
        // seams, a dusty one turns.
        StadiumConditions.Pitch pitch = StadiumConditions.Current;
        param.pitchTurn = Random.Range(minPitchTurn, maxPitchTurn) * level.movement *
                          (DeliveryPicker.IsSpin(swingType) ? pitch.turnScale : pitch.seamScale);
        Vary(param, ownVariations);
        param.applySwing = true;
        param.applyPitchTurn = true;
        return param;
    }

    /// Chance a swing bowler's ball holds its line (barely swings), and how much it does then.
    public const float NoSwingChance = 0.12f, NoSwingMax = 0.12f;
    /// Chance of a leg-spinner's googly (turns the other way) and an off-spinner's arm ball (goes
    /// straight on, a touch of drift the other way).
    public const float GooglyChance = 0.12f, ArmBallChance = 0.12f;
    /// Chance a quick's short ball gets away over the batter's head (a wide).
    public const float OverHeadChance = 0.2f;

    /// The odd ball that is not like the others: they are what makes a batter watch every one.
    private void Vary(BowlingParams param, bool ownVariations = true)
    {
        switch (ownVariations ? swingType : eSwingType.None)
        {
            case eSwingType.InSwing:
            case eSwingType.OutSwing:
                if (Random.value < NoSwingChance)
                    param.swing = Random.Range(0f, NoSwingMax);
                break;
            case eSwingType.LegSpin:
                if (Random.value < GooglyChance)
                    param.pitchTurn = -param.pitchTurn;
                break;
            case eSwingType.OffSpin:
                if (Random.value < ArmBallChance)
                    param.pitchTurn = Random.Range(-0.05f, 0.05f);
                break;
        }
        param.overHead = !DeliveryPicker.IsSpin(swingType) && param.lengthBand == eLengthBand.Short &&
                         Random.value < OverHeadChance;
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
