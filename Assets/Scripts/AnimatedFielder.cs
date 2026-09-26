using System;
using UnityEngine;

/// <summary>
/// One fielder. FieldingController decides where each fielder goes and when the ball is taken;
/// this moves the body there and holds the ball.
///
/// The old version waited on animation states before moving (Idle -> Start Running -> Run),
/// moved by root motion at ~4 m/s while planning with ~10-12 m/s, and only stopped the ball after
/// a stop-then-pick-up animation had played - so fielders arrived late and the ball rolled on
/// through them. Now the body is moved in code, straight away, at the speed the plan assumed, and
/// the ball is held the instant it is in reach. Animation is only for looks.
/// </summary>
public class AnimatedFielder : MonoBehaviour
{
    // Serialized names kept so the scene's nine fielders stay wired up.
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
    [SerializeField]
    private float pickUpDistance;

    [HideInInspector]
    public Material JerseyMaterial;
    [HideInInspector]
    public Vector3 StartPosition;

    /// Top running speed, m/s, before the B-menu "Fielder Speed" slider (default 1.5 -> 7.5 m/s).
    public const float BaseRunSpeed = 5f;
    public const float Acceleration = 10f;
    public const float TurnRate = 720f;
    /// Forward speed baked into the run animation, used to keep the feet from sliding.
    private const float AnimatedRunSpeed = 4f;

    private Transform holdBallOffset;
    private bool hasTarget;
    private Vector3 target;
    private float speed;
    private bool holdingBall;

    public float RunSpeed => BaseRunSpeed * Mathf.Max(0.5f, Main.Instance != null ? Main.Instance.fielderSpeed : 1.5f);
    public bool HoldingBall => holdingBall;
    public float CurrentSpeed => speed;
    public Vector3 Target => target;
    public bool HasTarget => hasTarget;

    void Start()
    {
        if (theObject != null)
        {
            foreach (Material mat in theObject.GetComponent<Renderer>().materials)
            {
                if (mat.name.Contains("Material.001"))
                {
                    JerseyMaterial = mat;
                    break;
                }
            }
        }
        StartPosition = Constants.fieldingPositions[Convert.ToInt32(gameObject.name[gameObject.name.Length - 1].ToString()) - 1];
        ReturnToStart();
        if (myAnimator != null)
            myAnimator.applyRootMotion = false;
        if (fielderHand != null && fielderHand.transform.childCount > 5)
            holdBallOffset = fielderHand.transform.GetChild(5);
        Main.Instance.onGameStateChanged += HandleGameState;
    }

    public void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        eGameState state = Main.Instance.gameState;
        // A new delivery: drop the ball (Main parks it) and go back to the fielding position.
        if (state == eGameState.InGame_SelectDelivery || state == eGameState.InGame_ResetToReadyLoop)
        {
            if (state == eGameState.InGame_SelectDelivery)
                holdingBall = false;
            ReturnToStart();
        }
    }

    public void ReturnToStart()
    {
        hasTarget = false;
        speed = 0f;
        transform.position = StartPosition;
        transform.LookAt(new Vector3(0f, transform.position.y, 0f));
        SetAnimation(0);
    }

    /// Run to this point (on the ground).
    public void SetTarget(Vector3 point)
    {
        point.y = transform.position.y;
        target = point;
        hasTarget = true;
    }

    public void Stop()
    {
        hasTarget = false;
    }

    /// The ball is in reach: take it and hold it.
    public void TakeBall()
    {
        holdingBall = true;
        hasTarget = false;
        speed = 0f;
        SetAnimation(4);
    }

    private void Update()
    {
        if (!hasTarget)
        {
            speed = 0f;
            if (!holdingBall)
                SetAnimation(0);
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.05f)
        {
            speed = 0f;
            SetAnimation(0);
            FaceBall();
            return;
        }

        // Accelerate to full speed, and ease off just in time to stop on the spot.
        float stopping = Mathf.Sqrt(2f * Acceleration * distance);
        speed = Mathf.Min(RunSpeed, speed + Acceleration * Time.deltaTime, stopping);
        Vector3 step = to / distance * Mathf.Min(distance, speed * Time.deltaTime);
        transform.position += step;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnRate * Time.deltaTime);
        SetAnimation(1);
        if (myAnimator != null)
            myAnimator.speed = Mathf.Clamp(speed / AnimatedRunSpeed, 0.6f, 2f);
    }

    private void FaceBall()
    {
        Vector3 ball = Main.Instance.theBall.transform.position - transform.position;
        ball.y = 0f;
        if (ball.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(ball), TurnRate * Time.deltaTime);
    }

    private void SetAnimation(int action)
    {
        if (myAnimator == null)
            return;
        myAnimator.SetInteger("Action", action);
        if (action != 1)
            myAnimator.speed = 1f;
    }

    public void LateUpdate()
    {
        if (holdingBall)
        {
            Transform ball = Main.Instance.theBall.transform;
            ball.position = holdBallOffset != null ? holdBallOffset.position : transform.position + Vector3.up;
        }
    }
}
