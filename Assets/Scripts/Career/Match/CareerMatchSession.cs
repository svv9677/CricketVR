using System;

/// <summary>
/// A career match about to be played or being played: set from the career hub before the match
/// scene loads (the plan is static, so it survives the load, like NetsSession), read there by
/// CareerMatch, and finished by it with the result - which goes into the career
/// (CareerProgress.RecordMatch) and is saved before the scene goes home.
///
/// The match scene is the game's own match (HUD, MatchFlow): the career only sets it up. Its format
/// and conditions go in through CareerSettingsGuard (the player's own settings come back however the
/// match ends), and MatchFlow is told to keep the career match out of the Quick Match history and to
/// take the other side's innings from the career's simulation (their real squad against yours)
/// rather than its own generic AI side.
/// </summary>
public static class CareerMatchSession
{
    public const string MatchScene = "CricketVR";

    /// A career match is set up or in progress.
    public static bool Active => Plan != null;
    public static MatchPlan Plan { get; private set; }

    /// Coming back to Splash, open the career hub; the match just finished (shown once).
    public static bool ReturnToHub;
    public static CareerMatchResult LastResult;
    public static MatchPlan LastPlan;

    /// The other side's innings as the career played it for MatchFlow: the card behind the AI result
    /// MatchFlow was handed (their first innings when chasing, their chase when batting first), so the
    /// match result can carry their batting card too. Null until MatchFlow asks.
    public static InningsCard TheirInnings { get; private set; }

    /// Apply the plan's settings for the match (CareerSettingsGuard, so the player's own come back)
    /// and load the match scene.
    public static void Begin(MatchPlan plan)
    {
        if (plan == null)
            return;
        Plan = plan;
        TheirInnings = null;
        CareerProfile p = CareerSaves.Current;
        CareerSettingsGuard.ApplyMatch(p != null ? p.hand : GameSettings.BattingStyle, plan.bowling, plan.overs,
                                       plan.batFirst ? eMatchType.SetTotal : eMatchType.Chase,
                                       plan.crowd, plan.pitch, plan.time);
        ApplyOverrides();
        SceneFader.Load(MatchScene);
    }

    /// The match is over (or abandoned): record it in the career, save, put the player's settings
    /// back, and ask for the hub on the way home. Safe to call once; later calls do nothing.
    public static void Finish(CareerMatchResult result)
    {
        MatchPlan plan = Plan;
        if (plan == null)
            return;
        // Cleared first: anything below that fails must not leave a match that can be finished twice.
        Plan = null;
        CareerProfile p = CareerSaves.Current;
        if (p != null && result != null)
        {
            try
            {
                CareerProgress.RecordMatch(p, plan, result, new Random(plan.seed ^ 0x2F6B5A1));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Career] recording the match: {e.Message}");
            }
            CareerSaves.SaveCurrent();
        }
        CareerSettingsGuard.Restore();
        MatchFlow.ClearOverrides();
        LastResult = result;
        LastPlan = plan;
        ReturnToHub = true;
    }

    /// MatchFlow's career overrides for the plan in force: no Quick Match history, the other side's
    /// innings from the career. Set again by CareerMatch when the scene is built (so a MatchFlow that
    /// cleared them on load still gets them before its Start asks for the target).
    public static void ApplyOverrides()
    {
        if (Plan == null)
            return;
        MatchFlow.KeepHistory = false;
        MatchFlow.AIInningsOverride = PlayTheirs;
        MatchFlow.OpponentName = string.IsNullOrEmpty(Plan.opponentName) ? "AI" : Plan.opponentName;
        MatchFlow.YourName = string.IsNullOrEmpty(Plan.yourTeamName) ? "You" : Plan.yourTeamName;
    }

    /// MatchFlow asking for the other side's innings: with no target it is their first innings (we
    /// chase it: the one already played, in the plan); with a target it is their chase of our total.
    private static AIInnings.Result PlayTheirs(int overs, int target, eBowlingLevel level, Random rng)
    {
        MatchPlan plan = Plan;
        if (plan == null)
            return AIInnings.Play(overs, target, level, rng ?? new Random());
        InningsCard card = target <= 0 && plan.theirs != null ? plan.theirs : PlayTheirInnings(overs, target);
        TheirInnings = card;
        return CareerSim.ToAIResult(card);
    }

    /// The other side batting against ours for `overs`, chasing `target` (0: setting a total).
    /// Seeded from the plan, so the same match always plays the same.
    public static InningsCard PlayTheirInnings(int overs, int target)
    {
        MatchPlan plan = Plan ?? LastPlan;
        if (plan == null)
            return new InningsCard { overs = overs, target = target };
        Team theirs = TeamOf(plan, false), ours = TeamOf(plan, true);
        InningsCard card = CareerSim.Innings(theirs, ours, overs, target, plan.bowling, new Random(plan.seed ^ 0x6C1E2D));
        if (card != null && string.IsNullOrEmpty(card.team))
            card.team = plan.opponentName;
        return card;
    }

    /// One of the plan's two sides from the career's season, or (no season to hand: a test, an old
    /// save) a stand-in made from the plan's own names, so a match can always be finished.
    public static Team TeamOf(MatchPlan plan, bool yours)
    {
        if (plan == null)
            return null;
        Team t = CareerSaves.Current?.season?.Team(yours ? plan.yourTeam : plan.opponent);
        if (t != null && t.players != null && t.players.Count > 0)
            return t;
        return yours
            ? CareerMatchLogic.StandInTeam(plan.yourTeam, plan.yourTeamName, plan.order, plan.orderRatings, Ladder.SquadMean(plan.rung))
            : CareerMatchLogic.StandInTeam(plan.opponent, plan.opponentName, plan.bowlers, null, Ladder.SquadMean(plan.rung));
    }

    /// Tests: a plan in force without loading a scene or touching the settings.
    public static void SetPlanForTests(MatchPlan plan)
    {
        Plan = plan;
        TheirInnings = null;
    }

    /// Tests.
    public static void ResetForTests() { Plan = null; ReturnToHub = false; LastResult = null; LastPlan = null; TheirInnings = null; }
}
