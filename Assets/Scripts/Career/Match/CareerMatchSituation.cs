using UnityEngine;

/// <summary>
/// What a match situation does in the career match (phase 7): a collapse's scripted balls played
/// before he comes in, and the situation's card before the first ball. With no situation, the
/// partners' balls come from CareerSim exactly as before.
///
/// The collapse is worked out at home (CareerSituations.Apply, scored on CareerMatchLogic.Score the
/// way HUD scores a ball), so the hub can say "You come in at 28/3" before the match. Here its codes
/// are fed to HUD one by one in place of CareerSim's; only the batters before him ever get one, and
/// his own balls are never scripted. A code counts as used only once HUD took the ball (its
/// scorebook grew): HUD turns a ball away with no bowler on or the innings over, and the script must
/// not run ahead of the scoreboard.
/// </summary>
public partial class CareerMatch
{
    // The next scripted code of the collapse, and whether the situation's card has been shown.
    private int scriptAt;
    private bool introShown;

    /// The next simulated ball's CareerSim code: the collapse's next scripted code while any remain,
    /// otherwise from CareerSim at the striker's rating, as hard as the match situation says.
    private int NextCode(HUD hud, Batsman b)
    {
        if (ScriptedCode(plan, scriptAt, out int code))
            return code;
        float aggression = CareerSim.Aggression(hud.Runs, hud.Wickets, Bowled(hud), hud.TotalOvers * 6, MatchFlow.Target);
        return CareerSim.Ball(b.Rating, plan.bowling, aggression, rng);
    }

    /// After a simulated ball is scored: a scripted code is used up once the scoreboard took it. With
    /// the last one used, HUD should stand where the plan said he comes in; a warning if it does not
    /// (the hub promised that score).
    private void ScriptTaken(int bowledBefore, HUD hud)
    {
        int count = plan?.collapse != null ? plan.collapse.Count : 0;
        if (scriptAt >= count)
            return;
        int bowledAfter = hud.BowledBalls != null ? hud.BowledBalls.Count : 0;
        scriptAt = ScriptStep(scriptAt, count, bowledBefore, bowledAfter);
        if (scriptAt == count && !ScriptLanded(plan, hud.Runs, hud.Wickets))
            Debug.LogWarning($"[CareerMatch] the collapse ended at {hud.Runs}/{hud.Wickets}, not the planned {plan.startRuns}/{plan.startWickets}");
    }

    /// True while the situation's card is up before the first ball: shown once, when the situation
    /// has one (CareerSituationText.IntroHeading), through the same card and gate as the catch-up.
    private bool SituationIntro()
    {
        if (introShown)
            return false;
        introShown = true;
        string heading = CareerSituationText.IntroHeading(plan);
        if (string.IsNullOrEmpty(heading))
            return false;
        ShowCard(heading, CareerSituationText.IntroBody(plan), false);
        return true;
    }

    // ---- The rules, without a scene (CareerSituationMatchTests) --------------------------------

    /// The collapse's code at `at`, while the script has one.
    public static bool ScriptedCode(MatchPlan plan, int at, out int code)
    {
        code = 0;
        if (plan?.collapse == null || at < 0 || at >= plan.collapse.Count)
            return false;
        code = plan.collapse[at];
        return true;
    }

    /// Where the script stands after a ball: one on when HUD's scorebook grew, else where it was.
    public static int ScriptStep(int at, int count, int bowledBefore, int bowledAfter) =>
        at < count && bowledAfter > bowledBefore ? at + 1 : at;

    /// The scoreboard is where the plan said the collapse leaves it.
    public static bool ScriptLanded(MatchPlan plan, int runs, int wickets) =>
        plan == null || (runs == plan.startRuns && wickets == plan.startWickets);
}
