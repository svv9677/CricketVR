using UnityEngine;

/// <summary>
/// A drill's say over the next delivery's length and line (BowlingAttack.ForceShape): which length
/// band it pitches in and a window for its line at the stumps. The pace, the bowler and the
/// movement are still the game's own (BowlingAttack.Shape puts the pick over the bowler's ball).
/// </summary>
public struct DeliveryShape
{
    /// Never a wide by line: the window is clamped to this (metres, batter's frame, + off side).
    /// The wide lines are 0.889 m off and 0.114 m leg, each plus a ball's width (DeliveryPicker).
    public const float MinLine = -0.10f, MaxLine = 0.85f;

    public eLengthBand band;
    public float minLine, maxLine;

    public DeliveryShape(eLengthBand b, float min, float max) { band = b; minLine = min; maxLine = max; }

    /// A pick in DeliveryPicker.BandsFor(family)'s range for the band (pitch clamped as DeliveryPicker
    /// does, so BallDelivery never has to move it), the line uniform in the window clamped to
    /// [MinLine, MaxLine] (either order). Pure: the length draw first, then the line, as
    /// DeliveryPicker.PickDelivery draws them.
    public static DeliveryPicker.Pick Pick(DeliveryShape s, eSwingType family, System.Func<float> rand01)
    {
        DeliveryPicker.Band b = BandOf(DeliveryPicker.BandsFor(family), s.band);
        float distance = Mathf.Lerp(b.minDistance, b.maxDistance, rand01());
        float pitchX = Mathf.Clamp(BallDelivery.BatsmanStumpsX - distance, BallDelivery.MinPitchX,
                                   BallDelivery.BatsmanStumpsX - BallDelivery.PoppingCreaseDistance);
        float lo = Mathf.Clamp(Mathf.Min(s.minLine, s.maxLine), MinLine, MaxLine);
        float hi = Mathf.Clamp(Mathf.Max(s.minLine, s.maxLine), MinLine, MaxLine);
        return new DeliveryPicker.Pick
        {
            band = b.band,
            distance = BallDelivery.BatsmanStumpsX - pitchX,
            pitchX = pitchX,
            line = Mathf.Lerp(lo, hi, rand01()),
        };
    }

    /// The band's distances in this family's table (every table has all five bands).
    static DeliveryPicker.Band BandOf(DeliveryPicker.Band[] bands, eLengthBand band)
    {
        foreach (DeliveryPicker.Band b in bands)
            if (b.band == band)
                return b;
        return bands[0];
    }
}
