/// <summary>
/// A drill's say over the next delivery's length and line (BowlingAttack.ForceShape): which length
/// band it pitches in and a window for its line at the stumps. The pace, the bowler and the
/// movement are still the game's own. CONTRACT STUB - the body is Unit 1's.
/// </summary>
public struct DeliveryShape
{
    /// Never a wide by line: the window is clamped to this (metres, batter's frame, + off side).
    public const float MinLine = -0.10f, MaxLine = 0.85f;

    public eLengthBand band;
    public float minLine, maxLine;

    public DeliveryShape(eLengthBand b, float min, float max) { band = b; minLine = min; maxLine = max; }

    /// A pick in DeliveryPicker.BandsFor(family)'s range for the band (pitch clamped as DeliveryPicker
    /// does), the line uniform in the window clamped to [MinLine, MaxLine]. Pure.
    public static DeliveryPicker.Pick Pick(DeliveryShape s, eSwingType family, System.Func<float> rand01) =>
        new DeliveryPicker.Pick { band = s.band };
}
