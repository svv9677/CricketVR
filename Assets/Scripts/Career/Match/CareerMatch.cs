using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A career match in the match scene: the career player bats as himself, at his slot in his side's
/// order (CareerMatchSession.Plan, set up by the career hub). Built when the match scene loads, only
/// for a career match - Quick Match is the game as it always was.
///
/// He plays only his own balls. Everything else is simulated (CareerSim) and scored through HUD,
/// so the scoreboard, the bowlers' figures and MatchFlow's ending all work as in any match:
///   * his partners' balls - whenever a partner is on strike between balls, his balls are simulated
///     one after another until the career player is on strike again (or the innings is over), and a
///     catch-up card says what happened ("While you were at the other end" / "You come in at 45/4");
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
/// (CareerMatchSession.Finish; MatchFlow then shows its card and goes home on its own). Leaving
/// before that (the pause menu, a restart) counts as retiring out: the rest of the match is
/// simulated from the last state seen and the result recorded as abandoned.
/// </summary>
public class CareerMatch : MonoBehaviour
{
    /// Seconds each card stays up.
    private const float CatchUpSeconds = 3.5f, ComeInSeconds = 4f, SummarySeconds = 5f;

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
    private float cardUntil = -1f;

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
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed)
        {
            BallFeed.Faced -= OnFaced;
            MatchFlow.Ended -= OnEnded;
            subscribed = false;
        }
        if (main != null)
            main.onGameStateChanged -= OnGameState;
        if (!ended)
        {
            try { Abandon(); }
            catch (System.Exception e) { Debug.LogError($"[CareerMatch] finishing a match left early: {e}"); }
        }
        holding = false;
        MatchGate.Blocking = false;
        if (teamSet)
            ReplayOverlay.Team = oldTeam;
        if (noteSet)
            MatchFlow.CardNote = null;
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
        if (CardUp())
            return;
        // Not under the pause menu either: the cards would come up over it.
        bool menuOpen = MenuRoot.Instance != null && MenuRoot.Instance.IsOpen;
        if (main.gameState != eGameState.InGame_Ready || OverBreak.Blocking || menuOpen || (MatchGate.Blocking && !holding))
            return;
        if (youOut && !restDone)
            FinishInnings(hud);
        else if (!InningsOver(hud) && !StrikerIsYou(hud))
            CatchUp(hud);
        else
            Hold(false);
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
            // A partner on strike: his balls are simulated in Update; no ball may start before then.
            if (rosterApplied && !MatchFlow.Over && !InningsOver(hud) && !StrikerIsYou(hud))
                Hold(true);
        }
    }

    /// A wicket off a real ball (the career player's: only he faces them) is HUD's: its dismissal
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

    /// A ball he faced, settled: into the career, the ratings recomputed, saved. Partners' balls
    /// are never bowled, but only a ball he was on strike for counts, whatever comes through. Even
    /// after the match has ended: the last ball can end it before it is settled here.
    private void OnFaced(BallRecord r)
    {
        if (r == null || !youAtRelease)
            return;
        youAtRelease = false;
        if (!boundaryTaken)
            CountBoundary(strikerAtRelease, r.end == eBallEnd.Four ? 4 : r.end == eBallEnd.Six ? 6 : 0);
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        r.source = eBallSource.Match;
        p.Add(r);
        p.ratings = CareerRatings.Compute(p.balls);
        CareerSaves.SaveCurrent();
    }

    // ---- Simulated balls ------------------------------------------------------------------------

    /// A partner is on strike: his balls (and the wickets before the career player comes in) until
    /// the career player has the strike, then the catch-up card - or straight to the end of the match.
    private void CatchUp(HUD hud)
    {
        var batch = new Batch();
        int simulated = SimulateBatch(hud, batch);
        bool over = InningsOver(hud);
        if (over || simulated == 0)
        {
            Hold(false);
            if (over)
                MatchFlow.CheckFinished();
            return;
        }
        var lines = FellLines(batch);
        Batsman partner = StrikerIsYou(hud) ? hud.CurrentRunner : null;
        if (partner != null && !partner.IsHuman && partner.Balls > 0 && !batch.fell.Contains(partner))
            lines.Add(CareerMatchLogic.BatterText(partner.Name, partner.Runs, partner.Balls, false, null));
        string heading = batch.cameIn ? CareerMatchLogic.ComeIn(batch.inRuns, batch.inWickets, batch.inBalls) : CareerMatchLogic.AtTheOtherEnd;
        ShowCard(heading, CareerMatchLogic.CatchUpBody(lines, hud.Runs, hud.Wickets, Bowled(hud)),
                 batch.cameIn ? ComeInSeconds : CatchUpSeconds, false);
    }

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
        ShowCard(heading, CareerMatchLogic.CatchUpBody(lines, hud.Runs, hud.Wickets, Bowled(hud)), SummarySeconds, true);
    }

    /// Simulated balls one after another until CareerMatchLogic.StopBatch says stop. Returns how many.
    private int SimulateBatch(HUD hud, Batch batch)
    {
        int cap = CareerMatchLogic.BatchCap(hud.TotalOvers * 6, Bowled(hud));
        bool wasIn = YouAtCrease(hud);
        int n = 0;
        while (!CareerMatchLogic.StopBatch(StrikerIsYou(hud), youOut, InningsOver(hud), n, cap) && hud.CurrentBatsman != null)
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
            Debug.Log($"[CareerMatch] simulated {n} ball{(n == 1 ? "" : "s")}: {hud.Runs}/{hud.Wickets} after {CareerMatchLogic.OversText(Bowled(hud))}");
        return n;
    }

    /// One partner's ball from CareerSim at his rating, against the plan's attack, as hard as the
    /// match situation says; scored by HUD like any ball.
    private void SimulateOne(HUD hud, Batch batch)
    {
        Batsman b = hud.CurrentBatsman;
        float aggression = CareerSim.Aggression(hud.Runs, hud.Wickets, Bowled(hud), hud.TotalOvers * 6, MatchFlow.Target);
        int code = CareerSim.Ball(b.Rating, plan.bowling, aggression, rng);
        string how = null;
        if (code == CareerSim.Wicket)
        {
            string bowler = hud.CurrentBowler != null ? hud.CurrentBowler.Name : "";
            how = CareerSim.HowOut(bowler, CareerMatchLogic.PickFielder(fielders, bowler, rng), rng);
        }
        int wicketsBefore = hud.Wickets;
        hud.ScoreSimulatedBall(code, how);
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

    private void ShowCard(string heading, string body, float seconds, bool finishAfter)
    {
        Hold(true);
        ui.Show(heading, body);
        cardUntil = Time.time + seconds;
        finishAfterCard = finishAfter;
    }

    /// True while a card is up. When it goes, the next ball is free - or, after the summary, the
    /// match is checked for its end (the rest of the innings has been simulated).
    private bool CardUp()
    {
        if (cardUntil < 0f)
            return false;
        if (Time.time < cardUntil)
            return true;
        cardUntil = -1f;
        ui.Hide();
        Hold(false);
        if (finishAfterCard)
        {
            finishAfterCard = false;
            MatchFlow.CheckFinished();
        }
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
    /// MatchFlow's card, and into the career now (MatchFlow goes home by itself after its card).
    private void FinishFromScene(AIInnings.Result ai)
    {
        if (ended)
            return;
        // The last ball can end the match before BallFeed has settled it: its boundary from the score.
        if (youAtRelease && strikerAtRelease != null && !boundaryTaken)
        {
            CountBoundary(strikerAtRelease, strikerAtRelease.Runs - runsAtRelease);
            boundaryTaken = true;
        }
        HUD hud = Hud();
        if (hud != null && hud.Batsmen != null)
        {
            DetectRealWicket(hud);
            Sync(hud);
        }
        ended = true;
        cardUntil = -1f;
        ui.Hide();
        Hold(false);
        InningsCard theirs = CareerMatchSession.TheirInnings ?? plan.theirs ?? CareerMatchLogic.FromAI(ai, plan.opponentName);
        CareerMatchResult result = CareerMatchLogic.Build(state, plan, theirs, false);
        string note = CareerMatchLogic.CardNote(result);
        MatchFlow.CardNote = note;
        Debug.Log($"[CareerMatch] {result.yours?.Score} - {result.result}; {note}");
        CareerMatchSession.Finish(result);
        // Finish clears MatchFlow's career overrides; the note is for the card MatchFlow is about to
        // build, so it goes back if that took it too (and is cleared with this scene: OnDestroy).
        if (string.IsNullOrEmpty(MatchFlow.CardNote))
            MatchFlow.CardNote = note;
        noteSet = true;
    }

    /// Left before the end: he retires out (it counts as out), the rest of his side's innings is
    /// simulated from the last state seen, then the other side's if they had still to bat.
    private void Abandon()
    {
        ended = true;
        MatchState s = state;
        if (!CareerMatchLogic.InningsDone(s))
        {
            CareerMatchLogic.Retire(s);
            CareerMatchLogic.Complete(s, plan.bowling, plan.bowlers, fielders, rng);
        }
        int overs = s.totalBalls > 0 ? s.totalBalls / 6 : plan.overs;
        InningsCard theirs = plan.batFirst ? CareerMatchSession.PlayTheirInnings(overs, s.runs + 1) : plan.theirs;
        CareerMatchResult result = CareerMatchLogic.Build(s, plan, theirs, true);
        Debug.Log($"[CareerMatch] left early: {result.yours?.Score} - {result.result}");
        CareerMatchSession.Finish(result);
    }

    // ---- The scoreboard -------------------------------------------------------------------------

    /// The career's copy of the innings in step with HUD (what is left to finish from if the scene
    /// goes): the score, each batter's runs and balls, who is in, and what HUD does not keep.
    private void Sync(HUD hud)
    {
        if (!rosterApplied || state == null)
            return;
        state.runs = hud.Runs; state.wickets = hud.Wickets; state.balls = Bowled(hud);
        state.totalBalls = hud.TotalOvers * 6; state.target = MatchFlow.Target;
        int n = Mathf.Min(hud.Batsmen.Count, state.batters.Count);
        for (int i = 0; i < n; i++)
        {
            Batsman b = hud.Batsmen[i];
            MatchBatter m = state.batters[i];
            if (b == null || m == null)
                continue;
            m.name = b.Name; m.runs = b.Runs; m.balls = b.Balls; m.rating = b.Rating;
            m.batted |= b == hud.CurrentBatsman || b == hud.CurrentRunner || b.Balls > 0;
            if (outs.TryGetValue(b, out string how))
            {
                m.isOut = m.batted = true;
                m.how = how;
            }
            m.fours = fours.TryGetValue(b, out int f) ? f : 0;
            m.sixes = sixes.TryGetValue(b, out int x) ? x : 0;
        }
        state.striker = hud.Batsmen.IndexOf(hud.CurrentBatsman);
        state.runner = hud.Batsmen.IndexOf(hud.CurrentRunner);
        CareerMatchLogic.ObserveOvers(state);
    }

    private static int Bowled(HUD hud) => hud.Overs * 6 + hud.Balls;

    private static bool InningsOver(HUD hud) =>
        CareerMatchLogic.InningsDone(hud.Runs, hud.Wickets, Bowled(hud), hud.TotalOvers * 6, MatchFlow.Target);

    private static bool StrikerIsYou(HUD hud) => hud.CurrentBatsman != null && hud.CurrentBatsman.IsHuman;

    private static bool YouAtCrease(HUD hud) =>
        StrikerIsYou(hud) || (hud.CurrentRunner != null && hud.CurrentRunner.IsHuman);

    /// A four or a six (runs 4 or 6) to his name; anything else is not a boundary.
    private void CountBoundary(Batsman b, int runs)
    {
        Dictionary<Batsman, int> d = runs == 4 ? fours : runs == 6 ? sixes : null;
        if (b != null && d != null)
            d[b] = d.TryGetValue(b, out int n) ? n + 1 : 1;
    }
}
