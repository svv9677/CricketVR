using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The career player in the nets: the 30-ball trial, or measured practice (NetsSession.Mode, set by
/// the career hub before the Nets scene loads). Built when the Nets scene loads, only for a career
/// session - free nets practice from the main menu is the game as it always was.
///
/// Every ball the player faces (BallFeed.Faced, raised by BallLedger) is stamped with where it was
/// faced, added to the active career and the ratings recomputed and saved, ball by ball - so a career
/// loses at most the ball in the air if the headset comes off. The ratings only MEASURE: nothing here
/// changes how a ball is bowled or struck for the player's benefit.
///
/// For the session the player bats with their career player's hand, against a county-standard attack
/// (the ratings must mean the same for everyone); both settings are saved preferences
/// (GameSettings.BattingStyle, MatchSetup.Bowling write PlayerPrefs), so the player's own are put back
/// when the session ends however it ends.
///
/// The trial bowls its plan (NetsTrialPlan): before each ball the planned kind of bowler is brought
/// on (HUD.SelectBowlerOfKind: his run-up, his action, the keeper where he stands for him) and the
/// planned delivery forced (BowlingAttack.ForceKind and ForceDelivery - one alone does nothing). A
/// wide or a dead ball is bowled again. Progress lives in NetsSession.TrialIndex. After the 30th the
/// game is held in InMenu - no next ball (Main.StartNextBall needs InGame_Ready) - re-asserted every
/// frame, because a coroutine still running from the last ball, the bat's auto-reset or the X button
/// can each move it on; the result card shows, and A (or 20 s) goes back to the career hub.
///
/// Practice: no plan and no end; the normal bowling, every ball measured the same way. It ends when
/// the player leaves through the pause menu.
/// </summary>
public class NetsTrial : MonoBehaviour
{
    /// Seconds the result card stays up before going back on its own; A is ignored for the first one
    /// (the press that played the last ball must not dismiss it).
    private const float ContinueAfter = 20f, IgnoreAFor = 1f;

    private eNetsMode mode;
    private NetsTrialBall[] plan;
    private NetsTrialCard ui;
    private Main main;

    private bool applied, feedSubscribed;

    private bool finishPending, finished, leaving;
    private float finishedAt;
    private RatingSet result;
    private int practiceBalls;

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
        if (scene.name != NetsSession.NetsScene || !NetsSession.Career || FindFirstObjectByType<NetsTrial>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Nets Trial", typeof(NetsTrial)), scene);
    }

    /// Before Main.Start reads the settings (this is built after the scene's Awakes, before any Start),
    /// so the bat goes in the career player's hand from the first frame.
    private void Awake()
    {
        mode = NetsSession.Mode;
        CareerProfile p = CareerSaves.Current;
        ui = new NetsTrialCard(transform);
        applied = true;
        CareerSettingsGuard.Apply(p != null ? p.hand : GameSettings.BattingStyle, NetsTrialPlan.Level);
        if (mode == eNetsMode.Trial)
        {
            plan = NetsTrialPlan.Make(NetsTrialPlan.SeedFor(p, CareerSaves.Active));
            finishPending = NetsSession.TrialIndex >= NetsSession.TrialBalls;
        }
        BallFeed.Faced += OnFaced;
        feedSubscribed = true;
        ShowProgress();
    }

    private void OnDestroy()
    {
        if (feedSubscribed)
            BallFeed.Faced -= OnFaced;
        feedSubscribed = false;
        if (main != null)
            main.onGameStateChanged -= OnGameState;
        // A ball forced at SelectDelivery but not yet bowled must not carry into the next scene.
        if (mode == eNetsMode.Trial)
        {
            BowlingAttack.ForceKind = null;
            BowlingAttack.ForceDelivery = null;
        }
        // Left before the end: the pause menu's Main Menu ends the session (the hub opens on return;
        // the balls already measured stay in the career), a restart of the nets carries on with it
        // (NetsSession.Left decides when the next scene loads). A finished trial left before its card
        // was dismissed still reports its result.
        if (!leaving)
            NetsSession.Left(finished ? result : null);
        RestoreSettings();
    }

    private void Update()
    {
        if (main == null)
            TrySubscribe();
        if (finishPending && !finished)
            Finish();
        if (!finished)
            return;
        HoldInMenu();
        if (leaving)
            return;
        bool menuOpen = MenuRoot.Instance != null && MenuRoot.Instance.IsOpen;
        bool pressed = main != null && !menuOpen && Time.time - finishedAt > IgnoreAFor && main.GetButton(XRButton.A);
        if (pressed || Time.time - finishedAt > ContinueAfter)
            Continue();
    }

    /// Main sets Instance in its Start, which may come after this one's: so here, once it is up.
    private void TrySubscribe()
    {
        Main m = Main.Instance;
        if (m == null)
            return;
        main = m;
        main.onGameStateChanged += OnGameState;
        if (main.gameState == eGameState.InGame_Ready)
            OnGameState();
    }

    private void OnGameState()
    {
        eGameState s = main.gameState;
        if (finished || finishPending)
        {
            // Held between balls: nothing may start another (re-asserted in Update for the rest).
            // finishPending too: the 30th ball has counted but Update has not run Finish yet, and an
            // unforced 31st ball must not start in that frame.
            if (s == eGameState.InGame_Ready || s == eGameState.InGame_SelectDelivery)
                main.gameState = eGameState.InMenu;
            return;
        }
        if (s == eGameState.InGame_Ready)
        {
            if (mode == eNetsMode.Trial)
                BringOnPlannedBowler();
            ShowProgress();
            ui.PlaceProgress();
        }
        else if (s == eGameState.InGame_SelectDelivery && mode == eNetsMode.Trial && !finishPending)
        {
            // Again here, in case the bowler was changed on the next-ball card after Ready (the keeper
            // then moves now: HUD's own move for this ball has already run). BowlingAttack.Next reads
            // the force on the next step, so this is in time.
            BringOnPlannedBowler();
            HUD hud = Hud();
            if (hud != null)
                hud.MoveKeeperIfPending();
            NetsTrialBall b = Planned();
            BowlingAttack.ForceKind = b.kind;
            BowlingAttack.ForceDelivery = b.delivery;
        }
    }

    /// A ball faced and settled: into the career, ratings recomputed, saved; the trial moves on if it counted.
    private void OnFaced(BallRecord r)
    {
        if (r == null || finished || finishPending)
            return;
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        bool advances = Measure(p, r, mode);
        CareerSaves.SaveCurrent();
        if (mode == eNetsMode.Trial)
        {
            if (advances)
            {
                // The next ball's bowler comes on at InGame_Ready, not now: HUD may not have scored
                // this ball yet, and would credit it to him.
                NetsSession.TrialIndex++;
                if (NetsSession.TrialIndex >= NetsSession.TrialBalls)
                    finishPending = true;   // finished from Update, not inside whoever raised the ball
            }
        }
        else if (r.Counts)
            practiceBalls++;
        ShowProgress();
    }

    /// The trial is over: hold the game, record the trial in the career, show the result.
    private void Finish()
    {
        finishPending = false;
        finished = true;
        finishedAt = Time.time;
        BowlingAttack.ForceKind = null;
        BowlingAttack.ForceDelivery = null;
        CareerProfile p = CareerSaves.Current;
        // Recorded once: a restart while the card was up finishes again, from the same balls.
        RatingSet ratings = p == null ? new RatingSet() : p.trialDone ? p.trialRatings.Clone() : RecordTrial(p);
        if (p != null)
            CareerSaves.SaveCurrent();
        result = ratings.Clone();
        HoldInMenu();
        ui.HideProgress();
        ui.ShowResult(NetsTrialCard.ResultHeading, NetsTrialCard.ResultBody(result, CareerRatings.Verdict(result)), Continue);
        Debug.Log($"[NetsTrial] trial complete: overall {result.Overall:0} from {result.sample} balls");
    }

    // ---- The bookkeeping (static and pure of the scene: NetsTrialTests checks it) ----------------

    /// One faced ball into the career: stamped with where it was faced (the trial, or measured
    /// practice in the nets), added (CareerProfile.Add keeps the window and the totals) and the ratings
    /// recomputed from every kept ball. True when, in the trial, it moves the trial on.
    public static bool Measure(CareerProfile p, BallRecord r, eNetsMode mode)
    {
        if (p == null || r == null)
            return false;
        r.source = mode == eNetsMode.Trial ? eBallSource.Trial : eBallSource.Nets;
        p.Add(r);
        p.ratings = CareerRatings.Compute(p.balls);
        return mode == eNetsMode.Trial && NetsTrialPlan.Advances(r);
    }

    /// The trial is done: its ratings become the career's starting ratings (kept apart as
    /// trialRatings, a copy - the live ratings go on changing). Returns the ratings now.
    public static RatingSet RecordTrial(CareerProfile p)
    {
        // The starting ratings are this trial's: its TrialBalls counting balls, the most recent trial
        // balls (an attempt left half way stays in the career's ratings, not in the trial's).
        p.trialRatings = CareerRatings.Compute(TrialBalls(p.balls, NetsSession.TrialBalls));
        p.ratings = CareerRatings.Compute(p.balls);
        p.trialDone = true;
        p.totals.trials++;
        return p.trialRatings.Clone();
    }

    /// The most recent trial balls, back to the `count`th counting one (the wides and dead balls
    /// among them come too; Compute skips them).
    public static List<BallRecord> TrialBalls(List<BallRecord> balls, int count)
    {
        int start = balls.Count, counted = 0;
        for (int i = balls.Count - 1; i >= 0 && counted < count; i--)
        {
            if (balls[i] == null || balls[i].source != eBallSource.Trial)
                continue;
            start = i;
            if (balls[i].Counts && balls[i].end != eBallEnd.Dead)
                counted++;
        }
        var trial = new List<BallRecord>();
        for (int i = start; i < balls.Count; i++)
            if (balls[i] != null && balls[i].source == eBallSource.Trial)
                trial.Add(balls[i]);
        return trial;
    }

    /// Back to the career hub with the result (the card's button, A, or the time running out).
    private void Continue()
    {
        if (leaving)
            return;
        leaving = true;
        ui.HideResult();
        NetsSession.End(result);
        RestoreSettings();
        SceneFader.Load(NetsSession.SplashScene);
    }

    private void HoldInMenu()
    {
        if (main != null && main.gameState != eGameState.InMenu)
            main.gameState = eGameState.InMenu;
    }

    private NetsTrialBall Planned() => plan[Mathf.Clamp(NetsSession.TrialIndex, 0, plan.Length - 1)];

    private HUD Hud() => main != null && main.theHUD != null ? main.theHUD : HUD.Instance;

    private void BringOnPlannedBowler()
    {
        HUD hud = Hud();
        if (hud == null || plan == null)
            return;
        NetsTrialBall b = Planned();
        if (!hud.SelectBowlerOfKind(b.kind))
            Debug.LogWarning($"[NetsTrial] no {b.kind} bowler in the side: bowling ball {NetsSession.TrialIndex + 1} with {hud.CurrentBowler?.Name}");
    }

    private void ShowProgress()
    {
        if (mode == eNetsMode.Trial && plan != null)
            ui.ShowProgress(NetsTrialCard.ProgressText(NetsSession.TrialIndex + 1, Planned().kind));
        else if (mode == eNetsMode.Practice)
            ui.ShowProgress(NetsTrialCard.PracticeText(practiceBalls));
    }

    /// The player's own hand and bowling level back, once (both setters save to PlayerPrefs). The hand
    /// last: changing it re-applies the settings into Main, which may be mid-teardown on a scene change.
    private void RestoreSettings()
    {
        if (!applied)
            return;
        applied = false;
        CareerSettingsGuard.Restore();
    }
}
