using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


/// <summary>
/// Main's game-state machine: one step per frame (Update calls StepGameState unless the debug
/// menu is open), the delivery itself (DeliverBall), and bowling the next ball. Split out of
/// Main.cs on 2026-11-05; the code is unchanged.
/// </summary>
public partial class Main
{
    private void StepGameState()
    {
    switch(gameState)
    {
        case eGameState.None:
            {
                // For now, skip the menus
                gameState = eGameState.InGame_ResetToReady;
            }
            break;
        case eGameState.InMenu:
            {

            }
            break;
        case eGameState.InGame_Ready:
            {
                if (GetButton(XRButton.A))
                    StartNextBall();
            }
            break;
        case eGameState.InGame_SelectDelivery:
            {
                // Get current bowler from Score keeper
                // Will he overstep? Is it a free hit? (NoBalls)
                NoBalls.NewDelivery(() => UnityEngine.Random.value);
                // The bowler's over plan: his stock ball or one of his variations (BowlingAttack).
                currentBowlingConfig = BowlingAttack.Next(theHUD, bowlingProfileManager);
                // Each ball starts from the normal gap; a boundary or a keeper's miss sets its own.
                resetDelay = NextBallDelay;
                CameraReplay.Instance.ResetForNextBall();
                ShotDistance.Instance.setText("");
                BallSpeed.Instance.setText("");
                // TODO: Move bowling machine

                // For now, switch directly to loop state
                gameState = eGameState.InGame_SelectDeliveryLoop;
            }
            break;
        case eGameState.InGame_SelectDeliveryLoop:
            {
                // TODO: Add the coroutines & tweening waits
                // For now, switch to deliver ball state
                // Uncomment the below line to release the ball straight away without waiting for the animation.
                //gameState = eGameState.InGame_DeliverBall;
            }
            break;
            case eGameState.InGame_DeliverBall:
                DeliverBall();
                break;
        case eGameState.InGame_DeliverBallLoop:
            {
                // Nothing to do here for now!
                // Bat collision with the ball or
                // Ball collision with the stumps or
                // Ball collision with the keeper (invisible collider)
                // will trigger next state change
            }
            break;
        case eGameState.InGame_BallHit:
            {
                // TODO: Add any special processing needed here
                // For now, switch to next state
                gameState = eGameState.InGame_BallHitLoop;
            }
            break;
        case eGameState.InGame_BallHitLoop:
            {
                // Nothing to do here for now!
                // Fielder collision with the ball or
                // Boundary collision with the ball
                // will trigger next state change
            }
            break;
        case eGameState.InGame_BallFielded:
            {
                // TODO: Add any special processing needed here
                // For now, switch to next state
                gameState = eGameState.InGame_BallFielded_Loop;
                // The replay waits for the dead ball: filmed while the ball is in play, it
                // cost frames while the player was still watching the shot. It shows the
                // fielder gathering it, two seconds on.
                CameraReplay.Instance.ShowReplayIn(2f);

                // Reset once the ball is back with the bowler (ThrowBack).
                StartCoroutine(WaitForReturnThenReset());
            }
            break;
        case eGameState.InGame_BallFielded_Loop:
            {
                // Nothing to do here for now!
            }
            break;
        case eGameState.InGame_Bowled:
            {
                // TODO: Add any special processing needed here
                // For now, switch to next state
                CameraReplay.Instance.ShowReplayIn(2f);
                gameState = eGameState.InGame_BowledLoop;

                // Wait for '2' seconds and reset to Ready - longer for a wicket, while the bowling
                // side celebrates (WicketMoment).
                StartCoroutine(WaitAndSetGameState(DeadBallPause + (WicketMoment.Active ? WicketMoment.ExtraPause : 0f), eGameState.InGame_ResetToReady));
            }
            break;
        case eGameState.InGame_BowledLoop:
            {
                // Nothing to do here for now!
            }
            break;
        case eGameState.InGame_BallMissed:
            {
                // TODO: Add any special processing needed here
                // For now, switch to next state
                // A miss or a wide is called before the keeper has it: the replay runs on to
                // his take (the ball still in his gloves), and two seconds after.
                CameraReplay.Instance.ShowReplayWhen(() => theBallRigidBody.isKinematic, 2f, 6f);
                gameState = eGameState.InGame_BallMissedLoop;

                // Reset once the keeper has passed it back to the bowler (ThrowBack).
                StartCoroutine(WaitForReturnThenReset());
            }
            break;
        case eGameState.InGame_BallMissedLoop:
            {
                // Nothing to do here for now!
            }
            break;
        case eGameState.InGame_BallPastBoundary:
            {
                // TODO: Add any special processing needed here
                // For now, switch to next state
                gameState = eGameState.InGame_BallPastBoundaryLoop;
                // A four: two seconds past the rope. A six: on until it comes down in the
                // crowd (or is out of the camera's sight), then a second more.
                if (theBallScript.bounced)
                    CameraReplay.Instance.ShowReplayIn(2f);
                else
                    CameraReplay.Instance.ShowReplayWhen(() => theBall.transform.position.y < 1.5f ||
                        theBallRigidBody.linearVelocity.sqrMagnitude < 9f ||          // came down in the stands
                        new Vector2(theBall.transform.position.x, theBall.transform.position.z).magnitude > 110f, 1f, 4f);

                // Wait for '2' seconds and reset to Ready
                StartCoroutine(WaitAndSetGameState(DeadBallPause, eGameState.InGame_ResetToReady));
            }
            break;
        case eGameState.InGame_BallPastBoundaryLoop:
            {
                // Nothing to do here for now!
            }
            break;
        case eGameState.InGame_ResetToReady:
            {
                gameState = eGameState.InGame_ResetToReadyLoop;

                // Wait for 'ResetDelay' seconds and reset to Ready
                StartCoroutine(WaitAndSetGameState(resetDelay, eGameState.InGame_Ready));
            }
            break;
        case eGameState.InGame_ResetToReadyLoop:
            {
                // Nothing to do here for now!
            }
            break;

        default:
            break;
    }
    }

