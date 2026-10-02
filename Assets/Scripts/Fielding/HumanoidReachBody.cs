using UnityEngine;

/// <summary>
/// Keeping the hands out of the body: the torso's own frame, and the wrist-target guard every
/// IK goal passes through (ClearOfBody) - in front of the chest, out of the torso and thighs,
/// clear of the face. Split from HumanoidReach.cs for size.
///
/// The guard works on the body as it is being posed this pass (PoseGuardBody), not on the bone
/// transforms: in OnAnimatorIK the bones do not yet carry this pass's hinge and pelvis drop, which
/// ApplyBodyAndFeet has only just asked for. Checked against the bones, a fielder straightening up
/// with the ball had his wrist goals judged clear while they were 5 cm inside his torso as posed
/// (measured: the guard moved nothing, and the wrists landed on those goals exactly).
/// </summary>
public partial class HumanoidReach
{
    /// Hands never go into the head or behind the body: a wrist target is kept at least
    /// MinAheadOfChest in front of the chest from the hips to above the head, and outside a sphere
    /// of HeadClearance round the head. A ball arriving at or over the head used to send the hands
    /// straight through the face. Metres; the body's current pose (hinged forward or not) counts.
    private const float MinAheadOfChest = 0.22f, HeadClearance = 0.24f;
    /// A wrist is this far from the spine's line at least (torso depth ~0.12 plus the wrist), and
    /// this far from a thigh's (thigh ~0.075 plus the wrist). Metres.
    /// Thigh clearance from the bone's axis: a thigh is ~0.08 m in radius, a glove ~0.06 thick -
    /// at 0.12 a keeper's glove reaching down between his legs went 5 cm into his thigh (audit).
    private const float TorsoClearance = 0.19f, ThighClearance = 0.16f;

    /// The torso's own front and up, from the actual spine (hips to head) - right for a man stood
    /// up or folded over at the hips alike.
    public Vector3 TorsoUp => head != null && hips != null ? (head.position - hips.position).normalized : transform.up;
    public Vector3 TorsoFront => Vector3.Cross(transform.right, TorsoUp).normalized;
    public Vector3 ChestPosition => chest != null ? chest.position : transform.position + transform.up * scale;

    /// The wrist goals of the last IK pass, before and after the body guard (for the fielding film).
    public Vector3 RawGoalLeft { get; private set; }
    public Vector3 RawGoalRight { get; private set; }
    public Vector3 GoalLeft { get; private set; }
    public Vector3 GoalRight { get; private set; }

    // Leg bone lengths (world metres at this scale), measured once, for solving the knees.
    private float thighLength, shinLength;
    private bool guardMeasured;

    // This pass's body, as it is being posed: spine line, chest, head, hip joints and knees.
    private Vector3 gHips, gNeck, gChest, gHead, gUp, gFront, gHipL, gHipR, gKneeL, gKneeR;
    private bool guardPosed;

    private void MeasureGuardBody()
    {
        Transform lowerLegL = leftLowerLeg, footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (hips == null || neck == null || head == null || chest == null || leftUpperLeg == null ||
            rightUpperLeg == null || lowerLegL == null || footL == null)
            return;
        thighLength = Vector3.Distance(leftUpperLeg.position, lowerLegL.position);
        shinLength = Vector3.Distance(lowerLegL.position, footL.position);
        guardMeasured = true;
    }

    private bool spineLetGo;

    /// Back to the animation's spine now, for an animation that moves the spine itself (a throw),
    /// until the next reach. Fading out with the reach, the spine stayed held in the pose taken when
    /// he started holding the ball: through the first half of the throw his upper body was held
    /// 25 degrees behind the clip's - bent back into the wind-up - and then snapped forward in
    /// one frame near the release (measured, keeper and clip sampled on his rig). Let go at the
    /// start of the throw, the held pose is still his standing one, so nothing jumps.
    public void LetSpineGo()
    {
        spineFrozen = false;
        spineLetGo = true;
    }

    /// Planted feet stay reachable: legs straightened no further than this share of their length,
    /// the body brought down at most MaxReachDrop to get there.
    private const float MaxLegStretch = 0.97f, MaxReachDrop = 0.3f;
    /// Over planted feet the body (centre of mass) stays within this of the point between them.
    /// At 0.25 his pelvis still sat 0.32 m behind his feet through the throw, trunk 11 degrees
    /// back - the throw looked bent back (measured).
    private const float MaxBodyOffFeet = 0.12f;

