using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BowlingParams
{
    public eSwingType swingType;
    public float torqueX;
    public float speedX;
    public float speedY;
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
        speedY = -1f;
        speedZ = 0f;
        swing = 0f;
        pitchTurn = 0f;
        applySwing = false;
        applyPitchTurn = false;
    }

    public override string ToString()
    {
        return swingType.ToString() + ", " + torqueX.ToString() + ", (" +
                speedX.ToString() + "," + speedY.ToString() + "," + speedZ.ToString() + "), " +
                swing.ToString() + ", " + pitchTurn.ToString() + ", (" +
                applySwing.ToString() + "/" + applyPitchTurn.ToString() + ")";
    }
}

public class BowlingProfile
{
    public eSwingType swingType;

    public float minX;
    public float maxX;
    public float minY;
    public float maxY;
    public float minZ;
    public float maxZ;

    public float minSwing;
    public float maxSwing;
    public float minPitchTurn;
    public float maxPitchTurn;

    public BowlingProfile(eSwingType _swingType = eSwingType.Pace,
                          float _minX = 0f, float _maxX = 10f,
                          float _minY = -2f, float _maxY = 2f,
                          float _minZ = -0.5f, float _maxZ = 0.5f,
                          float _minSwing = 0f, float _maxSwing = 1f,
                          float _minPitchTurn = 0f, float _maxPitchTurn = 1f
                          )
    {
        swingType = _swingType;

        minX = _minX;
        maxX = _maxX;
        minY = _minY;
        maxY = _maxY;
        minZ = _minZ;
        maxZ = _maxZ;

        minSwing = _minSwing;
        maxSwing = _maxSwing;
        minPitchTurn = _minPitchTurn;
        maxPitchTurn = _maxPitchTurn;
    }

    public BowlingParams GetRandomDelivery()
    {
        BowlingParams param = new BowlingParams();
        float director = (swingType == eSwingType.InSwing || swingType == eSwingType.OutSwing) ? -1f : 1f;

        param.swingType = swingType;
        param.torqueX = director * 50f;
        param.speedX = Random.Range(minX, maxX);
        param.speedY = Random.Range(minY, maxY);
        param.speedZ = Random.Range(minZ, maxZ);
        param.swing = Random.Range(minSwing, maxSwing);
        param.pitchTurn = Random.Range(minPitchTurn, maxPitchTurn);
        param.applySwing = Random.Range(0f, 1f) > 0.05f || swingType == eSwingType.InSwing || swingType == eSwingType.OutSwing;
        param.applyPitchTurn = Random.Range(0f, 1f) > 0.15f || swingType == eSwingType.LegSpin || swingType == eSwingType.OffSpin;

        // For spin bowling, if we are looping (Y is greater than half of maxY), then limit the x-speed to not throw a no-ball
        if (swingType == eSwingType.LegSpin || swingType == eSwingType.OffSpin)
        {
            if (param.speedY > ((minY+maxY)/2f))
                param.speedX = Mathf.Clamp(param.speedX, minX, (minX + maxX) / 2f);
        }
        // For in-swing, if swing amount is large, make sure we start from way off-side
        if(swingType == eSwingType.InSwing)
        {
            if (param.swing > ((minSwing + maxSwing) / 2f))
                param.speedZ = Mathf.Clamp(param.speedZ, (minZ + maxZ) / 2f, maxZ);
        }
        // Same applies for out swing
        if (swingType == eSwingType.OutSwing)
        {
            if (param.swing > ((minSwing + maxSwing) / 2f))
                param.speedZ = Mathf.Clamp(param.speedZ, minZ, (minZ + maxZ) / 2f);
        }
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
