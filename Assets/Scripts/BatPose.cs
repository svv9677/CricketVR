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
/// The ground. The bat never goes into it: when any corner of the bat would be below the ground,
/// the bat is turned about the hand until it rests on the surface - it stays in the hand, it just
/// stops at the ground the way a real bat does. Only if the hand itself is at the ground does the
/// bat slide up instead. Applied to the simulated pose as well, so the ball meets the bat where
/// it is drawn.
/// </summary>
public partial class Bat
{
    /// Resting just on the surface, not in it.
    private const float GroundClearance = 0.005f;
    private const int GroundPasses = 4;

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

    /// Turn the bat about the hand (pivot) until no corner of it is below the ground.
    private void KeepAboveGround(Vector3 pivot, ref Vector3 position, ref Quaternion rotation)
    {
        if (localCorners == null && !FindCorners())
            return;
        Vector3 scale = transform.lossyScale;
        for (int pass = 0; pass < GroundPasses; pass++)
        {
            // The deepest corner, by how far it is under the ground beneath it.
            Vector3 deepest = Vector3.zero;
            float depth = 0f;
            foreach (Vector3 local in localCorners)
            {
                Vector3 p = position + rotation * Vector3.Scale(scale, local);
                float d = BallFlight.GroundY(p) + GroundClearance - p.y;
                if (d > depth) { depth = d; deepest = p; }
            }
            if (depth <= 0f)
                return;

            // Raise that corner by `depth`, swinging it about the hand at the same distance.
            Vector3 r = deepest - pivot;
            float length = r.magnitude;
            Vector3 flat = new Vector3(r.x, 0f, r.z);
            float wantY = r.y + depth;
            if (length < 1e-4f || flat.sqrMagnitude < 1e-8f || wantY >= length)
            {
                position.y += depth;   // the hand is at the ground itself: lift the whole bat
                continue;
            }
            Vector3 raised = flat.normalized * Mathf.Sqrt(length * length - wantY * wantY) + Vector3.up * wantY;
            Quaternion turn = Quaternion.FromToRotation(r, raised);
            position = pivot + turn * (position - pivot);
            rotation = turn * rotation;
        }
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