    /// Bowl the next ball (the A button, or "Next Ball" on the between-balls panel).
    public void StartNextBall()
    {
        // Not through the next-over card: the bowler is announced first (OverBreak). And not once
        // the match is over (MatchFlow). Nor while a career match is catching up (MatchGate).
        if (gameState != eGameState.InGame_Ready || OverBreak.Blocking || MatchFlow.Over || MatchGate.Blocking)
            return;
        currentFielderName = "";
        Extras.Clear();

        // Reset stumps - both ends
        foreach (Stumps stumps in FindObjectsByType<Stumps>(FindObjectsSortMode.None))
            stumps.Reset();

        StopTheBall();

        // Switch to the process of selecting delivery type, stride & other params before delivery loop
        gameState = eGameState.InGame_SelectDelivery;
    }


    /// The ball leaves the bowler's hand (the release animation event set InGame_DeliverBall).
    private void DeliverBall()
    {
        // The umpire calls a front-foot no-ball as the ball is bowled.
        NoBalls.Released();
        // Remove hand parent
        theBall.transform.SetParent(null);
        Vector3 torque = new Vector3(currentBowlingConfig.torqueX, 0f, 0f);

        // Take the release point from the bowler's hand, captured by the release
        // animation event itself (AnimatedBowler.ReleaseBall), not from the ball's
        // own transform. The ball is supposed to be sitting on the hand, so in the
        // healthy case these are the same point and nothing moves. They are not the
        // same when carrying the ball has failed - on device the ball was orbiting
        // the hand on a 12 m arm and its transform read (-1.36, 7.11, 8.10), eight
        // metres off the pitch and seven in the air. The hand is the thing the
        // player watches, so it is the thing the delivery should start from.
        Vector3 releaseFrom = theBall.transform.position;
        AnimatedBowler bowler = AnimatedBowler.Instance;
        if (bowler != null && bowler.HasReleasePosition)
            releaseFrom = bowler.ReleasePosition;

        // Work out the whole release from one validated solve (see BallDelivery).
        // speedX is an impulse against the configs' 0.2 kg ball; `length` is the
        // world X the ball pitches at; speedZ is the line at the batsman's stumps,
        // off side positive, so mirror it for a left-hander.
        float offSign = battingStyle == eBattingStyle.LeftHanded ? -1f : 1f;
        float speedMps = currentBowlingConfig.speedX / Constants.ConfigImpulseMass;
        float targetLine = offSign * currentBowlingConfig.speedZ;
        // A left-arm spinner's turn and drift are his fingers' mirror image (BowlerArm.EffectSign).
        bool leftArm = bowler != null && bowler.LeftArm;
        BallFlight.DeliveryEffects effects = currentBowlingConfig.Effects(offSign * BowlerArm.EffectSign(currentBowlingConfig.swingType, leftArm));
        theBallScript.deliveryEffects = effects;
        BallDelivery.Solution delivery = BallDelivery.Solve(
            releaseFrom, currentBowlingConfig.length, speedMps, targetLine, effects,
            currentBowlingConfig.overHead ? BallDelivery.OverHeadBouncerHeight : BallDelivery.MaxHeightAtStumps);
        if (!string.IsNullOrEmpty(delivery.warnings))
            Debug.LogWarning($"[Delivery] {delivery}");
        else if (verboseStateLogging)
            Debug.Log($"[Delivery] {delivery}");

        // The collider is switched off while the bowler carries the ball, so that
        // teleporting it onto his hand cannot shove the stumps (see
        // AnimatedBowler.HoldBall). Switch it back on now the ball is live.
        Collider ballCollider = theBall.GetComponent<Collider>();
        if (ballCollider != null)
            ballCollider.enabled = true;

        // enable physics
        theBallRigidBody.isKinematic = false;
        //Set collision type to continuous dynamic
        theBallRigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Set interpolation mode to interpolate
        theBallRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        // Put the ball exactly on the solved release point. This is safe to do
        // unconditionally now that the point comes from the hand rather than from
        // the ball: on a healthy delivery the ball is already there and this moves
        // it by nothing, so there is no snap. When carrying the ball has gone
        // wrong it is the correction that keeps the delivery sane.
        theBall.transform.position = delivery.releasePosition;
        // And the body itself: autoSyncTransforms is off, so the transform write
        // alone left PhysX starting the ball from where the hand was on the last
        // physics step - measured up to 0.24 m behind, pitching that much short.
        theBallRigidBody.position = delivery.releasePosition;

        // ASSIGN both velocities rather than AddForce onto whatever the body was
        // carrying. While parented to the bowler's hand the ball is kinematic, and
        // a kinematic body carries its hand motion (~14-16 m/s, mostly downward)
        // across the switch to dynamic. Assignment overwrites; AddForce would add.
        // Zero first, then set. The assignment alone is already sufficient (it
        // overwrites rather than accumulates), but zeroing explicitly makes the
        // intent obvious and costs nothing.
        theBallRigidBody.linearVelocity = Vector3.zero;
        theBallRigidBody.angularVelocity = Vector3.zero;
        // delivery.releaseVelocity is the old `speed / theBallRigidBody.mass`,
        // but solved against a validated release point (see BallDelivery).
        theBallRigidBody.linearVelocity = delivery.releaseVelocity;
        theBallRigidBody.AddTorque(torque, ForceMode.Impulse);
        
        // save it
        //theBallScript.lastVelocity = speed;   (commented out because the lastVelocity for the bat collision equations should be updated after the ball hits the pitch.
        // mark as fresh delivery!
        theBallScript.fresh = true;
        theBallScript.lastPitchPoint = new Vector3(float.NaN, 0f, 0f);
        theBallScript.lastLineAtStumps = float.NaN;
        lastDelivery = delivery;
        theBallScript.bounced = false;
        theBallScript.wide = false;
        theBatScript.hasHitBall = false;

        StartCoroutine(BallSpeed.Instance.updateBallSpeed());
        gameState = eGameState.InGame_DeliverBallLoop;
    }

