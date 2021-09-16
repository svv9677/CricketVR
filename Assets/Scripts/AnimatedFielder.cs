using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AnimatedFielder : MonoBehaviour
{
    [SerializeField]
    protected GameObject theObject;
    [SerializeField]
    private GameObject fielderHand;
    [SerializeField]
    protected bool isKeeper;
    [SerializeField]
    private Animator myAnimator;

    [HideInInspector]
    public Material JerseyMaterial;
    [HideInInspector]
    public Vector3 StartPosition;

    private bool holdBall;
    private bool animationHoldBall;
    private bool isRunning_StartRunning;
    private bool isRunning_FieldTheBall;
    private bool isBallInRange;

    [SerializeField]
    private float pickUpDistance;

    private eFielderState fielderState;
    private AnimatorStateInfo myAnimInfo;

    // Start is called before the first frame update
    void Start()
    {
        if (theObject != null)
        {
            Material[] mats = theObject.GetComponent<Renderer>().materials;
            foreach (Material mat in mats)
            {
                if (mat.name.Contains("Material.001"))
                {
                    JerseyMaterial = mat;
                    break;
                }
            }
        }
        StartPosition = this.transform.position;
        Main.Instance.onGameStateChanged += HandleGameState;
        holdBall = false;
        animationHoldBall = false;
        isRunning_StartRunning = false;
        isRunning_FieldTheBall = false;
        isBallInRange = false;
        pickUpDistance = 2f;
        fielderState = eFielderState.Idle;

        if (this.gameObject.name.Contains("Keeper"))
            isKeeper = true;

        
    }

    public void OnDestroy()
    {
        Main.Instance.onGameStateChanged -= HandleGameState;
    }

    //private void OnDrawGizmos()
    //{
    //    Gizmos.DrawSphere(transform.position + (transform.forward * 0.85f) + transform.up, 0.365f);
    //}

    public void HandleGameState()
    {
        Main inst = Main.Instance;
        if ((inst.gameState == eGameState.InGame_BallFielded ||
            inst.gameState == eGameState.InGame_BallFielded_Loop ||
            inst.gameState == eGameState.InGame_ResetToReady ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop ||
            inst.gameState == eGameState.InGame_Ready) &&
           inst.currentFielderName == transform.name)
        {
            holdBall = true;
        }
        else
        {
            holdBall = false;
            animationHoldBall = false;
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Ball" && !holdBall)
        {
            Main inst = Main.Instance;
            if (inst.gameState != eGameState.InGame_BallHitLoop && inst.gameState != eGameState.InGame_DeliverBallLoop)
            {
                Debug.LogWarning("CAUTION: " + gameObject.name + " collided with ball, but game state was " + inst.gameState.ToString());
                return;
            }
            /*
            if (isKeeper)
            {
                // batsman missed the ball
                if (inst.gameState == eGameState.InGame_DeliverBallLoop)
                {
                    inst.gameState = eGameState.InGame_BallMissed;
                }

                if (inst.gameState == eGameState.InGame_BallHitLoop)
                {
                    inst.currentFielderName = transform.name;
                    inst.gameState = eGameState.InGame_BallFielded;
                }
            }
            else
            {
                inst.currentFielderName = transform.name;
                holdBall = true;  // Finished : Replace this with a call for a method that waits for the correct time to set holdBall=true for the current fielding animation.
                isBallInRange = true;
            }
            
            // pause the particles
            inst.theBallScript.myParticles.Stop();
            inst.theBallScript.myParticles.Clear();
            // disable physics
            inst.theBallRigidBody.isKinematic = true;
            // make the ball static
            inst.theBallRigidBody.velocity = Vector3.zero;
            */
        }
    }

    public void SetState(object state)
    {
        fielderState = (eFielderState)state;
    }

    public void Update()
    {
        myAnimInfo = myAnimator.GetCurrentAnimatorStateInfo(0);
        Vector3 ballpos = Main.Instance.theBall.transform.position;
        if (Mathf.Sqrt(Mathf.Pow(transform.position.x - ballpos.x, 2f) +
                       Mathf.Pow(transform.position.y - ballpos.y, 2f) +
                       Mathf.Pow(transform.position.z - ballpos.z, 2f)) <= pickUpDistance)
        {
            isBallInRange = true;
            Main.Instance.currentFielderName = transform.name;
        }
        else
        {
            isBallInRange = false;
        }
    }
    public void LateUpdate()
    {
        Main inst = Main.Instance;
        if (holdBall && animationHoldBall)
            inst.theBall.transform.position = fielderHand.transform.position;

        if (inst.gameState == eGameState.InGame_SelectDelivery ||
           inst.gameState == eGameState.InGame_SelectDeliveryLoop ||
           inst.gameState == eGameState.InGame_DeliverBall ||
           inst.gameState == eGameState.InGame_Ready ||
           inst.gameState == eGameState.InGame_ResetToReadyLoop)
        {
            if (fielderState != eFielderState.Idle && fielderState != eFielderState.ToIdle)
            {
                myAnimator.SetInteger("Action", 0);
                fielderState = eFielderState.ToIdle;

                isRunning_StartRunning = false;
                isRunning_FieldTheBall = false;
                isBallInRange = false;
                //animationHoldBall = false;  <-- Insert if maybe the holdBall timing isn't working.

                Hashtable args = new Hashtable();
                Vector3 pos = inst.theStumps.transform.position;
                pos.y = transform.position.y;
                args["position"] = StartPosition;
                args["speed"] = inst.fielderSpeed;
                args["looktarget"] = pos;
                args["looktime"] = 1f;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.Idle;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if (inst.gameState == eGameState.InGame_DeliverBallLoop)
        {
            if (fielderState != eFielderState.TakingStart && fielderState != eFielderState.ToTakingStart &&
                !isKeeper)
            {
                fielderState = eFielderState.ToTakingStart;

                Hashtable args = new Hashtable();
                Vector3 pos = transform.position + (transform.forward * 2f);
                pos.y = transform.position.y;
                args["position"] = pos;
                args["speed"] = inst.fielderSpeed;
                args["oncomplete"] = "SetState";
                args["oncompleteparams"] = eFielderState.TakingStart;
                iTween.MoveTo(this.gameObject, args);
            }
        }

        if (inst.gameState == eGameState.InGame_BallHit ||
           inst.gameState == eGameState.InGame_BallHitLoop)
        {
            if (fielderState != eFielderState.Fielded &&
                !isKeeper)
            {
                fielderState = eFielderState.MovingTowardsBall;
                RunTowardsAndFieldBall();
                LookTowardsBall();
            }
        }

        if (inst.gameState == eGameState.InGame_BallFielded ||
            inst.gameState == eGameState.InGame_BallFielded_Loop ||
            inst.gameState == eGameState.InGame_BallPastBoundary ||
            inst.gameState == eGameState.InGame_BallPastBoundaryLoop ||
            inst.gameState == eGameState.InGame_Bowled ||
            inst.gameState == eGameState.InGame_BowledLoop)
        {
            myAnimator.SetInteger("Action", 0);
        }
    }

    void LookTowardsBall()
    {
        Vector3 ballPos;
        Vector3 prev = transform.rotation.eulerAngles;
        ballPos = Main.Instance.theBall.transform.position;
        transform.LookAt(ballPos);
        transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(prev), transform.rotation, 4f);
        transform.rotation = Quaternion.Euler(new Vector3(prev.x, transform.rotation.eulerAngles.y, prev.z));
    }


    void RunTowardsAndFieldBall()
    {
        if (myAnimInfo.IsName("0 Idle") && !isRunning_FieldTheBall)
        {
            Debug.Log("1");
            if (!isRunning_StartRunning)
            {
                StartCoroutine(StartRunning());
                isRunning_StartRunning = true;
            }
        }

        else if (myAnimInfo.IsName("1 Run"))
        {
            
            if (isBallInRange)
            {
                if (!isRunning_FieldTheBall)
                {
                    StartCoroutine(FieldTheBall());
                    isRunning_FieldTheBall = true;
                }
            }

            //LookTowardsBall();
        }
    }

    IEnumerator StartRunning()
    {
        animationHoldBall = false;
        myAnimator.SetInteger("Action", 1);
        while (!myAnimInfo.IsName("1 Run"))
        {
            yield return null;
        }
        isRunning_StartRunning = false;
        
    }

    IEnumerator FieldTheBall()
    {
        
        myAnimator.SetInteger("Action", 0);
        /*if (Main.Instance.currentFielderName != transform.name)
        {
            yield break;
        }*/
        while (!myAnimInfo.IsName("0 Idle"))
        {
            yield return null;
        }

        Debug.Log("YAYYYYYYYYYYYYYYYYYYY");

        ////////////////////////////////////////////////////////
        /// Insert logic for choosing between the "Catch" animation and the "Pick Up" animation based on the height of the ball.
        ////////////////////////////////////////////////////////

        myAnimator.SetInteger("Action", 4);
        while (!myAnimInfo.IsName("4 Pick Up"))
        {
            yield return null;
        }

        while (myAnimInfo.normalizedTime % 1f < 0.49f)
        {
            yield return null;
        }

        holdBall = true;
        animationHoldBall = true;

        ////////////////////////////////////////////////////////
        /// Insert logic for throwing: calculating distance, speed and angleof throw, etc
        ////////////////////////////////////////////////////////

        myAnimator.SetInteger("Action", 0);
        Main.Instance.gameState = eGameState.InGame_BallFielded;
    }
}

