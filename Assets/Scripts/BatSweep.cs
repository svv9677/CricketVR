using UnityEngine;

/// <summary>
/// Continuous bat-ball contact. The old test was a trigger overlap sampled once per physics step:
/// the overlap window along the face is only bat thickness plus ball diameter (0.19 m), while a
/// 40 m/s ball covers 0.28 m per step and the bat jumps once per rendered frame, so even a still
/// bat missed about a third of balls and a hard swing missed over half.
///
/// Instead, each frame, the ball's path is expressed in the bat's own frame at the start and end
/// of the frame, and that segment is tested against the blade box grown by the ball's radius.
/// Relative motion is what matters, so a hit cannot slip between samples however fast either
/// moves.
/// </summary>
public static class BatSweep
{
    /// <summary>
    /// Segment p0->p1 against the axis-aligned box [min, max]. On a hit returns the entry fraction
    /// t (0..1), the face axis (0 = x, 1 = y, 2 = z) and the outward normal sign on that axis.
    /// A segment that starts inside the box hits at t = 0 through the nearest face.
    /// </summary>
    public static bool SegmentBox(Vector3 p0, Vector3 p1, Vector3 min, Vector3 max,
                                  out float t, out int axis, out float normalSign)
    {
        Vector3 d = p1 - p0;
        float tEnter = 0f, tExit = 1f;
        axis = -1;
        normalSign = 0f;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(d[i]) < 1e-9f)
            {
                if (p0[i] < min[i] || p0[i] > max[i]) { t = 0f; return false; }
                continue;
            }
            float t1 = (min[i] - p0[i]) / d[i];
            float t2 = (max[i] - p0[i]) / d[i];
            float near = Mathf.Min(t1, t2), far = Mathf.Max(t1, t2);
            if (near > tEnter)
            {
                tEnter = near;
                axis = i;
                normalSign = d[i] > 0f ? -1f : 1f;
            }
            tExit = Mathf.Min(tExit, far);
            if (tEnter > tExit) { t = 0f; return false; }
        }

        t = tEnter;
        if (axis < 0)
        {
            // Started inside: leave through the closest face.
            float best = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float toMin = p0[i] - min[i], toMax = max[i] - p0[i];
                if (toMin < best) { best = toMin; axis = i; normalSign = -1f; }
                if (toMax < best) { best = toMax; axis = i; normalSign = 1f; }
            }
        }
        return true;
    }

}
