/// <summary>
/// The match around the innings into and out of a MatchSnapshot (QuickPlayAutosave): the target
/// and the AI's first innings in a chase, and a Test's innings so far, who batted first, the balls
/// gone, and whether the card between innings was up. Restored after Start has made its own (a
/// new chase target, a Test's new first innings), which the saved ones replace.
/// </summary>
public partial class MatchFlow
{
    public static void Capture(MatchSnapshot s)
    {
        s.target = Target;
        s.hasAIFirst = AIFirst != null;
        s.aiFirst = AIFirst != null ? MatchSnapshot.AILine.From(AIFirst) : new MatchSnapshot.AILine();
        s.test = Test;
        s.innings.Clear();
        if (instance == null)
            return;
        s.youFirst = instance.youFirst;
        s.matchBalls = instance.matchBalls;
        s.betweenInnings = instance.betweenInnings;
        foreach (Innings i in instance.innings)
            s.innings.Add(new MatchSnapshot.InningsLine { yours = i.yours, declared = i.declared, runs = i.runs, wickets = i.wickets, balls = i.balls });
    }

    /// The saved match back, once HUD has its innings (HUD.Restore). False if there is no match
    /// flow, or the save is a Test and this is not (or the other way round).
    public static bool Restore(MatchSnapshot s)
    {
        if (instance == null || s == null || s.test != Test)
            return false;
        Target = s.target;
        AIFirst = s.hasAIFirst ? s.aiFirst.ToResult() : null;
        if (Test)
        {
            instance.youFirst = s.youFirst;
            instance.matchBalls = s.matchBalls;
            instance.innings.Clear();
            foreach (MatchSnapshot.InningsLine i in s.innings)
                instance.innings.Add(new Innings { yours = i.yours, declared = i.declared, runs = i.runs, wickets = i.wickets, balls = i.balls });
            ApplyTestOvers();
            if (s.betweenInnings)
            {
                instance.ShowBetweenInnings();
                return true;
            }
        }
        instance.UpdateStatus();
        // Saved on the ball that ended it (a last wicket, the target): it ends now.
        instance.Check();
        return true;
    }
}
