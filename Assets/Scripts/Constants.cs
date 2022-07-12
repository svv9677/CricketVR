using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    public const string PP_Difficulty = "difficulty";
    public const string PP_BattingStyle = "style";
    public const string PP_StadiumMode = "mode";
    public const string PP_ZOffset = "menu_offset";
    public const string PP_HudOffset = "hud_offset";

    public const string PP_Overlay = "overlay";
    public const string PP_ResetDelay = "reset_delay";
    public const string PP_FielderSpeed = "fielder_speed";
    public const string PP_AmpMin = "amp_min";
    public const string PP_AmpMax = "amp_max";

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

    public static readonly string[] CT_SwingPrefixes = { "none", "pace", "inSwing", "outSwing", "legSpin", "offSpin", "random" };

    // Configs for bowling params in the order:  minX,   maxX,   minY,   maxY,   minZ,   maxZ, minSwn, maxSwn, minTrn, maxTrn

    public static readonly float[] paceCfg = { 7f, 10f, -2.5f, -1f, -0.05f, 0.25f, 0f, 0f, 0f, 0f };
    public static readonly float[] inSwingCfg = { 6f, 8f, -2f, -0.3f, 0.7f, 1f, 0.4f, 0.9f, -0.03f, 0.03f };
    public static readonly float[] outSwingCfg = { 6f, 8f, -2f, -0.3f, -0.8f, -0.2f, 0.4f, 0.9f, -0.03f, 0.03f };
    public static readonly float[] legSpinCfg = { 3f, 4f, 0f, 0.5f, -0.1f, 0.2f, 0.1f, 0.3f, 0.2f, 0.5f };
    public static readonly float[] offSpinCfg = { 3f, 4f, 0f, 0.5f, 0f, 0.2f, 0.1f, 0.3f, 0.2f, 0.5f };


    // For testing (same ball every time)

    //public static readonly float[] paceCfg = { 7f, 7f, -1f, -1f, 0f, 0f, 0f, 0f, 0f, 0f };
    //public static readonly float[] inSwingCfg = paceCfg;
    //public static readonly float[] outSwingCfg = paceCfg;
    //public static readonly float[] legSpinCfg = paceCfg;
    //public static readonly float[] offSpinCfg = paceCfg;

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

