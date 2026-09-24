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
            stumps[1] = MiddleStump.GetComponent<Stump>();   // was LegStump - copy/paste
        }
        if (OffStump != null)
        {
            resetPositions[2] = OffStump.transform.position;
            resetRotations[2] = OffStump.transform.rotation;
            rigidBodies[2] = OffStump.GetComponent<Rigidbody>();
            stumps[2] = OffStump.GetComponent<Stump>();       // was LegStump - copy/paste
        }
    }

    public void Reset()
    {
        ResetOne(LegStump, 0);
        ResetOne(MiddleStump, 1);
        ResetOne(OffStump, 2);
    }

    /// <summary>
    /// Puts one stump back on its mark, dead still.
    ///
    /// <para>The previous version toggled <c>isKinematic</c> around a transform write and never
    /// touched the velocities. A Rigidbody keeps its linear and angular velocity across that
    /// toggle, so a stump that had been knocked flying came back to its mark still carrying the
    /// motion and immediately fell over again - "they fall down as soon as the delivery is
    /// reset". Zeroing has to happen while the body is dynamic, because velocity writes to a
    /// kinematic body are ignored.</para>
    ///
    /// <para>The pose is written through the Rigidbody as well as the Transform.
    /// <c>Physics.autoSyncTransforms</c> is false in this project, so a Transform write on its own
    /// does not reach PhysX until the next physics step - the same trap that broke the ball in
    /// the bowler's hand.</para>
    /// </summary>
    private void ResetOne(GameObject stump, int index)
    {
        if (stump == null)
            return;

        Rigidbody rb = rigidBodies[index];
        if (rb != null)
        {
            // Kill the motion first, while the body still listens to velocity writes.
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.isKinematic = true;
            rb.position = resetPositions[index];
            rb.rotation = resetRotations[index];
        }

        stump.transform.position = resetPositions[index];
        stump.transform.rotation = resetRotations[index];

        if (rb != null)
        {
            rb.isKinematic = false;
            // Zero again after the switch back: the toggle can leave a residue, and a stump that
            // starts the delivery with any motion at all topples, because a capsule standing on
            // its rounded end has a single contact point and no righting moment.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
