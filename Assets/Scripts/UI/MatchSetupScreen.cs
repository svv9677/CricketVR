using TMPro;
using UnityEngine;

/// <summary>
/// Match Setup, from the Splash main menu: the overs, set a total or chase one, how good the
/// bowling is, and the bat shield (bat width - GameSettings.Difficulty, the same setting as
/// Settings > Batting). Start Match goes to the ground with all of it (MatchFlow,
/// BowlingLevels). Built by Tools > CricketVR > Build Menu Prefabs; nothing made at runtime.
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

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        int overs = GameSettings.Overs;
        if (oversLabel != null) oversLabel.text = overs == 1 ? "1 over" : $"{overs} overs";
        if (matchType != null) matchType.SetValueWithoutNotify((int)MatchSetup.Type);
        if (matchTypeHint != null)
            matchTypeHint.text = MatchSetup.Type == eMatchType.Chase
                ? $"The AI bats first (it usually makes about {AIInnings.Typical(overs, MatchSetup.Bowling)}); you chase it."
                : "Set a total in your overs, then the AI chases it.";
        if (bowling != null) bowling.SetValueWithoutNotify((int)MatchSetup.Bowling);
        if (bowlingHint != null) bowlingHint.text = MatchSetup.Hint(MatchSetup.Bowling);
        if (shield != null) shield.SetValueWithoutNotify(Mathf.Clamp((int)GameSettings.Difficulty - 1, 0, 2));
        if (shieldHint != null) shieldHint.text = ShieldHint(GameSettings.Difficulty);
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

    public void OnMoreOvers() { GameSettings.Overs++; Refresh(); }
    public void OnFewerOvers() { GameSettings.Overs--; Refresh(); }
    public void OnMatchType(int i) { MatchSetup.Type = (eMatchType)i; Refresh(); }
    public void OnBowling(int i) { MatchSetup.Bowling = (eBowlingLevel)i; Refresh(); }
    public void OnShield(int i) { GameSettings.Difficulty = (eDifficulty)(i + 1); Refresh(); }
    public void OnStartMatch() => SceneFader.Load(MainMenuScreen.CricketScene);
}
