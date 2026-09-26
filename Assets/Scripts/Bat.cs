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
    protected AudioClip audioShot1, audioShot2, audioShot3;
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
    private bool havePreviousPose;
    private Vector3 previousPosition;
    private Quaternion previousRotation;
    private Vector3 previousBallPosition;
    private readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> handDevices =
        new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

    private void CheckForContact()
    {
        Main inst = Main.Instance;
        Rigidbody ball = inst != null ? inst.theBallRigidBody : null;
        Vector3 ballNow = ball != null ? ball.position : Vector3.zero;

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
        Vector3 batPointVelocity = BatContact.PointVelocity(hit.localBat, scale, previousPosition, previousRotation,
                                                            transform.position, transform.rotation, dt, pivotLocal, hit.t);
        // The controller measures its own velocity and spin directly, which beats a frame-to-frame
        // difference in a fast swing (no lag, no chord-across-an-arc error). Use it when the runtime
        // provides it and it is sane; the finite difference is the fallback.
        if (TryDevicePointVelocity(contactWorld, out Vector3 deviceVelocity) &&
            (deviceVelocity - batPointVelocity).magnitude < Mathf.Max(8f, 0.6f * batPointVelocity.magnitude))
            batPointVelocity = deviceVelocity;

        Vector3 incoming = ball.linearVelocity;
        BatContact.Result result = BatContact.Respond(hit, blade, scale, incoming, batPointVelocity, ball.mass);
        if (result.velocity == incoming)
            return; // grazed, already separating
        float power = inst.BatAmplifier / 75f;   // the B-menu "batAmplifier" slider, 75 = realistic
        Vector3 outgoing = result.velocity * power;

        // Put the ball back where it touched the bat - the frame may have carried it through.
        Vector3 contactCentre = hit.position + hit.rotation * (hit.localBall * scale);
        Vector3 normal = (contactCentre - contactWorld).normalized;
        contactCentre += normal * 0.002f;
        ball.position = contactCentre;
        ball.transform.position = contactCentre;
        ball.linearVelocity = outgoing;

        Debug.Log($"[BatHit] quality={result.quality:F2} edge={result.edge} e={result.restitution:F2} M={result.effectiveMass:F2} " +
                  $"in={incoming.magnitude:F1} bat={batPointVelocity.magnitude:F1} out={outgoing.magnitude:F1} local={hit.localBat.ToString("F3")}");
        OnBallHit(inst, incoming, outgoing, Vector3.Dot(batPointVelocity, normal), result);
    }

    private bool TryDevicePointVelocity(Vector3 point, out Vector3 velocity)
    {
        velocity = Vector3.zero;
        if (attachParent == null || attachParent.parent == null)
            return false;
        var chars = (attachParent == leftHandParent ? UnityEngine.XR.InputDeviceCharacteristics.Left
                                                    : UnityEngine.XR.InputDeviceCharacteristics.Right)
                    | UnityEngine.XR.InputDeviceCharacteristics.Controller;
        handDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(chars, handDevices);
        if (handDevices.Count == 0)
            return false;
        var device = handDevices[0];
        if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out Vector3 v) ||
            !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceAngularVelocity, out Vector3 w))
            return false;
        // Tracking-space values: turn them into world space with the rig.
        Transform anchor = attachParent.parent;
        Transform space = anchor.parent != null ? anchor.parent : anchor;
        Vector3 linear = space.TransformVector(v);
        Vector3 angular = space.TransformDirection(w);
        if (angular.magnitude > 60f || linear.magnitude > 40f)
            return false;
        velocity = linear + Vector3.Cross(angular, point - anchor.position);
        return true;
    }

    private void OnBallHit(Main inst, Vector3 incoming, Vector3 outgoing, float batSpeedIntoBall, BatContact.Result result)
    {
        hasHitBall = true;
        inst.gameState = eGameState.InGame_BallHit;
        BallSpeed.Instance.updateBatAndFinalSpeed(Mathf.Abs(batSpeedIntoBall), outgoing.magnitude);

        // The sound follows how well it was struck: an edge or the toe clicks, the middle cracks.
        AudioClip clip = result.edge || result.quality < 0.35f ? audioShot1 : result.quality < 0.7f ? audioShot2 : audioShot3;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, trackerPos);

        StartCoroutine(ProvideVibration());
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

    public IEnumerator ProvideVibration()
    {
        XRInput.SendHaptics(attachParent == leftHandParent, 1f, 0.1f);
        yield return new WaitForSeconds(0.1f);
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