    private void StopTheBall()
    {
        // pause the particles
        //theBallScript.myParticles.Stop();
        //theBallScript.myParticles.Clear();  PARTICLE
        theBallScript.myParticles.Clear();
        theBallScript.myParticles.enabled = false;
        // Stop the ball BEFORE making it kinematic. Setting velocity on a kinematic body is a
        // no-op (Unity even warns about it), which is what let the ball carry its motion into
        // the next delivery.
        //
        // The ball has usually already been parked as kinematic by the time we get here - by the
        // boundary, a fielder or the keeper - so it has to be made dynamic again first or the
        // zeroing below is silently discarded. That was still happening: every delivery logged
        // "Setting angular velocity of a kinematic body is not supported" from this line.
        theBallRigidBody.isKinematic = false;
        theBallRigidBody.linearVelocity = Vector3.zero;
        theBallRigidBody.angularVelocity = Vector3.zero;
        // disable physics
        theBallRigidBody.isKinematic = true;
        // Take the collider out of the world before teleporting. This can be a long jump - from
        // the boundary rope back to the bowler's mark - and PhysX treats a kinematic body's
        // teleport as motion, so an enabled collider sweeps everything on the way and knocks the
        // stumps over. Re-enabled when the ball is delivered.
        Collider parkedCollider = theBall.GetComponent<Collider>();
        if (parkedCollider != null)
            parkedCollider.enabled = false;
        // reset ball position to inside machine
        theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);
        theBallRigidBody.position = theBall.transform.position;
    }

    /// Longest the next ball waits for a missed ball to be in the keeper's gloves.
    public const float MaxReturnWait = 10f;
    /// A beat after the ball is in someone's hands before the next ball is set up.
    public const float InHandsPause = 0.3f;

    /// The dead-ball pause, then the next ball is offered as soon as someone has the ball in his
    /// hands - the fielder who picked it up, or the keeper - while the throw in and the relay home
    /// (ThrowBack) carry on behind. It used to wait for the keeper to receive the throw. The
    /// fielders are not reset under the throw: walking back leaves anyone throwing, holding or
    /// expecting the ball alone.
    private IEnumerator WaitForReturnThenReset()
    {
        bool extrasBefore = Extras.InPlay;
        yield return new WaitForSeconds(InHandsPause);
        float giveUp = Time.time + MaxReturnWait;
        // A missed ball still loose (not yet in the keeper's gloves): wait for him to have it, or
        // for it to get past him and be played on for byes.
        while (!theBallRigidBody.isKinematic && Time.time < giveUp && Extras.InPlay == extrasBefore)
            yield return null;
        // It got past the keeper meanwhile (a wide he missed) and is being played on for byes
        // (Extras): that play ends the ball, not this wait. Resetting here cut a wide's byes off
        // a second in, never scored (review).
        if (Extras.InPlay && !extrasBefore)
            yield break;
        // A catch: the bowling side celebrates before the next ball (WicketMoment).
        if (WicketMoment.Active)
            yield return new WaitForSeconds(WicketMoment.ExtraPause);
        gameState = eGameState.InGame_ResetToReady;
    }

    public IEnumerator WaitAndSetGameState(float delay, eGameState state)
    {
        yield return new WaitForSeconds(delay);

        if (verboseStateLogging)
            Debug.Log("Setting GameState from: " + gameState.ToString() + " to: " + state.ToString());
        gameState = state;
    }
}
