using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A career match in the match scene: the career player bats as himself, at his slot in his side's
/// order (CareerMatchSession.Plan, set up by the career hub). Built when the match scene loads, only
/// for a career match - Quick Match is the game as it always was.
///
/// He plays only his own balls (and watches his partner's from the other end: CareerMatchPartner). The
/// rest is simulated (CareerSim) and scored by HUD, so the scoreboard and MatchFlow work as in any match:
///   * his partners' balls - whenever a partner is on strike between balls, his balls are simulated
///     one at a time, each added to a ball-by-ball card (CareerMatchBallByBall), until the career
///     player is on strike again (or the innings is over) ("While you were at the other end" /
///     "You come in at 45/4");
///   * the wickets before he comes in - the same thing, from the first ball;
///   * the rest of the innings once he is out - simulated straight away, with a summary card;
///   * the other side's innings - MatchFlow asks for it (CareerMatchSession's override).
/// No batch of balls is ever run from inside a game-state handler (they change the scoreboard that
/// other handlers of the same change are reading): only from Update, with the game between balls.
/// While a card is up, or a batch is waiting, no ball can be started (MatchGate).
///
/// Only the balls he faces are measured (BallFeed.Faced, while he was on strike as the ball was
/// released): into the career, the ratings recomputed and saved, ball by ball. The ratings only
/// MEASURE - nothing here changes a ball for his benefit.
///
/// When MatchFlow ends the match the result goes into the career there and then
/// (CareerMatchSession.Finish; MatchFlow then shows its card, and Home goes home). Leaving
/// before that (the pause menu, closing the app) keeps the match to continue from the hub
/// (CareerMatchResume; it was retiring out until 2026-11-09).
/// </summary>
public partial class CareerMatch : MonoBehaviour
{
    /// A is not read for this long after a card goes up (the press that started the last ball, or
    /// skipped the partner's, must not also dismiss it). The cards wait for Next: none times out
    /// (2026-11-09; they were up 3.5-5 s and went on their own).
    private const float IgnoreAFor = 0.4f;

    /// What one batch of simulated balls did, for its card.
    private class Batch
    {
        public readonly List<Batsman> fell = new List<Batsman>();
        public bool cameIn;
        public int inRuns, inWickets, inBalls;
    }

    private MatchPlan plan;
    private Main main;
    private CareerMatchCard ui;
    private System.Random rng;
    private MatchState state;
    private List<string> fielders = new List<string>();

    private bool rosterApplied, subscribed, ended;
    private bool teamSet, noteSet;
    private string oldTeam;

    // The ball in the air: who was on strike as it was released, and the wickets then.
    private bool releasePending, youAtRelease, boundaryTaken;
    private Batsman strikerAtRelease;
    private int wicketsAtRelease, runsAtRelease;

    // What the scoreboard does not keep.
    private readonly Dictionary<Batsman, string> outs = new Dictionary<Batsman, string>();
    private readonly Dictionary<Batsman, int> fours = new Dictionary<Batsman, int>(), sixes = new Dictionary<Batsman, int>();

