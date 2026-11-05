using UnityEngine;

/// <summary>
/// Where the bat is drawn and simulated, beyond following the hand.
///
/// Fresh pose for rendering. The head is re-read just before rendering (the camera's
/// TrackedPoseDriver), but the controller was read once in Update, so the bat was drawn from a hand
/// pose older than the view by an amount that varied frame to frame - uneven steps in a fast swing.
/// The controller anchor is re-read then too (XRControllerTracker.Refresh) and the bat re-posed.
/// (No smoothing filter: one was tried and read as the bat lagging the hand. The bat's Rigidbody
/// is not interpolated either - on a kinematic body posed by hand, interpolation drew it from the
/// last two physics steps instead, behind the hand and beating against the display rate.)
///
/// The ground. Touching down is a knock, heard and felt. The bat never goes into it: when any
/// corner of the bat would be below the ground, the bat is turned about the hand until it rests on the surface - it stays in the hand, it just
/// stops at the ground the way a real bat does. Only if the hand itself is at the ground does the
/// bat slide up instead. Applied to the simulated pose as well, so the ball meets the bat where
/// it is drawn.
/// </summary>
public partial class Bat
{
    /// Resting just on the surface, not in it.
    private const float GroundClearance = 0.005f;

    private Vector3[] localCorners;

    private void OnEnable() => Application.onBeforeRender += PoseForRender;
    private void OnDisable() => Application.onBeforeRender -= PoseForRender;

