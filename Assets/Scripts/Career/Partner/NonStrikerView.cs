using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves the career player between the ends as the strike changes while he is watching his partner
/// (phase 3): a short blink to black, the rig placed at the other end, his bat off or on, the
/// partner to the crease or to the non-striker's spot; and bowls the next ball on its own while the
/// partner is on strike.
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
///   * The partner's ball comes 1.5 s after Ready (and after any blink), unless something is in the
///     way - settings, grip calibration, the over break, the catch-up gate, a replay - in which case
///     it waits until 1.5 s after that clears. Main.StartNextBall still checks its own gates.
/// </summary>
public class NonStrikerView : MonoBehaviour
{
    public const float FadeSeconds = 0.25f, HoldSeconds = 0.2f, AutoBowlDelay = 1.5f;
    public const float BlinkSeconds = FadeSeconds + HoldSeconds + FadeSeconds;

    /// Mid-blink (the screen going to or coming back from black).
    public static bool Blinking { get; private set; }

    private Main main;
    private bool subscribed;
    private float bowlAt;
    private XRRigSetup rig;

    private GameObject blackQuad;
    private MeshRenderer blackRenderer;
    private Material blackMaterial;
    private Mesh blackMesh;

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
        bowlAt = Time.unscaledTime + AutoBowlDelay;
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
        if (blackQuad != null) Destroy(blackQuad);
        if (blackMaterial != null) Destroy(blackMaterial);
        if (blackMesh != null) Destroy(blackMesh);
    }

    private void OnGameState()
    {
        eGameState s = main.gameState;
        if (s == eGameState.InGame_Ready)
            bowlAt = Time.unscaledTime + AutoBowlDelay;
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
        AutoBowl();
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
        if (PartnerWatch.Live != PlayerEnd.NonStriker)
            StartCoroutine(Blink());
        else
            SyncPartner();
    }

    // ---- The blink -------------------------------------------------------------------------------

    private IEnumerator Blink()
    {
        Blinking = true;
        bool visible = EnsureBlack();
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
                SetBlack(1f, visible);
                // Read again at black: the strike may have changed while the screen went dark, a
                // panel may have opened (it would be left 19 m behind), the match may have ended.
                // Not moved: the per-frame check between balls tries again.
                if (CanChangeEnds(main.gameState) && !PanelOpen() && !MatchFlow.Over)
                    MoveTo(PartnerWatch.Live);
            }
            SetBlack(BlinkAlpha(t), visible);
            if (t >= BlinkSeconds)
                break;
            yield return null;
        }
        Blinking = false;
        bowlAt = Time.unscaledTime + AutoBowlDelay;
    }

    /// The player, his bat and the partner to their places for this end.
    private void MoveTo(bool nonStriker)
    {
        PlayerEnd.Set(nonStriker);
        if (rig == null)
            rig = FindFirstObjectByType<XRRigSetup>();
        if (rig != null)
            rig.PlaceAtEnd(nonStriker);
        Bat.ContactSuspended = nonStriker;
        SyncPartner();
    }

    /// The partner on strike exactly when the player is at the non-striker's end.
    private static void SyncPartner()
    {
        PartnerBatter partner = PartnerBatter.Instance;
        if (partner == null || !partner.Ready || partner.OnStrike == PlayerEnd.NonStriker)
            return;
        if (PlayerEnd.NonStriker)
            partner.TakeStrike();
        else
            partner.StandAtNonStriker();
    }

    // ---- Bowling the partner's ball ---------------------------------------------------------------

    private void AutoBowl()
    {
        eGameState s = main.gameState;
        bool live = PartnerWatch.Live;
        if (s != eGameState.InGame_Ready || !live || !PlayerEnd.NonStriker)
            return;
        float now = Time.unscaledTime;
        if (Held())
        {
            bowlAt = Mathf.Max(bowlAt, now + AutoBowlDelay);
            return;
        }
        if (!MayAutoBowl(s, live, PlayerEnd.NonStriker, false, now, bowlAt))
            return;
        // Again in 1.5 s should the ball somehow not start (a gate of Main's own).
        bowlAt = now + AutoBowlDelay;
        main.StartNextBall();
    }

    /// Something the player is doing, or the match is showing, comes before the next ball.
    private bool Held() =>
        Blinking || PanelOpen() || OverBreak.Blocking || MatchFlow.Over || MatchGate.Blocking;

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
    private bool EnsureBlack()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        if (blackQuad != null && blackQuad.transform.parent == cam.transform)
            return true;
        if (blackMaterial == null)
        {
            // Both in the build's always-included shaders; UI/Default's depth test can be switched off.
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return false;
            blackMaterial = new Material(shader) { name = "Blink", renderQueue = 5000, color = new Color(0f, 0f, 0f, 0f) };
            blackMaterial.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        }
        if (blackMesh == null)
            blackMesh = QuadMesh();
        if (blackQuad == null)
        {
            blackQuad = new GameObject("Blink", typeof(MeshFilter), typeof(MeshRenderer));
            blackQuad.GetComponent<MeshFilter>().sharedMesh = blackMesh;
            blackRenderer = blackQuad.GetComponent<MeshRenderer>();
            blackRenderer.sharedMaterial = blackMaterial;
            blackRenderer.shadowCastingMode = ShadowCastingMode.Off;
            blackRenderer.receiveShadows = false;
            blackRenderer.enabled = false;
        }
        blackQuad.layer = FirstLayer(cam.cullingMask);
        blackQuad.transform.SetParent(cam.transform, false);
        // Just past the near plane, and wide enough to fill any headset's view from there.
        float z = Mathf.Max(cam.nearClipPlane * 1.5f, 0.02f);
        blackQuad.transform.localPosition = new Vector3(0f, 0f, z);
        blackQuad.transform.localRotation = Quaternion.identity;
        blackQuad.transform.localScale = Vector3.one * (z * 6f);
        return true;
    }

    /// Off when clear: a full-view transparent quad is not free on a Quest.
    private void SetBlack(float alpha, bool visible)
    {
        if (!visible || blackRenderer == null)
            return;
        blackMaterial.color = new Color(0f, 0f, 0f, alpha);
        blackRenderer.enabled = alpha > 0.001f;
    }

    /// Unit quad facing the camera, white vertex colours (both shaders multiply by them).
    private static Mesh QuadMesh()
    {
        var mesh = new Mesh { name = "Blink" };
        mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        return mesh;
    }

    private static int FirstLayer(int mask)
    {
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0)
                return layer;
        return 0;
    }

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

    /// The partner's next ball comes now: waiting at Ready, the partner on strike with the player at
    /// the other end, nothing in the way, and the delay run out.
    public static bool MayAutoBowl(eGameState s, bool live, bool atNonStriker, bool held, float now, float bowlAt) =>
        s == eGameState.InGame_Ready && live && atNonStriker && !held && now >= bowlAt;
}
