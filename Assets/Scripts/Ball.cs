using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody myRigidBody;

    [HideInInspector]
    public float minSwing;
    [HideInInspector]
    public float maxSwing;
    [HideInInspector]
    public eSwingType swingType;
    [HideInInspector]
    public bool fresh;
    [HideInInspector]
    public Vector3 lastVelocity;

    // Start is called before the first frame update
    void Start()
    {
        myRigidBody = GetComponent<Rigidbody>();
        minSwing = 0.1f;
        maxSwing = 0.5f;
        swingType = eSwingType.None;
        fresh = true;
    }

    void LateUpdate()
    {
        lastVelocity = myRigidBody.velocity;

        // Air Resistance Formula
        var p = 1.225f;
        var cd = 0.47f;
        var a = Mathf.PI * 0.0575f * 0.0575f;
        var v = myRigidBody.velocity.magnitude;
        var direction = -myRigidBody.velocity.normalized;
        var forceAmount = (p * v * v * cd * a) / 2;

        if(forceAmount > 0f)
            myRigidBody.AddForce(direction * forceAmount, ForceMode.VelocityChange);

        // Add swing as a percentage of its current force
        Vector3 right = Vector3.zero;
        bool inSwing = Random.Range(0f, 1f) > 0.5f;
        if(swingType == eSwingType.InSwing ||
            (swingType == eSwingType.Random && inSwing))
            right = Vector3.Cross(direction, Vector3.up).normalized; // direction is negative already
        if(swingType == eSwingType.OutSwing ||
            (swingType == eSwingType.Random && !inSwing))
            right = Vector3.Cross(-direction, Vector3.up).normalized;

        if(right != Vector3.zero && forceAmount > 0f)
            myRigidBody.AddForce(right * forceAmount * Random.Range(minSwing, maxSwing), ForceMode.VelocityChange);
    }

    public void OnCollisionExit(Collision collisionInfo)
    {
        // If we hit pitch, and this is our first bounce after release of delivery
        if(fresh && collisionInfo.gameObject.name == "Pitch")
        {
            fresh = false;

            // treat in-swing as leg-spin and out-swing as off-spin
            // Add spin as a percentage of its current force
            var direction = -myRigidBody.velocity.normalized;
            Vector3 right = Vector3.zero;
            bool inSwing = Random.Range(0f, 1f) > 0.5f;
            if (swingType == eSwingType.InSwing ||
                (swingType == eSwingType.Random && inSwing))
                right = Vector3.Cross(-direction, Vector3.up).normalized; // direction is negative already
            if (swingType == eSwingType.OutSwing ||
                (swingType == eSwingType.Random && !inSwing))
                right = Vector3.Cross(direction, Vector3.up).normalized;

            if(right.magnitude > 0f)
                myRigidBody.AddForce(right * 0.1f * myRigidBody.velocity.magnitude * Random.Range(minSwing, maxSwing), ForceMode.VelocityChange);
                // 0.1f can be hooked as difficulty-variable, TODO: Add this to main.cs tweakables
        }

    }
}
