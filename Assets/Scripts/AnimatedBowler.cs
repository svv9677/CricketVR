using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class AnimatedBowler : MonoBehaviour
{
    public static AnimatedBowler Instance;

    [SerializeField]
    private GameObject hand;

    //private float runupLength;
    //private Vector3 startPosition;
    //private Vector3 startRotation;

    private Animator animator;
    private AnimatorStateInfo stateInfo;

    private Coroutine startBowling;
    private Coroutine walkingBack;
    private bool hasRun_StartBowling;

    public AFInfoData myBowlers;
    private AFInfo currentBowlerInfo;

    private bool glide;
    private int myFrame;
    private Vector3 myPrevPos;

    /// <summary>
    /// True from the start of the run-up until the release animation event. While it is set the
    /// ball is driven onto the hand every LateUpdate.
    ///
    /// <para>This replaces parenting the ball to the hand bone. That approach used
    /// <c>SetParent(hand.transform)</c>, which keeps the ball's world position and therefore gives
    /// it a large local offset - the bowler is teleported to the top of his run-up in the same
    /// frame, so the offset was about 12 m - and then relied on a <c>parent.name == hand.name</c>
    /// check in LateUpdate to zero it again. On device that zeroing did not happen from the second
    /// delivery onwards, leaving the ball orbiting the hand on a 12 m arm. Device telemetry:</para>
    ///
    /// <code>
    /// runup: ballPos=(-11.48, 9.92, 3.73) ballLocal=(-8.382, 6.285, -3.876) parent=Offset
    /// [Delivery] ... [corrected: release (-1.36, 7.11, 8.10) outside the plausible box]
    /// </code>
    ///
    /// <para>Driving the position directly has no local-space offset to lose and no name
    /// comparison to get wrong.</para>
    /// </summary>
    private bool ballInHand;

    /// <summary>
    /// Where the hand was at the exact frame the release animation event fired. This is the single
    /// source of truth for where a delivery starts; reading the ball's own transform instead meant
    /// reading whatever the ball had drifted to.
    /// </summary>
    public Vector3 ReleasePosition { get; private set; }

    /// False until the first release of the session, so callers know whether ReleasePosition means
    /// anything yet.
    public bool HasReleasePosition { get; private set; }

    /// The bowling hand, for callers that need it directly. Null if the reference was lost.
    public Transform HandTransform => hand == null ? null : hand.transform;

    public AFInfoData configs;

    private void Awake()
    {
        configs = new AFInfoData();
        // Marks moved so the front foot lands on the popping crease (x -9.14: toes just over it,
        // heel behind) close in to the stumps. Measured from the old marks, front toe at release:
        // pace and spin x -9.39, swing -9.20..-9.33 (one run to the next), all at z 0.75-0.8, 0.65 m
        // wide of the stumps. Hence forward 0.29 / 0.17 m and 0.28 m across.
        const float PaceIn = 0.29f, SwingIn = 0.17f, SpinIn = 0.29f, Across = 0.28f;
        configs.data.Add(new AFInfo(new Vector3(-9f - (6f * transform.localScale.x) + PaceIn, 0f, 2.6f - Across), new Vector3(0f, 90f, 0f), 2, 4.5f, 1, 3));
        configs.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x) + SwingIn, 0f, 2.3f - Across), new Vector3(0f, 90f, 0f), 4, 4.5f, 0, 2));
        configs.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x) + SwingIn, 0f, 2.3f - Across), new Vector3(0f, 90f, 0f), 4, 4.5f, 0, 2));
        configs.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x) + SpinIn, 0f, 2f - Across), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
        configs.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x) + SpinIn, 0f, 2f - Across), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
        myBowlers = new AFInfoData();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (AnimatedBowler.Instance == null)
            AnimatedBowler.Instance = this;

        //runupLength = 7f * transform.localScale.x;
        //startPosition = new Vector3(-9f - runupLength, 0f, 2.6f);
        //startRotation = new Vector3(0f, 90f, 0f);

        animator = GetComponent<Animator>();

        hasRun_StartBowling = false;
        // After he has bowled he fields like anyone else (BowlerFielding).
        if (GetComponent<BowlerFielding>() == null)
            gameObject.AddComponent<BowlerFielding>();
    }

    private void Update()
    {
        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    }

    // Update is called once per frame
    /// Fielding after his delivery (BowlerFielding): his bowling code stands aside - no walk back
    /// to his mark, no turning toward it - until the next ball hands him back.
    public bool Fielding { get; private set; }

    public void BeginFielding()
    {
        Fielding = true;
        if (walkingBack != null)
            StopCoroutine(walkingBack);
        walkingBack = null;
        glide = false;
    }

    public void EndFielding(RuntimeAnimatorController bowlingController)
    {
        Fielding = false;
        // LateUpdate clears this between balls, but it stood aside the whole time he was fielding:
        // left set from the last run-up, the next one never started and the game waited for a
        // delivery that never came (played end to end: stuck on the second ball).
        hasRun_StartBowling = false;
        if (startBowling != null)
            StopCoroutine(startBowling);
        startBowling = null;
        animator.runtimeAnimatorController = bowlingController;
        animator.applyRootMotion = true;
        animator.speed = 1f;
        animator.SetInteger("Action", 0);
    }

    void LateUpdate()
    {
        if (Fielding)
            return;
        Main inst = Main.Instance;

        // First, before anything below can fail: keep the ball on the hand. The old pin sat at the
        // bottom of LateUpdate, so any exception above it silently left the ball behind.
        HoldBall(inst);

        if (inst.gameState == eGameState.InGame_Ready ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop)
        {
            hasRun_StartBowling = false;
        }

        if (inst.gameState == eGameState.InGame_SelectDeliveryLoop)
        {
            // The next ball: straight to his mark, whatever the last one left him doing. The run-up
            // used to wait for Action 0, which only came when the walk back from the last ball got
            // him home - ~11 s after the first ball of a session (traced: walking from x -7.6 to
            // -24.5 before the jog began; run-ups 12.2 s against 4.6 s from the mark). It had a reset
            // in the SelectDelivery state, but Main moves on from that state within its own Update,
            // so this LateUpdate never saw it.
            if (!hasRun_StartBowling && animator.GetInteger("Action") != 0)
                ReturnToMark();
            if (!hasRun_StartBowling && animator.GetInteger("Action") == 0)
            {
                transform.position = currentBowlerInfo.startPos;
                transform.rotation = Quaternion.Euler(currentBowlerInfo.startRot);
                startBowling = StartCoroutine(StartBowling());
                hasRun_StartBowling = true;
            }
        }
        if (inst.gameState != eGameState.InGame_SelectDelivery &&
            inst.gameState != eGameState.InGame_SelectDeliveryLoop)
        {
            if (startBowling != null)
            {
                StopCoroutine(startBowling);
            }
        }

        if (animator.GetInteger("Action") == -1)
        {
            Quaternion prev = transform.rotation;
            transform.LookAt(currentBowlerInfo.startPos);
            transform.rotation = Quaternion.RotateTowards(prev, transform.rotation, 3f);
        }
    }

    private void FixedUpdate()
    {
        if (glide && !Fielding)
        {
            // Frames it takes for the transition from walk to idle to complete
            float totalFrames = 60f;
            myFrame++;
            transform.position = myPrevPos + (new Vector3(currentBowlerInfo.startPos.x - myPrevPos.x, currentBowlerInfo.startPos.y - myPrevPos.y, currentBowlerInfo.startPos.z - myPrevPos.z) / totalFrames * myFrame);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(currentBowlerInfo.startRot), 3f);
            if (myFrame == totalFrames)
            {
                glide = false;
            }
        }
    }

    /// <summary>
    /// Pins the ball to the bowling hand for as long as he is carrying it. Runs first in
    /// LateUpdate, after the Animator has posed the skeleton for this frame, so the ball sits on
    /// the hand in the pose the player actually sees.
    /// </summary>
    private void HoldBall(Main inst)
    {
        if (!ballInHand || hand == null || inst == null || inst.theBall == null)
            return;

        // The ball is only ever carried during the run-up. Bounding it by state as well as by the
        // flag means a missed ReleaseBall event cannot drag the ball around during play.
        if (inst.gameState != eGameState.InGame_SelectDelivery &&
            inst.gameState != eGameState.InGame_SelectDeliveryLoop)
        {
            ballInHand = false;
            return;
        }

        Vector3 target = hand.transform.position;
        Rigidbody rb = inst.theBallRigidBody;

        // A carried ball must not collide with anything. It is teleported roughly 11 m onto the
        // hand when the bowler jumps to the top of his run-up, and PhysX reads a kinematic body's
        // teleport as motion: it reported the ball sweeping through the bowler's-end stumps at
        // 1786 m/s and launched them out of the ground. They have no reset, so they stayed gone -
        // which is what "the stumps at the bowling end sometimes disappear" actually was.
        Collider ballCollider = inst.theBall.GetComponent<Collider>();
        if (ballCollider != null && ballCollider.enabled)
            ballCollider.enabled = false;

        // Move the RIGIDBODY, not just the Transform. The ball is kinematic while it is carried,
        // and a kinematic body with interpolation enabled has its Transform overwritten from the
        // rigidbody's own pose before rendering - so a plain `transform.position = ...` here is
        // silently thrown away. Measured: the ball fell 0.45 m, then 0.84 m, then 1.54 m behind
        // the hand as the arm sped up. `Physics.autoSyncTransforms` is false in this project, so
        // nothing pushes the Transform back into PhysX to compensate.
        //
        // This is also what broke the old parented version. Its pin wrote `localPosition = 0` and
        // that write was discarded the same way; parenting merely hid the damage, because the
        // hierarchy re-applied whatever stale local offset the ball had. On device that offset was
        // about 12 m, which is where the wild release points came from.
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.None;   // restored at release, in Main
            rb.position = target;
        }
        inst.theBall.transform.position = target;
    }

    private IEnumerator StartBowling()
    {
        Main inst = Main.Instance;
        inst.theBallRigidBody.isKinematic = true;
        // Carry the ball by driving its world position (see ballInHand) rather than parenting it.
        inst.theBall.transform.SetParent(null);
        ballInHand = true;
        HoldBall(inst);

        animator.SetInteger("Jog Repeat", currentBowlerInfo.jogRepeat + 1);
        animator.SetInteger("Run Repeat", currentBowlerInfo.runRepeat + 2);

        animator.SetFloat("Time", (0.5f * inst.currentBowlingConfig.speedX + 1.5f) / currentBowlerInfo.animSpeed);

        while (!stateInfo.IsName("Idle"))
        {
            yield return null;
        }

        transform.position = currentBowlerInfo.startPos;
        transform.rotation = Quaternion.Euler(currentBowlerInfo.startRot);

        animator.SetInteger("Action", 1);

        while (!stateInfo.IsName("Jog"))
        {
            yield return null;
        }

        animator.SetInteger("Action", currentBowlerInfo.myIndex);

        while (!stateInfo.IsName("Run"))
        {
            yield return null;
        }

        while (stateInfo.IsName("Run"))
        {
            yield return null;
        }
    }

    public void UpdateInfo(int index)
    {
        currentBowlerInfo = myBowlers.data[index];
    }

    /// <summary>
    /// Animation event fired at the top of the bowling action, the frame the ball leaves the hand.
    /// Capture the hand pose here: this is the only moment at which it is the release point, and
    /// sampling it anywhere else picks up however far the arm has swung through since.
    /// </summary>
    public void ReleaseBall()
    {
        ballInHand = false;
        if (hand != null)
        {
            ReleasePosition = hand.transform.position;
            HasReleasePosition = true;
        }

        Main.Instance.gameState = eGameState.InGame_DeliverBall;

        animator.SetInteger("Action", 0);
        walkingBack = StartCoroutine(StartWalkingBack());
    }

    /// Stop walking back and stand on the mark, ready to run in. He is 20 m away up the pitch.
    private void ReturnToMark()
    {
        if (walkingBack != null)
            StopCoroutine(walkingBack);
        walkingBack = null;
        glide = false;
        transform.position = currentBowlerInfo.startPos;
        transform.rotation = Quaternion.Euler(currentBowlerInfo.startRot);
        animator.Play("Idle");
        animator.SetInteger("Action", 0);
    }

    /// Start the walk back to his mark now (ThrowBack: the keeper has the ball), rather than on
    /// the timer after his delivery. Not during a run-up or delivery.
    public void WalkBackToMark()
    {
        eGameState s = Main.Instance.gameState;
        if (Fielding || s == eGameState.InGame_SelectDelivery || s == eGameState.InGame_SelectDeliveryLoop ||
            s == eGameState.InGame_DeliverBall || s == eGameState.InGame_DeliverBallLoop)
            return;
        if (walkingBack != null)
            StopCoroutine(walkingBack);
        walkingBack = null;
        if (Vector3.Distance(transform.position, currentBowlerInfo.startPos) > 1.7f)
            animator.SetInteger("Action", -1);
    }

    IEnumerator StartWalkingBack()
    {
        yield return new WaitForSeconds(2f);
        // Never into the next ball's run-up.
        eGameState s = Main.Instance.gameState;
        if (s != eGameState.InGame_SelectDelivery && s != eGameState.InGame_SelectDeliveryLoop &&
            s != eGameState.InGame_DeliverBall && s != eGameState.InGame_DeliverBallLoop)
            animator.SetInteger("Action", -1);
        walkingBack = null;
    }

    public void ChangeJogRepeat(int amount)
    {
        int repeat = animator.GetInteger("Jog Repeat");
        if (repeat > 0)
        {
            animator.SetInteger("Jog Repeat", repeat + amount);
        }
    }

    public void ChangeRunRepeat(int amount)
    {
        int repeat = animator.GetInteger("Run Repeat");
        if (repeat > 0)
        {
            animator.SetInteger("Run Repeat", repeat + amount);
        }
    }

    public void CheckDistanceToStart()
    {
        Main inst = Main.Instance;

        if (inst.gameState != eGameState.InGame_SelectDelivery ||
            inst.gameState != eGameState.InGame_SelectDeliveryLoop)
        {
            float distance = Vector3.Distance(transform.position, currentBowlerInfo.startPos);
            if (distance <= 1.7f)
            {
                animator.SetInteger("Action", 0);
                myFrame = 0;
                myPrevPos = transform.position;
                glide = true;
            }
        }
    }
}

public class AFInfo
{
    public Vector3 startPos;
    public Vector3 startRot;
    public int myIndex;
    public float animSpeed;
    public int jogRepeat;
    public int runRepeat;

    public AFInfo(Vector3 sP, Vector3 sR, int mI, float aS, int jR, int rR)
    {
        startPos = new Vector3(sP.x - ((jR + rR) * 2.2f), sP.y, sP.z);
        startRot = sR;
        myIndex = mI;
        animSpeed = aS;
        jogRepeat = jR;
        runRepeat = rR;
    }
}

public class AFInfoData
{
    public List<AFInfo> data;

    public AFInfoData()
    {
        data = new List<AFInfo>();
    }
}