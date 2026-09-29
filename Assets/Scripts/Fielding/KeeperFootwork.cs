using UnityEngine;

/// <summary>
/// A wicketkeeper's side shuffle: the feet stay planted on the ground and step, one at a time,
/// to keep up with the body - they never slide. Each foot wants to be StanceHalfWidth out to its
/// side of the pelvis; when one has fallen StepTrigger behind, it lifts, travels on a low arc and
/// plants where it will be needed by the time it lands (its spot plus the body's velocity over the
/// step). The foot furthest behind goes first, so moving right the right foot leads and the left
/// closes up to it: a shuffle, not a cross-over.
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
    /// Set while standing up: the feet step in to the new stance even for a small change.
    public bool Settle;
    private const float SettleTrigger = 0.04f;
    /// Ankle height above the ground for the foot IK goal.
    public float AnkleHeight = 0.08f;

    private Vector3 plantL, plantR, fromPos, toPos;
    private int stepping;          // 0 none, 1 left, 2 right
    private float stepT, stepTime = 0.15f;

    public Vector3 Left { get; private set; }
    public Vector3 Right { get; private set; }

    /// Put both feet straight down in the stance under the body (a new delivery, a moved keeper).
    public void Plant(Vector3 pelvisGround, Vector3 right)
    {
        plantL = pelvisGround - right * StanceHalfWidth;
        plantR = pelvisGround + right * StanceHalfWidth;
        stepping = 0;
        Left = plantL + Vector3.up * AnkleHeight;
        Right = plantR + Vector3.up * AnkleHeight;
    }

    /// Advance the feet. pelvisGround: the pelvis projected onto the ground; velocity: the body's.
    public void Tick(Vector3 pelvisGround, Vector3 right, Vector3 velocity, float dt)
    {
        Vector3 wantL = pelvisGround - right * StanceHalfWidth;
        Vector3 wantR = pelvisGround + right * StanceHalfWidth;
        float fast = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / FastShuffle);
        float trigger = Settle ? SettleTrigger : Mathf.Lerp(StepTrigger, FastTrigger, fast);
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
