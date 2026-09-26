using System.Collections;
using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody myRigidBody;

    [SerializeField]
    protected AudioClip audioClip;

    //[HideInInspector]
    //public ParticleSystem myParticles;
    [HideInInspector]
    public TrailRenderer myParticles;
    [HideInInspector]
    public bool fresh;
    [HideInInspector]
    public bool bounced;
    [HideInInspector]
    public bool wide;
    [HideInInspector]
    public Vector3 lastVelocity;

    /// Swing and turn for the delivery in flight, set by Main at release.
    [HideInInspector]
    public BallFlight.DeliveryEffects deliveryEffects;
    private bool touchingGround;

    /// Where the current delivery actually pitched, and where it crossed the batsman's stumps
    /// (NaN until it has). What the aim is checked against.
    [HideInInspector] public Vector3 lastPitchPoint = new Vector3(float.NaN, 0f, 0f);
    [HideInInspector] public float lastLineAtStumps = float.NaN;
    private Vector3 prevPosition;

    // Start is called before the first frame update
    void Start()
    {
        myRigidBody = GetComponent<Rigidbody>();
        myRigidBody.maxAngularVelocity = 100f;
        fresh = true;
        bounced = false;
        wide = false;
    }

    void FixedUpdate()
    {
        Main inst = Main.Instance;
        //if (myParticles == null)  PARTICLE
        //    myParticles = GetComponent<ParticleSystem>();
        if (myParticles == null)
           myParticles = GetComponent<TrailRenderer>();

        //If, ball comes to a stop by itself, BEFORE a shot, assume that it was a dead ball
        if (bounced && myRigidBody.linearVelocity.magnitude < 0.1f && inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            inst.resetDelay = 0.5f;
            inst.gameState = eGameState.InGame_ResetToReady;
        }

        // Set the particles to not leave unwanted trails
        if (inst.gameState != eGameState.InGame_BallHit &&
            inst.gameState != eGameState.InGame_BallHitLoop &&
            inst.gameState != eGameState.InGame_BallMissed &&
            inst.gameState != eGameState.InGame_BallMissedLoop &&
            inst.gameState != eGameState.InGame_BallPastBoundary &&
            inst.gameState != eGameState.InGame_BallPastBoundaryLoop &&
            inst.gameState != eGameState.InGame_DeliverBall &&
            inst.gameState != eGameState.InGame_DeliverBallLoop)
        {
            //if (myParticles.isPlaying) PARTICLE
            //{
            //    myParticles.Stop();
            //    myParticles.Clear();
            //}
            if (myParticles.enabled)
            {
                myParticles.Clear();
                myParticles.enabled = false;
            }
        }
        //else if (!myParticles.isPlaying) PARTICLE
        //{
        //    myParticles.Play();
        //}
        else if (!myParticles.enabled)
        {
            myParticles.enabled = true;
        }

        Vector3 here = myRigidBody.position;
        if (inst.gameState == eGameState.InGame_DeliverBallLoop && float.IsNaN(lastLineAtStumps) &&
            prevPosition.x < BallDelivery.BatsmanStumpsX && here.x >= BallDelivery.BatsmanStumpsX)
        {
            float f = (BallDelivery.BatsmanStumpsX - prevPosition.x) / Mathf.Max(1e-5f, here.x - prevPosition.x);
            lastLineAtStumps = Mathf.Lerp(prevPosition.z, here.z, f);
        }
        prevPosition = here;

        // Air and ground forces, from the shared model in BallFlight so that delivery aiming and the
        // fielders' prediction see exactly the flight the player sees.
        if (!myRigidBody.isKinematic)
        {
            Vector3 velocity = myRigidBody.linearVelocity;
            bool swinging = inst.gameState == eGameState.InGame_DeliverBallLoop && fresh;
            Vector3 air = BallFlight.AirAcceleration(velocity,
                swinging ? deliveryEffects.swingAccelPerV2 : 0f, deliveryEffects.swingSign);
            myRigidBody.AddForce(air, ForceMode.Acceleration);

            // PhysX has no rolling resistance, so without this a ball on the ground rolls forever.
            if (touchingGround && Mathf.Abs(velocity.y) < 0.5f)
            {
                Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
                float speed = horizontal.magnitude;
                if (speed > 1e-4f)
                {
                    float slowed = Mathf.Max(0f, speed - BallFlight.RollingDecel * Time.fixedDeltaTime);
                    myRigidBody.linearVelocity = new Vector3(velocity.x * slowed / speed, velocity.y, velocity.z * slowed / speed);
                    myRigidBody.angularVelocity *= slowed / speed;
                }
            }
        }
        touchingGround = false;

        if (transform.position.y <= -10f)
        {
            myRigidBody.linearVelocity = Vector3.zero;
            myRigidBody.isKinematic = true;
        }
    }

    public void PlayTrail()
    {

    }

    public void StopTrail()
    {

    }

    private void LateUpdate()
    {
        lastVelocity = myRigidBody.linearVelocity;
    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if (fresh && collision.gameObject.name == "Plane")
    //    {
    //        firstImpact = transform.position.x;
    //        //print("1: " + transform.position.x);
    //        //print(Main.Instance.currentBowlingConfig.length);
    //        //print("### " + transform.position.z);
    //    }
    //}

    private void OnCollisionStay(Collision collisionInfo)
    {
        if (collisionInfo.gameObject.CompareTag("Ground"))
            touchingGround = true;
    }

    public void OnCollisionEnter(Collision collisionInfo)
    {
        Main inst = Main.Instance;
        if(inst.gameState == eGameState.InGame_DeliverBall ||
            inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            // If we hit pitch, and this is our first bounce after release of delivery
            if (fresh && collisionInfo.gameObject.CompareTag("Ground"))
            {
                //print(transform.position.x);
                //print(((firstImpact + transform.position.x) / 2) - Main.Instance.currentBowlingConfig.length);
                fresh = false;
                lastPitchPoint = collisionInfo.GetContact(0).point;

                // Turn off the pitch: PhysX has already bounced the ball, so rotate the velocity it
                // bounced with (BallFlight.Turn - the same rule BallDelivery aimed with).
                if (deliveryEffects.turnDegrees != 0f)
                    myRigidBody.linearVelocity = BallFlight.Turn(myRigidBody.linearVelocity, deliveryEffects.turnDegrees);
            }
        }
        if(inst.gameState == eGameState.InGame_BallHit ||
            inst.gameState == eGameState.InGame_BallHitLoop)
        {
            // If we hit the ground after the shot, mark as bounce.
            // (Was name == "Plane" - an object that does not exist in the CricketVR scene,
            //  so this never fired and the dead-ball reset in FixedUpdate never ran.)
            if (collisionInfo.gameObject.CompareTag("Ground"))
                bounced = true;
        }

        if (collisionInfo.gameObject.CompareTag("Ground"))
            touchingGround = true;

        //if (collisionInfo.gameObject.name.Contains("Stump") && audioClip != null)
        //{
        //    if (this.GetComponent<Collider>().bounds.Intersects(collisionInfo.gameObject.GetComponent<Collider>().bounds))
        //    {
        //        AudioSource.PlayClipAtPoint(audioClip, transform.position);
        //    }
        //}
    }

    

    
}
