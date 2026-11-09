using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Every line of drill text: the progress line in the nets, the result card, the coach's verdict,
/// the drills screen's rows and header. All Latin-1 (the TMP default font has nothing else): the
/// lines here are plain ASCII but for the middle dot (U+00B7), and everything goes out through
/// NetsTrialCard.Latin1. Pure (DrillTests).
///
/// The verdict is the coach talking about this session, not the career: how it went (the grade),
/// the best thing in it (the commonest good tag, with its count), and the one thing to work on (the
/// drill's own line for its commonest failing) - always something to build on, never just a mark.
/// </summary>
public static class DrillText
{
    private const string Dot = " · ";
    private const string Grey = "#98A2B3", Gold = "#F2C94C";
    /// Tags that are a ball handled well, and ones that are a ball that got the better of him.
    private static readonly eDrillTag[] GoodTags =
    {
        eDrillTag.DugOut, eDrillTag.GoodLeave, eDrillTag.Pulled, eDrillTag.Ducked, eDrillTag.Evaded,
        eDrillTag.UsedFeet, eDrillTag.Swept, eDrillTag.PlayedTurn, eDrillTag.Gap, eDrillTag.OverTop,
        eDrillTag.Six, eDrillTag.Survived, eDrillTag.InTheV, eDrillTag.Blocked, eDrillTag.Boundary, eDrillTag.Scored,
    };
    private static readonly eDrillTag[] BadTags =
    {
        eDrillTag.Bowled, eDrillTag.LeftStraight, eDrillTag.HitHead, eDrillTag.Caught, eDrillTag.Fished,
        eDrillTag.Edged, eDrillTag.Fielder, eDrillTag.Beaten, eDrillTag.Wasted, eDrillTag.Mistimed,
    };

    /// "Yorker", "Leaving", ... for "Yorker drill complete".
    private static string Noun(eDrill d) => d switch
    {
        eDrill.Yorkers => "Yorker",
        eDrill.LeaveOutsideOff => "Leaving",
        eDrill.ShortBall => "Short-ball",
        eDrill.Spin => "Spin",
        eDrill.Gaps => "Gaps",
        eDrill.StraightHitting => "Straight-hitting",
        eDrill.Swing => "Swing",
        eDrill.PowerHitting => "Power-hitting",
        eDrill.DeathOvers => "Death-overs",
        eDrill.SoftHands => "Soft-hands",
        _ => "Training",
    };

    public static string Heading(DrillResult d) => Noun(d != null ? d.drill : eDrill.None) + " drill complete";

    /// The inbox title for a drill the coach reports on: "Spin drill: 72, Good".
    internal static string MessageTitle(DrillResult d) =>
        NetsTrialCard.Latin1($"{Noun(d.drill)} drill: {d.score}, {d.grade}");

