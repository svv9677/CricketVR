using UnityEngine;

/// <summary>
/// The practice nets (NetsSceneBuilder puts this on the "Practice Nets" root). The nets have no
/// keeper, so this does the two things he does for a ball the batter does not hit, and gives the
/// netting its sound:
///   * the sound: the ball into any of the nets plays "net" (Tools/sfx/build_net_sounds.py) where
///     it went in, louder the harder it came in;
///   * missed: a delivery still being bowled (not touched, not bowled, not already called wide)
///     that gets MissedPastStumps beyond the batter's stumps is called missed, as the keeper's take
///     would call it, so it is a ball faced (BallLedger) and not a dead ball;
///   * gathered: once a ball is missed (that, a wide, or Bat's automatic call on a hit in the nets)
///     it is stopped and held still where it is once it has come to rest, or once GatherAfter
///     seconds are up and it is rolling on the ground (never in the air), as if picked up - Main
///     waits for the ball to be held (WaitForReturnThenReset: the keeper's gloves in a match)
///     before the next one, and gives up waiting on its own after MaxReturnWait.
/// None of this happens while a keeper is on the field (a scene with one keeps his rules).
/// </summary>
public class NetsBackstop : MonoBehaviour
{
    /// How far past the batter's stumps (m) an untouched delivery is called missed: about where a
    /// keeper standing up would take it, short of the back net.
    public const float MissedPastStumps = 1.2f;
    /// A missed ball is gathered once it is this slow (m/s), or this long (s) after the call.
    private const float GatherSpeed = 0.6f, GatherAfter = 2.5f;
    /// A ball's centre below this (m) is on the ground (rolling), not in the air.
    private const float OnTheGround = 0.3f;
    /// Speed into the net (m/s) at full volume; the softest sound's volume; slower than this, and
    /// within MinGap of the last, nothing (a ball rolling along the bottom of the net).
    private const float FullVolumeImpact = 18f, MinVolume = 0.15f, Quietest = 0.8f, MinGap = 0.07f;

    private Main main;
    private Collider[] nets;
    private float lastSound = -1f, missedAt = -1f;

    private void Start()
    {
        main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        nets = GetComponentsInChildren<Collider>(true);
        Ball.Collided += OnBallCollided;
        if (main != null)
            main.onGameStateChanged += OnState;
    }

    private void OnDestroy()
    {
        Ball.Collided -= OnBallCollided;
        if (main != null)
            main.onGameStateChanged -= OnState;
    }

    private bool Keeperless => main != null && (main.theKeeper == null || !main.theKeeper.activeInHierarchy);

    private void OnBallCollided(Collision c, float impact)
    {
        if (!IsNet(c.collider) || impact < Quietest || Time.time - lastSound < MinGap)
            return;
        lastSound = Time.time;
        float volume = Mathf.Lerp(MinVolume, 1f, Mathf.Clamp01(impact / FullVolumeImpact));
        GameSfx.Play("net", c.GetContact(0).point, GameAudio.Channel.Stumps, volume,
                     Random.Range(0.93f, 1.07f), 1.5f, 50f);
    }

    private bool IsNet(Collider c)
    {
        if (c == null || nets == null)
            return false;
        foreach (Collider n in nets)
            if (n == c)
                return true;
        return false;
    }

    private void OnState()
    {
        eGameState s = main.gameState;
        if (s == eGameState.InGame_BallMissed)
            missedAt = Time.time;
        else if (s != eGameState.InGame_BallMissedLoop)
            missedAt = -1f;
    }

    private void Update()
    {
        if (!Keeperless || main.theBall == null || main.theBallRigidBody == null)
            return;
        eGameState s = main.gameState;
        if (s == eGameState.InGame_DeliverBallLoop &&
            main.theBall.transform.position.x > BallDelivery.BatsmanStumpsX + MissedPastStumps)
        {
            main.gameState = eGameState.InGame_BallMissed;
            return;
        }
        if (s == eGameState.InGame_BallMissedLoop && missedAt >= 0f)
            Gather(main.theBallRigidBody);
    }

    private void Gather(Rigidbody ball)
    {
        if (ball.isKinematic)
            return;
        // Still: gather it. Still moving: only once GatherAfter is up and it is on the ground, so a
        // skier is never held up in the sky; one still in the air waits for Main's own give-up.
        bool still = ball.linearVelocity.magnitude <= GatherSpeed;
        bool down = ball.position.y < OnTheGround;
        if (!still && (Time.time - missedAt < GatherAfter || !down))
            return;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isKinematic = true;
    }
}
