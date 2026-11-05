using System.Collections.Generic;
using System.Text;

/// <summary>
/// Every line the match situations show (phase 7): the hub's next-match line, the card before the
/// first ball, and the tag on the result. "" when the match has no situation. All of it reads the
/// plan the match is played with (the hub's preview is that same plan: CareerSituations.PlanRng), so
/// the numbers here are the match's own. Latin-1 only ("·", never an em dash).
/// </summary>
public static class CareerSituationText
{
    /// The situation's badge: SlotLine's FINAL pink, so a situation reads like the final's badge.
    /// A copy of CareerHubScreen.Pink (this file stays free of the UI): change both together.
    private const string Badge = "#F43F8E";

    /// The card's body is wrapped to this many characters a line (CareerMatchCard's 640 px at 30 pt).
    public const int BodyWidth = 40;

    /// The hub's next-match line for a situation ("" for none: the hub shows SlotLine, the final's
    /// FINAL badge included). "RAIN  ·  124 to win off 14  ·  You bat at 2",
    /// "CHASE  ·  165 to win  ·  You bat at 2", "COLLAPSE  ·  You come in at 28/3".
    public static string HubLine(MatchPlan plan, int slot)
    {
        if (plan == null)
            return "";
        string at = $"You bat at {(slot < 1 ? 1 : slot > 11 ? 11 : slot)}";
        switch (plan.situation)
        {
            case eSituation.RainShortened:
                if (!Rained(plan))
                    return "";
                return Chasing(plan) ? Line("RAIN", $"{plan.Target} to win off {plan.overs}", at)
                                     : Line("RAIN", $"Overs cut from {Full(plan)}", at);
            case eSituation.Chase:
                return Chasing(plan) ? Line("CHASE", $"{plan.Target} to win", at) : "";
            case eSituation.Rescue:
                return Collapsed(plan) ? Line("COLLAPSE", $"You come in at {plan.startRuns}/{plan.startWickets}", null) : "";
            default:
                return "";
        }
    }

    /// The card's heading before the first ball ("" for none). A collapse has none: its card is the
    /// come-in card after the scripted wickets ("You come in at 28/3 (1.5 overs)").
    public static string IntroHeading(MatchPlan plan)
    {
        if (plan == null)
            return "";
        switch (plan.situation)
        {
            case eSituation.RainShortened:
                if (!Rained(plan))
                    return "";
                return Chasing(plan) ? $"Rain: {plan.Target} to win off {plan.overs}" : $"Rain: {plan.overs} overs a side";
            case eSituation.Chase:
                return Chasing(plan) ? $"Chase: {plan.Target} to win" : "";
            case eSituation.Final:
                return "The final";
            default:
                return "";
        }
    }

    /// The card's body before the first ball, wrapped to BodyWidth ("" when there is no heading).
    public static string IntroBody(MatchPlan plan)
    {
        if (IntroHeading(plan).Length == 0)
            return "";
        var paragraphs = new List<string>();
        switch (plan.situation)
        {
            case eSituation.RainShortened when Chasing(plan):
                paragraphs.Add(TheirInnings(plan));
                paragraphs.Add("The target is revised for overs lost.");
                break;
            case eSituation.RainShortened:
                paragraphs.Add($"Rain before the start cut the match from {Full(plan)} overs to {plan.overs} a side.");
                break;
            case eSituation.Chase:
                paragraphs.Add(TheirInnings(plan));
                break;
            case eSituation.Final:
                paragraphs.Add($"{Name(plan.yourTeamName, "Your side")} against {Name(plan.opponentName, "them")}.");
                paragraphs.Add("The winner takes the title.");
                if (Chasing(plan))
                    paragraphs.Add($"{plan.Target} to win.");
                break;
        }
        var lines = new List<string>();
        foreach (string para in paragraphs)
            lines.AddRange(Wrap(para, BodyWidth));
        return string.Join("\n", lines);
    }

    /// The short tag after the result's headline ("" for none): "rain, target 124",
    /// "rain, 14 overs a side", "chase of 165", "came in at 28/3", "the final".
    public static string ResultTag(MatchPlan plan)
    {
        if (plan == null)
            return "";
        switch (plan.situation)
        {
            case eSituation.RainShortened:
                if (!Rained(plan))
                    return "";
                return Chasing(plan) ? $"rain, target {plan.Target}" : $"rain, {plan.overs} overs a side";
            case eSituation.Chase:
                return Chasing(plan) ? $"chase of {plan.Target}" : "";
            case eSituation.Rescue:
                return Collapsed(plan) ? $"came in at {plan.startRuns}/{plan.startWickets}" : "";
            case eSituation.Final:
                return "the final";
            default:
                return "";
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------

    /// His side chases a total already made (the plan holds their innings and a target).
    private static bool Chasing(MatchPlan plan) => !plan.batFirst && plan.theirs != null && plan.Target > 0;

    /// The format's own overs at the plan's rung (rain cuts the plan's below it).
    private static int Full(MatchPlan plan) => Ladder.Overs(plan.rung);

    /// Rain took overs off this match.
    private static bool Rained(MatchPlan plan) => plan.overs > 0 && plan.overs < Full(plan);

    /// A collapse was scripted: he comes in with wickets down.
    private static bool Collapsed(MatchPlan plan) => plan.startWickets > 0;

    /// "Old Mill CC made 164/6 in 20 overs." / "... made 120 all out in 17.3 overs."
    private static string TheirInnings(MatchPlan plan)
    {
        InningsCard c = plan.theirs;
        string score = c.wickets >= 10 ? $"{c.runs} all out" : c.Score;
        int full = c.overs > 0 ? c.overs : Full(plan);
        string overs = c.balls > 0 && c.balls < full * 6 ? c.OversText : full.ToString();
        return $"{Name(plan.opponentName, Name(c.team, "They"))} made {score} in {overs} overs.";
    }

    private static string Name(string name, string fallback) => string.IsNullOrEmpty(name) ? fallback : name;

    /// "RAIN  ·  124 to win off 14  ·  You bat at 2", the badge pink and bold, as SlotLine's FINAL.
    private static string Line(string badge, string middle, string tail)
    {
        string line = $"<color={Badge}><b>{badge}</b></color>  ·  {middle}";
        return string.IsNullOrEmpty(tail) ? line : line + "  ·  " + tail;
    }

    /// `text` broken at spaces into lines of at most `width` characters (a longer word on its own).
    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (string word in (text ?? "").Split(' '))
        {
            if (word.Length == 0)
                continue;
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }
            if (line.Length > 0)
                line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0)
            lines.Add(line.ToString());
        return lines;
    }
}
