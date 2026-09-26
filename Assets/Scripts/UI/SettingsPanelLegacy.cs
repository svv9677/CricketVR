using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LEGACY - the old settings layout's fields and handlers, kept only so that
/// CricketVRSceneBuilder.BuildSettingsPanel (Tools > CricketVR > Build UI Prefabs) still compiles
/// and its panel still works. Tools > CricketVR > Build Player UI builds the new layout, which
/// uses none of this. Delete this file once CricketVRSceneBuilder calls
/// UIPrefabBuilder.BuildPanels instead of building the panels itself.
/// </summary>
public partial class SettingsPanel
{
    [HideInInspector] public Toggle leftHanded;
    [HideInInspector] public Toggle rightHanded;
    [HideInInspector] public Toggle easy;
    [HideInInspector] public Toggle medium;
    [HideInInspector] public Toggle hard;
    [HideInInspector] public Toggle overlay;
    [HideInInspector] public SliderRow resetDelay;
    [HideInInspector] public SliderRow fielderSpeed;
    [HideInInspector] public SliderRow batPower;
    [HideInInspector] public SliderRow ampMin;
    [HideInInspector] public SliderRow ampMax;
    [HideInInspector] public TMP_Text bowlingType;
    [HideInInspector] public SliderRow minSpeed, maxSpeed, minLine, maxLine, minSwing, maxSwing, minTurn, maxTurn;

    private void RefreshLegacy(Main m)
    {
        SetToggle(leftHanded, m.BattingStyle == eBattingStyle.LeftHanded);
        SetToggle(rightHanded, m.BattingStyle == eBattingStyle.RightHanded);
        SetToggle(easy, m.Difficulty == eDifficulty.Easy);
        SetToggle(medium, m.Difficulty == eDifficulty.Medium);
        SetToggle(hard, m.Difficulty == eDifficulty.Hard);
        SetToggle(overlay, m.overlayVisible);
        SetSlider(resetDelay, m.resetDelay, m.resetDelay.ToString("0.0"));
        SetSlider(fielderSpeed, m.fielderSpeed, m.fielderSpeed.ToString("0.0"));
        SetSlider(batPower, m.BatAmplifier, UIFormat.BatPower(m.BatAmplifier));
        SetSlider(ampMin, m.ampMin, m.ampMin.ToString("0.0"));
        SetSlider(ampMax, m.ampMax, m.ampMax.ToString("0.0"));
        SetText(bowlingType, "Bowling - " + UIFormat.BowlerType(m.swingType));
        SetSlider(minSpeed, m.MinX, m.MinX.ToString("0.00"));
        SetSlider(maxSpeed, m.MaxX, m.MaxX.ToString("0.00"));
        SetSlider(minLine, m.MinZ, m.MinZ.ToString("0.00"));
        SetSlider(maxLine, m.MaxZ, m.MaxZ.ToString("0.00"));
        SetSlider(minSwing, m.MinSwing, m.MinSwing.ToString("0.00"));
        SetSlider(maxSwing, m.MaxSwing, m.MaxSwing.ToString("0.00"));
        SetSlider(minTurn, m.MinPitchTurn, m.MinPitchTurn.ToString("0.00"));
        SetSlider(maxTurn, m.MaxPitchTurn, m.MaxPitchTurn.ToString("0.00"));
    }

    private static void SetToggle(Toggle toggle, bool on)
    {
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(on);
    }

    public void OnLeftHanded(bool on) { if (on) M.SetBattingStyle(eBattingStyle.LeftHanded); }
    public void OnRightHanded(bool on) { if (on) M.SetBattingStyle(eBattingStyle.RightHanded); }
    public void OnEasy(bool on) { if (on) M.SetDifficulty(eDifficulty.Easy); }
    public void OnMedium(bool on) { if (on) M.SetDifficulty(eDifficulty.Medium); }
    public void OnHard(bool on) { if (on) M.SetDifficulty(eDifficulty.Hard); }
    public void OnResetDelay(float v) => M.onResetDelay(v);
    public void OnAmpMin(float v) => M.onAmpMinChange(v);
    public void OnAmpMax(float v) => M.onAmpMaxChange(v);
    public void OnMinSpeed(float v) { M.MinX = v; M.ApplyTweaks(); }
    public void OnMaxSpeed(float v) { M.MaxX = v; M.ApplyTweaks(); }
    public void OnMinLine(float v) { M.MinZ = v; M.ApplyTweaks(); }
    public void OnMaxLine(float v) { M.MaxZ = v; M.ApplyTweaks(); }
    public void OnMinSwing(float v) { M.MinSwing = v; M.ApplyTweaks(); }
    public void OnMaxSwing(float v) { M.MaxSwing = v; M.ApplyTweaks(); }
    public void OnMinTurn(float v) { M.MinPitchTurn = v; M.ApplyTweaks(); }
    public void OnMaxTurn(float v) { M.MaxPitchTurn = v; M.ApplyTweaks(); }
}
