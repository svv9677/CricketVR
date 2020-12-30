using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stumps : MonoBehaviour
{
    [SerializeField]
    protected GameObject LegStump;
    [SerializeField]
    protected GameObject MiddleStump;
    [SerializeField]
    protected GameObject OffStump;

    [HideInInspector]
    private Vector3[] resetPositions;
    [HideInInspector]
    private Quaternion[] resetRotations;
    [HideInInspector]
    private Rigidbody[] rigidBodies;
    [HideInInspector]
    Stump[] stumps;

    // Start is called before the first frame update
    void Start()
    {
        resetPositions = new Vector3[3];
        resetRotations = new Quaternion[3];
        rigidBodies = new Rigidbody[3];
        stumps = new Stump[3];
        if (LegStump != null)
        {
            resetPositions[0] = LegStump.transform.position;
            resetRotations[0] = LegStump.transform.rotation;
            rigidBodies[0] = LegStump.GetComponent<Rigidbody>();
            stumps[0] = LegStump.GetComponent<Stump>();
        }
        if (MiddleStump != null)
        {
            resetPositions[1] = MiddleStump.transform.position;
            resetRotations[1] = MiddleStump.transform.rotation;
            rigidBodies[1] = MiddleStump.GetComponent<Rigidbody>();
            stumps[1] = LegStump.GetComponent<Stump>();
        }
        if (OffStump != null)
        {
            resetPositions[2] = OffStump.transform.position;
            resetRotations[2] = OffStump.transform.rotation;
            rigidBodies[2] = OffStump.GetComponent<Rigidbody>();
            stumps[2] = LegStump.GetComponent<Stump>();
        }
    }

    public void Reset()
    {
        if (LegStump != null)
        {
            // disable physics
            rigidBodies[0].isKinematic = true;
            LegStump.transform.position = resetPositions[0];
            LegStump.transform.rotation = resetRotations[0];
            // Re-enable physics
            rigidBodies[0].isKinematic = false;
        }
        if (MiddleStump != null)
        {
            // disable physics
            rigidBodies[1].isKinematic = true;
            MiddleStump.transform.position = resetPositions[1];
            MiddleStump.transform.rotation = resetRotations[1];
            // Re-enable physics
            rigidBodies[1].isKinematic = false;
        }
        if (OffStump != null)
        {
            // disable physics
            rigidBodies[2].isKinematic = true;
            OffStump.transform.position = resetPositions[2];
            OffStump.transform.rotation = resetRotations[2];
            // Re-enable physics
            rigidBodies[2].isKinematic = false;
        }
    }
}