    /// The fielders' Throw clip carries the body through its stride (baked into the pose): over the
    /// keeper's planted feet it took his pelvis 0.7 m back from them as the throw began - he slid
    /// back into it, leaning off his heels - and snapped it back 0.6 m in a frame as it ended
    /// (measured). With his feet planted, the body stays over them: a weight shift, not a walk.
    private void KeepBodyOverFeet(Vector3 leftFoot, Vector3 rightFoot)
    {
        if (footBlend <= 0f)
            return;
        Vector3 off = animator.bodyPosition - 0.5f * (leftFoot + rightFoot);
        off.y = 0f;
        float limit = MaxBodyOffFeet * scale;
        float over = off.magnitude - limit;
        if (over > 0f)
            animator.bodyPosition -= off.normalized * (over * footBlend);
    }

    /// Feet planted (feetOverride) out of reach of the hips the animation has - a throw clip
    /// rising onto the toes, a get-up still half up - and the IK left both feet hanging in the
    /// air, ankles 14 cm up through the keeper's throw (measured). The body comes down until the
    /// legs reach the ground instead.
    private void KeepFeetReachable(Vector3 leftFoot, Vector3 rightFoot)
    {
        if (!calibrated || footBlend <= 0f)
            return;
        float length = (thighLength + shinLength) * MaxLegStretch;
        float drop = Mathf.Max(DropToReach(leftFoot, cHipL, length), DropToReach(rightFoot, cHipR, length));
        if (drop > 0f)
            animator.bodyPosition -= Vector3.up * (Mathf.Min(drop, MaxReachDrop * scale) * footBlend);
    }

    private float DropToReach(Vector3 foot, Vector3 hipLocal, float length)
    {
        Vector3 toFoot = foot - (animator.bodyPosition + animator.bodyRotation * hipLocal);
        float across = new Vector2(toFoot.x, toFoot.z).magnitude;
        float below = Mathf.Sqrt(Mathf.Max(0f, length * length - across * across));
        return Mathf.Max(0f, -toFoot.y - below);
    }

    // Where the bones sat relative to the body (bodyPosition/bodyRotation) as last posed.
    private Vector3 bodyPosPosed;
    private Quaternion bodyRotPosed = Quaternion.identity;
    private bool calibrated, posedThisFrame;
    private Vector3 cHips, cNeck, cChest, cHead, cHipL, cHipR;

    /// After the frame is posed: where hips, neck, chest, head and hip joints ended up in the frame
    /// of the body position and rotation this pass set. Next pass puts them back on its own body
    /// position and rotation. The body's rotation carries the fast part (the hip hinge); the shape
    /// of the spine within it changes little from one frame to the next. Modelled from bone
    /// lengths instead, the spine came out 9-18 cm off (bodyPosition is the centre of mass, not
    /// the hips).
    private void CalibrateGuardBody()
    {
        if (!posedThisFrame || !guardMeasured)
            return;
        posedThisFrame = false;
        Quaternion inv = Quaternion.Inverse(bodyRotPosed);
        Vector3 L(Transform t) => inv * (t.position - bodyPosPosed);
        cHips = L(hips); cNeck = L(neck); cChest = L(chest); cHead = L(head);
        cHipL = L(leftUpperLeg); cHipR = L(rightUpperLeg);
        calibrated = true;
    }

    /// Called in the IK pass once the body has been moved and the spine flexed, before the hands:
    /// the torso and hip joints from this pass's body position and rotation (the hinge is in it),
    /// the thighs from the hip joints to knees solved on the foot goals, bending forward.
    private void PoseGuardBody()
    {
        guardPosed = false;
        if (!guardMeasured)
            return;
        Quaternion bodyRot = animator.bodyRotation;
        Vector3 bodyPos = animator.bodyPosition;
        bodyPosPosed = bodyPos;
        bodyRotPosed = bodyRot;
        posedThisFrame = true;
        if (!calibrated)
            return;
        Vector3 W(Vector3 local) => bodyPos + bodyRot * local;
        gHips = W(cHips); gNeck = W(cNeck); gChest = W(cChest); gHead = W(cHead);
        gHipL = W(cHipL); gHipR = W(cHipR);
        gUp = (gNeck - gHips).normalized;
        gFront = Vector3.Cross(transform.right, gUp).normalized;
        Vector3 fwd = transform.forward;
        gKneeL = SolveKnee(gHipL, animator.GetIKPosition(AvatarIKGoal.LeftFoot), fwd);
        gKneeR = SolveKnee(gHipR, animator.GetIKPosition(AvatarIKGoal.RightFoot), fwd);
        guardPosed = true;
    }

