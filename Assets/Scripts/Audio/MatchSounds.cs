using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The sounds of a delivery and what follows it (2026-11-05: "add sounds for all the things going
/// on in the game... go through a delivery and see what the natural sounds are"):
///
///   run-up        footsteps on the grass, quickening; the delivery stride thumping into the crease
///   release       a quick's grunt of effort, a medium-pacer's lighter one, a spinner's breath
///   in the air    a whoosh as a quick ball goes past the player's head
///   pitching      leather on the hard strip; a duller thud on the outfield, and each bounce after
///   the bat       BatSounds (edge, good, middled; the bat on the ground)
///   the keeper    the pop of the gloves; a fielder's hands as he takes it, louder for a catch
///   fielding      every player's footsteps (Footsteps), the thud of a dive landing, a grunt on a
///                 hard throw, the ball into the gloves at the other end
///   the stumps    Stump's own sound, and the bails clattering off
///   the rope      the ball into the advertising boards
///   the crowd     over Rao's looping crowd (CrowdController): cheering a four, roaring a six or a
///                 wicket, an "ooh" when the ball beats the bat close to the stumps
///
/// Made for each scene that has a Main; it listens for events (Ball.Collided, BowlerGrip.Released,
/// AnimatedFielder.DiveStarted, Bail.KnockedOff, HUD.BallScored) and watches the ball, and plays
/// everything through GameSfx's pooled voices on the GameAudio channels.
/// </summary>
public class MatchSounds : MonoBehaviour
{
    private Main main;
    private Rigidbody ball;
    private bool wasKinematic;
    private Vector3 lastBallVelocity;
    private float lastBounceAt, lastBailsAt, lastBoardsAt;
    private float whooshMin = float.MaxValue;
    private bool whooshed;
    private float diveAt = -1f;
    private Vector3 divePos;
    private bool airborneSinceHit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        MakeFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => MakeFor(s);

    private static void MakeFor(Scene s)
    {
        if (Object.FindFirstObjectByType<Main>() == null || Object.FindFirstObjectByType<MatchSounds>() != null)
            return;
        new GameObject("Match Sounds").AddComponent<MatchSounds>();
    }

    private void Start()
    {
        main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        ball = main != null ? main.theBallRigidBody : null;
        // Footsteps for every player on the field (not the player: he has no feet in VR).
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!a.isHuman || a.GetComponent<Footsteps>() != null) continue;
            bool player = a.GetComponent<AnimatedFielder>() != null || a.GetComponent<AnimatedBowler>() != null ||
                          a.GetComponent<UmpireSignals>() != null || a.GetComponentInParent<KeeperCatcher>() != null ||
                          (main != null && main.theKeeper != null && a.transform.IsChildOf(main.theKeeper.transform));
            if (!player) continue;
            a.gameObject.AddComponent<Footsteps>().Init(a.GetComponent<UmpireSignals>() != null ? 0.4f : 1f);
        }
        if (FindFirstObjectByType<CrowdVoices>() == null && MatchSetup.Crowd != eCrowd.Empty)
            new GameObject("Crowd Voices").AddComponent<CrowdVoices>();
        Ball.Collided += OnBallCollided;
        BowlerGrip.Released += OnReleased;
        AnimatedFielder.DiveStarted += OnDive;
        Bail.KnockedOff += OnBail;
        HUD.BallScored += OnScored;
        if (main != null) main.onGameStateChanged += OnState;
    }


    private void OnDestroy()
    {
        Ball.Collided -= OnBallCollided;
        BowlerGrip.Released -= OnReleased;
        AnimatedFielder.DiveStarted -= OnDive;
        Bail.KnockedOff -= OnBail;
        HUD.BallScored -= OnScored;
        if (main != null) main.onGameStateChanged -= OnState;
    }

    // ---- The bowler --------------------------------------------------------------------------

    private void OnReleased(AnimatedBowler b, BowlingParams p)
    {
        if (b == null) return;
        Animator a = b.GetComponent<Animator>();
        Transform head = a != null ? a.GetBoneTransform(HumanBodyBones.Head) : b.transform;
        eBowlerKind k = p != null ? p.kind : eBowlerKind.Fast;
        if (BowlerRepertoire.IsSpin(k))
            GameSfx.Play("breath", head.position, GameAudio.Channel.Fielders, 0.5f, Random.Range(0.95f, 1.05f), 1f, 40f);
        else
            GameSfx.Play("grunt", head.position, GameAudio.Channel.Fielders, k == eBowlerKind.Fast ? 0.6f : 0.42f,
                         Random.Range(0.96f, 1.06f), 1.5f, 50f);
        whooshMin = float.MaxValue;
        whooshed = false;
    }

    // ---- The ball ----------------------------------------------------------------------------

    private void OnBallCollided(Collision c, float impact)
    {
        Vector3 at = c.GetContact(0).point;
        if (c.gameObject.CompareTag("Ground"))
        {
            if (impact < 1.2f || Time.time - lastBounceAt < 0.06f) return;
            lastBounceAt = Time.time;
            bool pitch = BallFlight.OnPitch(at);
            float vol = pitch ? Mathf.Clamp01(impact / 9f) : 0.85f * Mathf.Clamp01(impact / 10f);
            GameSfx.Play(pitch ? "bounce_pitch" : "bounce_grass", at, GameAudio.Channel.Stumps, vol,
                         Random.Range(0.95f, 1.05f), 1.5f, 70f);
            return;
        }
        // Into the boards (or anything else round the rope).
        if (new Vector2(at.x, at.z).magnitude > 45f && impact > 2f && Time.time - lastBoardsAt > 0.2f &&
            c.rigidbody == null)
        {
            lastBoardsAt = Time.time;
            GameSfx.Play("boards", at, GameAudio.Channel.Stumps, Mathf.Clamp01(impact / 15f), 1f, 3f, 110f);
        }
    }

    private void OnBail(Vector3 at)
    {
        if (Time.time - lastBailsAt < 0.3f) return;
        lastBailsAt = Time.time;
        GameSfx.Play("bails", at, GameAudio.Channel.Stumps, 0.8f, Random.Range(0.95f, 1.08f), 1.5f, 60f);
    }

    private void OnDive(Transform who, float flight)
    {
        diveAt = Time.time + flight * 0.85f;
        divePos = who.position;
    }

    private void Update()
    {
        if (main == null || ball == null) return;
        eGameState st = main.gameState;

        if (diveAt > 0f && Time.time >= diveAt)
        {
            diveAt = -1f;
            GameSfx.Play("dive", divePos, GameAudio.Channel.Fielders, 0.8f, Random.Range(0.92f, 1.05f), 1.5f, 50f);
        }

        if (st == eGameState.InGame_DeliverBallLoop || st == eGameState.InGame_BallMissed)
            Beaten();
        // Until it is past him: the ball is called missed before it reaches the player's head.
        if (!whooshed && (st == eGameState.InGame_DeliverBallLoop || st == eGameState.InGame_BallMissed ||
                          st == eGameState.InGame_BallMissedLoop))
            Whoosh();

        bool carried = AnimatedBowler.Instance != null && AnimatedBowler.Instance.CarryingBall;
        bool live = !carried && st != eGameState.InGame_SelectDelivery && st != eGameState.InGame_SelectDeliveryLoop &&
                    st != eGameState.InGame_Ready && st != eGameState.InGame_ResetToReady && st != eGameState.None;
        bool kin = ball.isKinematic;
        if (live && kin && !wasKinematic)
            Gathered();
        else if (live && !kin && wasKinematic && ball.linearVelocity.magnitude > 12f && st != eGameState.InGame_DeliverBall)
            Thrown();
        if (st == eGameState.InGame_BallHit) airborneSinceHit = true;
        if (main.theBallScript != null && main.theBallScript.bounced) airborneSinceHit = false;
        wasKinematic = kin;
        if (!kin) lastBallVelocity = ball.linearVelocity;
    }

    /// A quick ball past the head: when it goes by the player's camera close enough to hear.
    private void Whoosh()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 to = cam.transform.position - ball.position;
        float d = to.magnitude;
        whooshMin = Mathf.Min(whooshMin, d);
        bool passing = Vector3.Dot(ball.linearVelocity, to) < 0f;     // gone past
        if (passing)
        {
            whooshed = true;
            float speed = lastBallVelocity.magnitude;
            if (whooshMin < 1.4f && speed > 20f)
                GameSfx.Play("whoosh", ball.position, GameAudio.Channel.Stumps,
                             (1f - whooshMin / 1.4f) * Mathf.InverseLerp(20f, 40f, speed) + 0.15f, Random.Range(0.95f, 1.1f), 0.5f, 15f);
        }
    }

    /// The ball has stopped in someone's hands.
    private void Gathered()
    {
        Vector3 at = ball.position;
        float speed = lastBallVelocity.magnitude;
        bool keeper = NearestPlayer(at) is Footsteps f && f.GetComponentInParent<KeeperCatcher>() != null;
        if (keeper)
            GameSfx.Play("glove", at, GameAudio.Channel.Fielders, Mathf.Lerp(0.35f, 1f, speed / 30f), Random.Range(0.96f, 1.04f), 1.5f, 60f);
        else
            GameSfx.Play("hands", at, GameAudio.Channel.Fielders, (airborneSinceHit ? 0.9f : 0.5f) * Mathf.Lerp(0.6f, 1f, speed / 25f),
                         Random.Range(0.95f, 1.05f), 1.2f, 50f);
    }

    private Footsteps[] players;

    /// Who is nearest the ball (the one who has just taken it).
    private Footsteps NearestPlayer(Vector3 at)
    {
        if (players == null || players.Length == 0) players = FindObjectsByType<Footsteps>(FindObjectsSortMode.None);
        Footsteps best = null;
        float bestD = float.MaxValue;
        foreach (Footsteps f in players)
        {
            if (f == null) continue;
            float d = (f.transform.position - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = f; }
        }
        return best;
    }

    /// The ball has left a fielder's hand hard: his grunt.
    private void Thrown()
    {
        if (Random.value > 0.6f) return;     // not every throw
        GameSfx.Play("throw", ball.position + Vector3.up * 0.3f, GameAudio.Channel.Fielders, 0.45f, Random.Range(0.95f, 1.1f), 1.2f, 40f);
    }

    // ---- The crowd ---------------------------------------------------------------------------

    private void OnScored(BallOutcome o)
    {
        switch (o.Signal)
        {
            // The crowd's cheers are people, one by one round the stands (CrowdVoices).
            default: break;
        }
    }

    private void OnState()
    {
        if (main.gameState == eGameState.InGame_SelectDelivery)
            oohDone = false;
    }

    private bool oohDone;

    /// Beaten close to the stumps: "ooh!" - the moment the ball goes past the bat, not when the
    /// ball is called missed (at the keeper: the ooh came late, 2026-11-05).
    private void Beaten()
    {
        if (oohDone || main.theBatScript == null || main.theBatScript.hasHitBall) return;
        Vector3 p = ball.position;
        if (p.x < BallDelivery.BatsmanStumpsX - 0.5f) return;
        oohDone = true;
        bool close = Mathf.Abs(p.z) < 0.4f && p.y < 1.3f;
        if (close && !main.theBallScript.wide && Random.value < 0.65f)
            GameSfx.Play2D("crowd_ooh", GameAudio.Channel.Crowd, 0.6f, Random.Range(0.97f, 1.04f));
    }
}
