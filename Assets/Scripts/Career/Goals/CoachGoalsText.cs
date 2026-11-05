using System.Collections.Generic;

/// <summary>
/// Every line the coach's goals show (phase 7): the hub's button, each goal's value and bar, and the
/// result screen's line when a match reached one.
/// </summary>
public static class CoachGoalsText
{
    /// The hub's Goals button ("Goals 1/3" once there are goals).
    public static string HubButton(Season s) => "Goals";

    /// A goal's progress as text ("212 / 300").
    public static string Value(SeasonGoal g) => "";

    /// A goal's progress for its bar, 0..1.
    public static float Fraction(SeasonGoal g) => 0f;

    /// The goals `s` has newly done against `doneBefore` ("" when none, or `doneBefore` is null).
    public static string Reached(Season s, List<string> doneBefore) => "";
}
