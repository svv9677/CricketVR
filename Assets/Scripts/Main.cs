using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Main : MonoBehaviour
{
    [SerializeField]
    protected GameObject theBall;
    [SerializeField]
    protected GameObject theBat;
    [SerializeField]
    protected OVRPlayerController theController;
    [SerializeField]
    protected DebugPad thePad;

    private Rigidbody theBallRigidBody;
    private OVRGrabber theGrabber;

    private float tX, tY, tZ, fX, fY, fZ;
    private int dbgCurrentToggle = 0;

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
        tX = -50.0f;
        tY = 0.0f;
        tZ = 0.0f;

        fX = 6.0f;
        fY = -1.0f;
        fZ = 0.0f;
    }

    // Update is called once per frame
    void Update()
    {
        if(GetButton(OVRInput.Button.One))
        {
            // disable physics
            theBallRigidBody.isKinematic = true;
            // reset ball position to inside machine
            theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);

            // enable physics
            theBallRigidBody.isKinematic = false;
            // Add the required force & rotation
            theBallRigidBody.AddTorque(new Vector3(tX, tY, tZ), ForceMode.Impulse);
            theBallRigidBody.AddForce(new Vector3(fX, fY, fZ), ForceMode.Impulse);
        }

        if (GetButton(OVRInput.Button.Two))
        {
            // Cycle current toggle
            dbgCurrentToggle++;
            if (dbgCurrentToggle >= 6)
                dbgCurrentToggle = 0;
        }

        float delta = 0f;
        if (GetButton(OVRInput.Button.Three))
            delta = 0.25f;
        if (GetButton(OVRInput.Button.Four))
            delta = -0.25f;

        if(delta != 0f)
        {
            switch(dbgCurrentToggle)
            {
                case 0:
                    fX += delta;
                    break;
                case 1:
                    fY += delta;
                    break;
                case 2:
                    fZ += delta;
                    break;
                case 3:
                    tX += delta;
                    break;
                case 4:
                    tY += delta;
                    break;
                case 5:
                    tZ += delta;
                    break;
            }
        }

        thePad.Text = string.Format("force: {0}, {1}, {2} \ntorque: {3}, {4}, {5} \nToggle: {6}", fX, fY, fZ, tX, tY, tZ, dbgCurrentToggle);
    }

    private KeyCode GetKeyCodeForButton(OVRInput.Button button)
    {
        switch(button)
        {
            case OVRInput.Button.One:
                return KeyCode.RightShift;  // A -> Right Shift
            case OVRInput.Button.Two:
                return KeyCode.Slash;       // B -> /
            case OVRInput.Button.Three:
                return KeyCode.LeftShift;   // X -> Left Shift
            case OVRInput.Button.Four:
                return KeyCode.Z;           // Y -> Z
        }

        return KeyCode.None;
    }

    public bool GetButton(OVRInput.Button button, bool justDown = true)
    {
        if(justDown)
            return (Application.isEditor && Input.GetKeyDown(GetKeyCodeForButton(button)) ||
                !Application.isEditor && OVRInput.GetDown(button));
        else
            return (Application.isEditor && Input.GetKey(GetKeyCodeForButton(button)) ||
                !Application.isEditor && OVRInput.Get(button));
    }
}
