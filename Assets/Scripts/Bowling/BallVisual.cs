using UnityEngine;

/// <summary>
/// What the player sees of the ball: a seamed ball, held the way the bowler grips it, spinning the
/// way it was bowled.
///
/// The ball's own mesh (Models/Ball.fbx) is 444,024 vertices with no UVs - so its seam texture
/// never showed (a plain white sphere) and it cost more to draw than most of the ground. This
/// draws a light one instead (~3k vertices): a latitude/longitude sphere whose poles are the
/// seam's axis (local X), so the texture's seam band (ball-white.png, ~0.12..0.45 across) lies
/// along the equator, its stitching repeated round it. The mesh, texture and material are assets
/// made by BallVisualBuilder (CricketVR > Scene Objects > Place Seamed Ball) and the "Seamed Ball"
/// child that uses them is on the Ball prefab. The physics ball is untouched (collider,
/// rigidbody, trail): only its own MeshRenderer is switched off, and the visual child is turned here.
///
///   * Held   - BowlerGrip sets the rotation every frame: the seam where the fingers put it.
///   * Flight - from the release (Spin) until it is hit or pitches after a shot: spinning about
///     the axis the bowler gave it - backspin on the seam for a seamer, side- and over-spin for a
///     spinner. Visual only: a spinning rigidbody would grip the pitch through PhysX friction and
///     move off it differently from the turn model BallDelivery aims with.
///   * Rolling - once it is on the ground after a shot, or in a fielder's hands: it follows the
///     rigidbody, so it rolls the way PhysX rolls it.
/// </summary>
public class BallVisual : MonoBehaviour
{
    public enum Mode { Physics, Held, Flight }

    [Tooltip("The Seamed Ball child: the generated mesh and material, turned here. Placed by BallVisualBuilder.")]
    [SerializeField] private Transform visual;

    private Mode mode = Mode.Physics;
    private Vector3 spin;                 // rad/s, world
    private Quaternion offsetFromBody = Quaternion.identity;
    private Ball ball;

    /// Spin lost per second in flight (it hardly slows in the second the ball is in the air).
    private const float SpinDecay = 0.15f;

    public Mode Current => mode;
    public Transform Visual => visual;

    private void Awake()
    {
        ball = GetComponent<Ball>();
        if (visual == null)
        {
            Debug.LogError("BallVisual has no Seamed Ball child - run CricketVR > Scene Objects > Place Seamed Ball.", this);
            enabled = false;
            return;
        }
        // The scene's instance of the Ball prefab sets its own layer, and overrides the ball's own
        // renderer back on; the child follows the layer, and the drawn mesh is the child's.
        visual.gameObject.layer = gameObject.layer;
        MeshRenderer own = GetComponent<MeshRenderer>();
        if (own != null)
            own.enabled = false;
        Bat.ShotStruck += OnShot;
    }

    private void OnDestroy() => Bat.ShotStruck -= OnShot;

    /// The bowler has it: the seam where his fingers put it (world rotation; local X = seam axis).
    public void Hold(Quaternion rotation)
    {
        if (visual == null) return;
        mode = Mode.Held;
        visual.rotation = rotation;
    }

    /// Let go with this spin (rad/s, world).
    public void Release(Vector3 angularVelocity)
    {
        if (visual == null) return;
        mode = Mode.Flight;
        spin = angularVelocity;
    }

    private void OnShot(ShotInfo info)
    {
        // Off the bat it tumbles, the harder hit the faster.
        mode = Mode.Flight;
        spin = Random.onUnitSphere * Random.Range(40f, 110f);
    }

    private void LateUpdate()
    {
        if (visual == null) return;
        Main inst = Main.Instance;
        if (mode == Mode.Flight)
        {
            bool shot = inst != null && (inst.gameState == eGameState.InGame_BallHit || inst.gameState == eGameState.InGame_BallHitLoop);
            bool delivering = inst != null && (inst.gameState == eGameState.InGame_DeliverBall || inst.gameState == eGameState.InGame_DeliverBallLoop);
            Rigidbody rb = inst != null ? inst.theBallRigidBody : null;
            bool parked = rb != null && rb.isKinematic;
            if (parked || (!delivering && !shot) || (shot && ball != null && ball.bounced))
                ToPhysics();
            else
            {
                float w = spin.magnitude;
                if (w > 1e-3f)
                    visual.rotation = Quaternion.AngleAxis(w * Mathf.Rad2Deg * Time.deltaTime, spin / w) * visual.rotation;
                spin *= 1f - SpinDecay * Time.deltaTime;
            }
        }
        else if (mode == Mode.Held)
        {
            // Nobody set it this frame (the bowler let go some other way): follow the body again.
            if (inst == null || AnimatedBowler.Instance == null || !AnimatedBowler.Instance.CarryingBall)
                ToPhysics();
        }
        else
            visual.localRotation = offsetFromBody;
    }

    private void ToPhysics()
    {
        mode = Mode.Physics;
        offsetFromBody = Quaternion.Inverse(transform.rotation) * visual.rotation;
    }
}