    /// The knee of a two-bone leg from hip to ankle, bending toward `bend`.
    private Vector3 SolveKnee(Vector3 hip, Vector3 ankle, Vector3 bend)
    {
        Vector3 toAnkle = ankle - hip;
        float d = Mathf.Clamp(toAnkle.magnitude, 1e-4f, thighLength + shinLength - 1e-4f);
        Vector3 dir = toAnkle.sqrMagnitude > 1e-8f ? toAnkle.normalized : -transform.up;
        float along = (thighLength * thighLength - shinLength * shinLength + d * d) / (2f * d);
        float off = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - along * along));
        Vector3 perp = Vector3.ProjectOnPlane(bend, dir);
        perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.Cross(dir, transform.right).normalized;
        return hip + dir * along + perp * off;
    }

    private void ApplyHands()
    {
        float w = smoothedWeight, share = Mathf.Clamp01(leftHandShare);
        float wl = w * share, wr = w;
        // The reach is blended in by moving the goal from where the animation has the hand toward
        // the target (each step kept clear of the body), at near-full IK weight - not by weighting
        // the IK, which slides the hand in a straight line from the animated hand to the goal. On a
        // catch to his right that line ran from the run cycle's left hand, swinging at his left
        // hip, straight through his chest (measured 10 cm); on the keeper's glove fading back to his
        // crouch pose, through his thigh. The IK weight still ramps over the first tenth of the
        // reach so nothing pops when it starts.
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, Mathf.Clamp01(wl * 10f));
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, Mathf.Clamp01(wr * 10f));
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, wl);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, wr);
        if (w < 0.001f)
            return;
        // Where the animation has the hands this pass (the goals start there until set).
        Vector3 animLeft = animator.GetIKPosition(AvatarIKGoal.LeftHand);
        Vector3 animRight = animator.GetIKPosition(AvatarIKGoal.RightHand);
        Frame(out Vector3 finger, out Vector3 side);
        // IK moves the wrist; put it so the palm centre, not the wrist, lands on the target.
        float reachBack = (0.6f * palmLength + palmExtra);
        Vector3 facing = smoothFacing;
        // Two hands sit either side of the target; one hand alone goes right onto it.
        Vector3 lPalm = handPos - side * (handGap * 0.5f), rPalm = handPos + side * (handGap * 0.5f * share);
        RawGoalLeft = lPalm - finger * reachBack - facing * 0.02f;
        RawGoalRight = rPalm - finger * reachBack - facing * 0.02f;
        GoalLeft = ClearOfBody(Vector3.Lerp(animLeft, RawGoalLeft, wl));
        GoalRight = ClearOfBody(Vector3.Lerp(animRight, RawGoalRight, wr));
        animator.SetIKPosition(AvatarIKGoal.LeftHand, GoalLeft);
        animator.SetIKPosition(AvatarIKGoal.RightHand, GoalRight);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, ElbowHint(leftUpperArm.position, animator.GetIKPosition(AvatarIKGoal.LeftHand), -1f));
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, ElbowHint(rightUpperArm.position, animator.GetIKPosition(AvatarIKGoal.RightHand), 1f));
    }

    private Vector3 ClearOfBody(Vector3 wrist)
    {
        if (!guardPosed)
            return wrist;
        // In front of the torso, measured across the torso itself. It was "in front" along the
        // feet's forward, which a man folded over at the hips no longer faces: filmed, a fielder
        // rising with the ball reached for a standing-chest point and his arms went up over his
        // back, shoulders bent back behind him.
        Vector3 up = gUp, front = gFront;
        float shoulders = Vector3.Dot(gNeck - gHips, up);
        float along = Vector3.Dot(wrist - gHips, up);
        // Only up to the shoulders: past them the arms reach on in line with the body (a keeper's
        // dive, a catch overhead), and pulling them out in front cost a 2 m dive its last 0.25 m.
        // The torso capsule and the face still keep them out of the body up there.
        if (along > -0.1f * scale && along < shoulders)
        {
            float ahead = Vector3.Dot(wrist - gChest, front);
            if (ahead < MinAheadOfChest * scale)
                wrist += front * (MinAheadOfChest * scale - ahead);
        }
        // The head bone's pivot is at the top of the neck; the face is above and in front of it.
        Vector3 face = gHead + up * 0.09f * scale + front * 0.04f * scale;
        float clear = HeadClearance * scale;
        // Each push can land the wrist in another part (bent over, the face is right by the
        // chest), so go round them a few times; a point clear of all of them stays put.
        for (int pass = 0; pass < 4; pass++)
        {
            // Out of the torso itself, all round - a wrist beside or under a bent-over chest too,
            // not only one behind the chest plane - and out the front or side, never to the back.
            wrist = OutOfCapsule(wrist, gHips, gNeck, TorsoClearance * scale, front);
            // Out of the thighs: a keeper's glove held at the waist while he is still crouched went
            // into his thigh (measured 6-7 cm).
            wrist = OutOfCapsule(wrist, gHipL, gKneeL, ThighClearance * scale, Vector3.zero);
            wrist = OutOfCapsule(wrist, gHipR, gKneeR, ThighClearance * scale, Vector3.zero);
            // Clear of the face - away from it, but never back toward the chest: out in front.
            Vector3 fromFace = wrist - face;
            if (fromFace.sqrMagnitude < clear * clear)
            {
                Vector3 dir = fromFace.sqrMagnitude > 1e-8f ? fromFace.normalized : front;
                if (Vector3.Dot(dir, front) < 0.3f)
                    dir = (dir + front * (0.3f - Vector3.Dot(dir, front)) * 2f).normalized;
                wrist = face + dir * clear;
            }
        }
        return wrist;
    }

    /// <summary>
    /// Where the elbow bends toward: out from the middle of the shoulder-to-wrist line, away from
    /// the spine, a little toward the arm's own side and down the torso. It was a fixed point out,
    /// down and back from the shoulder in the feet's frame: an arm reaching across the chest had its
    /// elbow pulled back to its own side, the forearm through the chest (measured 9 cm), and bent
    /// over, "down" was the ground and the elbows went into the thighs (6 cm).
    /// </summary>
    private Vector3 ElbowHint(Vector3 shoulder, Vector3 wrist, float sideSign)
    {
        Vector3 mid = 0.5f * (shoulder + wrist);
        Vector3 up = guardPosed ? gUp : TorsoUp;
        Vector3 away = Vector3.zero;
        if (guardPosed)
        {
            Vector3 ab = gNeck - gHips;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(mid - gHips, ab) / ab.sqrMagnitude) : 0f;
            away = Vector3.ProjectOnPlane(mid - (gHips + ab * t), up);
        }
        Vector3 side = transform.right * sideSign;
        Vector3 dir = (away.sqrMagnitude > 1e-6f ? away.normalized : side) * 0.7f + side * 0.6f - up * 0.35f;
        Vector3 hint = mid + dir.normalized * (0.3f * scale);
        if (!guardPosed)
            return hint;
        // Never a bend toward the head or into the torso: holding the ball at his chest while
        // bent over, the elbows came up into his head (measured 8 cm).
        hint = OutOfCapsule(hint, gHips, gNeck, ElbowClearance * scale, gFront);
        // ...nor into a thigh: reaching down beside the legs (a pick-up) the bend took the forearm
        // across the thigh (audit: 7 cm in).
        hint = OutOfCapsule(hint, gHipL, gKneeL, (ThighClearance + 0.06f) * scale, Vector3.zero);
        hint = OutOfCapsule(hint, gHipR, gKneeR, (ThighClearance + 0.06f) * scale, Vector3.zero);
        Vector3 face = gHead + up * 0.09f * scale;
        Vector3 fromHead = hint - face;
        if (fromHead.sqrMagnitude < ElbowClearance * ElbowClearance * scale * scale)
            hint = face + (fromHead.sqrMagnitude > 1e-8f ? fromHead.normalized : side) * (ElbowClearance * scale);
        return hint;
    }

    /// The elbow's bend point stays this far from the spine's line and from the head, metres.
    private const float ElbowClearance = 0.3f;

    /// `p` moved radially out of the capsule a-b of radius r. With `front` set, a point behind the
    /// axis goes out the front instead (hands go round the body, not through it to the back).
    private static Vector3 OutOfCapsule(Vector3 p, Vector3 a, Vector3 b, float r, Vector3 front)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        Vector3 axis = a + ab * t;
        Vector3 away = p - axis;
        float d = away.magnitude;
        if (d >= r)
            return p;
        Vector3 dir = d > 1e-5f ? away / d : (front.sqrMagnitude > 0f ? front : Vector3.up);
        if (front.sqrMagnitude > 0f && Vector3.Dot(dir, front) < 0f)
        {
            // Keep its sideways part, flip the backward part to the front.
            dir = (dir - 2f * Vector3.Dot(dir, front) * front).normalized;
        }
        return axis + dir * r;
    }

    /// Where the guard thinks the spine is this pass (for the fielding film to check against).
    public Vector3 GuardHips => gHips;
    public Vector3 GuardNeck => gNeck;
}