    /// The nets' left-hand text before a drill ball - kept to what the player chases (2026-11-06:
    /// "too much text on the left side"): the drill and ball, the bowler, the goal with how many
    /// count so far and what is still needed, the rule for a ball to count, and the last ball -
    /// whether it counted and why, with how hard it was hit. `ballNumber` is 1-based (the ball
    /// about to be bowled), `hits` the balls that have counted, `last` the last ball (null before
    /// the first) and `lastTag` what DrillScoring made of it.
    public static string ProgressText(DrillDef d, int ballNumber, eBowlerKind next, int hits, BallRecord last, eDrillTag lastTag)
    {
        int n = Mathf.Clamp(ballNumber, 1, DrillDefs.BallsPerDrill);
        DrillGoals.Goal goal = DrillGoals.For(d != null ? d.id : eDrill.None);
        var sb = new StringBuilder();
        sb.Append("<b>").Append(d != null ? d.title : "DRILL").Append("</b>  Ball ").Append(n).Append(" of ").Append(DrillDefs.BallsPerDrill);
        sb.Append("\n<size=70%><color=").Append(Grey).Append('>').Append(NetsTrialCard.KindLabel(next)).Append("</color></size>");
        if (goal.text.Length > 0)
        {
            sb.Append("\n<size=70%><b>").Append(goal.text).Append("</b>").Append(Dot).Append(GoalProgress(goal, hits, n - 1)).Append("</size>");
            sb.Append("\n<size=60%><color=").Append(Grey).Append('>').Append(goal.rule).Append("</color></size>");
        }
        if (lastTag != eDrillTag.None)
            sb.Append("\n<size=70%>Last: ").Append(LastLine(d, last, lastTag)).Append("</size>");
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// "3 of 8, 5 more from 9 balls"; "Goal met (9)"; "out of reach: 3 of 8".
    public static string GoalProgress(DrillGoals.Goal goal, int hits, int faced)
    {
        int left = Mathf.Max(0, DrillDefs.BallsPerDrill - faced);
        if (hits >= goal.target)
            return "<color=" + GoodColour + ">Goal met (" + hits + ")</color>";
        int need = goal.target - hits;
        if (need > left)
            return "<color=" + BadColour + ">out of reach: " + hits + " of " + goal.target + "</color>";
        return "<b>" + hits + "</b> of " + goal.target + ", " + need + " more from " + left + " ball" + (left == 1 ? "" : "s");
    }

    /// "+1 Dug out · 104 km/h" in blue, or "Edged · doesn't count" in orange.
    private static string LastLine(DrillDef d, BallRecord last, eDrillTag tag)
    {
        eDrill id = d != null ? d.id : eDrill.None;
        bool counted = DrillGoals.Counts(id, last, tag);
        string why = DrillGoals.Why(id, last, tag, counted);
        string speed = last != null && last.action == eBallAction.Played && last.exitSpeed > 0f
            ? Dot + Mathf.RoundToInt(last.exitSpeed * 3.6f) + " km/h" : "";
        return counted
            ? "<color=" + GoodColour + "><b>+1</b> " + why + "</color>" + speed
            : "<color=" + BadColour + ">" + why + "</color>" + speed + Dot + "doesn't count";
    }

    private const string GoodColour = "#8AB8FF", BadColour = "#F2A65A";

    /// The result card's first line: the goal met or missed, how many counted, what it needed.
    public static string GoalLine(DrillResult r)
    {
        DrillGoals.Goal goal = DrillGoals.For(r.drill);
        if (goal.text.Length == 0)
            return "";
        bool met = r.hits >= goal.target;
        return "<b><color=" + (met ? GoodColour : BadColour) + ">" + (met ? "Goal met" : "Goal missed") + "</color></b>  " +
               r.hits + " of " + DrillDefs.BallsPerDrill + " counted (needed " + goal.target + ")\n" +
               "<size=70%><color=" + Grey + ">" + goal.text + ": " + goal.rule + "</color></size>";
    }

    /// The result card: the score and grade, how the balls went, the best, the ratings the drill
    /// feeds (before -> after), the coach's verdict, and how to go on.
    public static string ResultBody(DrillDef d, DrillResult r)
    {
        var sb = new StringBuilder();
        if (r == null)
            return "<size=70%><color=" + Grey + ">Press A to continue</color></size>";
        string goalLine = GoalLine(r);
        if (goalLine.Length > 0)
            sb.Append(goalLine).Append('\n');
        sb.Append("<size=80%>Score  <b>").Append(r.score).Append("</b> / 100  ").Append(r.grade).Append("</size>\n");
        string tags = TopTags(r, 3);
        if (tags.Length > 0)
            sb.Append("<size=75%>").Append(tags).Append("</size>\n");
        sb.Append("<size=75%>");
        if (r.newBest)
            sb.Append("<color=").Append(Gold).Append(">New best!</color>");
        else
            sb.Append("Best ").Append(r.best);
        sb.Append("</size>\n");
        if (d != null)
            foreach (int i in d.feeds)
                sb.Append(DeltaLine(i, r.before, r.after)).Append('\n');
        sb.Append("<size=80%>").Append(Verdict(d, r)).Append("</size>\n");
        sb.Append("<size=70%><color=").Append(Grey).Append(">Press A to continue</color></size>");
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// "Defence<pos>48 -> 51<pos>(+3)"; "--" for a rating not measured yet.
    private static string DeltaLine(int index, RatingSet before, RatingSet after)
    {
        string name = RatingSet.Names[Mathf.Clamp(index, 0, RatingSet.Count - 1)];
        float a = before != null ? before[index] : RatingSet.Unknown;
        float b = after != null ? after[index] : RatingSet.Unknown;
        string from = RatingSet.Known(a) ? Mathf.RoundToInt(a).ToString() : "--";
        string to = RatingSet.Known(b) ? Mathf.RoundToInt(b).ToString() : "--";
        string change = "";
        if (RatingSet.Known(a) && RatingSet.Known(b))
        {
            int delta = Mathf.RoundToInt(b) - Mathf.RoundToInt(a);
            change = delta > 0 ? "(+" + delta + ")" : delta < 0 ? "(" + delta + ")" : "(=)";
        }
        else if (RatingSet.Known(b))
            change = "(new)";
        return $"{name}<pos=45%>{from} -> {to}<pos=75%>{change}";
    }

    /// "Dug out 7 · Beaten 2 · Out 3": the commonest tags, most first.
    private static string TopTags(DrillResult r, int most)
    {
        var order = new List<int>();
        for (int t = 1; t < r.tags.Length; t++)
            if (r.tags[t] > 0) order.Add(t);
        order.Sort((x, y) => r.tags[y] != r.tags[x] ? r.tags[y].CompareTo(r.tags[x]) : x.CompareTo(y));
        var sb = new StringBuilder();
        for (int i = 0; i < order.Count && i < most; i++)
        {
            if (i > 0) sb.Append(Dot);
            sb.Append(TagLabel((eDrillTag)order[i])).Append(' ').Append(r.tags[order[i]]);
        }
        return sb.ToString();
    }

    /// The coach on this session: how it went, the best of it, and the one thing to work on.
    public static string Verdict(DrillDef d, DrillResult r)
    {
        if (r == null)
            return "";
        eDrill id = d != null ? d.id : r.drill;
        string opener = r.score >= 80 ? "Superb session." : r.score >= 60 ? "Good session."
                      : r.score >= 40 ? "Getting there." : "A tough one, but this is what the nets are for.";
        var sb = new StringBuilder(opener);
        eDrillTag good = Commonest(r, GoodTags);
        if (good != eDrillTag.None && r.tags[(int)good] >= 2)
            sb.Append(' ').Append(Praise(good, r.tags[(int)good]));
        eDrillTag bad = Commonest(r, BadTags);
        if (bad == eDrillTag.None)
            sb.Append(" Nothing got past you. Keep that going.");
        else if (r.score >= 80 && r.tags[(int)bad] < 2)
            sb.Append(" Keep that going.");
        else
            sb.Append(' ').Append(Work(id, bad));
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// The one thing to work on: the drill's own line for what got the better of him most.
    private static string Work(eDrill d, eDrillTag bad)
    {
        switch (d)
        {
            case eDrill.Yorkers:
                return bad == eDrillTag.Bowled ? "Get the bat down earlier: the yorker beats a high backlift."
                     : bad == eDrillTag.Edged ? "Dig it out with a straight bat, the face pointing back down the pitch."
                     : bad == eDrillTag.LeftStraight ? "A yorker on the stumps has to be played. Never leave the straight one."
                     : "Pick the length from the hand and get the bat down to it.";
            case eDrill.LeaveOutsideOff:
                return bad == eDrillTag.LeftStraight ? "Know where your off stump is: the ones on the stumps must be played."
                     : bad == eDrillTag.Fished || bad == eDrillTag.Edged ? "You are fishing outside off. Let the wide ones go and make the bowler come to you."
                     : bad == eDrillTag.Bowled ? "Watch the straight ones right onto the bat."
                     : "Playing and missing out there is a warning. Leave it alone.";
            case eDrill.ShortBall:
                return bad == eDrillTag.HitHead ? "Sway out of line or take it on. Do not get stuck in between."
                     : bad == eDrillTag.Edged || bad == eDrillTag.Bowled ? "Roll the wrists and keep the pull down."
                     : "Get inside the line early and you will have time for it.";
            case eDrill.Spin:
                return bad == eDrillTag.Bowled ? "Play with the turn, not across it. If you come down the pitch, get right to it."
                     : bad == eDrillTag.Edged ? "Soft hands against spin: let it come to you."
                     : bad == eDrillTag.LeftStraight ? "A spinner will hit the stumps if you let him. Play the straight one."
                     : "Read it from the hand and play with the turn.";
            case eDrill.Gaps:
                return bad == eDrillTag.Fielder ? "Pick the gap before the ball, then hit it there."
                     : bad == eDrillTag.Caught || bad == eDrillTag.Bowled ? "Keep it on the ground when there are men around the bat."
                     : bad == eDrillTag.Wasted ? "Every ball is a scoring chance here. Look to put each one away."
                     : "Watch it onto the bat first. The gap will still be there.";
            default:
                return DrillTextMore.Work(d, bad);
        }
    }

    /// The tag of `among` with the most balls (the first in `among` on a tie), None if none.
    private static eDrillTag Commonest(DrillResult r, eDrillTag[] among)
    {
        eDrillTag best = eDrillTag.None;
        foreach (eDrillTag t in among)
            if (r.tags[(int)t] > 0 && (best == eDrillTag.None || r.tags[(int)t] > r.tags[(int)best]))
                best = t;
        return best;
    }

    private static string Praise(eDrillTag t, int n) => t switch
    {
        eDrillTag.DugOut => $"You dug out {n} of them.",
        eDrillTag.GoodLeave => $"{n} good leaves: you know where your off stump is.",
        eDrillTag.Pulled => $"You took on {n} of the short ones.",
        eDrillTag.Ducked => $"You got under {n} bouncers cleanly.",
        eDrillTag.Evaded => $"You got out of the way of {n} bouncers.",
        eDrillTag.UsedFeet => $"You used your feet well, {n} times.",
        eDrillTag.Swept => $"{n} sweeps that came off.",
        eDrillTag.PlayedTurn => $"You played with the turn {n} times.",
        eDrillTag.Gap => $"You found {n} gaps.",
        eDrillTag.OverTop => $"You cleared the ring safely {n} times.",
        eDrillTag.Six => $"{n} sixes!",
        eDrillTag.InTheV => $"{n} hit straight, in the V.",
        eDrillTag.Blocked => $"{n} killed dead with soft hands.",
        eDrillTag.Boundary => $"{n} boundaries.",
        eDrillTag.Scored => $"You scored off {n} of them.",
        _ => $"{n} played well.",
    };

    public static string TagLabel(eDrillTag t) => t switch
    {
        eDrillTag.Survived => "Played well",
        eDrillTag.DugOut => "Dug out",
        eDrillTag.Bowled => "Out",
        eDrillTag.Edged => "Edged",
        eDrillTag.Beaten => "Beaten",
        eDrillTag.GoodLeave => "Good leave",
        eDrillTag.Fished => "Fished outside off",
        eDrillTag.LeftStraight => "Left a straight one",
        eDrillTag.Pulled => "Pulled or cut",
        eDrillTag.Ducked => "Ducked",
        eDrillTag.Evaded => "Swayed out of line",
        eDrillTag.HitHead => "Would have hit you",
        eDrillTag.UsedFeet => "Used the feet",
        eDrillTag.Swept => "Swept",
        eDrillTag.PlayedTurn => "With the turn",
        eDrillTag.Gap => "Found the gap",
        eDrillTag.Fielder => "Found a fielder",
        eDrillTag.Caught => "Caught",
        eDrillTag.OverTop => "Over the top",
        eDrillTag.Six => "Six",
        eDrillTag.Wasted => "Wasted",
        eDrillTag.InTheV => "In the V",
        eDrillTag.Blocked => "Dead bat",
        eDrillTag.Boundary => "Boundary",
        eDrillTag.Mistimed => "Mistimed",
        eDrillTag.Scored => "Scored",
        _ => "",
    };

    /// "Feeds Defence · vs Pace".
    public static string Feeds(DrillDef d)
    {
        if (d == null || d.feeds == null || d.feeds.Length == 0)
            return "";
        var sb = new StringBuilder("Feeds ");
        for (int i = 0; i < d.feeds.Length; i++)
        {
            if (i > 0) sb.Append(Dot);
            sb.Append(RatingSet.Names[Mathf.Clamp(d.feeds[i], 0, RatingSet.Count - 1)]);
        }
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// The drills screen's line under a drill's name: the coach's pick, what it feeds, last and best.
    public static string RowDetail(DrillDef d, DrillRecord r, bool coachPick)
    {
        var sb = new StringBuilder();
        if (coachPick)
            sb.Append("<color=").Append(Gold).Append(">Coach's pick</color>").Append(Dot);
        sb.Append(Feeds(d)).Append(Dot);
        if (r == null || r.attempts <= 0)
            sb.Append("Not tried yet");
        else
            sb.Append("Last ").Append(r.last).Append(Dot).Append("Best ").Append(r.best);
        return NetsTrialCard.Latin1(sb.ToString());
    }

    /// The drills screen's header: the training slot (if one is open) and the coach's pick.
    public static string Header(CareerProfile p)
    {
        if (p == null)
            return "";
        DrillDef pick = DrillDefs.For(DrillCoach.Recommend(p.ratings));
        string pickLine = "Coach's pick: " + (pick != null ? pick.name : "Yorkers");
        Fixture next = DrillCoach.SlotOpen(p) ? CareerProgress.NextFixture(p) : null;
        if (next == null)
            return NetsTrialCard.Latin1(pickLine);
        string before = next.final ? "the final" : "Round " + next.round;
        return NetsTrialCard.Latin1($"Training before {before}: the coach will report on your next drill." + "\n" + pickLine);
    }
}
