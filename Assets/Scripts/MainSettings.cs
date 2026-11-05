using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


/// <summary>
/// Main's settings: difficulty (bat width), batting style, stadium mode, the debug tweakables and
/// the bowling sliders, grip calibration, and the settings panel. Split out of Main.cs on
/// 2026-11-05; the code is unchanged.
/// </summary>
public partial class Main
{
    /// Apply tuning changes made on the settings panel.
    public void ApplyTweaks() => updateTweakables();

    public void SetBattingStyle(eBattingStyle style)
    {
        if (battingStyle == style)
            return;
        battingStyle = style;
        updateBattingStyle();
    }

    public void SetDifficulty(eDifficulty level)
    {
        if (difficulty == level)
            return;
        difficulty = level;
        updateDifficulty();
    }

    public void CloseSettings()
    {
        if (menuToggle)
            ToggleUI(false);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Difficulty settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateDifficulty(bool updateUI = false)
    {
        if (difficulty == _difficulty)
            return;
        _difficulty = difficulty;
        // Applied on every change. (It used to be applied only when the old radio toggles were out
        // of step, which never happened after a click - so picking a level in the menu saved it
        // but left the bat width alone until the next launch.)
        updateBatColliderSize();
        PlayerPrefs.SetInt(Constants.PP_Difficulty, (int)difficulty);
        if (updateUI)
            PlayerPrefs.Save();
        RefreshSettingsPanel();
    }
    /// How much wider than the real blade a hit is found at this difficulty (BatContact).
    public float BatWidthMultiplier => difficulty switch
    {
        eDifficulty.Easy => Constants.BatColliderMultiplierEasy,
        eDifficulty.Medium => Constants.BatColliderMultiplierMedium,
        _ => Constants.BatColliderMultiplierHard,
    };
    public void onRadioEasy(Toggle t) { if (t.isOn) SetDifficulty(eDifficulty.Easy); }
    public void onRadioMedium(Toggle t) { if (t.isOn) SetDifficulty(eDifficulty.Medium); }
    public void onRadioHard(Toggle t) { if (t.isOn) SetDifficulty(eDifficulty.Hard); }

    void updateBatColliderSize()
    {
        switch (difficulty)
        {
            case eDifficulty.Easy:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierEasy;
                    theBatScript.batCollider.size = theSize;
                }
                break;
            case eDifficulty.Medium:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierMedium;
                    theBatScript.batCollider.size = theSize;
                }
                break;
            case eDifficulty.Hard:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierHard;
                    theBatScript.batCollider.size = theSize;
                }
                break;
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Batting Style settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateBattingStyle(bool updateUI = false)
    {
        bool changed = false;
        if (theBatScript.leftHandParent != null && battingStyle == eBattingStyle.LeftHanded)
        {
            changed = true;
            theBatScript.attachParent = theBatScript.leftHandParent;
            theBatScript.CheckAndGrab();
            PlayerPrefs.SetInt(Constants.PP_BattingStyle, (int)battingStyle);
        }
        if (theBatScript.rightHandParent != null && battingStyle == eBattingStyle.RightHanded)
        {
            changed = true;
            theBatScript.attachParent = theBatScript.rightHandParent;
            theBatScript.CheckAndGrab();
            PlayerPrefs.SetInt(Constants.PP_BattingStyle, (int)battingStyle);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (battingStyle != _shownBattingStyle)
        {
            _shownBattingStyle = battingStyle;
            RefreshSettingsPanel();
        }

        if (changed)
            PlayerPrefs.Save();
    }
    public void onRadioRightHanded(Toggle t) { if (t.isOn) SetBattingStyle(eBattingStyle.RightHanded); }
    public void onRadioLeftHanded(Toggle t) { if (t.isOn) SetBattingStyle(eBattingStyle.LeftHanded); }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Stadium settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateStadiumMode(bool updateUI = false)
    {
        bool changed = false;
        if (stadiumMode == eStadiumMode.Night && !theNightLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = NightMaterial;
            theNightLights.SetActive(true);
            theDayLights.SetActive(false);
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }
        else if(stadiumMode == eStadiumMode.Day && !theDayLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = DayMaterial;
            theNightLights.SetActive(false);
            theDayLights.SetActive(true);
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        if(changed)
            PlayerPrefs.Save();
    }
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Z Offset
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateZOffset(bool savePrefs = false)
    {
        // The settings panel now opens in front of the player, wherever they face, so the old
        // "Menu Position" slider is gone; the value is only kept in PlayerPrefs.
        if (_zOffset != zOffset)
        {
            _zOffset = zOffset;
            PlayerPrefs.SetFloat(Constants.PP_ZOffset, zOffset);
            if (savePrefs)
                PlayerPrefs.Save();
        }
    }
    public void onZChange(float val)
    {
        zOffset = val;
        updateZOffset();
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Debug Tweakables
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateTweakables(bool savePrefs = false)
    {
        bool changed = false, refresh = false;
        if (ampMin != _ampMin)
        {
            changed = true;
            _ampMin = ampMin;
            PlayerPrefs.SetFloat(Constants.PP_AmpMin, ampMin);
        }
        if (ampMax != _ampMax)
        {
            changed = true;
            _ampMax = ampMax;
            PlayerPrefs.SetFloat(Constants.PP_AmpMax, ampMax);
        }
        if (resetDelay != _resetDelay)
        {
            changed = true;
            _resetDelay = resetDelay;
            PlayerPrefs.SetFloat(Constants.PP_ResetDelay, resetDelay);
        }
        if (fielderSpeed != _fielderSpeed)
        {
            changed = refresh = true;
            _fielderSpeed = fielderSpeed;
            PlayerPrefs.SetFloat(Constants.PP_FielderSpeed, fielderSpeed);
        }
        if (overlayVisible != _overlayVisible)
        {
            changed = refresh = true;
            _overlayVisible = overlayVisible;
            if (dbgOverlayParent != null)
                dbgOverlayParent.SetActive(overlayVisible);
            PlayerPrefs.SetInt(Constants.PP_Overlay, overlayVisible ? 1 : 0);
        }
        if(swingType != _swingType)
        {
            _swingType = swingType;
            reloadTweakables();
            refresh = true;
        }
        if (_BatAmplifier != BatAmplifier)
        {
            changed = refresh = true;
            _BatAmplifier = BatAmplifier;
            PlayerPrefs.SetFloat(Constants.PP_BatPower, BatAmplifier);
        }
        if (applyBowlingTweaks())
            refresh = true;

        if (refresh)
            RefreshSettingsPanel();
        if (savePrefs && changed)
            PlayerPrefs.Save();
    }

    /// Copy the bowling ranges into the current bowler type's profile. True if any changed.
    private bool applyBowlingTweaks()
    {
        if (bowlingProfileManager == null)
            return false;
        BowlingProfile profile = bowlingProfileManager.GetProfile(swingType);
        bool changed = false;
        if (_MinX != MinX) { _MinX = MinX; profile.minX = MinX; changed = true; }
        if (_MaxX != MaxX) { _MaxX = MaxX; profile.maxX = MaxX; changed = true; }
        if (_MinZ != MinZ) { _MinZ = MinZ; profile.minZ = MinZ; changed = true; }
        if (_MaxZ != MaxZ) { _MaxZ = MaxZ; profile.maxZ = MaxZ; changed = true; }
        if (_MinSwing != MinSwing) { _MinSwing = MinSwing; profile.minSwing = MinSwing; changed = true; }
        if (_MaxSwing != MaxSwing) { _MaxSwing = MaxSwing; profile.maxSwing = MaxSwing; changed = true; }
        if (_MinPitchTurn != MinPitchTurn) { _MinPitchTurn = MinPitchTurn; profile.minPitchTurn = MinPitchTurn; changed = true; }
        if (_MaxPitchTurn != MaxPitchTurn) { _MaxPitchTurn = MaxPitchTurn; profile.maxPitchTurn = MaxPitchTurn; changed = true; }
        return changed;
    }
    public void onVoid(float val) { }
    public void OpenSettings()
    {
        // The settings panel is a screen of the menu (MenuRoot) now, and MenuRoot keeps its
        // screens switched off until it shows them: showing the panel directly left it inside an
        // inactive parent, so nothing appeared.
        if (MenuRoot.Instance != null)
        {
            MenuRoot.Instance.OpenAt("settings");
            return;
        }
        if (!menuToggle)
            ToggleUI(true);
    }

    public bool SettingsOpen => menuToggle || (MenuRoot.Instance != null && MenuRoot.Instance.IsOpen);
    public bool CalibratingGrip => gripCalibration != null && gripCalibration.IsActive;

    public void onCalibrateGrip() { StartGripCalibration(false); }
    public void onCalibrateGripFlat() { StartGripCalibration(true); }
    private void StartGripCalibration(bool flat)
    {
        if (gripCalibration == null)
            return;
        // Close the menu first: showing it hides the bat.
        if (menuToggle)
            ToggleUI(false);
        gripCalibration.Begin(flat);
    }
    public void onResetGrip()
    {
        if (gripCalibration != null)
            gripCalibration.ResetToDefault();
    }
    public void reloadTweakables()
    {
        float[] config;
        switch(swingType)
        {
            case eSwingType.Pace:
                config = Constants.paceCfg;
                break;
            case eSwingType.InSwing:
                config = Constants.inSwingCfg;
                break;
            case eSwingType.OutSwing:
                config = Constants.outSwingCfg;
                break;
            case eSwingType.LegSpin:
                config = Constants.legSpinCfg;
                break;
            case eSwingType.OffSpin:
                config = Constants.offSpinCfg;
                break;
            default:
                config = Constants.paceCfg;
                break;
        }
        MinX = config[0];
        MaxX = config[1];
        MinY = config[2];
        MaxY = config[3];
        MinZ = config[4];
        MaxZ = config[5];
        MinSwing = config[6];
        MaxSwing = config[7];
        MinPitchTurn = config[8];
        MaxPitchTurn = config[9];
    }
    public void onOverlayToggle(Toggle t)
    {
        overlayVisible = t.isOn;
        updateTweakables();
    }
    public void onResetDelay(float val)
    {
        resetDelay = val;
        updateTweakables();
    }
    public void onFielderSpeed(float val)
    {
        fielderSpeed = val;
        updateTweakables();
    }
    public void onAmpMinChange(float val)
    {
        ampMin = val;
        updateTweakables();
    }
    public void onAmpMaxChange(float val)
    {
        ampMax = val;
        updateTweakables();
    }
}
