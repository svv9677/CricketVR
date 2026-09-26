using UnityEngine;

/// <summary>
/// The words and numbers the panels show, in one place so every panel says the same thing the
/// same way. Text sticks to Latin-1 characters (the default TMP font's static atlas).
/// </summary>
public static class UIFormat
{
    public const float KmhPerMps = 3.6f;

    /// The bowler types the settings panel offers, in the order of its segmented control.
    public static readonly eSwingType[] BowlerTypes =
        { eSwingType.Pace, eSwingType.InSwing, eSwingType.OutSwing, eSwingType.OffSpin, eSwingType.LegSpin };

    public static int BowlerTypeIndex(eSwingType type) => System.Array.IndexOf(BowlerTypes, type);

    public static string BowlerType(eSwingType type)
    {
        switch (type)
        {
            case eSwingType.Pace: return "Pace";
            case eSwingType.InSwing: return "Inswing";
            case eSwingType.OutSwing: return "Outswing";
            case eSwingType.OffSpin: return "Off-spin";
            case eSwingType.LegSpin: return "Leg-spin";
            case eSwingType.Random: return "Mixed";
            default: return "-";
        }
    }

    public static bool IsSpin(eSwingType type) => type == eSwingType.OffSpin || type == eSwingType.LegSpin;

    /// "138" plus a smaller unit, from metres per second.
    public static string Kmh(float metresPerSecond) => WithUnit(Mathf.RoundToInt(metresPerSecond * KmhPerMps).ToString(), "km/h");

    public static string WithUnit(string value, string unit) => $"{value}<size=45%> {unit}</size>";

    /// "140 to 155 km/h", or just "140 km/h" when both ends agree. % and degrees sit tight.
    public static string Range(float low, float high, string format, string unit)
    {
        string a = low.ToString(format), b = high.ToString(format);
        string u = unit == "%" || unit == Degrees ? unit : " " + unit;
        return a == b ? a + u : $"{a} to {b}{u}";
    }

    public const string Degrees = "°";

    /// A line at the stumps, metres from middle stump with the off side positive: "8 cm leg".
    public static string Line(float metres)
    {
        int cm = Mathf.RoundToInt(metres * 100f);
        return cm == 0 ? "middle" : cm > 0 ? $"{cm} cm off" : $"{-cm} cm leg";
    }

    public static string LineRange(float low, float high)
    {
        string a = Line(low), b = Line(high);
        return a == b ? a : $"{a} to {b}";
    }

    /// Used when the bat reports no word of its own.
    public static string Contact(float quality, bool edge)
    {
        if (edge) return "Edge";
        if (quality >= 0.7f) return "Middled";
        if (quality >= 0.35f) return "Good contact";
        return "Toe";
    }

    /// Bat power, where 75 is a real bat.
    public static string BatPower(float amplifier)
    {
        if (Mathf.Abs(amplifier - Constants.BatPowerRealistic) < 0.5f) return "Realistic";
        return $"{Mathf.RoundToInt(amplifier / Constants.BatPowerRealistic * 100f)}%";
    }
}
