using System.Collections.Generic;

/// <summary>
/// Which menu screen is showing, and what Back does. Screens are identified by name so this is
/// pure logic with no GameObject references - MenuRoot maps a name to the screen it owns, and
/// these rules are provable without a scene.
///
/// Splash resets to "main"; CricketVR and Nets reset to "pause". Settings is pushed from either,
/// and Pop returns to whoever pushed it, which is what lets one settings prefab serve all three
/// scenes.
/// </summary>
public class MenuStack
{
    private readonly List<string> screens = new List<string>();

    /// The screen that should be visible, or null when the menu is closed.
    public string Current => screens.Count > 0 ? screens[screens.Count - 1] : null;

    public bool IsOpen => screens.Count > 0;

    /// Back is offered on everything except the root screen.
    public bool CanGoBack => screens.Count > 1;

    /// Open the menu at its root screen, discarding any previous history.
    public void Reset(string root)
    {
        screens.Clear();
        if (root != null)
            screens.Add(root);
    }

    /// Show `screen` over the current one. Pushing the screen that is already showing does
    /// nothing, so a double-click does not need two Backs to undo.
    public void Push(string screen)
    {
        if (screen == null || Current == screen)
            return;
        screens.Add(screen);
    }

    /// Back. At the root this empties the stack, which closes the menu.
    public void Pop()
    {
        if (screens.Count > 0)
            screens.RemoveAt(screens.Count - 1);
    }

    /// Close: forget every screen.
    public void Clear() => screens.Clear();
}
