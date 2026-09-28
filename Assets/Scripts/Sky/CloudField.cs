using UnityEngine;

/// <summary>
/// Where one cloud sits on the sky dome and how it is drawn. A plain value type with no
/// GameObject in it, so a whole cloudscape can be generated and checked in an EditMode test.
/// </summary>
public struct CloudPlacement
{
    /// Compass angle around the dome. This is the only part that changes as the cloud drifts.
    public float AzimuthDeg;
    /// Angle above the horizon. Fixed for the life of the cloud.
    public float ElevationDeg;
    /// Distance from the world origin. Well inside the 1000m far clip of the VR cameras.
    public float Radius;
    /// Width of the billboard in world units. Height follows from the plate's aspect.
    public float WidthMetres;
    /// Degrees of azimuth per second. Varies per cloud so the sky is not one rigid shell.
    public float DriftDegPerSecond;
    /// Which cloud plate to draw.
    public int PlateIndex;
    /// Flip the plate horizontally. The plates are lit from above rather than from one side,
    /// so mirroring doubles the apparent variety without putting the light on the wrong edge.
    public bool MirrorX;

    public Vector3 Position => CloudField.PointOnDome(AzimuthDeg, ElevationDeg, Radius);
}

/// <summary>Tunables for a generated cloudscape.</summary>
public struct CloudFieldSettings
{
    public Vector2Int CountRange;
    public Vector2 ElevationRange;
    public Vector2 WidthRange;
    public float Radius;
    public float DriftDegPerSecond;
    /// Fraction by which each cloud's drift may differ from the base speed, e.g. 0.2 for +/-20%.
    public float DriftVariance;
    /// How much smaller a cloud at the bottom of the elevation range is drawn than one at the
    /// top. Clouds near the horizon are further away in reality, so a smaller angular size is
    /// what sells the distance.
    public float HorizonShrink;
    public bool AllowMirroring;

    public static CloudFieldSettings Default => new CloudFieldSettings
    {
        CountRange = new Vector2Int(50, 75),
        // A fair-weather cumulus band. Nothing at the zenith: clouds directly overhead read as
        // a ceiling, and a batter looking up for a catch should see sky.
        ElevationRange = new Vector2(12f, 50f),
        WidthRange = new Vector2(90f, 230f),
        Radius = 420f,
        DriftDegPerSecond = 0.35f,
        DriftVariance = 0.2f,
        HorizonShrink = 0.45f,
        AllowMirroring = true,
    };
}

/// <summary>
/// Generates the layout of a cloudscape. Pure maths over a supplied random source, so the same
/// seed always produces the same sky and the rules below can be proved in a test.
/// </summary>
public static class CloudField
{
    public static Vector3 PointOnDome(float azimuthDeg, float elevationDeg, float radius)
    {
        float az = azimuthDeg * Mathf.Deg2Rad;
        float el = elevationDeg * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(el) * radius;
        return new Vector3(Mathf.Cos(az) * horizontal, Mathf.Sin(el) * radius, Mathf.Sin(az) * horizontal);
    }

    public static CloudPlacement[] Generate(CloudFieldSettings s, int plateCount, System.Random rng)
    {
        if (plateCount <= 0) return new CloudPlacement[0];

        int count = rng.Next(s.CountRange.x, s.CountRange.y + 1);
        var clouds = new CloudPlacement[count];

        // Azimuth is stratified rather than uniform: one cloud per equal slice of the compass,
        // jittered inside its slice. Pure uniform sampling clumps visibly, and a clump reads as
        // a mistake rather than as weather.
        float slice = 360f / count;

        for (int i = 0; i < count; i++)
        {
            // Bias elevation toward the horizon. Perspective stacks distant clouds down there,
            // so an even spread up the dome looks wrong.
            float t = Mathf.Pow((float)rng.NextDouble(), 1.6f);
            float elevation = Mathf.Lerp(s.ElevationRange.x, s.ElevationRange.y, t);

            float shrink = Mathf.Lerp(1f - s.HorizonShrink, 1f, t);
            float width = Mathf.Lerp(s.WidthRange.x, s.WidthRange.y, (float)rng.NextDouble()) * shrink;

            float drift = s.DriftDegPerSecond *
                          (1f + Mathf.Lerp(-s.DriftVariance, s.DriftVariance, (float)rng.NextDouble()));

            clouds[i] = new CloudPlacement
            {
                AzimuthDeg = i * slice + (float)rng.NextDouble() * slice,
                ElevationDeg = elevation,
                Radius = s.Radius,
                WidthMetres = width,
                DriftDegPerSecond = drift,
                PlateIndex = rng.Next(0, plateCount),
                MirrorX = s.AllowMirroring && rng.Next(2) == 0,
            };
        }
        return clouds;
    }
}
