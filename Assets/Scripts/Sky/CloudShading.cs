using UnityEngine;

/// <summary>
/// The colour of a cloud: how the sun brightens the side facing it, and how the horizon washes
/// out the ones low in the sky. Pure maths, no Unity objects, so the rules are testable.
///
/// This is where the "artificial" look is won or lost. A sky whose every cloud is the same flat
/// white reads as a toy, because a real sky never lights two clouds the same way.
/// </summary>
public static class CloudShading
{
    /// <summary>
    /// 0 when the cloud sits directly away from the sun, 1 when it sits in front of it.
    /// <paramref name="sunTravelDirection"/> is the direction the light travels, i.e. the
    /// directional light's forward vector.
    /// </summary>
    public static float SunFacing(Vector3 cloudDirection, Vector3 sunTravelDirection)
    {
        Vector3 toCloud = cloudDirection.normalized;
        Vector3 towardSun = -sunTravelDirection.normalized;
        return Mathf.InverseLerp(-1f, 1f, Vector3.Dot(toCloud, towardSun));
    }

    /// <summary>
    /// Tints a cloud by how much of the sun it is facing: lit ones gain <paramref name="sunBoost"/>,
    /// ones turned away lose <paramref name="shadeDrop"/>. Alpha is passed through untouched.
    /// </summary>
    public static Color BodyTint(Color baseColour, float sunFacing, float sunBoost, float shadeDrop)
    {
        float m = Mathf.Lerp(1f - shadeDrop, 1f + sunBoost, Mathf.Clamp01(sunFacing));
        return new Color(baseColour.r * m, baseColour.g * m, baseColour.b * m, baseColour.a);
    }

    /// <summary>
    /// How far a cloud has faded into the horizon haze: 1 at or below <paramref name="horizonDeg"/>,
    /// falling to 0 at <paramref name="clearDeg"/> and above.
    /// </summary>
    public static float HazeAmount(float elevationDeg, float horizonDeg, float clearDeg)
    {
        return 1f - Mathf.Clamp01(Mathf.InverseLerp(horizonDeg, clearDeg, elevationDeg));
    }
}
