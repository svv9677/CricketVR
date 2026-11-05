using UnityEngine;

/// <summary>
/// The pitch at the club ground: a neat prepared 3.05 m strip whose edges sit on PitchLines' edge
/// lines, put on the match scene's Pitch Base (CricketVR/PitchBlend, "Hard Pitch Blend") by
/// ClubGroundVenue as it dresses the scene for the club. The stadium never runs this.
///
/// Why the stadium's settings frayed at the club. The 24 x 6 m pitch quad fades to clear across
/// its long edges: clear for 0.99 m (_InsetY 0.165 of 6 m), then a 0.96 m fade (_FadeWidthY
/// 0.16) wobbled by blotches of the detail map (_EdgeNoise 0.55), half-clear at about 1.53 m. At
/// the stadium that fade lands on the ground's own baked pitch rectangle, the same colour, so it
/// only softens the edge. At the club it lands on mown grass: the strip frayed over a metre
/// either side, the edge lines sat on half-clear turf, and the blotches pushed it wider on one
/// side than the other, so one line read as lying on the grass.
///
/// Here the surface is fully prepared to OpaqueHalfWidth (just outside the edge lines' outer
/// edge), fades to the outfield over EdgeFeather (a clean mown edge, no blotches), and runs
/// opaque along its length to EndFeather from each end, past the edge lines' ends. The material
/// is the renderer's own instance (r.material: the copy StadiumConditions tints for a Green or
/// Dusty pitch, if it made one), so the asset and the stadium keep their settings, and the
/// pitch's colour, texture and physics are not touched (cosmetic only).
/// </summary>
public static class ClubPitchSurface
{
    /// The prepared surface's half-width at full cover: the edge lines' outer edge plus 2 cm, so
    /// the white lines lie on clay, not on the fade.
    public const float OpaqueHalfWidth = PitchLines.PitchHalf + PitchLines.EdgeWidth * 0.5f + 0.02f;
    /// The mown edge, from full cover to clear: 8 cm, a few pixels from the crease, no fringe.
    public const float EdgeFeather = 0.08f;
    /// The fade at each end of the quad, along its length: short of where the edge lines stop.
    public const float EndFeather = 0.3f;

    /// PitchBlend's properties (names, not ids: an id is a native call, and the numbers here are
    /// tested without the engine).
    public const string InsetY = "_InsetY", FadeWidthY = "_FadeWidthY", FadeWidthX = "_FadeWidthX", EdgeNoise = "_EdgeNoise";

    /// PitchBlend's edge settings, in its quad-UV units (0..1 across the whole quad).
    public struct Settings
    {
        public float insetY, fadeY, fadeX, edgeNoise;
    }

    /// The settings for a pitch quad `halfWidth` x `halfLength` metres from its middle.
    public static Settings For(float halfWidth, float halfLength)
    {
        float clear = Mathf.Max(0f, halfWidth - OpaqueHalfWidth - EdgeFeather);
        return new Settings
        {
            insetY = clear / (2f * halfWidth),
            fadeY = EdgeFeather / (2f * halfWidth),
            fadeX = EndFeather / (2f * halfLength),
            edgeNoise = 0f,
        };
    }

    /// PitchBlend's EdgeAlpha (with no edge noise) at `across` metres from the quad's middle line
    /// and `along` metres from its middle, so tests can see where the strip's edge falls.
    public static float Alpha(Settings s, float halfWidth, float halfLength, float across, float along)
    {
        float ey = (halfWidth - Mathf.Abs(across)) / (2f * halfWidth);
        float ex = (halfLength - Mathf.Abs(along)) / (2f * halfLength);
        float ax = s.fadeX <= 1e-5f ? 1f : Mathf.Clamp01(ex / s.fadeX);
        float ay = s.fadeY <= 1e-5f ? (ey >= s.insetY ? 1f : 0f) : Mathf.Clamp01((ey - s.insetY) / s.fadeY);
        float a = Mathf.Min(ax, ay);
        return a * a * (3f - 2f * a);
    }

    /// Puts the club's edge on the pitch quad `r` (its world bounds give its size: the quad lies
    /// along X, square to the world). False, and nothing changed, if its material is not one with
    /// these properties.
    public static bool Apply(Renderer r)
    {
        if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(InsetY))
            return false;
        Vector3 half = r.bounds.extents;
        if (half.x <= 0f || half.z <= 0f)
            return false;
        Settings s = For(half.z, half.x);
        Material m = r.material;   // the renderer's own instance: the asset stays as it is
        m.SetFloat(InsetY, s.insetY);
        m.SetFloat(FadeWidthY, s.fadeY);
        m.SetFloat(FadeWidthX, s.fadeX);
        m.SetFloat(EdgeNoise, s.edgeNoise);
        return true;
    }
}
