using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody myRigidBody;

    [SerializeField]
    protected AudioClip audioClip;

    [HideInInspector]
    public ParticleSystem myParticles;
    [HideInInspector]
    public float pitchTurn;
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
        pitchTurn = 0.1f;
        swingType = eSwingType.None;
        fresh = true;
    }

    void FixedUpdate()
    {
        if (myParticles == null)
            myParticles = GetComponent<ParticleSystem>();

        // If, ball comes to a stop by itself, after a shot, assume that it was collected by a fielder
        //if (myRigidBody.velocity.magnitude < 0.1f && Main.Instance.gameState == eGameState.InGame_BallHitLoop)
        //{
        //    Main.Instance.currentFielderName = "";
        //    Main.Instance.gameState = eGameState.InGame_BallFielded;
        //}

        // Set the particles to not leave unwanted trails
        if (Main.Instance.gameState != eGameState.InGame_BallHit &&
            Main.Instance.gameState != eGameState.InGame_BallHitLoop &&
            Main.Instance.gameState != eGameState.InGame_BallMissed &&
            Main.Instance.gameState != eGameState.InGame_BallMissedLoop &&
            Main.Instance.gameState != eGameState.InGame_BallPastBoundary &&
            Main.Instance.gameState != eGameState.InGame_BallPastBoundaryLoop &&
            Main.Instance.gameState != eGameState.InGame_DeliverBall &&
            Main.Instance.gameState != eGameState.InGame_DeliverBallLoop)
        {
            if(myParticles.isPlaying)
            {
                myParticles.Stop();
                myParticles.Clear();
            }
        }
        else if (!myParticles.isPlaying)
        {
            myParticles.Play();
        }

        // Air Resistance Formula
        var p = 0.25f; // 1.225f;
        var cd = 0.25f; // 0.47f;
        var a = Mathf.PI * 0.0575f * 0.0575f;
        var v = myRigidBody.velocity.magnitude;
        var direction = -myRigidBody.velocity.normalized;
        var forceAmount = (p * v * v * cd * a) / 2;

        if(forceAmount > 0f)
            myRigidBody.AddForce(direction * forceAmount, ForceMode.Force);

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
            myRigidBody.AddForce(right * forceAmount * Random.Range(minSwing, maxSwing), ForceMode.Force);
    }

    public void OnCollisionEnter(Collision collisionInfo)
    {
        // If we hit pitch, and this is our first bounce after release of delivery
        if(fresh && collisionInfo.gameObject.name == "Plane")
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
                myRigidBody.AddForce(right * pitchTurn * myRigidBody.velocity.magnitude * Random.Range(minSwing, maxSwing), ForceMode.Impulse);
        }

        if(collisionInfo.gameObject.name.Contains("Stump") && audioClip != null)
        {
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }

    }
}
