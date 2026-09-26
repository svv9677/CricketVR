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

   
    public void OnTriggerEnter(Collider collisionInfo)
    {
        // If we hit the ball
        if (collisionInfo.gameObject.name == "Ball" && !hasHitBall)
        {
            Main inst = Main.Instance;
            if (inst.gameState == eGameState.InGame_DeliverBallLoop ||
                inst.gameState == eGameState.InGame_BallHit ||
                inst.gameState == eGameState.InGame_BallHitLoop)
            {
                inst.gameState = eGameState.InGame_BallHit;

                // make a note of the ball's initial velocity
                ballInitialVelocity = inst.theBallScript.lastVelocity;

                float dp = 0f;
                //if (trackerVelocity.magnitude > -0.1f)
                //{
                //    Vector3 force;
                //    // Clamp magnitude to min & max
                //    float magn = trackerVelocity.magnitude;
                //    magn = Mathf.Clamp(magn, 0.25f, 1f);

                //    // If it is spin bowling, add extra push from the bat
                //    if (ballInitialVelocity.magnitude < 5f)
                //        magn *= 2.5f;

                //    // start with softer impact
                //    float amplifier = inst.ampMin;
                //    float ballAmplifier = 0f;
                //    // check if the initial ball direction and bat's tracker direction is along same lines
                //    dp = Vector3.Dot(ballInitialVelocity, trackerVelocity);
                //    // if dot product is positive, the angle is between -90 & 90, so they are headed in same direction
                //    if (dp < 0f)
                //    {
                //        // heading in opposite direction, so harder impact
                //        amplifier = inst.ampMax;
                //        ballAmplifier = 4f;
                //    }

                //    // Calculate the final force now with all the params
                //    //force = (amplifier * magn * trackerVelocity.normalized) +
                //    //        (ballAmplifier * magn * inst.theBallRigidBody.velocity);
                //    //force = (amplifier * magn * trackerVelocity.normalized) +
                //    //(ballAmplifier * inst.theBallRigidBody.velocity);

                //    force = (amplifier * trackerVelocity);

                //    //print(ballInitialVelocity);
                //    //print(inst.theBallRigidBody.velocity);

                //    //Debug.LogWarning("Adding force: " + force.ToString() +
                //    //                 ", Tracker: " + trackerVelocity.ToString() +
                //    //                 ", Ball: " + ballRigidBody.velocity.ToString() +
                //    //                 ", initial: " + ballInitialVelocity.ToString() +
                //    //                 ", dp: " + dp.ToString());

                //    // TODO Calculate where it hit on the bat here and make the dampen factor accordingly.
                //    float dampenFactor = 1f;

                //    // Calculate the collision of ball and bat:
                //    Vector3 direction = inst.theBallRigidBody.velocity.normalized + gameObject.transform.up.normalized;
                //    float magnitude = (inst.theBallRigidBody.velocity.magnitude * dampenFactor) + (force.magnitude);
                //    inst.theBallRigidBody.velocity = (direction*magnitude);


                //    //inst.theBallRigidBody.velocity = Vector3.ClampMagnitude(inst.theBallRigidBody.velocity, 60f); TODO Uncomment this for clamping magnitude.
                //}
                //// if we are not moving the bat, check if we want to retain the ball's velocity,
                ////  based on bat's direction
                //else
                //{
                //    Vector3 batFacing = trackerPos - attachParent.transform.position;
                //    dp = Vector3.Dot(inst.theBallRigidBody.velocity, batFacing);
                //    // if the dp is positive, angle is between -90 & 90, so no need to dampen
                //    if (dp < 0f)
                //    {
                //        // dampen the velocity on the ball
                //        float magnit = inst.theBallRigidBody.velocity.magnitude;
                //        inst.theBallRigidBody.velocity *= (Random.Range(2f, 5f) / magnit);
                //    }
                //}


                // Using momentum
                float contactRadius = 1f;  // TODO insert calculation for distance to contact
                // (Removed ballMomentum/batMomentum: both were computed and never used, and the
                //  bat rigidbody is teleported every LateUpdate so its velocities are always zero.)

                Vector3 ballBounce;
                float dampenFactor = 1f;
                float angle = 180f - Vector3.Angle(gameObject.transform.up, ballInitialVelocity);
                if (angle < 45f)
                {
                    ballBounce = ((-ballInitialVelocity).normalized + gameObject.transform.up.normalized).normalized * ballInitialVelocity.magnitude * dampenFactor;
                }
                else
                {
                    ballBounce = (ballInitialVelocity.normalized + gameObject.transform.up.normalized).normalized * ballInitialVelocity.magnitude * dampenFactor;
                }

                float avgBatSpeed = trackerMags.Average();
                
                // Calculate amount of bat movement in the direction of the bat face...
                Vector3 batSwing = gameObject.transform.up * Mathf.Cos(Mathf.Deg2Rad * Vector3.Angle(gameObject.transform.up, trackerVelocity))
                                   * avgBatSpeed * SwingTuningReferenceDeltaTime;
                ballBounce = ballBounce / 75f;
                Vector3 finalVel = ballBounce + batSwing;
                finalVel *= Main.Instance.BatAmplifier * contactRadius;
                inst.theBallRigidBody.linearVelocity = finalVel;
                BallSpeed.Instance.updateBatAndFinalSpeed(batSwing.magnitude, finalVel.magnitude);

                dp = Vector3.Dot(ballInitialVelocity, gameObject.transform.up);

                // Play sound
                Vector3 delta = ballInitialVelocity - (trackerVelocity.normalized * avgBatSpeed * SwingTuningReferenceDeltaTime);
                float mag = delta.magnitude;
                if (mag < 25f || dp <= 0f)
                {
                    //Debug.Log("Playing shot1");
                    AudioSource.PlayClipAtPoint(audioShot1, trackerPos);
                }
                else if (mag >= 25f && mag < 30f)
                {
                    //Debug.Log("Playing shot2");
                    AudioSource.PlayClipAtPoint(audioShot2, trackerPos);
                }
                else
                {
                    //Debug.Log("Playing shot3");
                    AudioSource.PlayClipAtPoint(audioShot3, trackerPos);
                }

                // Haptics feedback
                StartCoroutine(ProvideVibration());
                ShotDistance.Instance.calculateDistance(inst.theBall.transform.position, finalVel); ;

                hasHitBall = true;
                CameraReplay.Instance.setViewSetting(1, 1f);
                //fieldersParent.BroadcastMessage("StartRotateTowardsIntercept");
                if (SceneManager.GetActiveScene().name == "Nets")
                {
                    StartCoroutine(AutomaticReset(2f));
                }
            }
            else
                Debug.Log("CAUTION: " + gameObject.name + " collided with ball, but game state was " + inst.gameState.ToString());
        }
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
    }

}
