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
    /// Half the distance between the feet in the crouch: a keeper squats with the feet a little
    /// wider than the shoulders.
    public float StanceHalfWidth = 0.3f;
    /// A foot this far from where it should be takes a step.
    public float StepTrigger = 0.1f;
    /// One quick step: 0.13 s, 7 cm off the ground.
    public float StepTime = 0.13f, StepHeight = 0.07f;
    /// Ankle height above the ground for the foot IK goal.
    public float AnkleHeight = 0.08f;

    private Vector3 plantL, plantR, fromPos, toPos;
    private int stepping;          // 0 none, 1 left, 2 right
    private float stepT;

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
        if (stepping == 0)
        {
            float errL = Flat(wantL - plantL), errR = Flat(wantR - plantR);
            if (Mathf.Max(errL, errR) > StepTrigger)
            {
                stepping = errL >= errR ? 1 : 2;
                fromPos = stepping == 1 ? plantL : plantR;
                toPos = (stepping == 1 ? wantL : wantR) + new Vector3(velocity.x, 0f, velocity.z) * StepTime;
                toPos.y = fromPos.y;
                stepT = 0f;
            }
        }
        Vector3 swing = Vector3.zero;
        if (stepping != 0)
        {
            stepT += dt / StepTime;
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
