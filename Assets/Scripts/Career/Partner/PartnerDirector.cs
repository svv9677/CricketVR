using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the partner's balls while the career player watches from the non-striker's end (phase 3):
///   * InGame_SelectDeliveryLoop (the run-up): what he means to do with the ball (PartnerIntent, his
///     rating against this attack, as hard as the match situation says) and, spread over the frames
///     of the run-up, where to hit it to get that past or to this field (PartnerShotPlanner);
///   * InGame_DeliverBallLoop (the release): the shot itself against the ball as bowled
///     (Main.lastDelivery and its swing and turn), the swing started (PartnerBatter.Play) and the
///     contact armed (PartnerContact);
///   * InGame_ResetToReady: back to his stance, and one log line comparing what he meant with what
///     the game made of it ("[Partner] intent=.. shot=.. got=..", for tuning in the headset).
/// The game's own fielding, catching and scoring decide every ball: the intent only picks the shot.
///
/// Built with the career match; does nothing on a ball that is not the partner's (PartnerWatch), so
/// with watching off the match is exactly phase 2's.
/// </summary>
public class PartnerDirector : MonoBehaviour
{
    /// Most time the direction search may take in one frame (ms): the run-up must stay smooth on a
    /// Quest. Whatever is not done by the release is left; the best direction so far is played.
    public const float PlanBudgetMs = 1.5f;
    /// Candidate directions tried between looks at the clock: few, so one step cannot run far past
    /// the budget on a Quest (tune from the "search=.. ms" log in the headset).
    public const int CandidatesPerStep = 2;
    /// Where the bowler fields from once he has bowled: his follow-through, down the pitch (world x).
    private const float BowlerFinishX = -7f;

    public static PartnerDirector Instance { get; private set; }

    /// This ball's plan, once made at the release.
    public ShotPlan CurrentPlan => shotPlan;
    public bool HasPlan { get; private set; }

    private Main main;
    private PartnerContact contact;
    private CareerMatch careerMatch;
    private readonly List<BallFlight.Sample> scratch = new List<BallFlight.Sample>(1024);

    // The plan being made: the intent, the field it was made against, and the search's best.
    private int intent;
    private FieldSnapshot field;
    private ShotDirection best;
    private bool prepared;
    private IEnumerator<ShotDirection> steps;
    private Coroutine searching;
    private int stepsRun;
    private float searchMs;
    private ShotPlan shotPlan;

    // The partner's ball in play: who faced it and what the scorer called it.
    private bool partnersBall, boundaryCounted;
    private Batsman striker;
    private int runsAtRelease;
    private string got;

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
        if (scene.name != CareerMatchSession.MatchScene || !CareerMatchSession.Active || FindFirstObjectByType<PartnerDirector>() != null)
            return;
        var go = new GameObject("Partner Director", typeof(PartnerDirector), typeof(PartnerContact));
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    private void Awake()
    {
        Instance = this;
        contact = GetComponent<PartnerContact>();
        HUD.BallScored += OnScored;
    }

    /// Main.Instance is set in Main.Start, which may not have run (and the last scene's destroyed
    /// Main may still be it): this scene's own.
    private void Start()
    {
        main = FindFirstObjectByType<Main>();
        if (main != null)
            main.onGameStateChanged += OnGameState;
    }

    private void OnDestroy()
    {
        HUD.BallScored -= OnScored;
        if (main != null)
            main.onGameStateChanged -= OnGameState;
        EndSteps();
        if (Instance == this)
            Instance = null;
    }

    private void OnGameState()
    {
        eGameState s = main.gameState;
        PartnerWatch.OnState(s);
        switch (s)
        {
            case eGameState.InGame_SelectDeliveryLoop:
                if (PartnerWatch.Live)
                    BeginSearch();
                break;
            case eGameState.InGame_DeliverBall:
                partnersBall = PartnerWatch.ThisBall;
                boundaryCounted = false;
                got = null;
                striker = partnersBall && HUD.Instance != null ? HUD.Instance.CurrentBatsman : null;
                runsAtRelease = striker != null ? striker.Runs : 0;
                // A fresh ball: no plan or contact from the last one left for this one's log line.
                shotPlan = default;
                if (contact != null)
                    contact.Arm(default);
                break;
            case eGameState.InGame_DeliverBallLoop:
                if (partnersBall && PartnerBatter.Instance != null)
                    Release();
                break;
            case eGameState.InGame_ResetToReady:
                Finish();
                break;
        }
    }

    // ---- Before the release: the intent and the direction --------------------------------------

