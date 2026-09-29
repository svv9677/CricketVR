using UnityEngine;

/// <summary>
/// Makes the existing SettingsPanel prefab usable as a screen inside a MenuRoot, without
/// SettingsPanel itself having to know about the stack.
///
/// SettingsPanel brings its own world-space canvas and card and places itself in front of the
/// player, so this screen reports OwnsItsCanvas and MenuRoot steps out of the way while it is up.
/// The panel refreshes from GameSettings on every show, so the controls mirror the current
/// settings whether it was opened from the Splash main menu or from the pause card mid-match.
/// </summary>
[RequireComponent(typeof(SettingsPanel))]
public class SettingsScreen : MenuScreen
{
    private SettingsPanel panel;

    public override bool OwnsItsCanvas => true;

    private void Awake() => panel = GetComponent<SettingsPanel>();

    public override void OnShow()
    {
        if (panel == null)
            panel = GetComponent<SettingsPanel>();
        if (panel != null)
            panel.Show();
    }

    public override void OnHide()
    {
        if (panel != null)
            panel.Hide();
    }

    /// Wired to the settings panel's Done button in place of Main.CloseSettings, so Done goes
    /// back to whoever opened it - the main menu in Splash, the pause card in a match.
    public void OnDone()
    {
        if (Root != null)
            Root.Back();
    }
}
