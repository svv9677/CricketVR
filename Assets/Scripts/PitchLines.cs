using UnityEngine;

/// <summary>
/// The pitch's white lines as geometry, built when the match scene loads, in place of the
/// markings texture. That texture stretched 2048 x 318 pixels over the whole 23.5 m pitch, with
/// bilinear filtering and no anisotropy: from the crease, looking down the pitch at a low angle,
/// the far end's creases blurred into the turf and the near ones went soft and blocky. Geometry
/// stays a sharp edge at any distance and angle (the eye buffers' 4x MSAA smooths it).
///
/// Laid out from the stumps, to the Laws; the layout and the mesh are made by PitchLinesBuilder
/// (CricketVR > Scene Objects > Place Pitch Lines), which puts the object "Pitch Lines" in CricketVR
/// and Nets with its mesh and two materials saved as assets, and turns the old markings renderer
/// off. Until 2026-11-15 this class built all of it in code as the scene loaded. What stays here
/// are the numbers other code reads.
/// </summary>
public static class PitchLines
{
    /// The scenes the lines are placed in: the match, and the practice nets.
    public static readonly string[] Scenes = { "CricketVR", "Nets" };
    /// Half the pitch's width to the middle of each edge line (the Laws' 3.05 m pitch), and the
    /// edge lines' width: ClubPitchSurface lays the club's prepared strip to just outside them.
    public const float PitchHalf = 1.525f, EdgeWidth = 0.03f;
    /// The lines' shader: flat colour, biased in front of the pitch (PitchBlend's own bias hid them).
    public const string LinesShader = "CricketVR/PitchLines";

    /// Whether the lines are placed in the scene of this name.
    public static bool BuildsIn(string sceneName) => System.Array.IndexOf(Scenes, sceneName) >= 0;
}
