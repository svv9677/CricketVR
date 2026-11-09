using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves the career player between the ends as the strike changes while he is watching his partner
/// (phase 3): a short blink to black, the rig placed at the other end, his bat off or on, the
/// partner to the crease or to the non-striker's spot. The partner's balls are not bowled on their
/// own: the next-ball card comes up at the non-striker's end and A (or Next ball) bowls, as at the crease.
///
/// Built with a career match, and only when the player's "Partner's innings" is Watch
/// (CareerProfile.watchPartner); with Skip there is none, and the match cuts from his ball to his
/// next as in phase 2. Inert while PartnerWatch.Live stays false.
///
///   * The ends change between balls - at ResetToReady (the strike already changed at the dead ball)
///     or at Ready, or as soon as Live changes while the game waits there - behind its own black
///     quad on Camera.main: 0.25 s to black, a hold while everything moves, 0.25 s back, in real
///     time (pause-proof). It is not Rao's SceneFader, which belongs to scene loads.
///   * At black: PlayerEnd.Set, XRRigSetup.PlaceAtEnd, Bat.ContactSuspended (his bat must not
///     touch the partner's ball), PartnerBatter.TakeStrike or StandAtNonStriker.
/// </summary>
public class NonStrikerView : MonoBehaviour
{
    public const float FadeSeconds = 0.25f, HoldSeconds = 0.2f;
    public const float BlinkSeconds = FadeSeconds + HoldSeconds + FadeSeconds;

    /// Mid-blink (the screen going to or coming back from black).
    public static bool Blinking { get; private set; }

    private Main main;
    private bool subscribed;
    private XRRigSetup rig;

    private readonly ViewBlack black = new ViewBlack();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Blinking = false;

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
        if (scene.name != CareerMatchSession.MatchScene || !CareerMatchSession.Active || !Watching(CareerSaves.Current) ||
            FindFirstObjectByType<NonStrikerView>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Non-Striker View", typeof(NonStrikerView)), scene);
    }

    /// The player wants to watch his partner bat (the hub's "Partner's innings: Watch").
    public static bool Watching(CareerProfile p) => p != null && p.watchPartner;

    private void Start()
    {
        main = Main.Instance;
        if (main == null)
            return;
        main.onGameStateChanged += OnGameState;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed && main != null)
            main.onGameStateChanged -= OnGameState;
        Blinking = false;
        Bat.ContactSuspended = false;
        black.Dispose();
    }

    private void OnGameState()
    {
        eGameState s = main.gameState;
        if (s == eGameState.InGame_ResetToReady || s == eGameState.InGame_Ready)
            CheckEnd();
    }

    private void Update()
    {
        if (main == null)
            return;
        // Live can also change while the game waits between balls (the catch-up card, the partner
        // being built): looked at every frame there, a static read.
        if (BetweenBalls(main.gameState))
            CheckEnd();
    }

    /// Blink to the other end if the player is at the wrong one; else just keep the partner at his.
    private void CheckEnd()
    {
        // Not once the match is over: its result card is in front of him at this end.
        if (Blinking || MatchFlow.Over)
            return;
        // Not with a panel open: the rig would move about 19 m and leave it at the old end. The
        // per-frame check between balls tries again once it closes.
        if (PanelOpen())
            return;
        if (PartnerWatch.Live != PlayerEnd.NonStriker || (PlayerEnd.NonStriker && placedSide != EndPose.NonStrikerSide))
            StartCoroutine(Blink());   // the other end, or the other side of it for a new bowler's arm
        else
            SyncPartner();
    }

    /// The side of the stumps he was last put at the non-striker's end (EndPose.NonStrikerSide).
    private float placedSide = -1f;

    // ---- The blink -------------------------------------------------------------------------------

    private IEnumerator Blink()
    {
        Blinking = true;
        bool visible = black.Ensure();
        float start = Time.unscaledTime;
        bool moved = false;
        while (true)
        {
            float t = Time.unscaledTime - start;
            if (!moved && t >= FadeSeconds)
            {
                moved = true;
                // The clock restarts the hold from black, however long the frame was.
                start = Time.unscaledTime - FadeSeconds;
                t = FadeSeconds;
                black.Set(1f, visible);
                // Read again at black: the strike may have changed while the screen went dark, a
                // panel may have opened (it would be left 19 m behind), the match may have ended.
                // Not moved: the per-frame check between balls tries again.
                if (CanChangeEnds(main.gameState) && !PanelOpen() && !MatchFlow.Over)
                    MoveTo(PartnerWatch.Live);
            }
            black.Set(BlinkAlpha(t), visible);
            if (t >= BlinkSeconds)
                break;
            yield return null;
        }
        Blinking = false;
    }

    /// The player, his bat and the partner to their places for this end.
    private void MoveTo(bool nonStriker)
    {
        PlayerEnd.Set(nonStriker);
        if (rig == null)
            rig = FindFirstObjectByType<XRRigSetup>();
        if (rig != null)
            rig.PlaceAtEnd(nonStriker);
        placedSide = EndPose.NonStrikerSide;
        Bat.ContactSuspended = nonStriker;
        SyncPartner();
    }

    /// The partner on strike exactly when the player is at the non-striker's end.
    private static void SyncPartner()
    {
        PartnerBatter partner = PartnerBatter.Instance;
        if (partner == null || !partner.Ready)
            return;
        if (partner.OnStrike == PlayerEnd.NonStriker)
        {
            partner.KeepNonStrikerSide();
            return;
        }
        if (PlayerEnd.NonStriker)
            partner.TakeStrike();
        else
            partner.StandAtNonStriker();
    }

    /// A panel or replay in front of the player at this end: settings, grip calibration, the over
    /// break card, a shot replay, the career's catch-up card ("You come in at 28/4": MatchGate).
    private bool PanelOpen()
    {
        ShotReplay replay = ShotReplay.Instance;
        return main.SettingsOpen || main.CalibratingGrip || OverBreak.Showing || MatchGate.Blocking ||
               (replay != null && replay.IsPlaying);
    }

    // ---- The black quad ---------------------------------------------------------------------------

    /// A black quad just in front of Camera.main, drawn last and over everything; built the first
    /// time it is needed. False when there is no camera (the ends still change, unseen).
    // ---- Rules (pure; PlayerEndTests) -------------------------------------------------------------

    /// How black the view is `t` seconds into a blink: in over FadeSeconds, held, out over FadeSeconds.
    public static float BlinkAlpha(float t)
    {
        if (t <= 0f || t >= BlinkSeconds)
            return 0f;
        if (t < FadeSeconds)
            return t / FadeSeconds;
        if (t <= FadeSeconds + HoldSeconds)
            return 1f;
        return 1f - (t - FadeSeconds - HoldSeconds) / FadeSeconds;
    }

    /// The ball is dead and the next not yet asked for: when a blink may start.
    public static bool BetweenBalls(eGameState s) =>
        s == eGameState.InGame_Ready || s == eGameState.InGame_ResetToReady || s == eGameState.InGame_ResetToReadyLoop;

    /// When the ends may still change at black: between balls, or the next ball asked for but not
    /// yet bowled (A pressed during the blink) - the bowler is still running in.
    public static bool CanChangeEnds(eGameState s) =>
        BetweenBalls(s) || s == eGameState.InGame_SelectDelivery || s == eGameState.InGame_SelectDeliveryLoop;
}
