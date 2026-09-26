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

    // ---- Contact (see BatSweep) -----------------------------------------------------------------
    // The blade in the bat's local frame (mesh units; the bat is uniformly scaled). The blade is
    // -Z, the face is +Y (transform.up), width is X. The handle, +Z beyond BladeTopZ, is not hit.
    private const float BladeHalfWidth = 0.1133f;
    private const float BladeHalfThickness = 0.0702f;
    private const float BladeToeZ = -0.89f;
    private const float BladeTopZ = 0.30f;
    /// Sweet spot, about 0.18 m up from the toe of a real bat.
    private const float SweetSpotFromToe = 0.18f;
    private const float EffectiveMassSweet = 0.7f, EffectiveMassFar = 0.35f;
    private const float RestitutionSweet = 0.5f, RestitutionFar = 0.3f, RestitutionEdge = 0.3f;
    /// World distance from the sweet spot at which the far values apply.
    private const float SweetSpotFalloff = 0.3f;

    private bool havePreviousPose;
    private Matrix4x4 previousPose;
    private Quaternion previousRotation;
    private Vector3 previousBallPosition;

    private void CheckForContact()
    {
        Main inst = Main.Instance;
        Rigidbody ball = inst != null ? inst.theBallRigidBody : null;
        Matrix4x4 pose = transform.localToWorldMatrix;
        Vector3 ballNow = ball != null ? ball.position : Vector3.zero;

        bool live = ball != null && !ball.isKinematic && !hasHitBall && !holdingStill &&
                    inst.gameState == eGameState.InGame_DeliverBallLoop;
        if (live && havePreviousPose && (ballNow - previousBallPosition).sqrMagnitude < 9f)
            TrySweep(inst, ball, pose, ballNow);

        previousPose = pose;
        previousRotation = transform.rotation;
        previousBallPosition = ballNow;
        havePreviousPose = true;
    }

    private void TrySweep(Main inst, Rigidbody ball, Matrix4x4 pose, Vector3 ballNow)
    {
        float scale = transform.lossyScale.x;
        float radius = BallFlight.Radius / scale;
        float widthMultiplier = originalSize.x > 0f ? batCollider.size.x / originalSize.x : 1f;
        Vector3 min = new Vector3(-BladeHalfWidth * widthMultiplier - radius, -BladeHalfThickness - radius, BladeToeZ - radius);
        Vector3 max = new Vector3(BladeHalfWidth * widthMultiplier + radius, BladeHalfThickness + radius, BladeTopZ);

        Vector3 l0 = previousPose.inverse.MultiplyPoint3x4(previousBallPosition);
        Vector3 l1 = pose.inverse.MultiplyPoint3x4(ballNow);
        if (!BatSweep.SegmentBox(l0, l1, min, max, out float t, out int axis, out float sign))
            return;

        // Where, on the bat and in the world, the two met.
        Vector3 localNormal = Vector3.zero;
        localNormal[axis] = sign;
        Vector3 localBallCentre = Vector3.Lerp(l0, l1, t);
        Vector3 localBatPoint = localBallCentre - localNormal * radius;
        Quaternion rotationAtHit = Quaternion.Slerp(previousRotation, transform.rotation, t);
        Vector3 normal = rotationAtHit * localNormal;
        Vector3 batPointBefore = previousPose.MultiplyPoint3x4(localBatPoint);
        Vector3 batPointAfter = pose.MultiplyPoint3x4(localBatPoint);
        Vector3 batPointVelocity = (batPointAfter - batPointBefore) / Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 ballCentreAtHit = Vector3.Lerp(previousPose.MultiplyPoint3x4(localBallCentre),
                                               pose.MultiplyPoint3x4(localBallCentre), t);

        Vector3 incoming = ball.linearVelocity;
        if (Vector3.Dot(incoming - batPointVelocity, normal) >= 0f)
            return; // already separating

        bool edge = axis == 0;
        float fromSweet = Mathf.Abs(localBatPoint.z - (BladeToeZ + SweetSpotFromToe / scale)) * scale;
        float falloff = Mathf.Clamp01(fromSweet / SweetSpotFalloff);
        float restitution = edge ? RestitutionEdge : Mathf.Lerp(RestitutionSweet, RestitutionFar, falloff);
        float batMass = edge ? EffectiveMassFar : Mathf.Lerp(EffectiveMassSweet, EffectiveMassFar, falloff);
        float power = inst.BatAmplifier / 75f;   // the B-menu "batAmplifier" slider, 75 = realistic
        Vector3 outgoing = BatSweep.Rebound(incoming, batPointVelocity, normal, restitution, batMass, ball.mass) * power;

        // Put the ball back where it touched the bat - the frame may have carried it through.
        Vector3 contactCentre = ballCentreAtHit + normal * 0.002f;
        ball.position = contactCentre;
        ball.transform.position = contactCentre;
        ball.linearVelocity = outgoing;

        OnBallHit(inst, incoming, outgoing, Vector3.Dot(batPointVelocity, normal));
    }

    private void OnBallHit(Main inst, Vector3 incoming, Vector3 outgoing, float batSpeedIntoBall)
    {
        hasHitBall = true;
        inst.gameState = eGameState.InGame_BallHit;
        BallSpeed.Instance.updateBatAndFinalSpeed(Mathf.Abs(batSpeedIntoBall), outgoing.magnitude);

        // Louder crack for a well-struck ball.
        float exit = outgoing.magnitude;
        AudioClip clip = exit < 20f ? audioShot1 : exit < 32f ? audioShot2 : audioShot3;
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
