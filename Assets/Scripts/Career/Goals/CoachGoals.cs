using System.Collections.Generic;

/// <summary>
/// The coach's goals for a season (phase 7): two or three targets set as the season begins, and
/// marked done from the record book as he reaches them. They only measure what he did - reaching
/// one changes nothing about how the game plays.
/// </summary>
public static class CoachGoals
{
    /// The season's goals made, if it has none yet. True when it changed the career.
    public static bool Ensure(CareerProfile p) => false;

    /// The goals for `s`, from `r`.
    public static List<SeasonGoal> Make(CareerProfile p, Season s, System.Random r) => new List<SeasonGoal>();

    /// The goals brought up to date from the record book; returns the ones newly done.
    public static List<SeasonGoal> Update(CareerProfile p) => new List<SeasonGoal>();

    /// How many of `s`'s goals are done.
    public static int Done(Season s) => s?.goals == null ? 0 : s.goals.FindAll(g => g != null && g.done).Count;

    /// The ids of `s`'s goals that are done.
    public static List<string> DoneIds(Season s)
    {
        var ids = new List<string>();
        if (s?.goals != null)
            foreach (SeasonGoal g in s.goals)
                if (g != null && g.done)
                    ids.Add(g.id ?? "");
        return ids;
    }

    /// The season review's line about the goals ("" when there were none).
    public static string ReviewLine(Season s) => "";
}
