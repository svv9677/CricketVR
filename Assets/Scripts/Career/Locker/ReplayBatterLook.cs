using UnityEngine;

/// <summary>
/// How the replay's stand-in batter is dressed (phase 7 locker): the helmet's shell and trim, and the
/// head's skin. Unset, the replay looks as it always has.
/// </summary>
public static class ReplayBatterLook
{
    public static readonly Color DefaultSkin = new Color(0.5f, 0.34f, 0.25f);

    /// The helmet shell's and trim's colours (null: as the helmet is).
    public static Color? Shell, Trim;
    public static Color Skin = DefaultSkin;

    /// The replay helmet in the colours above.
    public static void Dress(MeshRenderer helmet) { }

    public static void Clear()
    {
        Shell = null;
        Trim = null;
        Skin = DefaultSkin;
    }
}
