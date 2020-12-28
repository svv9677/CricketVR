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

    public bool grabbable = true;

    [HideInInspector]
    public Transform attachParent;
    [HideInInspector]
    public Vector3 trackerVelocity;

    private bool grabbing = false;
    private Collider[] myColliders;
    private Rigidbody myRigidBody;
    private Rigidbody ballRigidBody;
    private Ball ballScript;
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
        ballRigidBody = null;
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

    public void OnCollisionEnter(Collision collisionInfo)
    {
        // If we hit the ball
        if (collisionInfo.gameObject.name == "Ball")
        {
            if (ballScript == null)
                ballScript = collisionInfo.gameObject.GetComponent<Ball>();

            // make a note of the ball's initial velocity
            ballInitialVelocity = ballScript.lastVelocity;
        }
    }

    public void OnCollisionExit(Collision collisionInfo)
    {
        // If we hit the ball
        if (collisionInfo.gameObject.name == "Ball")
        {
            if (ballRigidBody == null)
                ballRigidBody = collisionInfo.gameObject.GetComponent<Rigidbody>();

            if(trackerVelocity.magnitude > 0f)
            {
                float amplifier = 0f;
                Vector3 force;
                // first check if the initial ball direction and bat's tracker direction is along same lines
                float dp = Vector3.Dot(ballInitialVelocity, trackerVelocity);
                // if dot product is positive, the angle is between -90 & 90, so they are headed in same direction
                if (dp >= 0f)
                    amplifier = 1f;
                else
                    amplifier = 4f;
                force = amplifier * trackerVelocity;
                float magn = force.magnitude;
                // clamp it at max 
                if (magn > amplifier)
                    force *= amplifier / magn;
                Debug.LogWarning("Adding force: " + force.ToString() + ", Ball velocity: " + ballRigidBody.velocity.ToString() + ", initial: " + ballInitialVelocity.ToString() + ", dp: " + dp.ToString());
                ballRigidBody.AddForce(trackerVelocity.normalized * amplifier, ForceMode.VelocityChange);
            }

            // Play sound
            Vector3 delta = ballRigidBody.velocity - trackerVelocity;
            float mag = delta.magnitude;
            if (mag < 25f)
            {
                Debug.Log("Playing shot1");
                AudioSource.PlayClipAtPoint(audioShot1, trackerPos);
            }
            else if (mag >= 25f && mag < 30f)
            {
                Debug.Log("Playing shot2");
                AudioSource.PlayClipAtPoint(audioShot2, trackerPos);
            }
            else
            {
                Debug.Log("Playing shot3");
                AudioSource.PlayClipAtPoint(audioShot3, trackerPos);
            }

            // Haptics feedback
            if (attachParent == leftHandParent)
                OVRInput.SetControllerVibration(1, 0.1f, OVRInput.Controller.LTouch);
            else
                OVRInput.SetControllerVibration(1, 0.1f, OVRInput.Controller.RTouch);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Check if we need to start the attach/detach step
        CheckAndGrab();

        if (attachParent == null)
            return;

        trackerVelocity = trackerPos - trackerPreviousPos;
        trackerPreviousPos = trackerPos;

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
