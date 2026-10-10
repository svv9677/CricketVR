using UnityEngine;

/// <summary>
/// A wicketkeeper's side shuffle: the feet stay planted on the ground and step, one at a time,
/// to keep up with the body - they never slide. Each foot wants to be StanceHalfWidth out to its
/// side of the pelvis; when one has fallen StepTrigger behind, it lifts, travels on a low arc and
/// plants where it will be needed by the time it lands (its spot plus the body's velocity over the
/// step). The foot furthest behind goes first, so moving right the right foot leads and the left
/// closes up to it: a shuffle, not a cross-over.
///
/// Two more rules tidy the feet, and are the only times a small error is stepped to: a change of
/// stance width (standing up, getting down into the crouch) is stepped to at once, a step each;
/// and once the body has come to rest - not moving and not turning - feet left a little out of
/// stance step back under him. Stepping to every small error (the old Settle flag, left on by
/// standing easy and set while turning to throw) had him pattering on the spot: eight steps in
/// 0.9 s getting down for a delivery 0.3 m wide (measured), a burst of them turning to throw.
/// </summary>
public class KeeperFootwork
{
    /// Half the distance between the feet: a keeper squats with them about shoulder width apart
    /// (0.3 filmed as a wide, knees-out sumo squat, pads and all), and stands with them closer.
    public float StanceHalfWidth = 0.24f;
    /// A foot this far from where it should be takes a step. At 0.1 every few centimetres of body
    /// drift set the feet going - a keeper forever pattering on his toes.
    public float StepTrigger = 0.16f;
    /// One step: 0.15 s, 7 cm off the ground.
    public float StepTime = 0.15f, StepHeight = 0.07f;
    /// Moving quickly the steps come sooner and faster, down to these at FastShuffle m/s: with the
    /// slow values a quick move across left the trailing leg behind and the legs went into the
    /// splits (filmed from in front).
    public float FastTrigger = 0.07f, FastStepTime = 0.1f, FastShuffle = 2.5f;
    /// Tidying: a foot this far out of stance steps back in.
    private const float SettleTrigger = 0.04f;
    /// At rest: slower than this (m/s) and turning slower than this (degrees/s), for this long.
    private const float RestSpeed = 0.15f, RestTurn = 30f, RestFor = 0.2f;
    /// Ankle height above the ground for the foot IK goal.
    public float AnkleHeight = 0.08f;

    private Vector3 plantL, plantR, fromPos, toPos, lastRight;
    private int stepping;          // 0 none, 1 left, 2 right
    private float stepT, stepTime = 0.15f, lastWidth = -1f, restingFor;
    private bool tidying;

    public Vector3 Left { get; private set; }
    public Vector3 Right { get; private set; }

    /// Put both feet straight down in the stance under the body (a new delivery, a moved keeper).
    public void Plant(Vector3 pelvisGround, Vector3 right)
    {
        plantL = pelvisGround - right * StanceHalfWidth;
        plantR = pelvisGround + right * StanceHalfWidth;
        Planted(right);
    }

    /// Plant both feet where they are now (world ankle positions), on the ground under them: taking
    /// the feet over from the animation (after running, throwing) without moving them. Once he is
    /// at rest they tidy into his stance.
    public void PlantAt(Vector3 leftAnkle, Vector3 rightAnkle)
    {
        plantL = new Vector3(leftAnkle.x, BallFlight.GroundY(leftAnkle), leftAnkle.z);
        plantR = new Vector3(rightAnkle.x, BallFlight.GroundY(rightAnkle), rightAnkle.z);
        Planted(lastRight);
    }

    private void Planted(Vector3 right)
    {
        stepping = 0;
        tidying = false;
        lastWidth = StanceHalfWidth;
        lastRight = right;
        restingFor = 0f;
        Left = plantL + Vector3.up * AnkleHeight;
        Right = plantR + Vector3.up * AnkleHeight;
    }

    /// Advance the feet. pelvisGround: the pelvis projected onto the ground; velocity: the body's.
    public void Tick(Vector3 pelvisGround, Vector3 right, Vector3 velocity, float dt)
    {
        Vector3 wantL = pelvisGround - right * StanceHalfWidth;
        Vector3 wantR = pelvisGround + right * StanceHalfWidth;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        float fast = Mathf.Clamp01(speed / FastShuffle);

        // A new stance width: step to it.
        if (!Mathf.Approximately(StanceHalfWidth, lastWidth))
        {
            if (lastWidth >= 0f)
                tidying = true;
            lastWidth = StanceHalfWidth;
        }
        // At rest: tidy the feet in under him.
        float turn = dt > 0f && lastRight.sqrMagnitude > 0f ? Vector3.Angle(lastRight, right) / dt : 0f;
        lastRight = right;
        restingFor = speed < RestSpeed && turn < RestTurn ? restingFor + dt : 0f;
        if (restingFor >= RestFor)
            tidying = true;

        float trigger = tidying ? SettleTrigger : Mathf.Lerp(StepTrigger, FastTrigger, fast);
        if (stepping == 0)
        {
            float errL = Flat(wantL - plantL), errR = Flat(wantR - plantR);
            if (Mathf.Max(errL, errR) > trigger)
            {
                stepping = errL >= errR ? 1 : 2;
                stepTime = Mathf.Lerp(StepTime, FastStepTime, fast);
                fromPos = stepping == 1 ? plantL : plantR;
                // Plant where the body will want it once the step lands, and a little beyond when
                // moving, so the next step is not immediately due.
                toPos = (stepping == 1 ? wantL : wantR) + new Vector3(velocity.x, 0f, velocity.z) * (stepTime * (1f + fast));
                toPos.y = fromPos.y;
                stepT = 0f;
            }
            else if (tidying)
            {
                tidying = false;   // both feet in stance
            }
        }
        Vector3 swing = Vector3.zero;
        if (stepping != 0)
        {
            stepT += dt / stepTime;
            float t = Mathf.Clamp01(stepT);
            swing = Vector3.Lerp(fromPos, toPos, t * t * (3f - 2f * t)) + Vector3.up * ReachMath.StepArc(t, StepHeight);
            if (stepT >= 1f)
            {
                if (stepping == 1) plantL = toPos; else plantR = toPos;
                stepping = 0;
            }
        }
        Left = (stepping == 1 ? swing : plantL) + Vector3.up * AnkleHeight;
        Right = (stepping == 2 ? swing : plantR) + Vector3.up * AnkleHeight;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
