using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    public const string PP_Difficulty = "difficulty";
    public const string PP_BattingStyle = "style";
    public const string PP_StadiumMode = "mode";
    public const string PP_ZOffset = "menu_offset";

    public const string PP_Overlay = "overlay";
    public const string PP_ResetDelay = "reset_delay";
    public const string PP_FielderSpeed = "fielder_speed";
    public const string PP_AmpMin = "amp_min";
    public const string PP_AmpMax = "amp_max";
    public const string PP_GripLeftPosition = "grip_left_pos";
    public const string PP_GripLeftEuler = "grip_left_euler";
    public const string PP_GripRightPosition = "grip_right_pos";
    public const string PP_GripRightEuler = "grip_right_euler";
    public const string PP_BatPower = "bat_power";

    public const string CT_MinX = "minX";
    public const string CT_MaxX = "maxX";
    public const string CT_MinY = "minY";
    public const string CT_MaxY = "maxY";
    public const string CT_MinZ = "minZ";
    public const string CT_MaxZ = "maxZ";
    public const string CT_MinSwing = "minSwing";
    public const string CT_MaxSwing = "maxSwing";
    public const string CT_MinPitchTurn = "minTurn";
    public const string CT_MaxPitchTurn = "maxTurn";
    public const string CT_BatAmplifier = "batAmplifier";

    public const float BatColliderMultiplierEasy = 4f;
    public const float BatColliderMultiplierMedium = 2f;
    public const float BatColliderMultiplierHard = 1f;

    public static readonly Vector3 KeeperPositionSlow = new Vector3(11.6f, 0f, 0f);
    public static readonly Vector3 KeeperPositionMedium = new Vector3(16f, 0f, 0f);
    public static readonly Vector3 KeeperPositionFast = new Vector3(20f, 0f, 0f);

    public static readonly string[] CT_SwingPrefixes = { "none", "pace", "inSwing", "outSwing", "legSpin", "offSpin", "random" };

    // Configs for bowling params in the order:  minX,   maxX,   minY,   maxY,   minZ,   maxZ, minSwn, maxSwn, minTrn, maxTrn

    //public static readonly float[] paceCfg = { 7f, 10f, -2.5f, -1f, -0.05f, 0.25f, 0f, 0f, 0f, 0f };
    //public static readonly float[] inSwingCfg = { 6f, 8f, -2f, -0.3f, 0.7f, 1f, 0.4f, 0.9f, -0.03f, 0.03f };
    //public static readonly float[] outSwingCfg = { 6f, 8f, -2f, -0.3f, -0.8f, -0.2f, 0.4f, 0.9f, -0.03f, 0.03f };
    //public static readonly float[] legSpinCfg = { 3f, 4f, 0f, 0.5f, -0.1f, 0.2f, 0.1f, 0.3f, 0.2f, 0.5f };
    //public static readonly float[] offSpinCfg = { 3f, 4f, 0f, 0.5f, 0f, 0.2f, 0.1f, 0.3f, 0.2f, 0.5f };

    //public static readonly float[] paceCfg = { 7f, 10f, -1.5f, -0.5f, -0.2f, 0.1f, 0f, 0f, 0f, 0f };
    //public static readonly float[] inSwingCfg = { 6f, 8f, -1f, 0f, 0.4f, 0.6f, 0.4f, 1f, -0.03f, 0.03f };
    //public static readonly float[] outSwingCfg = { 6f, 8f, -1f, 0f, -1f, -0.7f, 0.4f, 1f, -0.03f, 0.03f };
    //public static readonly float[] legSpinCfg = { 3f, 4f, 0.5f, 0.9f, -0.2f, 0f, 0.1f, 0.3f, 0.2f, 0.5f };
    //public static readonly float[] offSpinCfg = { 3f, 4f, 0.5f, 0.9f, -0.1f, 0.1f, 0.1f, 0.3f, 0.2f, 0.5f };


    // (REAL) Configs for bowling params in the order:  minX,   maxX,   minLen,   maxLen,   minZ,   maxZ, minSwn, maxSwn, minTrn, maxTrn
    // Multiply x y z values by 18 to convert to kmh: (7 / 0.2mass * 3.6) = 126 kph
    //public static readonly float[] paceCfg = { 7f, 10f, 0f, 9f, -0.4f, 0f, 0f, 0f, 0f, 0f };
    //public static readonly float[] inSwingCfg = { 6f, 8f, -2f, 9f, 0.35f, 0.55f, 0.2f, 0.8f, -0.02f, 0.02f };
    //public static readonly float[] outSwingCfg = { 6f, 8f, -2f, 9f, -0.65f, -0.6f, 0.2f, 0.8f, -0.02f, 0.02f };
    //public static readonly float[] legSpinCfg = { 3.5f, 4.5f, 4f, 10f, -0.3f, -0.1f, 0.1f, 0.3f, 0.2f, 0.5f };
    //public static readonly float[] offSpinCfg = { 3.5f, 4.5f, 4f, 10f, -0.2f, 0f, 0.1f, 0.3f, 0.2f, 0.5f };

    // Current configs. Columns:
    //   minX, maxX     release speed as an "impulse": speed m/s = value / ConfigImpulseMass
    //   minLen, maxLen world X the ball pitches at (batsman's stumps are at x 10.30)
    //   minZ, maxZ     LINE: where the ball passes the batsman's stumps, metres from middle
    //                  stump, off side positive (mirrored for a left-hander). Stumps span
    //                  +-0.114; the off-side wide guideline is 0.889. Occasional wayward balls
    //                  outside this range come from BowlingProfile.WaywardChance.
    //   minSwn, maxSwn swing (spinners: drift) 0..1 of BallFlight.SwingCoefficientMax
    //   minTrn, maxTrn turn off the pitch, 0..1 of BallFlight.MaxTurnDegrees (6 deg);
    //                  seamers use a small +- range for seam movement.
    public static readonly float[] paceCfg = { 7.8f, 8.6f, 0f, 9f, -0.08f, 0.35f, 0f, 0f, -0.04f, 0.04f };
    public static readonly float[] inSwingCfg = { 6.9f, 7.8f, -2f, 9f, -0.05f, 0.30f, 0.3f, 0.9f, -0.03f, 0.03f };
    public static readonly float[] outSwingCfg = { 6.9f, 7.8f, -2f, 9f, 0.05f, 0.40f, 0.3f, 0.9f, -0.03f, 0.03f };
    public static readonly float[] legSpinCfg = { 3.8f, 4.7f, 4f, 10f, -0.10f, 0.30f, 0.1f, 0.3f, 0.3f, 0.6f };
    public static readonly float[] offSpinCfg = { 3.8f, 4.7f, 4f, 10f, -0.10f, 0.25f, 0.1f, 0.3f, 0.25f, 0.5f };

    /// The configs' speed "impulses" were tuned against a 0.2 kg ball. The ball is now a real
    /// 0.16 kg, so speeds are converted with this, not with the Rigidbody mass.
    public const float ConfigImpulseMass = 0.2f;
    /// km/h per config speed unit (the settings panel shows bowling speed in km/h).
    public const float KmhPerSpeedUnit = 3.6f / ConfigImpulseMass;

    /// Main.BatAmplifier at which the bat behaves like a real one (the settings panel's "Realistic").
    public const float BatPowerRealistic = 75f;


    //For testing(same ball every time)
    //public static readonly float[] paceCfg = { 7f, 7f, 7f, 7f, 0.1f, 0.1f, 0f, 0f, 0f, 0f };
    //public static readonly float[] inSwingCfg = paceCfg;
    //public static readonly float[] outSwingCfg = paceCfg;
    //public static readonly float[] legSpinCfg = paceCfg;
    //public static readonly float[] offSpinCfg = paceCfg;

    public static readonly Vector3[] fieldingPositions = {
        new Vector3(-25f, 0f, -14f),
        new Vector3(-27f, 0f, 13f),
        new Vector3(-29f, 0f, -45f),
        new Vector3(47f, 0f, 28f),
        new Vector3(32f, 0f, -44f),
        new Vector3(10f, 0f, 19f),
        new Vector3(-10f, 0f, 18f),
        new Vector3(-11f, 0f, 53f),
        new Vector3(-54f, 0f, -5f)};

}

public enum eBattingStyle
{
    None,
    RightHanded,
    LeftHanded
}

public enum eDifficulty
{
    None,
    Easy,
    Medium,
    Hard
}

public enum eStadiumMode
{
    None,
    Night,
    Day
}

public enum eSwingType
{
    None,
    Pace,
    InSwing,
    OutSwing,
    LegSpin,
    OffSpin,
    Random
}

public enum eGameState
{
    None,

    InMenu, // TODO: Add these later
    InGame_Ready,
    InGame_SelectDelivery,
    InGame_SelectDeliveryLoop,
    InGame_DeliverBall,
    InGame_DeliverBallLoop,
    InGame_BallHit,
    InGame_BallHitLoop,
    InGame_BallFielded,
    InGame_BallFielded_Loop,
    InGame_BallMissed,
    InGame_BallMissedLoop,
    InGame_BallPastBoundary,
    InGame_BallPastBoundaryLoop,
    InGame_Bowled,
    InGame_BowledLoop,
    InGame_ResetToReady,
    InGame_ResetToReadyLoop
}

public enum eFielderState
{
    Idle,
    ToIdle,

    TakingStart,
    ToTakingStart,

    MovingTowardsBall,
    Fielded
}

