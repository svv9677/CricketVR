using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AnimatedFielder : MonoBehaviour
{
    [SerializeField]
    AnimatedFielderManagement animatedFielderManagementScript;
    [SerializeField]
    protected GameObject theObject;
    [SerializeField]
    private GameObject fielderHand;
    [SerializeField]
    protected bool isKeeper;
    [SerializeField]
    private Animator myAnimator;
    //[SerializeField]
    //private AnimationClip runClip;

    private float directionOffset;

    [HideInInspector]
    public Material JerseyMaterial;
    [HideInInspector]
    public Vector3 StartPosition;

    private bool holdBall;
    private bool animationHoldBall;
    private bool isRunning_StartRunning;
    private bool isRunning_FieldTheBall;
    public bool isRunning_RotateTowardsIntercept;
    private bool hasRun_CalculateInterceptPoint;
    private bool isBallInRange;
    private bool amRotatedCorrectly;
    private bool isBallReachable;
    public bool shouldFieldBall;    // Determines whether this fielder should field the ball or not, (only top three intercept times will field the ball)
    private bool canCalculate;

    [SerializeField]
    private float pickUpDistance;

    private float seconds;

    private Vector2 point;
    private bool achievedInterceptPoint;

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
        isRunning_RotateTowardsIntercept = false;
        isBallInRange = false;
        amRotatedCorrectly = false;
        isBallReachable = true;
        canCalculate = false;
        pickUpDistance = 2f;
        fielderState = eFielderState.Idle;

        if (this.gameObject.name.Contains("Keeper"))
            isKeeper = true;

        //runClip.SampleAnimation(this.gameObject, 0f);
        
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

    public void SetState(object state)
    {
        fielderState = (eFielderState)state;
    }

    public void Update()
    {
        Main inst = Main.Instance;
        myAnimInfo = myAnimator.GetCurrentAnimatorStateInfo(0);
        Vector3 ballPos = Main.Instance.theBall.transform.position;
        if (myAnimInfo.IsName("1 Run"))
        {
            pickUpDistance = inst.theBallRigidBody.velocity.magnitude * Time.deltaTime * 30f;
            Vector3 ballFuture = inst.theBall.transform.position + Vector3.Scale(inst.theBallRigidBody.velocity, Vector3.Scale(new Vector3(30f, 30f, 30f), new Vector3(Time.deltaTime, Time.deltaTime, Time.deltaTime)));
            if (canCalculate && Vector3.Distance(transform.position, ballPos) <= pickUpDistance && Vector3.Distance(transform.position, ballFuture) <= 2.9f/22f * 30f)
            {
                if (Main.Instance.currentFielderName == "")
                {
                    Main.Instance.currentFielderName = transform.name;
                    isBallInRange = true;
                }
                //Main.Instance.currentFielderName = transform.name;
            }
            else
            {
                isBallInRange = false;
            }

            if (!isBallReachable)
            {
                if (Vector3.Distance(transform.position, inst.theBall.transform.position) <= 1f)
                {
                    isBallInRange = true;
                }
                else
                {
                    isBallInRange = false;
                }
            }
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
                isRunning_RotateTowardsIntercept = false;
                isBallReachable = true;
                isBallInRange = false;
                amRotatedCorrectly = false;
                achievedInterceptPoint = false;
                shouldFieldBall = false;
                canCalculate = false;
                hasRun_CalculateInterceptPoint = false;
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
                args["transition"] = "punch";
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
                if (Main.Instance.currentFielderName != transform.name && Main.Instance.currentFielderName != "")
                {
                    myAnimator.SetInteger("Action", 0);
                }
                else
                {
                    if (!hasRun_CalculateInterceptPoint)
                    {
                        hasRun_CalculateInterceptPoint = true;
                        StartCoroutine(CalculateInterceptTime());
                    }

                    if (shouldFieldBall)
                    {
                        if (!isRunning_RotateTowardsIntercept)
                        {
                            isRunning_RotateTowardsIntercept = true;
                            StartCoroutine(RotateTowardsIntercept());
                        }
                        if (amRotatedCorrectly)
                        {
                            RunTowardsAndFieldBall();
                            if (myAnimInfo.normalizedTime % 2f < 1f / 22f)
                            {
                                directionOffset = transform.rotation.eulerAngles.y - 5f;
                            }

                            LookTowardsBall();
                        }
                    }
                }
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


    IEnumerator CalculateInterceptTime()
    {
        yield return new WaitForSeconds(0.2f);

        //yield return new WaitForFixedUpdate();
        //yield return new WaitForFixedUpdate();
        // Logic for finding intercept point and rotating towards it;

        //Testing();

        float ballX = Main.Instance.theBall.transform.position.x;
        float ballZ = Main.Instance.theBall.transform.position.z;
        float vx = Main.Instance.theBallRigidBody.velocity.x * 0.8f;
        float vz = Main.Instance.theBallRigidBody.velocity.z * 0.8f;
        float posx = transform.position.x;
        float posz = transform.position.z;
        point = new Vector2();
        float seconds = 0f;
        float time = 0f;


        while (true)
        {
            seconds += 0.5f;
            point.x = ballX + (vx * seconds); // TODO Add friction calculation
            point.y = ballZ + (vz * seconds);
            float distance = Mathf.Sqrt(Mathf.Pow(point.x - posx, 2f) + Mathf.Pow(point.y - posz, 2f));
            time = distance / (((2.75f / 22f)) / Time.deltaTime);  // Time it takes to get to point in frames
            time += (35f + 44f) * Time.deltaTime; // Time and buffer for transitioning to a stop and bending down to pick up ball

            if (Mathf.Abs(point.x) > 61f || Mathf.Abs(point.y) > 61f)
            {
                break;
            }
            if (time < seconds)
            {
                break;
            }
        }


        //StartCoroutine(SetBallReachable(time));
        StartCoroutine(SetBallReachable(Testing()));

        animatedFielderManagementScript.fielders.Add(time, this);
        animatedFielderManagementScript.interceptTimes.Add(time);
    }

    private float Testing()
    {
        float time;

        float bx1 = Main.Instance.theBall.transform.position.x;
        float bz1 = Main.Instance.theBall.transform.position.z;
        float fx = transform.position.x;
        float fz = transform.position.z;

        float bvx = Main.Instance.theBallRigidBody.velocity.x;
        float bvz = Main.Instance.theBallRigidBody.velocity.z;
        float fv = 2.75f / 22f / Time.deltaTime;


        //float bx2 = bx1 + bvx * time;
        //float bz2 = bz1 + bvz * time;

        // if (Mathf.Pow(bx2 - fx, 2) + Mathf.Pow(bz2 - fz, 2) == Mathf.Pow(fv * time, 2))
        // same thing as --> (bx1 + bvx*time - fx)**2 + (bz1 + bvz*time - fz)**2 = (fv*time)**2
        //            or --> (bx2 - fx)**2 + (bz2 - fz)**2 = (fv*time)**2
        // (bx2)^2 - 2(bx2)(fx) + (bz2)^2 - 2(bz2)(fz) = (fv)^2 * (time)^2
        // (bx1)^2 + 2(bx1)(bvx)(time) + ((bvx)^2)((time)^2) - 2(bx1 + (bvx)(time))(fx) + (bz1)^2 + 2(bz1)(bvz)(time) + ((bvz)^2)((time)^2) - 2(bz1 + (bvz)(time))(fz) = (fv)^2 * (time)^2
        // sympy solve the equation above...

        float time1 = (-bvx * bx1 + bvx * fx - bvz * bz1 + bvz * fz - Mathf.Sqrt(-Mathf.Pow(bvx, 2) * Mathf.Pow(bz1, 2) + 2 * Mathf.Pow(bvx, 2) * bz1 * fz - Mathf.Pow(bvx, 2) * Mathf.Pow(fz, 2) + 2 * bvx * bvz * bx1 * bz1 - 2 * bvx * bvz * bx1 * fz - 2 * bvx * bvz * bz1 * fx + 2 * bvx * bvz * fx * fz - Mathf.Pow(bvz, 2) * Mathf.Pow(bx1, 2) + 2 * Mathf.Pow(bvz, 2) * bx1 * fx - Mathf.Pow(bvz, 2) * Mathf.Pow(fx, 2) + Mathf.Pow(bx1, 2) * Mathf.Pow(fv, 2) - 2 * bx1 * Mathf.Pow(fv, 2) * fx + Mathf.Pow(bz1, 2) * Mathf.Pow(fv, 2) - 2 * bz1 * Mathf.Pow(fv, 2) * fz + Mathf.Pow(fv, 2) * Mathf.Pow(fx, 2) + Mathf.Pow(fv, 2) * Mathf.Pow(fz, 2))) / (Mathf.Pow(bvx, 2) + Mathf.Pow(bvz, 2) - Mathf.Pow(fv, 2));
        float time2 = (-bvx * bx1 + bvx * fx - bvz * bz1 + bvz * fz + Mathf.Sqrt(-Mathf.Pow(bvx, 2) * Mathf.Pow(bz1, 2) + 2 * Mathf.Pow(bvx, 2) * bz1 * fz - Mathf.Pow(bvx, 2) * Mathf.Pow(fz, 2) + 2 * bvx * bvz * bx1 * bz1 - 2 * bvx * bvz * bx1 * fz - 2 * bvx * bvz * bz1 * fx + 2 * bvx * bvz * fx * fz - Mathf.Pow(bvz, 2) * Mathf.Pow(bx1, 2) + 2 * Mathf.Pow(bvz, 2) * bx1 * fx - Mathf.Pow(bvz, 2) * Mathf.Pow(fx, 2) + Mathf.Pow(bx1, 2) * Mathf.Pow(fv, 2) - 2 * bx1 * Mathf.Pow(fv, 2) * fx + Mathf.Pow(bz1, 2) * Mathf.Pow(fv, 2) - 2 * bz1 * Mathf.Pow(fv, 2) * fz + Mathf.Pow(fv, 2) * Mathf.Pow(fx, 2) + Mathf.Pow(fv, 2) * Mathf.Pow(fz, 2))) / (Mathf.Pow(bvx, 2) + Mathf.Pow(bvz, 2) - Mathf.Pow(fv, 2));

        if (time1 < time2)
        {
            time = time1;
        }
        else
        {
            time = time2;
        }

        if (time1 <= 0f)
        {
            if (time2 <= 0f)
            {
                time = -1f;
            }
            else
            {
                time = time2;
            }
        }
        if (time2 <= 0f)
        {
            if (time1 <= 0f)
            {
                time = -1f;
            }
            else
            {
                time = time1;
            }
        }

        print(time1);
        print(time2);
        print(time);

        return time;
        
    }


    IEnumerator RotateTowardsIntercept()
    {
        yield return new WaitForFixedUpdate();
        //yield return new WaitForFixedUpdate();
        //yield return new WaitForFixedUpdate();
        // Logic for finding intercept point and rotating towards it;

        float ballX = Main.Instance.theBall.transform.position.x;
        float ballZ = Main.Instance.theBall.transform.position.z;
        float vx = Main.Instance.theBallRigidBody.velocity.x * 0.8f;
        float vz = Main.Instance.theBallRigidBody.velocity.z * 0.8f;
        float posx = transform.position.x;
        float posz = transform.position.z;
        point = new Vector2();
        seconds = 0f;
        float time = 0f;

        while (true)
        {
            seconds += 0.5f;
            point.x = ballX + (vx * seconds); // TODO Add friction calculation
            point.y = ballZ + (vz * seconds);
            float distance = Mathf.Sqrt(Mathf.Pow(point.x - posx, 2f) + Mathf.Pow(point.y - posz, 2f));
            time = distance / (2.75f / (22f / 30f));  // Time it takes to get to point in frames
            time += (35f + 44f) * Time.deltaTime; // Time and buffer for transitioning to a stop and bending down to pick up ball

            if (Mathf.Abs(point.x) > 61f || Mathf.Abs(point.y) > 61f)
            {
                isBallReachable = false;
                break;
            }
            if (time < seconds)
            {
                break;
            }
        }

        //if (isBallReachable)
        //{
        //    Vector3 prev = transform.rotation.eulerAngles;
        //    transform.LookAt(Main.Instance.theBall.transform.position);
        //    transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(prev), transform.rotation, 3f);
        //    transform.rotation = Quaternion.Euler(new Vector3(prev.x, transform.rotation.eulerAngles.y + 0f, prev.z));
        //}

        Vector3 prev = transform.rotation.eulerAngles;
        transform.LookAt(new Vector3(point.x, 0f, point.y));
        transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(prev), transform.rotation, 3f);
        transform.rotation = Quaternion.Euler(new Vector3(prev.x, transform.rotation.eulerAngles.y, prev.z));


        amRotatedCorrectly = true;
        
        isRunning_RotateTowardsIntercept = false;
    }

    IEnumerator SetBallReachable(float time)
    {
        if (time > 0)
        {
            yield return new WaitForSeconds(time);

            canCalculate = true;
        }
        else
        {
            yield return null;
        }
        
    }

    void LookTowardsBall()
    {
        Vector3 ballPos;
        Vector3 prev = transform.rotation.eulerAngles;
        ballPos = Main.Instance.theBall.transform.position;

        
        if (isBallReachable)
        {
            if (Mathf.Sqrt(Mathf.Pow(transform.position.x - ballPos.x, 2f) +
                       Mathf.Pow(transform.position.z - ballPos.z, 2f)) <= 6f)
            {
                isBallReachable = false;
                
            }

            else
            {
                if (Mathf.Sqrt(Mathf.Pow(transform.position.x - point.x, 2f) + Mathf.Pow(transform.position.z - point.y, 2f)) <= 1f && achievedInterceptPoint == false)
                {
                    achievedInterceptPoint = true;
                    prev = transform.rotation.eulerAngles;
                    transform.LookAt(new Vector3(point.x, 0f, point.y));
                    transform.rotation = Quaternion.Euler(new Vector3(prev.x, transform.rotation.eulerAngles.y, prev.z));
                }

                if (achievedInterceptPoint)
                {
                    isBallReachable = false;
                }
            }

        }

        else
        {
            transform.LookAt(ballPos);
            //transform.rotation = new Quaternion(transform.rotation.x, transform.rotation.y - 0.03f, transform.rotation.z, transform.rotation.w);
            transform.rotation = Quaternion.RotateTowards(Quaternion.Euler(prev), transform.rotation, 3f);
            transform.rotation = Quaternion.Euler(new Vector3(prev.x, transform.rotation.eulerAngles.y + 0f, prev.z));
        }
        
    }


    void RunTowardsAndFieldBall()
    {
        if (myAnimInfo.IsName("0 Idle") && !isRunning_FieldTheBall)
        {
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
        Main inst = Main.Instance;
        myAnimator.SetInteger("Action", 0);
        while (!myAnimInfo.IsName("0 Idle"))
        {
            yield return null;
        }

        //Debug.Log("YAYYYYYYYYYYYYYYYYYYY");

        ////////////////////////////////////////////////////////
        /// Insert logic for choosing between the "Catch" animation and the "Pick Up" animation based on the height of the ball.
        ////////////////////////////////////////////////////////

        if (inst.currentFielderName == transform.name)
        {
            myAnimator.SetInteger("Action", 4);
            while (!myAnimInfo.IsName("4 Pick Up"))
            {
                yield return null;
            }

            while (myAnimInfo.normalizedTime % 1f < 0.29f)
            {
                yield return null;
            }

            holdBall = true;
            animationHoldBall = true;
        }

        ////////////////////////////////////////////////////////
        /// Insert logic for throwing: calculating distance, speed and angleof throw, etc
        ////////////////////////////////////////////////////////

        myAnimator.SetInteger("Action", 0);
        // pause the particles
        inst.theBallScript.myParticles.Stop();
        inst.theBallScript.myParticles.Clear();
        //Set collision type to continuous speculative
        inst.theBallRigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        // disable physics
        inst.theBallRigidBody.isKinematic = true;
        // make the ball static
        inst.theBallRigidBody.velocity = Vector3.zero;
        inst.gameState = eGameState.InGame_BallFielded;

        isRunning_FieldTheBall = false;
    }
}

