using UnityEngine;

/// <summary>
/// One screen inside a MenuRoot - the main menu, the pause card, the settings panel. A screen is
/// a plain child GameObject that is switched on and off; it knows nothing about where the menu
/// sits in the world, how the laser or mouse reaches it, or what opened it. MenuRoot owns all of
/// that, which is what lets the same settings prefab be used in Splash and in a match.
///
/// Subclasses override OnShow to refresh themselves from GameSettings.
/// </summary>
public abstract class MenuScreen : MonoBehaviour
{
    [Tooltip("The name this screen is pushed and popped by. Must be unique within a MenuRoot.")]
    [SerializeField] private string screenName;

    public string ScreenName => screenName;

    /// The MenuRoot this screen belongs to; assigned by MenuRoot on Awake.
    public MenuRoot Root { get; internal set; }

    /// True when this screen brings its own world-space canvas and card, as the existing
    /// SettingsPanel prefab does. MenuRoot then hides its own card while the screen is up,
    /// instead of leaving an empty rounded box behind it.
    public virtual bool OwnsItsCanvas => false;

    /// Called every time the screen becomes visible. Re-read settings here, not in Awake: a
    /// screen is shown many times over a session.
    public virtual void OnShow() { }

    /// Called as the screen is hidden.
    public virtual void OnHide() { }

    // ---- Wired in the prefab --------------------------------------------------------------------

    public void OnBack()
    {
        if (Root != null)
            Root.Back();
    }
}
