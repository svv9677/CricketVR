using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    public GameObject theBall;
    public GameObject theBat;
    public OVRPlayerController theController;

    private Rigidbody theBallRigidBody;
    private OVRGrabber theGrabber;

    private void Awake()
    {
        if (theBall != null)
        {
            theBallRigidBody = theBall.GetComponent<Rigidbody>();
            Debug.Log("Setting up Ball Rigid Body: " + theBallRigidBody.ToString());
        }
        if(theController != null)
        {
            OVRGrabber[] grabbers = theController.GetComponentsInChildren<OVRGrabber>();
            if (grabbers != null && grabbers.Length > 0)
            {
                theGrabber = grabbers[0];
                Debug.Log("Found the grabber: " + theGrabber.ToString());
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {        
    }

    // Update is called once per frame
    void Update()
    {
        if(GetButtonDown(OVRInput.Button.One))
        {
            // enable physics
            theBallRigidBody.isKinematic = false;
            // Add the required force & rotation
            theBallRigidBody.AddTorque(new Vector3(5, 0, 0));
            theBallRigidBody.AddForce(new Vector3(10, 0, 0), ForceMode.Force);
        }

        if(GetButtonDown(OVRInput.Button.Three))
        {
            // disable physics
            theBallRigidBody.isKinematic = true; 
            // reset ball position to inside machine
            theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f); 
        }
    }

    KeyCode GetKeyCodeForButton(OVRInput.Button button)
    {
        switch(button)
        {
            case OVRInput.Button.One:
                return KeyCode.RightShift;  // A -> Right Shift
            case OVRInput.Button.Two:
                return KeyCode.Delete;      // B -> Delete
            case OVRInput.Button.Three:
                return KeyCode.LeftShift;   // X -> Left Shift
            case OVRInput.Button.Four:
                return KeyCode.Z;           // Y -> Z
        }

        return KeyCode.None;
    }

    bool GetButtonDown(OVRInput.Button button)
    {
        return (Application.isEditor && Input.GetKey(GetKeyCodeForButton(button)) ||
            !Application.isEditor && OVRInput.Get(button));
    }
}
