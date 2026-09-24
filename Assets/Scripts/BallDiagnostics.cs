using System.Linq;
using UnityEngine;

/// <summary>
/// TEMPORARY diagnostic instrumentation for the "balls start flying after the ball hits the
/// player's collider" report. Logs one compact line per interesting event, plus a full snapshot
/// of everything that persists across deliveries, so ball N can be compared with ball N+1.
/// Delete once the bug is found.
/// </summary>
public class BallDiagnostics : MonoBehaviour
{
    private eGameState _lastState = eGameState.None;
    private int _delivery;
    private float _stateEnteredAt;
    private bool _loggedRelease;
    private float _nextSample;
    private Vector3 _prevReleaseVel;

    private Main M => Main.Instance;

    [Tooltip("Bowl continuously without needing the A button, so a long sequence can be captured.")]
    public bool autoBowl;
    private float _autoBowlAt;
    private float _nextRunupSample;

    private void Update()
    {
        if (M == null) return;

        // Drive the same path the A button takes from InGame_Ready, so StopTheBall() and the
        // stump reset still happen exactly as they would in real play.
        if (autoBowl && M.gameState == eGameState.InGame_Ready && Time.time >= _autoBowlAt)
        {
            _autoBowlAt = Time.time + 1.0f;
            var stopBall = typeof(Main).GetMethod("StopTheBall",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (stopBall != null) stopBall.Invoke(M, null);
            Debug.Log($"[DIAG] auto-bowl: StopTheBall() invoked, ball now at {M.theBall.transform.position.ToString("F3")}");
            M.gameState = eGameState.InGame_SelectDelivery;
        }

        // During the run-up the ball should be parented to the bowler's hand and pinned to it.
        if (M.gameState == eGameState.InGame_SelectDeliveryLoop && Time.time >= _nextRunupSample)
        {
            _nextRunupSample = Time.time + 0.4f;
            var b = M.theBall.transform;
            var bowler = FindFirstObjectByType<AnimatedBowler>();
            string handInfo = "no bowler";
            if (bowler != null)
            {
                var hf = typeof(AnimatedBowler).GetField("hand",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                var hv = hf?.GetValue(bowler);
                Transform ht = hv as Transform ?? (hv as GameObject)?.transform;
                // The ball is carried by driving its world position onto the hand, so the number
                // that matters is how far it is from the hand. Anything above a few centimetres
                // means carrying the ball has failed and the release point will be wrong.
                float gap = ht == null ? -1f : Vector3.Distance(b.position, ht.position);
                handInfo = ht == null ? "hand null"
                    : $"hand='{ht.name}' handPos={ht.position.ToString("F2")} gap={gap:F3}m" +
                      (gap > 0.25f ? " <<< BALL NOT ON HAND" : "") +
                      $" bowlerPos={bowler.transform.position.ToString("F2")}";
            }
            Debug.Log($"[DIAG] runup: ballPos={b.position.ToString("F2")} " +
                      $"parent={(b.parent == null ? "none" : b.parent.name)} kin={M.theBallRigidBody.isKinematic}  {handInfo}");
        }

        if (M.gameState != _lastState)
        {
            Debug.Log($"[DIAG] state {_lastState} -> {M.gameState}   (held {Time.time - _stateEnteredAt:F2}s)");
            _lastState = M.gameState;
            _stateEnteredAt = Time.time;
            if (M.gameState == eGameState.InGame_DeliverBall) { _delivery++; _loggedRelease = false; }
        }
    }

    private void FixedUpdate()
    {
        if (M == null || M.theBall == null || M.theBallRigidBody == null) return;
        var rb = M.theBallRigidBody;
        var bs = M.theBallScript;

        if (!_loggedRelease && M.gameState == eGameState.InGame_DeliverBallLoop)
        {
            _loggedRelease = true;
            LogPersistentState(rb, bs);
            _prevReleaseVel = rb.linearVelocity;
        }

        bool inPlay = M.gameState == eGameState.InGame_DeliverBallLoop
                   || M.gameState == eGameState.InGame_BallHitLoop
                   || M.gameState == eGameState.InGame_BallMissedLoop;
        if (inPlay && Time.time >= _nextSample)
        {
            _nextSample = Time.time + 0.15f;
            Debug.Log($"[DIAG]   #{_delivery} t={Time.time - _stateEnteredAt:F2} pos={rb.position.ToString("F2")} " +
                      $"vel={rb.linearVelocity.ToString("F2")} |v|={rb.linearVelocity.magnitude:F2} " +
                      $"angV={rb.angularVelocity.magnitude:F1} fresh={bs.fresh} bounced={bs.bounced}");
        }
    }

    /// Everything that could carry over from one delivery to the next.
    private void LogPersistentState(Rigidbody rb, Ball bs)
    {
        var c = M.currentBowlingConfig;
        Debug.Log($"[DIAG] ===== DELIVERY #{_delivery} =====");
        Debug.Log($"[DIAG]  cfg          : {(c == null ? "NULL" : c.ToString())}");
        Debug.Log($"[DIAG]  RELEASE vel  : {rb.linearVelocity.ToString("F3")} |v|={rb.linearVelocity.magnitude:F2}" +
                  $"   (expected = speedX/mass = {(c == null ? 0f : c.speedX / rb.mass):F2} in X)" +
                  $"   prev release |v|={_prevReleaseVel.magnitude:F2}");
        Debug.Log($"[DIAG]  ball         : pos={rb.position.ToString("F3")} parent={(M.theBall.transform.parent == null ? "none" : M.theBall.transform.parent.name)}");

        // --- scale / collider drift: re-parenting through a rotated, scaled bone can accumulate ---
        var sc = M.theBall.transform.lossyScale;
        var sph = M.theBall.GetComponent<SphereCollider>();
        Debug.Log($"[DIAG]  ball scale   : lossy={sc.ToString("F4")} localScale={M.theBall.transform.localScale.ToString("F4")} " +
                  $"colliderR={(sph == null ? -1f : sph.radius):F4} worldR={(sph == null ? -1f : sph.radius * Mathf.Max(sc.x, Mathf.Max(sc.y, sc.z))):F4}");
        Debug.Log($"[DIAG]  ball rb      : mass={rb.mass} drag={rb.linearDamping} angDrag={rb.angularDamping} " +
                  $"maxAngVel={rb.maxAngularVelocity} angVel={rb.angularVelocity.magnitude:F1} " +
                  $"kin={rb.isKinematic} colMode={rb.collisionDetectionMode} gravity={rb.useGravity} constraints={rb.constraints}");

        // --- the player rig: a hit on the CharacterController could shove it ---
        var rig = GameObject.Find("XRPlayerController");
        if (rig != null)
        {
            var cc = rig.GetComponent<CharacterController>();
            Debug.Log($"[DIAG]  rig          : pos={rig.transform.position.ToString("F3")} rot={rig.transform.eulerAngles.ToString("F1")} " +
                      $"CC(h={(cc == null ? -1f : cc.height)} r={(cc == null ? -1f : cc.radius)} centre={(cc == null ? Vector3.zero : cc.center).ToString("F2")} " +
                      $"enabled={(cc != null && cc.enabled)})");
        }

        // --- the bat: its transform.up steers every shot, trackerMags feeds shot power ---
        var batGo = GameObject.Find("Bat");
        if (batGo != null)
        {
            var bat = batGo.GetComponent<Bat>();
            string mags = (bat.trackerMags == null || bat.trackerMags.Count == 0)
                ? "empty"
                : $"n={bat.trackerMags.Count} avg={bat.trackerMags.Average():F3} max={bat.trackerMags.Max():F3}";
            Debug.Log($"[DIAG]  bat          : pos={batGo.transform.position.ToString("F3")} up={batGo.transform.up.ToString("F3")} " +
                      $"active={batGo.activeInHierarchy} triggerOn={batGo.GetComponent<BoxCollider>().enabled} " +
                      $"hasHitBall={bat.hasHitBall} attach={(bat.attachParent == null ? "NULL" : bat.attachParent.name)}");
            Debug.Log($"[DIAG]  bat swing    : trackerVel={bat.trackerVelocity.ToString("F2")} |{bat.trackerVelocity.magnitude:F2}| trackerMags {mags}");
        }

        Debug.Log($"[DIAG]  main         : resetDelay={M.resetDelay} BatAmplifier={M.BatAmplifier} ampMin={M.ampMin} ampMax={M.ampMax} " +
                  $"overlay={M.overlayVisible} timeScale={Time.timeScale} fixedDt={Time.fixedDeltaTime} gravity={Physics.gravity.y}");
    }
}

/// <summary>Attach to the Ball to log every contact and the velocity change across it.</summary>
public class BallContactLogger : MonoBehaviour
{
    private Rigidbody _rb;
    private Vector3 _preVel;

    private void Awake() { _rb = GetComponent<Rigidbody>(); }
    private void FixedUpdate() { if (_rb != null) _preVel = _rb.linearVelocity; }

    private void OnCollisionEnter(Collision c)
    {
        var inst = Main.Instance;
        // Call out the player's CharacterController specifically - that is the suspect.
        bool isPlayer = c.gameObject.GetComponent<CharacterController>() != null
                     || c.gameObject.name.Contains("XRPlayerController");
        string flag = isPlayer ? "  <<<<< PLAYER COLLIDER" : "";
        Debug.Log($"[DIAG] CONTACT '{c.gameObject.name}' tag='{c.gameObject.tag}' layer={LayerMask.LayerToName(c.gameObject.layer)} " +
                  $"state={(inst == null ? "?" : inst.gameState.ToString())} " +
                  $"velBefore={_preVel.ToString("F2")}(|{_preVel.magnitude:F2}|) " +
                  $"velAfter={_rb.linearVelocity.ToString("F2")}(|{_rb.linearVelocity.magnitude:F2}|) " +
                  $"angV={_rb.angularVelocity.magnitude:F1} scale={transform.lossyScale.ToString("F4")}{flag}");
    }

    private void OnTriggerEnter(Collider c)
    {
        var inst = Main.Instance;
        Debug.Log($"[DIAG] TRIGGER-ENTER '{c.gameObject.name}' state={(inst == null ? "?" : inst.gameState.ToString())}");
    }

    private void OnTriggerExit(Collider c)
    {
        var inst = Main.Instance;
        Debug.Log($"[DIAG] TRIGGER-EXIT '{c.gameObject.name}' state={(inst == null ? "?" : inst.gameState.ToString())} " +
                  $"radius={new Vector2(transform.position.x, transform.position.z).magnitude:F2}");
    }
}
