using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour
{
    Rigidbody myRigidBody;

    // Start is called before the first frame update
    void Start()
    {
        myRigidBody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        var p = 1.225f;
        var cd = 0.47f;
        var a = Mathf.PI * 0.0575f * 0.0575f;
        var v = myRigidBody.velocity.magnitude;

        var direction = -myRigidBody.velocity.normalized;
        var forceAmount = (p * v * v * cd * a) / 2;
        myRigidBody.AddForce(direction * forceAmount);
    }
}