    /// The intent for this ball, the field as it stands, and the search started.
    private bool Prepare()
    {
        EndSteps();
        prepared = false;
        HUD hud = HUD.Instance;
        PartnerBatter partner = PartnerBatter.Instance;
        if (hud == null || hud.CurrentBatsman == null || partner == null || main == null)
            return false;
        float offSign = OffSign();
        // The no-ball is known from the run-up (NoBalls.Overstep), the free hit from the last ball:
        // neither can be out, so he never means to be.
        bool protectedBall = NoBalls.Overstep || NoBalls.FreeHit;
        intent = PartnerIntent.Draw(hud.CurrentBatsman.Rating, Level(), Aggression(hud), PartnerWatch.Rng, false, protectedBall);
        field = Snapshot(offSign);
        best = new ShotDirection { intent = intent };
        Vector3 nominal = new Vector3(BallDelivery.BatsmanStumpsX - 1.25f, 0.45f, partner.StanceZ);
        steps = PartnerShotPlanner.PlanDirectionSteps(intent, field, nominal, PartnerWatch.Rng, CandidatesPerStep);
        stepsRun = 0;
        searchMs = 0f;
        prepared = true;
        return true;
    }

    private void BeginSearch()
    {
        if (Prepare())
            searching = StartCoroutine(Search());
    }

    /// The search, a slice of at most PlanBudgetMs a frame, until it is done or the ball is bowled.
    private IEnumerator Search()
    {
        while (steps != null)
        {
            Slice();
            if (steps != null)
                yield return null;
        }
        searching = null;
    }

