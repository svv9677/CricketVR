using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bat : MonoBehaviour
{
    [SerializeField]
    protected Transform controllerParent;
    [SerializeField]
    public Transform leftHandParent;
    [SerializeField]
    public Transform rightHandParent;
    [SerializeField]
    public Vector3 leftGrabOffsetPosition;
    // Euler degrees, not a raw quaternion. The old Quaternion fields held degree values
    // typed into x/y/z/w (magnitude ~38), which normalised to an arbitrary rotation.
    [SerializeField]
    public Vector3 leftGrabOffsetEuler;
    [SerializeField]
    public Vector3 rightGrabOffsetPosition;
    [SerializeField]
    public Vector3 rightGrabOffsetEuler;
    [SerializeField]
    protected GameObject trackerObject;
    [SerializeField]
    protected GameObject fieldersParent;

    public bool grabbable = true;

    [HideInInspector]
    public Transform attachParent;
    [HideInInspector]
    public Vector3 trackerVelocity;
    [HideInInspector]
    public List<float> trackerMags;
    [HideInInspector]
    public bool hasHitBall;

    private bool grabbing = false;
    private Collider[] myColliders;
    private Rigidbody myRigidBody;
    private Vector3 ballInitialVelocity;

    public Vector3 startingPosition;
    public Quaternion startingRotation;

    public Vector3 trackerPos { get { return trackerObject.transform.position; } }
    private Vector3 trackerPreviousPos;

    [HideInInspector]
    public BoxCollider batCollider;
    [HideInInspector]
    public Vector3 originalSize;


    // Start is called before the first frame update
    void Start()
    {
        batCollider = GetComponent<BoxCollider>();
        originalSize = batCollider.size;

        startingPosition = transform.localPosition;
        startingRotation = transform.localRotation;

        myColliders = GetComponentsInChildren<Collider>();
        myRigidBody = GetComponent<Rigidbody>();

        hasHitBall = false;
    }

    public void CheckAndGrab()
    {
        if (grabbable && !grabbing)
        {
            // Set Bat to Ignore Hands Collision
            SetBatIgnoreHandCollision(true);

            grabbing = true;
        }
        if (!grabbable && grabbing)
        {
            // Enable Bat & Hands Collision
            SetBatIgnoreHandCollision(false);

            grabbing = false;
        }
    }

    public void SetBatIgnoreHandCollision(bool ignore)
    {
        Collider[] lColliders = controllerParent.GetComponentsInChildren<Collider>();

        foreach (Collider mc in myColliders)
        {
            foreach (Collider lc in lColliders)
            {
                if (!lc.isTrigger && !mc.isTrigger)
                    Physics.IgnoreCollision(lc, mc, ignore);
            }
        }
    }

    // ---- Contact (see BatContact / BatGeometry) -------------------------------------------------
    private BatGeometry geometry;
    private BatSounds sounds;
    private bool havePreviousPose;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Vector3 previousBallPosition;

    /// Every bat-ball contact, for the scoreboard and anything else that wants to know.
    public static event System.Action<ShotInfo> ShotStruck;

    private void CheckForContact()
    {
        Main inst = Main.Instance;
        Rigidbody ball = inst != null ? inst.theBallRigidBody : null;
        // The ball as drawn, not as simulated. With interpolation on (set at release) the transform
        // is where the player sees the ball this frame; ball.position is the last physics step,
        // which runs up to one step (10 ms, 35 cm at 35 m/s) ahead and jitters by a different amount
        // every frame as 100 Hz physics beats against the display rate. The bat is posed at display
        // time, so sweeping it against the physics ball met a ball the player never saw, early, with
        // the bat turned up to 20-40 degrees short of where it looked in a hard swing.
        Vector3 ballNow = ball != null ? ball.transform.position : Vector3.zero;

        bool live = ball != null && !ball.isKinematic && !hasHitBall && !holdingStill &&
                    inst.gameState == eGameState.InGame_DeliverBallLoop;
        if (live && havePreviousPose && (ballNow - previousBallPosition).sqrMagnitude < 9f)
            TrySweep(inst, ball, ballNow);

        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousBallPosition = ballNow;
        havePreviousPose = true;
    }

    private void TrySweep(Main inst, Rigidbody ball, Vector3 ballNow)
    {
        if (geometry == null)
            geometry = GetComponent<BatGeometry>();   // on the Bat prefab, markers fitted in the editor
        if (geometry == null || !geometry.IsFitted)
        {
            Debug.LogError("Bat has no fitted BatGeometry - run Tools > CricketVR > Build UI Prefabs (or Fit on the component).", this);
            return;
        }
        float scale = transform.lossyScale.x;
        float widthMultiplier = originalSize.x > 0f ? batCollider.size.x / originalSize.x : 1f;
        BatContact.Blade blade = geometry.Blade;

        // The bat turns about the hand holding it.
        Vector3 pivotLocal = attachParent != null ? transform.InverseTransformPoint(attachParent.position) : Vector3.zero;
        if (!BatContact.Sweep(previousPosition, previousRotation, transform.position, transform.rotation, scale,
                              previousBallPosition, ballNow, blade, widthMultiplier, BallFlight.Radius, pivotLocal,
                              out BatContact.Hit hit))
            return;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 contactWorld = hit.position + hit.rotation * (hit.localBat * scale);
        // The bat's velocity comes from the poses the player saw, the same two the sweep used, so
        // the direction of the hit agrees with the swing on screen. The controller's own reported
        // velocity used to override this when "close enough" (within 60% of the swing speed - 18 m/s
        // of disagreement at 30 m/s), but the runtime filters it, it lags the pose at the peak of a
        // swing, and its angular velocity is combined with a lever arm in a frame we never checked -
        // all errors that grow with swing speed, which is where shots went the wrong way.
        Vector3 swingVelocity = BatContact.PointVelocity(hit.localBat, scale, previousPosition, previousRotation,
                                                         transform.position, transform.rotation, dt, pivotLocal, hit.t);
        // The settings "Bat power" (75 = realistic) scales the swing, not the result, and only a
        // little: a harder swing hits further, while the ball's own pace still rebounds by the real
        // amount. It used to multiply the outgoing velocity, which also multiplied the ball's pace -
        // a ramp off a 17 km/h bat left at 172 km/h from a 143 km/h delivery.
        Vector3 batPointVelocity = swingVelocity * BatContact.SwingScale(inst.BatAmplifier);

        Vector3 incoming = ball.linearVelocity;
        BatContact.Result result = BatContact.Respond(hit, blade, scale, incoming, batPointVelocity, ball.mass);
        if (result.velocity == incoming)
            return; // grazed, already separating
        Vector3 outgoing = result.velocity;

        // Put the ball back where it touched the bat - the frame may have carried it through.
        Vector3 contactCentre = hit.position + hit.rotation * (hit.localBall * scale);
        Vector3 normal = (contactCentre - contactWorld).normalized;
        contactCentre += normal * 0.002f;
        ball.position = contactCentre;
        ball.transform.position = contactCentre;
        ball.linearVelocity = outgoing;

        Debug.Log($"[BatHit] {result.kind} quality={result.quality:F2} e={result.restitution:F2} M={result.effectiveMass:F2} " +
                  $"in={incoming.magnitude:F1} bat={batPointVelocity.magnitude:F1} impact={result.impactSpeed:F1} out={outgoing.magnitude:F1} " +
                  $"local={hit.localBat.ToString("F3")} normal={normal.ToString("F2")} t={hit.t:F2}");
        // Report the swing the player actually made, not the power-scaled one.
        OnBallHit(inst, incoming, outgoing, swingVelocity.magnitude, result);
    }

    private void OnBallHit(Main inst, Vector3 incoming, Vector3 outgoing, float batSpeed, BatContact.Result result)
    {
        hasHitBall = true;
        inst.gameState = eGameState.InGame_BallHit;
        BallSpeed.Instance.updateBatAndFinalSpeed(batSpeed, outgoing.magnitude);

        if (sounds == null)
            sounds = GetComponent<BatSounds>();
        if (sounds != null)
            sounds.Play(result.kind, result.impactSpeed);

        ShotStruck?.Invoke(new ShotInfo
        {
            ballSpeedIn = incoming.magnitude,
            batSpeed = batSpeed,
            exitSpeed = outgoing.magnitude,
            quality = result.quality,
            contactLabel = BatContact.Label(result.kind),
            edge = result.edge,
        });

        // Haptics follow the contact too: a middle is a firm short thump, an edge or the toe a
        // longer, weaker buzz - the sting a mistimed shot sends up the handle.
        bool clean = result.kind == BatContact.ContactKind.Middled || result.kind == BatContact.ContactKind.Good;
        float strength = Mathf.Clamp01(0.35f + result.impactSpeed / 40f);
        XRInput.SendHaptics(attachParent == leftHandParent, clean ? strength : strength * 0.6f, clean ? 0.06f : 0.16f);
        ShotDistance.Instance.RecordHit(inst.theBall.transform.position);
        CameraReplay.Instance.setViewSetting(1, 1f);
        if (SceneManager.GetActiveScene().name == "Nets")
            StartCoroutine(AutomaticReset(2f));
    }

    private IEnumerator AutomaticReset(float delay)
    {
        yield return new WaitForSeconds(delay);
        Main.Instance.gameState = eGameState.InGame_BallMissed;
    }

    // The bat-swing term below was originally tuned against a per-frame position delta
    // (metres/frame) rather than a velocity, which made every shot frame-rate dependent.
    // trackerVelocity is now a true m/s velocity; this constant is the frame time that the
    // existing BatAmplifier / ampMin / ampMax tuning implicitly assumed (72 Hz on Quest),
    // so shot power is unchanged on device but no longer varies with framerate.
    public const float SwingTuningReferenceDeltaTime = 1f / 72f;

    // Update is called once per frame
    private void Update()
    {
        trackerVelocity = (trackerPos - trackerPreviousPos) / Mathf.Max(Time.deltaTime, 1e-5f);
        if (trackerMags.Count < 5)
        {
            trackerMags.Add(trackerVelocity.magnitude);
        }
        else
        {
            trackerMags.RemoveAt(0);
            trackerMags.Add(trackerVelocity.magnitude);
        }
        trackerPreviousPos = trackerPos;
    }

    // While grip calibration runs the bat stands still in the world instead of following the hand,
    // so the player can move the controller onto the handle (see BatGripCalibration).
    private bool holdingStill;
    private Vector3 stillPosition;
    private Quaternion stillRotation;

    public void HoldStill(Vector3 position, Quaternion rotation)
    {
        holdingStill = true;
        stillPosition = position;
        stillRotation = rotation;
    }

    public void ReleaseHold()
    {
        holdingStill = false;
    }

    public void SetGrabOffset(bool leftHand, Vector3 position, Vector3 euler)
    {
        if (leftHand)
        {
            leftGrabOffsetPosition = position;
            leftGrabOffsetEuler = euler;
        }
        else
        {
            rightGrabOffsetPosition = position;
            rightGrabOffsetEuler = euler;
        }
    }

    void LateUpdate()
    {
        if (holdingStill)
        {
            myRigidBody.transform.position = stillPosition;
            myRigidBody.transform.rotation = stillRotation;
            havePreviousPose = false;
            return;
        }

        // Check if we need to start the attach/detach step
        CheckAndGrab();

        if (attachParent == null)
            return;

        // If all set, and grabbing, update the transforms
        if (grabbable && grabbing)
        {
            Vector3 destPos = attachParent.TransformPoint(Vector3.zero); // Replace with any local offset on hand, if needed
            Quaternion destRot = attachParent.rotation * Quaternion.identity; // Replace with any local rotation on hand

            Vector3 grabOffsetPosition;
            Quaternion grabOffsetRotation;
            if (attachParent == rightHandParent)
            {
                grabOffsetPosition = rightGrabOffsetPosition;
                grabOffsetRotation = Quaternion.Euler(rightGrabOffsetEuler);
            }
            else
            {
                grabOffsetPosition = leftGrabOffsetPosition;
                grabOffsetRotation = Quaternion.Euler(leftGrabOffsetEuler);
            }

            Vector3 finalPos = destPos + destRot * grabOffsetPosition;
            Quaternion finalRot = destRot * grabOffsetRotation;

            myRigidBody.transform.position = finalPos;
            myRigidBody.transform.rotation = finalRot;
        }

        CheckForContact();
    }

}
