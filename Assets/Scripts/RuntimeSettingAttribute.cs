using UnityEngine;

/// <summary>
/// A field the game sets itself when Play starts (from the player's settings, GameSettings or a
/// bowler's config), so a value typed into it outside Play is never used. The inspector shows it
/// greyed out outside Play, labelled with where its value comes from; in Play it can be changed
/// to try a value live. Drawn by Editor/RuntimeSettingDrawer.
/// </summary>
public class RuntimeSettingAttribute : PropertyAttribute
{
    public readonly string source;

    public RuntimeSettingAttribute(string source)
    {
        this.source = source;
    }
}
