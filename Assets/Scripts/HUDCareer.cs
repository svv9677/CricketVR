using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the career mode needs from the scorekeeper. Kept apart from HUD.cs so the career's additions
/// are in one place.
///
/// A career match is a normal match with the career's two sides put on it (ApplyCareerRoster): the
/// player bats only as himself, and every ball he does not face - his partners', the wickets before
/// he comes in, the rest of the innings once he is out - is scored by ScoreSimulatedBall, through
/// the same scoring as a real ball, so the strike, the overs, the bowling changes and the figures
/// all come out as they would have. Nothing here does anything in a match that does not call it.
/// </summary>
public partial class HUD
{
    /// A simulated ball is being scored (ScoreSimulatedBall); true through the events it raises
    /// (BatterOut, StrikeChanged, SimulatedBallScored), so a listener can tell it from a real one.
    public static bool Simulating { get; private set; }

    /// The striker is someone else now: raised once the ball (or the reset) that changed him is
    /// fully in the book - from UpdateUI, which every scoring path ends with - so the score, the
    /// overs and the next batter are all settled when it comes. A run then the end of the over
    /// (the same man back on strike) is not a change. Do not score a ball from inside it. After the
    /// tenth wicket the scorebook wraps back to Batsmen[0] (IncrementWickets), so check InningsOver.
    public static event System.Action<Batsman> StrikeChanged;

    /// A batter is out, off a real ball or a simulated one: who, and the scorecard line
    /// (LastDismissal). Raised once the ball is in the book, before StrikeChanged for the new man.
    public static event System.Action<Batsman, string> BatterOut;

    /// A simulated ball has been scored (FieldCaptain: it is not the batter's, keep it out of his
    /// book). Raised before the UpdateUI that follows it.
    public static event System.Action SimulatedBallScored;

    private Batsman strikeRaised, pendingOut;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCareerStatics()
    {
        Simulating = false;
        StrikeChanged = null;
        BatterOut = null;
        SimulatedBallScored = null;
    }

    /// The batter at the crease who is the player (null outside a career match).
    public Batsman Human
    {
        get
        {
            if (Batsmen == null)
                return null;
            foreach (Batsman b in Batsmen)
                if (b != null && b.IsHuman)
                    return b;
            return null;
        }
    }

    /// The batting side's innings is done: all out, the overs bowled, or the target reached (the
    /// same three ways MatchFlow.Finished ends a match).
    public bool InningsOver =>
        Wickets >= 10 ||
        (TotalOvers > 0 && Overs * 6 + Balls >= TotalOvers * 6) ||
        (MatchFlow.Target > 0 && Runs >= MatchFlow.Target);

    /// The career's sides onto the scorebook HUD.Reset made: the batting order (names, ratings, and
    /// which one is the player) and the other side's bowlers' names. The batters are renamed in
    /// place, in order, so Batsmen[0] and [1] still open; the bowlers keep the kinds Reset gave
    /// them (the run-ups and actions are built for those), and a short list leaves the rest of the
    /// names as they were. Call it once, after HUD.Start has run Reset and before the first ball.
    public void ApplyCareerRoster(IList<string> batters, IList<float> ratings, int humanIndex, IList<string> bowlerNames)
    {
        if (Batsmen != null)
        {
            for (int i = 0; i < Batsmen.Count; i++)
            {
                Batsman b = Batsmen[i];
                if (b == null)
                    continue;
                if (batters != null && i < batters.Count && !string.IsNullOrEmpty(batters[i]))
                    b.Name = batters[i];
                if (ratings != null && i < ratings.Count)
                    b.Rating = ratings[i];
                b.IsHuman = i == humanIndex;
            }
        }
        if (Bowlers != null && bowlerNames != null)
        {
            for (int i = 0; i < Bowlers.Count && i < bowlerNames.Count; i++)
                if (Bowlers[i] != null && !string.IsNullOrEmpty(bowlerNames[i]))
                    Bowlers[i].Name = bowlerNames[i];
        }
        // HUD.Reset already announced the opener before IsHuman was set: announce him again.
        strikeRaised = null;
        UpdateUI();
    }

