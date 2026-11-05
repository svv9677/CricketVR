using UnityEngine;

/// <summary>
/// Footsteps for any animated player (bowler, fielders, keeper, umpire), found from the feet
/// rather than keyed on each clip: a foot coming down fast and reaching the ground is a step. On
/// the pitch it is a hard knock; on the outfield a softer thud and a rustle of grass; louder the
/// faster he is going. The bowler's delivery stride, the heavy one, is timed from his release. MatchSounds adds this to every humanoid in the scene.
/// </summary>
public class Footsteps : MonoBehaviour
{
    /// A foot whose ankle comes below this (m above the ground) going down is a footfall; it must
    /// rise above Lift before it can step again.
    public const float Ground = 0.14f, Lift = 0.2f;
    private const float MinDownSpeed = 0.35f, MinGap = 0.16f, MinBodySpeed = 0.35f;

    private Transform left, right;
    private float leftY, rightY;
    private bool leftUp = true, rightUp = true;
    private float leftAt, rightAt;
    private Vector3 lastPos;
    private float speed;
    private AnimatedBowler bowler;
    private BowlerGrip grip;
    private float loudness = 1f;
    private bool strideDone;

    public void Init(float loudnessScale) => loudness = loudnessScale;

    private void Awake()
    {
        Animator a = GetComponent<Animator>();
        if (a == null || !a.isHuman) { enabled = false; return; }
        left = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        right = a.GetBoneTransform(HumanBodyBones.RightFoot);
        if (left == null || right == null) { enabled = false; return; }
        bowler = GetComponent<AnimatedBowler>();
        lastPos = transform.position;
        leftY = left.position.y; rightY = right.position.y;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        // Teleports (back to a mark, a reset) are not running.
        float v = d.magnitude / dt;
        speed = v > 15f ? speed : Mathf.Lerp(speed, v, 1f - Mathf.Exp(-10f * dt));
        if (bowler != null) DeliveryStride();
        Foot(left, ref leftY, ref leftUp, ref leftAt, dt);
        Foot(right, ref rightY, ref rightUp, ref rightAt, dt);
    }

    /// The bowler's delivery stride, the front foot crashing down at the crease just before he lets
    /// go: timed from the release (the actions' feet do not come down to Ground in the clips, so the
    /// footfall test misses it).
    private void DeliveryStride()
    {
        if (!bowler.CarryingBall) { strideDone = false; return; }
        if (strideDone) return;
        if (grip == null) grip = GetComponent<BowlerGrip>();
        if (grip == null || grip.TimeToRelease() > StrideBeforeRelease) return;
        strideDone = true;
        GameSfx.Play("stride", left.position, GameAudio.Channel.Fielders, 0.95f * loudness, Random.Range(0.95f, 1.05f), 1.5f, 50f);
    }

    /// Seconds before the release that the front foot lands.
    public const float StrideBeforeRelease = 0.12f;

    private void Foot(Transform foot, ref float lastY, ref bool up, ref float lastAt, float dt)
    {
        Vector3 at = foot.position;
        float height = at.y - BallFlight.GroundY(at);
        float down = (lastY - at.y) / dt;
        lastY = at.y;
        if (height > Lift) { up = true; return; }
        if (!up || height > Ground || down < MinDownSpeed || speed < MinBodySpeed || Time.time - lastAt < MinGap)
            return;
        up = false;
        lastAt = Time.time;
        Step(at, down);
    }

    private void Step(Vector3 at, float downSpeed)
    {
        bool pitch = BallFlight.OnPitch(at);
        float run = Mathf.InverseLerp(0.5f, 7f, speed);
        float vol = loudness * Mathf.Lerp(0.25f, 0.85f, run) * Mathf.Lerp(0.7f, 1.15f, Mathf.InverseLerp(0.5f, 3f, downSpeed));
        string group = pitch ? "step_pitch" : "step_grass";
        GameSfx.Play(group, at, GameAudio.Channel.Fielders, vol, Random.Range(0.94f, 1.06f), 1.2f, 45f);
    }
}
