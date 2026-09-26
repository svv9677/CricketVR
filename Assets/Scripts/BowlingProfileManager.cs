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
        speedZ = 0f;
        swing = 0f;
        pitchTurn = 0f;
        applySwing = false;
        applyPitchTurn = false;
    }

    public override string ToString()
    {
        return swingType.ToString() + ", " + torqueX.ToString() + ", (" +
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

    /// Chance a delivery strays off its line: down the off side (a few of these clear the wide
    /// guideline) or down the leg side (these are leg-side wides). Together they give roughly the
    /// 2-4% wide rate of international limited-overs cricket.
    public const float WaywardOffChance = 0.05f;
    public const float WaywardLegChance = 0.02f;
    public static readonly Vector2 WaywardOffLine = new Vector2(0.55f, 1.05f);
    public static readonly Vector2 WaywardLegLine = new Vector2(-0.45f, -0.22f);

    public BowlingParams GetRandomDelivery()
    {
        BowlingParams param = new BowlingParams();

        param.swingType = swingType;
        param.torqueX = 0f;
        param.speedX = Random.Range(minX, maxX);
        param.length = Random.Range(minLen, maxLen);
        // speedZ is the LINE at the batsman's stumps, off side positive (see Constants).
        float roll = Random.value;
        if (roll < WaywardOffChance)
            param.speedZ = Random.Range(WaywardOffLine.x, WaywardOffLine.y);
        else if (roll < WaywardOffChance + WaywardLegChance)
            param.speedZ = Random.Range(WaywardLegLine.x, WaywardLegLine.y);
        else
            param.speedZ = Random.Range(minZ, maxZ);
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
