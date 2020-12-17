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

    public bool grabbable = true;

    [HideInInspector]
    public Transform attachParent;

    private bool grabbing = false;
    private Collider[] myColliders;
    private Rigidbody myRigidBody;

    private Vector3 startingPosition;
    private Quaternion startingRotation;

    // Start is called before the first frame update
    void Start()
    {
        startingPosition = transform.localPosition;
        startingRotation = transform.localRotation;

        myColliders = GetComponentsInChildren<Collider>();
        myRigidBody = GetComponent<Rigidbody>();
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

    // Update is called once per frame
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
            //if(attachParent == rightHandParent)
            //{
            //    grabOffsetPosition = leftGrabOffsetPosition;
            //    grabOffsetRotation = leftGrabOffsetRotation;
            //}
            //else
            //{
            //    grabOffsetPosition = rightGrabOffsetPosition;
            //    grabOffsetRotation = rightGrabOffsetRotation;
            //}
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
