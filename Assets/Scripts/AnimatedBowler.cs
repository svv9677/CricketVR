using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class AnimatedBowler : MonoBehaviour
{
    public static AnimatedBowler Instance = null;

    [SerializeField]
    private GameObject hand;

    //private float runupLength;
    //private Vector3 startPosition;
    //private Vector3 startRotation;

    private Animator animator;
    private AnimatorStateInfo stateInfo;

    private Coroutine startBowling;
    private bool hasRun_StartBowling;

    private AFInfoData myBowlers;
    private AFInfo currentBowlerInfo;

    private bool glide;
    private int myFrame;
    private Vector3 myPrevPos;

    private void Awake()
    {
        myBowlers = new AFInfoData();
        myBowlers.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.6f), new Vector3(0f, 90f, 0f), 2, 4.5f, 0, 2));
        myBowlers.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x), 0f, 2.2f), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
        myBowlers.data.Add(new AFInfo(new Vector3(-9f - (5f * transform.localScale.x), 0f, 2.2f), new Vector3(0f, 90f, 0f), 3, 3f, 0, 0));
        myBowlers.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.6f), new Vector3(0f, 90f, 0f), 2, 4.5f, 0, 2));
        myBowlers.data.Add(new AFInfo(new Vector3(-9f - (7f * transform.localScale.x), 0f, 2.6f), new Vector3(0f, 90f, 0f), 2, 4.5f, 1, 3));
        
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

        if (inst.theBall.transform.parent != null && inst.theBall.transform.parent.gameObject.name == hand.name)
        {
            inst.theBall.transform.localPosition = Vector3.zero;
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

    private IEnumerator StartBowling()
    {
        Main inst = Main.Instance;
        inst.theBallRigidBody.isKinematic = true;
        inst.theBall.transform.SetParent(hand.transform);

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

    public void ReleaseBall()
    {
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

        if (inst.gameState == eGameState.InGame_Ready ||
            inst.gameState == eGameState.InGame_ResetToReadyLoop)
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

class AFInfo
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

class AFInfoData
{
    public List<AFInfo> data;

    public AFInfoData()
    {
        data = new List<AFInfo>();
    }
}