    private bool youOut, restDone;
    private bool holding, finishAfterCard;
    private bool cardWaiting, waitRelease;
    private float cardShownAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != CareerMatchSession.MatchScene || !CareerMatchSession.Active || FindFirstObjectByType<CareerMatch>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Career Match", typeof(CareerMatch)), scene);
    }

    /// Built after the scene's Awakes and before any Start: MatchFlow's Start asks for the target
    /// after this, so the overrides are (re)asserted here in time.
    private void Awake()
    {
        plan = CareerMatchSession.Plan;
        if (plan == null)
        {
            ended = true;
            enabled = false;
            return;
        }
        rng = new System.Random(plan.seed ^ 0x3B9F21);
        ui = new CareerMatchCard(transform);
        state = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        Team theirs = CareerMatchSession.TeamOf(plan, false);
        if (theirs?.players != null)
            foreach (SquadPlayer p in theirs.players)
                if (p != null && !string.IsNullOrEmpty(p.name))
                    fielders.Add(p.name);
        CareerMatchSession.ApplyOverrides();
        BallFeed.Faced += OnFaced;
        MatchFlow.Ended += OnEnded;
        Bat.ShotStruck += OnShotStruck;   // phase 7: the player's sixes, for the records (CareerMatchMoments)
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed)
        {
            BallFeed.Faced -= OnFaced;
            MatchFlow.Ended -= OnEnded;
            Bat.ShotStruck -= OnShotStruck;
            subscribed = false;
        }
        if (main != null)
            main.onGameStateChanged -= OnGameState;
        if (!ended)
        {
            // Left early: kept to continue, not retired (CareerMatchResume).
            try { SaveResume(true); }
            catch (System.Exception e) { Debug.LogError($"[CareerMatch] saving a match left early: {e}"); }
            ended = true;
            CareerMatchSession.Suspend();
        }
        holding = false;
        MatchGate.Blocking = false;
        if (teamSet)
            ReplayOverlay.Team = oldTeam;
        if (noteSet)   // the note and no-history kept for MatchFlow's card: back to normal
            { MatchFlow.CardNote = null; MatchFlow.KeepHistory = true; }
        CareerSettingsGuard.Restore();   // the player's own settings, as the scene goes
    }

    private void Update()
    {
        if (ended)
            return;
        if (main == null)
            TrySubscribe();
        HUD hud = Hud();
        if (main == null || hud == null || hud.Batsmen == null)
            return;
        if (!rosterApplied)
            ApplyRoster(hud);
        DetectRealWicket(hud);
        Sync(hud);
        if (MatchFlow.Over)
        {
            // MatchFlow.Ended finishes the match; this is in case it never came.
            FinishFromScene(null);
            return;
        }
        if (StepBallByBall(hud))   // a partner's balls, one at a time (CareerMatchBallByBall)
            return;
        if (CardUp())
            return;
        // Not under the pause menu either: the cards would come up over it.
        bool menuOpen = MenuRoot.Instance != null && MenuRoot.Instance.IsOpen;
        if (main.gameState != eGameState.InGame_Ready || OverBreak.Blocking || menuOpen || (MatchGate.Blocking && !holding))
            return;
        if (SituationIntro())   // phase 7: the situation's card before the first ball (CareerMatchSituation)
            return;
        if (youOut && !restDone)
            FinishInnings(hud);
        else if (!InningsOver(hud) && !StrikerIsYou(hud) && !PartnerWatch.Live)
            CatchUp(hud);
        else
        {
            Hold(false);
            KeepBetweenBalls(hud);
        }
    }

    /// Main sets Instance in its Start, which may come after this one's: so here, once it is up.
    private void TrySubscribe()
    {
        Main m = Main.Instance;
        if (m == null)
            return;
        main = m;
        main.onGameStateChanged += OnGameState;
    }

    private HUD Hud() => main != null && main.theHUD != null ? main.theHUD : HUD.Instance;

    /// The career's two sides into the scoreboard. Not before HUD.Start: its Reset builds the
    /// game's own roster, which would replace this one.
    private void ApplyRoster(HUD hud)
    {
        rosterApplied = true;
        try
        {
            hud.ApplyCareerRoster(plan.order, plan.orderRatings, plan.slot, plan.bowlers);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CareerMatch] the career roster did not go in: {e.Message}");
        }
        oldTeam = ReplayOverlay.Team;
        teamSet = true;
        if (!string.IsNullOrEmpty(plan.yourShort))
            ReplayOverlay.Team = plan.yourShort;
        Restore(hud);   // a match continued from the hub: its scoreboard, before Sync notes it
        Sync(hud);
    }

    /// Only watching here (the batches run from Update): who faces the ball as it is released, a
    /// wicket off it, and holding the next ball while a partner is on strike.
    private void OnGameState()
    {
        HUD hud = Hud();
        if (ended || hud == null || hud.Batsmen == null)
            return;
        eGameState s = main.gameState;
        // A new ball: the scoreboard as it stood between balls (partners' simulated balls included,
        // which the Ready check in Update can miss when the next ball starts the frame a card goes).
        if (s == eGameState.InGame_SelectDelivery)
            SaveResume(false);
        if (s == eGameState.InGame_DeliverBall)
        {
            strikerAtRelease = hud.CurrentBatsman;
            youAtRelease = strikerAtRelease != null && strikerAtRelease.IsHuman;
            wicketsAtRelease = hud.Wickets;
            runsAtRelease = strikerAtRelease != null ? strikerAtRelease.Runs : 0;
            releasePending = true;
            boundaryTaken = false;
        }
        else if (s == eGameState.InGame_ResetToReady || s == eGameState.InGame_Ready)
        {
            DetectRealWicket(hud);
            if (s != eGameState.InGame_Ready)
                return;
            releasePending = false;
            // A partner on strike: his balls are simulated in Update (unless watched); none may start first.
            if (rosterApplied && !MatchFlow.Over && !InningsOver(hud) && !StrikerIsYou(hud) && !PartnerWatch.Live)
                Hold(true);
        }
    }

    /// A wicket off a real ball (his, or a watched partner's) is HUD's: its dismissal
    /// line says how. Looked for wherever the scoreboard may have moved - before MatchFlow ends on it.
    private void DetectRealWicket(HUD hud)
    {
        if (!releasePending || strikerAtRelease == null || hud.Wickets <= wicketsAtRelease)
            return;
        releasePending = false;
        Batsman b = strikerAtRelease;
        if (!outs.ContainsKey(b))
            outs[b] = CareerMatchLogic.CleanHow(b.Name, hud.LastDismissal);
        if (b.IsHuman)
            youOut = true;
    }

    /// A ball he faced, settled: into the career, the ratings recomputed, saved. A partner's ball never
    /// comes (BallLedger), and only a ball he was on strike for counts, whatever comes through. Even
    /// after the match has ended: the last ball can end it before it is settled here.
    private void OnFaced(BallRecord r)
    {
        if (r == null || !youAtRelease)
            return;
        youAtRelease = false;
        if (!boundaryTaken)
            YourBoundary(strikerAtRelease, r.end == eBallEnd.Four ? 4 : r.end == eBallEnd.Six ? 6 : 0);
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        r.source = eBallSource.Match;
        p.Add(r);
        p.ratings = CareerRatings.Compute(p.balls);
        CareerSaves.SaveCurrent();
    }

    // ---- Simulated balls ------------------------------------------------------------------------

    /// He is out: the rest of the innings at once, his innings and the total on the card, and the
    /// match goes on (MatchFlow ends it) when the card goes.
    private void FinishInnings(HUD hud)
    {
        restDone = true;
        Batsman you = hud.Batsmen.Find(b => b != null && b.IsHuman);
        var batch = new Batch();
        SimulateBatch(hud, batch);
        var lines = FellLines(batch);
        string how = you != null && outs.TryGetValue(you, out string h) ? h : "out";
        string heading = you != null ? CareerMatchLogic.YourInnings(you.Runs, you.Balls, true, how) : "Innings over";
        ShowCard(heading, CareerMatchLogic.CatchUpBody(lines, hud.Runs, hud.Wickets, Bowled(hud)), true);
    }

    /// Simulated balls one after another until CareerMatchLogic.StopBatch says stop. Returns how many.
    private int SimulateBatch(HUD hud, Batch batch)
    {
        int cap = CareerMatchLogic.BatchCap(hud.TotalOvers * 6, Bowled(hud));
        bool wasIn = YouAtCrease(hud);
        int n = 0;
        while (!CareerMatchLogic.StopBatch(YourTurn(hud), youOut, InningsOver(hud), n, cap) && hud.CurrentBatsman != null)
        {
            SimulateOne(hud, batch);
            n++;
            if (!wasIn && YouAtCrease(hud))
            {
                wasIn = batch.cameIn = true;
                batch.inRuns = hud.Runs; batch.inWickets = hud.Wickets; batch.inBalls = Bowled(hud);
            }
        }
        if (n > 0)
        {
            MatchFlow.RefreshStatus();   // the "Need 18 off 18" line, now, not after his next ball
            Debug.Log($"[CareerMatch] simulated {n} ball{(n == 1 ? "" : "s")}: {hud.Runs}/{hud.Wickets} after {CareerMatchLogic.OversText(Bowled(hud))}");
        }
        return n;
    }

    /// One partner's ball from CareerSim at his rating, against the plan's attack, as hard as the
    /// match situation says; scored by HUD like any ball.
    private void SimulateOne(HUD hud, Batch batch)
    {
        Batsman b = hud.CurrentBatsman;
        int code = NextCode(hud, b);   // scripted while a collapse is replayed (CareerMatchSituation)
        string how = null;
        if (code == CareerSim.Wicket)
        {
            string bowler = hud.CurrentBowler != null ? hud.CurrentBowler.Name : "";
            how = CareerSim.HowOut(bowler, CareerMatchLogic.PickFielder(fielders, bowler, rng), rng);
        }
        int wicketsBefore = hud.Wickets;
        int bowledBefore = hud.BowledBalls != null ? hud.BowledBalls.Count : 0;
        hud.ScoreSimulatedBall(code, how);
        ScriptTaken(bowledBefore, hud);
        if (hud.Wickets > wicketsBefore)
        {
            outs[b] = !string.IsNullOrEmpty(how) ? how : CareerMatchLogic.CleanHow(b.Name, hud.LastDismissal);
            batch.fell.Add(b);
        }
        else
            CountBoundary(b, code);
        Sync(hud);
    }

    private List<string> FellLines(Batch batch)
    {
        var lines = new List<string>();
        foreach (Batsman b in batch.fell)
            lines.Add(CareerMatchLogic.BatterText(b.Name, b.Runs, b.Balls, true, outs.TryGetValue(b, out string h) ? h : null));
        return lines;
    }

    // ---- The card and the gate ------------------------------------------------------------------

    private void ShowCard(string heading, string body, bool finishAfter)
    {
        Hold(true);
        ui.Show(heading, body);
        ui.SetButton("Next  <size=75%>(A)</size>");
        ui.TakeClick();
        cardWaiting = true;
        cardShownAt = Time.time;
        finishAfterCard = finishAfter;
    }

    /// True while a card is up, until Next (A or its button). Then the next ball is free - once A
    /// is let go, so the same press cannot also bowl it - or, after the summary, the match is
    /// checked for its end (the rest of the innings has been simulated).
    private bool CardUp()
    {
        if (waitRelease)
        {
            if (main != null && main.GetButton(XRButton.A, false))
                return true;
            waitRelease = false;
            Hold(false);
            if (finishAfterCard)
            {
                finishAfterCard = false;
                MatchFlow.CheckFinished();
            }
            return true;
        }
        if (!cardWaiting)
            return false;
        bool menuOpen = MenuRoot.Instance != null && MenuRoot.Instance.IsOpen;
        bool next = ui.TakeClick() ||
                    (main != null && !menuOpen && Time.time - cardShownAt > IgnoreAFor && main.GetButton(XRButton.A));
        if (!next)
            return true;
        cardWaiting = false;
        ui.Hide();
        waitRelease = true;
        return true;
    }

    /// No ball can be started while held (MatchGate): a card is up, or a partner's balls are due.
    private void Hold(bool on)
    {
        if (on || holding)
            MatchGate.Blocking = holding = on;
    }

    // ---- The end --------------------------------------------------------------------------------

    private void OnEnded(MatchHistory.Record record, AIInnings.Result ai, string text) => FinishFromScene(ai);

    /// MatchFlow has ended the match: the result from the scoreboard, a line about his innings on
    /// MatchFlow's card, and into the career now (MatchFlow goes home on its card's Home).
    private void FinishFromScene(AIInnings.Result ai)
    {
        if (ended)
            return;
        // The last ball can end the match before BallFeed has settled it: its boundary from the score.
        if (youAtRelease && strikerAtRelease != null && !boundaryTaken)
        {
            YourBoundary(strikerAtRelease, strikerAtRelease.Runs - runsAtRelease);
            boundaryTaken = true;
        }
        HUD hud = Hud();
        if (hud != null && hud.Batsmen != null)
        {
            DetectRealWicket(hud);
            Sync(hud);
        }
        ended = true;
        cardWaiting = waitRelease = false;
        ui.Hide();
        Hold(false);
        InningsCard theirs = CareerMatchSession.TheirInnings ?? plan.theirs ?? CareerMatchLogic.FromAI(ai, plan.opponentName);
        CareerMatchResult result = CareerMatchLogic.Build(state, plan, theirs, false);
        string note = NetsTrialCard.Latin1(CareerMatchLogic.CardNote(result));
        MatchFlow.CardNote = note;
        Debug.Log($"[CareerMatch] {result.yours?.Score} - {result.result}; {note}");
        CareerMatchSession.Finish(result);
        // Finish clears MatchFlow's overrides, but the card it is about to build reads two: the note,
        // and no history (no "watch the chase at home": he goes to the hub). Undone in OnDestroy.
        if (string.IsNullOrEmpty(MatchFlow.CardNote))
            MatchFlow.CardNote = note;
        MatchFlow.KeepHistory = false;
        noteSet = true;
    }
}