    private void PoseForRender()
    {
        if (holdingStill || !grabbable || !grabbing || attachParent == null || myRigidBody == null)
            return;
        XRControllerTracker tracker = attachParent.GetComponentInParent<XRControllerTracker>();
        if (tracker != null)
            tracker.Refresh();
        HeldPose(out Vector3 position, out Quaternion rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    // ---- The bat touching down: a knock you can hear and feel -------------------------------------

    /// Within this of the ground counts as on it (so resting there does not re-trigger); slower
    /// than this coming down makes no sound; not again for this long.
    private const float TouchBand = 0.01f, MinTapSpeed = 0.25f, TapGap = 0.15f;
    private float groundDepth, groundGap = 1f, lastGap = 1f, nextTap;
    /// Where the lowest corner meets the ground: where the knock is heard.
    private Vector3 groundPoint;
    private bool onGround;

    /// After the simulated pose each frame (LateUpdate): touching down is a knock.
    private void CheckGroundTap()
    {
        // Twice the band to let go once down, so a bat resting on the ground with a trembling hand
        // does not count as touching down again and again.
        bool touching = groundDepth > 0f || groundGap < (onGround ? 2f * TouchBand : TouchBand);
        if (touching && !onGround && Time.time >= nextTap)
        {
            // How fast it came down: the gap it closed this frame, plus how far it tried to go in.
            float speed = (Mathf.Max(0f, lastGap) + groundDepth) / Mathf.Max(Time.deltaTime, 1e-3f);
            if (speed >= MinTapSpeed)
            {
                nextTap = Time.time + TapGap;
                // Heard where it touched, louder the harder it comes down (BatSounds.PlayGround).
                if (sounds == null)
                    sounds = GetComponent<BatSounds>();
                if (sounds != null)
                    sounds.PlayGround(groundPoint, speed);
                // Felt in the hand, harder and longer the harder it comes down.
                float hard = Mathf.Clamp01(speed / 4f);
                XRInput.SendHaptics(attachParent == leftHandParent, Mathf.Lerp(0.35f, 1f, hard), Mathf.Lerp(0.04f, 0.12f, hard));
            }
        }
        onGround = touching;
        lastGap = groundGap;
    }

    /// The most the ground can turn the bat away from the hand's pose, radians; whatever is still
    /// in the ground past it is lifted out instead.
    private const float MaxGroundTilt = 85f * Mathf.Deg2Rad;
    /// How much the bat's face decides which way it tips when it is pushed straight down (its
    /// long axis near vertical): the toe goes back, away from the face, as in a stance.
    private const float FaceBias = 0.2f;

    /// Turn the bat about the hand (pivot), toward where it already leans, by the least angle that
    /// keeps every corner of it out of the ground; lift it if turning cannot.
    ///
    /// One turn, about one axis, solved exactly. It was: find the deepest corner, swing it up
    /// about the hand along its own direction, repeat four times. Each corner's swing had its own
    /// axis, so as the bat twisted or the hand trembled the "deepest" corner swapped between toe
    /// and edge, toe and face, and the bat jumped between their answers - the jitter when the bat
    /// was held against the ground. Here the axis comes from the bat's lean, which changes
    /// smoothly, and the angle is the largest any corner needs, which does too.
    private void KeepAboveGround(Vector3 pivot, ref Vector3 position, ref Quaternion rotation)
    {
        groundDepth = 0f;
        groundGap = 1f;
        if (localCorners == null && !FindCorners())
            return;
        Vector3 scale = transform.lossyScale;
        // How close the lowest corner is before any correction (for the knock when it touches).
        foreach (Vector3 local in localCorners)
        {
            Vector3 p = position + rotation * Vector3.Scale(scale, local);
            float gap = p.y - BallFlight.GroundY(p) - GroundClearance;
            if (gap < groundGap) { groundGap = gap; groundPoint = new Vector3(p.x, BallFlight.GroundY(p), p.z); }
        }
        groundDepth = Mathf.Max(0f, -groundGap);
        if (groundGap >= 0f)
            return;

        Vector3 axis = TiltAxis(rotation);
        float angle = 0f;
        if (axis != Vector3.zero)
        {
            foreach (Vector3 local in localCorners)
            {
                Vector3 p = position + rotation * Vector3.Scale(scale, local);
                Vector3 r = p - pivot;
                // Height (above the hand) this corner has to reach, and its height as the bat
                // turns by phi about the axis: r.y cos(phi) + s sin(phi) = R cos(phi - alpha).
                float need = BallFlight.GroundY(p) + GroundClearance - pivot.y;
                if (r.y >= need)
                    continue;
                float s = Vector3.Cross(axis, r).y;
                float R = Mathf.Sqrt(r.y * r.y + s * s);
                float alpha = Mathf.Atan2(s, r.y);
                if (R <= need || alpha <= 0f)
                    continue;   // turning this way cannot raise it enough: it is lifted (below)
                angle = Mathf.Max(angle, alpha - Mathf.Acos(Mathf.Clamp(need / R, -1f, 1f)));
            }
            angle = Mathf.Min(angle, MaxGroundTilt);
            if (angle > 0f)
            {
                Quaternion turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
                position = pivot + turn * (position - pivot);
                rotation = turn * rotation;
            }
        }

        // Whatever turning left in the ground (the hand at the ground, or the toe now over the
        // lower outfield past the pitch's edge): lift the whole bat out by it.
        float depth = 0f;
        foreach (Vector3 local in localCorners)
        {
            Vector3 p = position + rotation * Vector3.Scale(scale, local);
            depth = Mathf.Max(depth, BallFlight.GroundY(p) + GroundClearance - p.y);
        }
        position.y += depth;
    }

    /// The horizontal axis the bat turns about to come up off the ground: across the direction it
    /// leans (toe away from the hand), so turning about it swings the toe up and out along that
    /// lean. Pushed straight down, the face decides (FaceBias) so the axis never flips.
    private static Vector3 TiltAxis(Quaternion rotation)
    {
        Vector3 toward = rotation * Vector3.back;   // handle to toe: the toe is the bat's lowest local Z
        Vector3 face = rotation * Vector3.up;       // the flat face is +Y (BatGeometry)
        Vector3 lean = new Vector3(toward.x, 0f, toward.z) - FaceBias * new Vector3(face.x, 0f, face.z).normalized;
        if (lean.sqrMagnitude < 1e-8f)
            return Vector3.zero;
        return Vector3.Cross(lean.normalized, Vector3.up);
    }

    /// The bat mesh's bounding-box corners, in the bat's unscaled local space.
    private bool FindCorners()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return false;
        Bounds b = filter.sharedMesh.bounds;
        localCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            localCorners[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                                          (i & 2) == 0 ? b.min.y : b.max.y,
                                          (i & 4) == 0 ? b.min.z : b.max.z);
        return true;
    }
}
