using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody myRigidBody;

    [SerializeField]
    protected AudioClip audioClip;

    [HideInInspector]
    public ParticleSystem myParticles;
    [HideInInspector]
    public bool fresh;
    [HideInInspector]
    public bool bounced;
    [HideInInspector]
    public Vector3 lastVelocity;

    // Start is called before the first frame update
    void Start()
    {
        myRigidBody = GetComponent<Rigidbody>();
        myRigidBody.maxAngularVelocity = 100f;
        fresh = true;
        bounced = false;
    }

    void FixedUpdate()
    {
        Main inst = Main.Instance;
        if (myParticles == null)
            myParticles = GetComponent<ParticleSystem>();

        //If, ball comes to a stop by itself, BEFORE a shot, assume that it was a dead ball
        if (bounced && myRigidBody.velocity.magnitude < 0.1f && inst.gameState == eGameState.InGame_DeliverBallLoop)
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

        if(inst.gameState == eGameState.InGame_BallHitLoop ||
            inst.gameState == eGameState.InGame_BallMissedLoop ||
            inst.gameState == eGameState.InGame_BallPastBoundaryLoop ||
            inst.gameState == eGameState.InGame_BowledLoop ||
            inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            // Air Resistance Formula
            var p = 0.25f; // 1.225f;
            var cd = 0.25f; // 0.47f;
            var a = Mathf.PI * 0.0575f * 0.0575f;
            var v = myRigidBody.velocity.magnitude;
            var direction = -myRigidBody.velocity.normalized;
            var forceAmount = (p * v * v * cd * a) / 2;

            if (forceAmount > 0f)
                myRigidBody.AddForce(direction * forceAmount, ForceMode.Force);

            if(inst.gameState == eGameState.InGame_DeliverBallLoop)
            {
                // Add swing as a percentage of its current force
                if (inst.currentBowlingConfig != null && inst.currentBowlingConfig.applySwing)
                {
                    Vector3 right = Vector3.zero;
                    bool inSwing = Random.Range(0f, 1f) > 0.5f;
                    if (inst.currentBowlingConfig.swingType == eSwingType.InSwing || inst.currentBowlingConfig.swingType == eSwingType.LegSpin ||
                        (inst.currentBowlingConfig.swingType == eSwingType.Random && inSwing))
                        right = Vector3.Cross(direction, Vector3.up).normalized; // direction is negative already
                    if (inst.currentBowlingConfig.swingType == eSwingType.OutSwing || inst.currentBowlingConfig.swingType == eSwingType.OffSpin ||
                        (inst.currentBowlingConfig.swingType == eSwingType.Random && !inSwing))
                        right = Vector3.Cross(-direction, Vector3.up).normalized;

                    if (right != Vector3.zero && forceAmount > 0f)
                    {
                        forceAmount *= 10f;
                        myRigidBody.AddForce(right * forceAmount * inst.currentBowlingConfig.swing, ForceMode.Force);
                        //Debug.Log("SWING: " + (right * forceAmount * inst.currentBowlingConfig.swing).ToString() +
                        //    ", forceAmt: " + forceAmount.ToString() + ", right: " + right.ToString());
                    }
                }
            }
        }
    }

    public void OnCollisionEnter(Collision collisionInfo)
    {
        Main inst = Main.Instance;
        if(inst.gameState == eGameState.InGame_DeliverBall ||
            inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            // If we hit pitch, and this is our first bounce after release of delivery
            if (fresh && collisionInfo.gameObject.name == "Plane")
            {
                fresh = false;

                // treat in-swing as leg-spin and out-swing as off-spin
                // Add spin as a percentage of its current force
                if(inst.currentBowlingConfig != null && inst.currentBowlingConfig.applyPitchTurn)
                {
                    var direction = -myRigidBody.velocity.normalized;
                    Vector3 right = Vector3.zero;
                    bool inSwing = Random.Range(0f, 1f) > 0.5f;
                    if (inst.currentBowlingConfig.swingType == eSwingType.InSwing || inst.currentBowlingConfig.swingType == eSwingType.LegSpin ||
                        (inst.currentBowlingConfig.swingType == eSwingType.Random && inSwing))
                        right = Vector3.Cross(-direction, Vector3.up).normalized; // direction is negative already
                    if (inst.currentBowlingConfig.swingType == eSwingType.OutSwing || inst.currentBowlingConfig.swingType == eSwingType.OffSpin ||
                        (inst.currentBowlingConfig.swingType == eSwingType.Random && !inSwing))
                        right = Vector3.Cross(direction, Vector3.up).normalized;

                    if (right.magnitude > 0f)
                    {
                        myRigidBody.AddForce(right * inst.currentBowlingConfig.pitchTurn * myRigidBody.velocity.magnitude * 0.1f, ForceMode.Impulse);
                        //Debug.Log("TURN: " + (right * inst.currentBowlingConfig.pitchTurn * myRigidBody.velocity.magnitude * 0.1f).ToString());
                    }
                }
            }
        }
        if(inst.gameState == eGameState.InGame_BallHit ||
            inst.gameState == eGameState.InGame_BallHitLoop)
        {
            // If we hit pitch after shot, mark as bounce 
            if (collisionInfo.gameObject.name == "Plane")
                bounced = true;
        }

        if (collisionInfo.gameObject.name.Contains("Stump") && audioClip != null)
        {
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }

    }
}