    /// One ball the player does not face, scored as if it had been bowled: code is AddRuns' (-1 a
    /// wicket, 0-6 runs off the bat, 10 a wide), howOut the scorecard's "c Point b Rana" for a
    /// wicket. Through the same private scoring as a real ball, so the strike changes, the over
    /// ends, the captain changes the bowler, and the batter's and bowler's figures and the
    /// scorebook (BowledBalls) all follow. It is never announced (no BallScored: no umpire signal,
    /// sound, celebration or floating result) and never a no-ball or a free hit.
    ///
    /// Score them while the ball is dead between real balls (InGame_Ready, with MatchGate holding
    /// the next one): a simulated ball between a real ball's scoring and its InGame_ResetToReady
    /// would leave that real ball out of the fielding captain's book. Balls that end the innings
    /// end the match only through MatchFlow.CheckFinished (or the next change of state).
    public void ScoreSimulatedBall(int code, string howOut)
    {
        if (!(code >= -1 && code <= 6 && code != 5) && code != 10)
        {
            Debug.LogWarning($"[HUD] ScoreSimulatedBall: no such ball code {code}");
            return;
        }
        if (Batsmen == null || CurrentBatsman == null || CurrentBowler == null || InningsOver)
            return;
        RaiseScoreEvents();   // anything left over from before, so this ball's are its own
        ReadyNoBallsForSimulatedBall();
        Simulating = true;
        Bowler bowling = CurrentBowler;
        try
        {
            AddRuns(code);
            // A run out is the side's wicket, not the bowler's (Credit gave it to him).
            if (code == -1 && bowling != null && bowling.Wickets > 0 && howOut != null &&
                howOut.StartsWith("run out", System.StringComparison.OrdinalIgnoreCase))
                bowling.Wickets--;
            if (pendingOut != null)
                LastDismissal = $"{pendingOut.Name} {(string.IsNullOrEmpty(howOut) ? "out" : howOut)}";
            // In the book already: AnnounceBall must not take it for a new ball at the next state.
            announcedCount = BowledBalls.Count;
            announcedLast = announcedCount > 0 ? BowledBalls[announcedCount - 1] : null;
            SimulatedBallScored?.Invoke();
            UpdateUI();
        }
        finally
        {
            Simulating = false;
        }
    }

    /// Clears the no-ball state for a simulated ball, which is never a no-ball and never a free
    /// hit: nothing called, nothing protected (so a wicket is a wicket), and no free hit waiting
    /// for the next real ball (a free hit earned before it is used up by it, as the next delivery
    /// whoever faces it). Two new deliveries that cannot overstep do it with NoBalls' own rules:
    /// the first takes any waiting free hit, the second has none to take. A forced no-ball (checks)
    /// is kept for the next real delivery. Public for the tests.
    public static void ReadyNoBallsForSimulatedBall()
    {
        bool forced = NoBalls.ForceNext;
        NoBalls.ForceNext = false;
        NoBalls.NewDelivery(() => 1f);
        NoBalls.NewDelivery(() => 1f);
        NoBalls.ForceNext = forced;
    }

    /// The wicket and the change of strike since the last call, if any (StrikeChanged, BatterOut).
    private void RaiseScoreEvents()
    {
        if (pendingOut != null)
        {
            Batsman gone = pendingOut;
            pendingOut = null;
            BatterOut?.Invoke(gone, LastDismissal);
        }
        if (CurrentBatsman != strikeRaised)
        {
            strikeRaised = CurrentBatsman;
            StrikeChanged?.Invoke(CurrentBatsman);
        }
    }

    /// Bring on the first bowler of this kind (the nets trial: each planned ball is bowled by the
    /// kind of bowler it is planned for, so the run-up, the action and the keeper match the ball).
    /// SelectBowlerOfType cannot do it: it goes by eSwingType, and the mystery spinner shares the
    /// off-spinner's, so it could never bring him on. Through SetBowler, so the bowling machine's
    /// view (Main.swingType), the keeper and AnimatedBowler's run-up all follow, as for any change.
    /// True when there is a bowler of that kind (whether or not he was on already).
    public bool SelectBowlerOfKind(eBowlerKind kind)
    {
        if (Bowlers == null)
            return false;
        int index = -1;
        for (int i = 0; i < Bowlers.Count; i++)
        {
            if (Bowlers[i] != null && Bowlers[i].Kind == kind)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return false;
        if (Bowlers[index] == CurrentBowler)
            return true;
        NextBowlerIndex = index;
        SetBowler(index);
        UpdateUI();
        return true;
    }

    /// Move the keeper for the bowler now, if a change is still waiting for it. HUD does this itself
    /// when the next ball starts (InGame_SelectDelivery), but a bowler brought on by a listener that
    /// runs after HUD's on that same state would otherwise keep the old keeper position for a ball.
    /// Only call it while the ball is parked (the start of a delivery), as HUD does.
    public void MoveKeeperIfPending()
    {
        if (!keeperMovePending)
            return;
        keeperMovePending = false;
        UpdateKeeperPosition();
    }
}
