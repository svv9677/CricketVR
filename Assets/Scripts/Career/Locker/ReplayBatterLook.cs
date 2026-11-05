using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the replay's stand-in batter is dressed (phase 7 locker): the helmet's shell and trim, and the
/// head's skin. Unset, the replay looks as it always has. CareerCosmetics sets them as a career
/// scene loads (before ShotRecorder builds the replay actors) and clears them with the scene.
/// The helmet is the keeper's (ReplayActors.AddBatterHelmet): submesh 0 its shell, 1 its grille.
/// </summary>
public static class ReplayBatterLook
{
    public const int ShellIndex = 0, TrimIndex = 1;

    public static readonly Color DefaultSkin = new Color(0.5f, 0.34f, 0.25f);

    /// The helmet shell's and trim's colours (null: as the helmet is).
    public static Color? Shell, Trim;
    public static Color Skin = DefaultSkin;

    // The material copies Dress made, destroyed by Clear (they live as long as the replay helmet).
    private static readonly List<Material> made = new List<Material>();

    /// The replay helmet in the colours above: copies of its shell and grille materials, when it has
    /// both. Nothing when neither colour is set.
    public static void Dress(MeshRenderer helmet)
    {
        if (helmet == null || (Shell == null && Trim == null))
            return;
        Material[] shared = helmet.sharedMaterials;
        if (shared.Length <= TrimIndex)
            return;
        var dressed = (Material[])shared.Clone();
        Tint(dressed, ShellIndex, Shell);
        Tint(dressed, TrimIndex, Trim);
        helmet.sharedMaterials = dressed;
    }

    private static void Tint(Material[] materials, int i, Color? colour)
    {
        if (colour == null || materials[i] == null)
            return;
        materials[i] = CosmeticLook.Tinted(materials[i], colour.Value, null);
        made.Add(materials[i]);
    }

    public static void Clear()
    {
        Shell = null;
        Trim = null;
        Skin = DefaultSkin;
        CosmeticLook.Destroy(made);
    }
}
