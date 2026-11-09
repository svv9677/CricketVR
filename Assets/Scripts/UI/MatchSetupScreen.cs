using TMPro;
using UnityEngine;

/// <summary>
/// Match Setup: set a total or chase one, how good the bowling is, the bat shield (bat width -
/// GameSettings.Difficulty, the same setting as Settings > Batting) and which hand you bat. Since
/// 2026-11-09 it is step 1 of Quick Play's New Game for the length chosen there (QuickPlayScreen):
/// Next goes to step 2, the ground (CustomiseScreen), whose Start Match plays it. Built by
/// Tools > CricketVR > Build Menu Prefabs; nothing made at runtime.
/// </summary>
public class MatchSetupScreen : MenuScreen
{
    [SerializeField] private TextMeshProUGUI oversLabel;
    [SerializeField] private SegmentedControl matchType;
    [SerializeField] private TextMeshProUGUI matchTypeHint;
    [SerializeField] private SegmentedControl bowling;
    [SerializeField] private TextMeshProUGUI bowlingHint;
    [SerializeField] private SegmentedControl shield;
    [SerializeField] private TextMeshProUGUI shieldHint;
    [SerializeField] private TextMeshProUGUI stepTitle;
    [SerializeField] private SegmentedControl hand;

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        MatchFormat.Sync();
        int overs = GameSettings.Overs;
        if (oversLabel != null) oversLabel.text = MatchFormat.Label();
        if (matchType != null) matchType.SetValueWithoutNotify((int)MatchSetup.Type);
        if (matchTypeHint != null)
            matchTypeHint.text = MatchFormat.IsTest
                ? (MatchSetup.Type == eMatchType.Chase ? "A Test: the AI bats first. Two innings each, five days." :
                                                         "A Test: you bat first. Two innings each, five days; declare when you like.")
                : MatchSetup.Type == eMatchType.Chase
                ? $"The AI bats first (it usually makes about {AIInnings.Typical(overs, MatchSetup.Bowling)}); you chase it."
                : "Set a total in your overs, then the AI chases it.";
        if (bowling != null) bowling.SetValueWithoutNotify((int)MatchSetup.Bowling);
        if (bowlingHint != null) bowlingHint.text = MatchSetup.Hint(MatchSetup.Bowling);
        if (shield != null) shield.SetValueWithoutNotify(Mathf.Clamp((int)GameSettings.Difficulty - 1, 0, 2));
        if (shieldHint != null) shieldHint.text = ShieldHint(GameSettings.Difficulty);
        if (stepTitle != null) stepTitle.text = $"New {MatchFormat.MatchName(MatchFormat.Current)}";
        if (hand != null) hand.SetValueWithoutNotify(GameSettings.BattingStyle == eBattingStyle.LeftHanded ? 1 : 0);
    }

    public static string ShieldHint(eDifficulty level)
    {
        switch (level)
        {
            case eDifficulty.Easy: return $"Bat shield: the blade plays {Constants.BatColliderMultiplierEasy:0}× wide.";
            case eDifficulty.Medium: return $"Bat shield: the blade plays {Constants.BatColliderMultiplierMedium:0}× wide.";
            default: return "No shield: a real bat's width.";
        }
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// 5, 10, 20, 50 overs or a Test (MatchFormat).
    public void OnMoreOvers() { MatchFormat.Next(); Refresh(); }
    public void OnFewerOvers() { MatchFormat.Previous(); Refresh(); }
    public void OnMatchType(int i) { MatchSetup.Type = (eMatchType)i; Refresh(); }
    public void OnBowling(int i) { MatchSetup.Bowling = (eBowlingLevel)i; Refresh(); }
    public void OnShield(int i) { GameSettings.Difficulty = (eDifficulty)(i + 1); Refresh(); }
    public void OnStartMatch() => QuickPlaySlots.StartNew();
    public void OnHand(int i) { GameSettings.BattingStyle = i == 1 ? eBattingStyle.LeftHanded : eBattingStyle.RightHanded; Refresh(); }

    /// Step 2: the ground.
    public void OnNext()
    {
        if (Root != null)
            Root.Push("customise");
    }
}