    /// One slice of the search. The search ending (or failing) lets go of it here, without stopping
    /// the coroutine this may be running in - that one ends by itself when it sees no steps.
    private void Slice()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (steps != null && clock.Elapsed.TotalMilliseconds < PlanBudgetMs)
        {
            bool more;
            try { more = steps.MoveNext(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Partner] the direction search failed: {e}");
                more = false;
            }
            if (!more)
            {
                DisposeSteps();
                break;
            }
            stepsRun++;
            ShotDirection d = steps.Current;
            if (d.valid || !best.valid)
                best = d;
        }
        searchMs += (float)clock.Elapsed.TotalMilliseconds;
    }

    private void EndSteps()
    {
        if (searching != null)
        {
            StopCoroutine(searching);
            searching = null;
        }
        DisposeSteps();
    }

    private void DisposeSteps()
    {
        if (steps == null)
            return;
        steps.Dispose();
        steps = null;
    }

    // ---- The release: the shot ------------------------------------------------------------------

    private void Release()
    {
        // No run-up seen (the strike came to him after it): one slice now, the best of it played.
        if (!prepared && Prepare())
            Slice();
        EndSteps();
        if (!prepared)
            return;
        PartnerBatter partner = PartnerBatter.Instance;
        BallDelivery.Solution d = main.lastDelivery;
        var incoming = new IncomingBall
        {
            release = d.releasePosition,
            velocity = d.releaseVelocity,
            effects = main.theBallScript != null ? main.theBallScript.deliveryEffects : BallFlight.DeliveryEffects.None,
        };
        float offSign = OffSign();
        ShotDirection dir = best;
        if (!dir.valid)
            dir.intent = intent;
        // A wide is left alone, whatever he meant to do with it.
        if (PartnerIntent.IsWide(d, partner.StanceZ, offSign, partner.StanceHeadTopY))
            dir = new ShotDirection { intent = PartnerIntent.LeaveWide };
        try { shotPlan = PartnerShotPlanner.Plan(dir, incoming, field, offSign, PartnerWatch.Rng, scratch); }
        catch (System.Exception e)
        {
            // No shot (the ball goes on to the stumps or the keeper) rather than a stuck ball.
            Debug.LogError($"[Partner] the shot plan failed: {e}");
            shotPlan = new ShotPlan { intent = dir.intent, shot = PartnerShot.Leave };
        }
        HasPlan = true;
        contact.Arm(shotPlan);
        // Seconds after the release, and the ball has only just left the hand (no physics step yet).
        partner.Play(shotPlan, shotPlan.contactTime);
    }

    // ---- The dead ball ----------------------------------------------------------------------------

    /// The scorer's call on the partner's ball: for the log, and a four or six to his name on the
    /// career's scorecard (CareerMatch counts the player's own from BallFeed, which never carries a
    /// partner's ball).
    private void OnScored(BallOutcome o)
    {
        if (!partnersBall || HUD.Simulating)
            return;
        got = o.Label;
        int runs = main != null && striker != null ? BoundaryRuns(main.gameState, runsAtRelease, striker.Runs) : 0;
        if (runs == 0 || boundaryCounted)
            return;
        boundaryCounted = true;
        if (careerMatch == null)
            careerMatch = FindFirstObjectByType<CareerMatch>();
        if (careerMatch != null)
            careerMatch.PartnerBoundary(striker, runs);
    }

    /// A four or six off the bat, from the runs to the striker's name over the ball: over the rope
    /// only, and a no-ball's ("5nb", "7nb" - signalled NoBall, not Four) as well as a legal ball's,
    /// as the player's own are counted (BallLedgerMath.End). Byes over the rope are not his. 0 if
    /// it was neither.
    public static int BoundaryRuns(eGameState state, int runsBefore, int runsNow)
    {
        if (state != eGameState.InGame_BallPastBoundary)
            return 0;
        int offTheBat = runsNow - runsBefore;
        return offTheBat == 4 || offTheBat == 6 ? offTheBat : 0;
    }

    private void Finish()
    {
        EndSteps();
        if (contact != null)
            contact.Disarm();
        if (partnersBall)
        {
            Debug.Log(LogLine());
            if (PartnerBatter.Instance != null)
                PartnerBatter.Instance.BackToStance();
        }
        partnersBall = prepared = HasPlan = false;
        striker = null;
    }

    private string LogLine()
    {
        FieldResult p = shotPlan.predicted;
        string meant = !HasPlan ? "-" : $"{shotPlan.shot}/{shotPlan.foot}{(shotPlan.edge ? " edge" : "")}";
        string predicted = p.caught ? "caught" : p.runs.ToString();
        // The intent played: a wide's is LeaveWide, whatever was drawn at the run-up.
        return $"[Partner] intent={IntentText(HasPlan ? shotPlan.intent : intent)} shot={meant} wicket={shotPlan.wicket} predicted={predicted} " +
               $"contact={(contact != null && contact.Struck ? "yes" : "no")} got={got ?? "dead"} " +
               $"search={stepsRun} steps {searchMs:F1} ms";
    }

    /// An intent as the log shows it: W, a number of runs, or wide (left alone).
    public static string IntentText(int intent) =>
        intent == CareerSim.Wicket ? "W" : intent == PartnerIntent.LeaveWide ? "wide" : intent.ToString();

    // ---- What the planner is given ---------------------------------------------------------------

    private float OffSign() => main != null && main.BattingStyle == eBattingStyle.LeftHanded ? -1f : 1f;

    private static eBowlingLevel Level()
    {
        MatchPlan plan = CareerMatchSession.Plan;
        return plan != null ? plan.bowling : MatchSetup.Bowling;
    }

    private static float Aggression(HUD hud) =>
        CareerSim.Aggression(hud.Runs, hud.Wickets, hud.Overs * 6 + hud.Balls, hud.TotalOvers * 6, MatchFlow.Target);

    /// The field as the planner needs it: every fielder where he stands and how fast he runs, the
    /// bowler at the end of his follow-through (he joins the field only once the ball is hit), the
    /// keeper, the rope, how long they take to react, and the player's head at the other end.
    private FieldSnapshot Snapshot(float offSign)
    {
        var at = new List<Vector3>();
        var speed = new List<float>();
        float runSpeed = AnimatedFielder.BaseRunSpeed * Mathf.Max(0.5f, main.fielderSpeed);
        foreach (AnimatedFielder f in AnimatedFielder.All)
        {
            if (f == null || !f.isActiveAndEnabled || f.IsBowler)
                continue;
            // His mark for this ball (FieldCaptain sets it, ReturnToStart puts him there), not
            // wherever he is on the walk to it.
            at.Add(f.StartPosition);
            speed.Add(f.RunSpeed);
        }
        float bowlerZ = AnimatedBowler.Instance != null ? Mathf.Clamp(AnimatedBowler.Instance.transform.position.z, -1.5f, 1.5f) : 0f;
        at.Add(new Vector3(BowlerFinishX, 0f, bowlerZ));
        speed.Add(runSpeed);
        int keeper = -1;
        if (main.theKeeper != null)
        {
            // The keeper runs slower in his pads (KeeperFetch): his own speed, as the fielding plan has it.
            KeeperCatcher k = main.theKeeper.GetComponentInChildren<KeeperCatcher>();
            keeper = at.Count;
            at.Add(k != null ? k.Position : main.theKeeper.transform.position);
            speed.Add(k != null ? k.RunSpeed : runSpeed);
        }
        Camera eye = Camera.main;
        return new FieldSnapshot
        {
            fielders = at.ToArray(),
            runSpeed = speed.ToArray(),
            keeperIndex = keeper,
            boundary = CatchJudge.Boundary,
            offSign = offSign,
            delay = AnimatedFielderManagement.SeeingDelay + AnimatedFielderManagement.ReactionTime,
            nonStrikerHead = eye != null ? eye.transform.position
                                         : new Vector3(EndPose.NonStrikerHeadXZ.x, 1.65f, EndPose.NonStrikerHeadXZ.y),
        };
    }
}
