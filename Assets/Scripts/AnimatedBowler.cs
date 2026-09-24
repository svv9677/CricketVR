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
        configs.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.6f), new Vector3(0f, 90f, 0f), 2, 4.5f, 1, 3));
        configs.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.5f), new Vector3(0f, 90f, 0f), 4, 4.5f, 0, 2));
        configs.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.5f), new Vector3(0f, 90f, 0f), 4, 4.5f, 0, 2));
        configs.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x), 0f, 2.2f), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
        configs.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x), 0f, 2.2f), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
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
    }

    private void Update()
    {
        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Main inst = Main.Instance;

        // First, before anything below can fail: keep the ball on the hand. The old pin sat at the
        // bottom of LateUpdate, so any exception above it silently left the ball behind.
        HoldBall(inst);

        if (inst.gameState == eGameState.InGame_Ready ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop)
        {
            hasRun_StartBowling = false;
        }

        if (inst.gameState == eGameState.InGame_SelectDelivery)
        {
            animator.Play("Idle");
            animator.SetInteger("Action", 0);
            glide = false;
        }

        if (inst.gameState == eGameState.InGame_SelectDeliveryLoop)
        {
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
        if (glide)
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
        StartCoroutine(StartWalkingBack());
    }

    IEnumerator StartWalkingBack()
    {
        yield return new WaitForSeconds(2f);
        animator.SetInteger("Action", -1);
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