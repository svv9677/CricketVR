using System;
using System.Collections.Generic;

/// <summary>
/// Every line the coach's goals show (phase 7): the hub's button, each goal's value and bar, the
/// Goals screen's subtitle, empty state and career line, and the result screen's line when a match
/// reached one. Latin-1 only (the menu font), a goal's text at most LabelMax characters and its
/// value at most ValueMax once rich text is taken out, so both fit RatingCell's line.
/// </summary>
public static class CoachGoalsText
{
    public const int LabelMax = 34, ValueMax = 12;
    public const string Good = "#22C55E", Muted = "#98A2B3";
    public const string NoTrial = "Finish your nets trial - the coach sets goals after it.";
    public const string SeasonOver = "Season over - new goals with the next season.";
    public const string NoCareer = "No career loaded.";

    /// The hub's Goals button ("Goals 1/3" once there are goals; "Goals" before).
    public static string HubButton(Season s)
    {
        int n = Count(s);
        return n == 0 ? "Goals" : $"Goals {CoachGoals.Done(s)}/{n}";
    }

    /// A goal's progress as text: "46 / 60", "SR 104 / 110", "43 / 50", or a green "Done".
    public static string Value(SeasonGoal g)
    {
        if (g == null)
            return "";
        if (g.done)
            return $"<color={Good}>Done</color>";
        string target = Whole(g.target);
        return g.kind switch
        {
            eGoalKind.StrikeRate => $"SR {Whole(g.progress)} / {target}",
            eGoalKind.Rating => $"{(RatingSet.Known(g.progress) ? Whole(g.progress) : "--")} / {target}",
            _ => $"{Whole(g.progress)} / {target}",
        };
    }

    /// A goal's progress for its bar, 0..1 (1 once done). A rating goal fills from where the rating
    /// stood when it was set, so its bar does not start most of the way along.
    public static float Fraction(SeasonGoal g)
    {
        if (g == null)
            return 0f;
        if (g.done)
            return 1f;
        float from = g.kind == eGoalKind.Rating ? g.baseline : 0f;
        if (g.target <= from || (g.kind == eGoalKind.Rating && !RatingSet.Known(g.progress)))
            return 0f;
        float t = (g.progress - from) / (g.target - from);
        // Not done is never a full bar (a strike rate met over too few balls, a rating 49.4).
        return t < 0f ? 0f : t > 0.95f ? 0.95f : t;
    }

    /// The goals `s` has newly done against `doneBefore` ("" when none, or `doneBefore` is null):
    /// "Coach's goal reached: Score 60 runs this season", or "Coach's goals reached: 2 of 3".
    public static string Reached(Season s, List<string> doneBefore)
    {
        if (doneBefore == null || s?.goals == null)
            return "";
        var newly = new List<SeasonGoal>();
        foreach (SeasonGoal g in s.goals)
            if (g != null && g.done && !doneBefore.Contains(g.id ?? ""))
                newly.Add(g);
        if (newly.Count == 0)
            return "";
        return newly.Count == 1
            ? $"Coach's goal reached: {newly[0].text}"
            : $"Coach's goals reached: {CoachGoals.Done(s)} of {Count(s)}";
    }

    /// The Goals screen's subtitle: "Season 2 · State cricket".
    public static string Subtitle(CareerProfile p)
    {
        Season s = p?.season;
        if (p == null)
            return NoCareer;
        return s == null ? p.name ?? "" : $"Season {s.number}  ·  {Ladder.Name(s.rung)}";
    }

    /// What the Goals screen says in place of the goals ("" when there are goals to show).
    public static string Empty(CareerProfile p)
    {
        if (p == null)
            return NoCareer;
        if (!p.trialDone)
            return NoTrial;
        if (Count(p.season) > 0)
            return "";
        return SeasonOver;
    }

    /// "Goals reached in your career: 4. They unlock Locker kit." (one 20 px line, 696 px wide).
    public static string CareerLine(CareerProfile p) =>
        p == null ? "" : $"Goals reached in your career: {Math.Max(0, p.goalsDone)}. They unlock Locker kit.";

    /// `s`'s goals (no gaps).
    public static int Count(Season s) => s?.goals == null ? 0 : s.goals.FindAll(g => g != null).Count;

    private static string Whole(float v) => ((int)Math.Round(v, MidpointRounding.AwayFromZero)).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
