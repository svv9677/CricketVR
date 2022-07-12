using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bat : MonoBehaviour
{
    [SerializeField]
    protected Transform controllerParent;
    [SerializeField]
    public Transform leftHandParent;
    [SerializeField]
    public Transform rightHandParent;
    [SerializeField]
    protected Vector3 leftGrabOffsetPosition;
    [SerializeField]
    protected Quaternion leftGrabOffsetRotation;
    [SerializeField]
    protected Vector3 rightGrabOffsetPosition;
    [SerializeField]
    protected Quaternion rightGrabOffsetRotation;
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
    public bool hasHitBall;

    private bool grabbing = false;
    private Collider[] myColliders;
    private Rigidbody myRigidBody;
    private Vector3 ballInitialVelocity;

    public Vector3 startingPosition;
    public Quaternion startingRotation;

    public Vector3 trackerPos { get { return trackerObject.transform.position; } }
    private Vector3 trackerPreviousPos;

    // Start is called before the first frame update
    void Start()
    {
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

    public void OnCollisionExit(Collision collisionInfo)
    {
        // If we hit the ball
        if (collisionInfo.gameObject.name == "Ball")
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
                if (trackerVelocity.magnitude > 0.1f)
                {
                    Vector3 force;
                    // Clamp magnitude to min & max
                    float magn = trackerVelocity.magnitude;
                    magn = Mathf.Clamp(magn, 0.25f, 1f);

                    // If it is spin bowling, add extra push from the bat
                    if (ballInitialVelocity.magnitude < 5f)
                        magn *= 2.5f;

                    // start with softer impact
                    float amplifier = inst.ampMin;
                    float ballAmplifier = 0f;
                    // check if the initial ball direction and bat's tracker direction is along same lines
                    dp = Vector3.Dot(ballInitialVelocity, trackerVelocity);
                    // if dot product is positive, the angle is between -90 & 90, so they are headed in same direction
                    if (dp < 0f)
                    {
                        // heading in opposite direction, so harder impact
                        amplifier = inst.ampMax;
                        ballAmplifier = 4f;
                    }

                    // Calculate the final force now with all the params
                    force = (amplifier * magn * trackerVelocity.normalized) +
                            (ballAmplifier * magn * inst.theBallRigidBody.velocity);

                    //Debug.LogWarning("Adding force: " + force.ToString() +
                    //                 ", Tracker: " + trackerVelocity.ToString() +
                    //                 ", Ball: " + ballRigidBody.velocity.ToString() +
                    //                 ", initial: " + ballInitialVelocity.ToString() +
                    //                 ", dp: " + dp.ToString());
                    inst.theBallRigidBody.velocity += force;
                    inst.theBallRigidBody.velocity = Vector3.ClampMagnitude(inst.theBallRigidBody.velocity, 60f);
                }
                // if we are not moving the bat, check if we want to retain the ball's velocity,
                //  based on bat's direction
                else
                {
                    Vector3 batFacing = trackerPos - attachParent.transform.position;
                    dp = Vector3.Dot(inst.theBallRigidBody.velocity, batFacing);
                    // if the dp is positive, angle is between -90 & 90, so no need to dampen
                    if (dp < 0f)
                    {
                        // dampen the velocity on the ball
                        float magnit = inst.theBallRigidBody.velocity.magnitude;
                        inst.theBallRigidBody.velocity *= (Random.Range(2f, 5f) / magnit);
                    }
                }
                
                // Play sound
                Vector3 delta = inst.theBallRigidBody.velocity - trackerVelocity;
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

                hasHitBall = true;
                fieldersParent.BroadcastMessage("StartRotateTowardsIntercept");
            }
            else
                Debug.Log("CAUTION: " + gameObject.name + " collided with ball, but game state was " + inst.gameState.ToString());
        }
    }

    public IEnumerator ProvideVibration()
    {
        if (attachParent == leftHandParent)
            OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.LTouch);
        else
            OVRInput.SetControllerVibration(1f, 1f, OVRInput.Controller.RTouch);

        yield return new WaitForSeconds(0.1f);

        if (attachParent == leftHandParent)
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        else
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    // Update is called once per frame
    private void Update()
    {
        trackerVelocity = trackerPos - trackerPreviousPos;
        trackerPreviousPos = trackerPos;
    }

    void LateUpdate()
    {
        // Check if we need to start the attach/detach step
        CheckAndGrab();

        if (attachParent == null)
            return;

        // If all set, and grabbing, update the transforms
        if (grabbable && grabbing)
        {
            Vector3 destPos = attachParent.TransformPoint(Vector3.zero); // Replace with any local offset on hand, if needed
            Quaternion destRot = attachParent.rotation * Quaternion.identity; // Replace with any local rotation on hand

            Vector3 grabOffsetPosition = Vector3.zero;
            Quaternion grabOffsetRotation = Quaternion.identity;
            if (attachParent == rightHandParent)
            {
                grabOffsetPosition = rightGrabOffsetPosition;
                grabOffsetRotation = rightGrabOffsetRotation;
            }
            else
            {
                grabOffsetPosition = leftGrabOffsetPosition;
                grabOffsetRotation = leftGrabOffsetRotation;
            }

            Vector3 finalPos = destPos + destRot * grabOffsetPosition;
            Quaternion finalRot = destRot * grabOffsetRotation;

            myRigidBody.transform.position = finalPos;
            myRigidBody.transform.rotation = finalRot;
        }
    }
}
