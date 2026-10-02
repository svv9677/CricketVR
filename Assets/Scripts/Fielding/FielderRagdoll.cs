using UnityEngine;

/// <summary>
/// A fielder going down under physics and getting back up with a real get-up animation.
///
/// The ragdoll (Rigidbodies, colliders and CharacterJoints on eleven bones, built on the fielder
/// prefab by Tools > CricketVR > Build Fielder Ragdolls) sits kinematic, following the animation,
/// until Fall hands the body to physics with the speed it was moving at - the end of a dive: he
/// lands, tumbles and slides as the ground and his joints let him. Once he has come to rest he
/// gets up with the Mixamo clip that fits how he lies (face down, face up, on his side), the
/// character turned and placed so the clip's first frame lies where the ragdoll does, and the
/// ragdoll's pose blended into the clip over BlendTime so there is no jump. Then back to Idle.
///
/// The colliders are on the Body layer, which the ball ignores: a fielder down on the ground does
/// not deflect the ball.
/// </summary>
// After HumanoidReach (and the fielders' own LateUpdates): the ground guard sees the final pose.
[DefaultExecutionOrder(100)]
public partial class FielderRagdoll : MonoBehaviour
{
    public const string FaceDownState = "Get Up Face Down", FaceUpState = "Get Up Face Up", SideState = "Get Up Side";

    /// Down at least this long; up once still for SettleTime, or after MaxDown whatever.
    private const float MinDown = 0.6f, SettleTime = 0.2f, MaxDown = 2.5f, StillSpeed = 0.4f;
    /// From the ragdoll's pose into the clip.
    private const float BlendTime = 0.35f;
    /// The long Mixamo get-ups (11 s, 8 s) played quicker: a fielder is up in a few seconds.
    private const float FaceDownSpeed = 3f, FaceUpSpeed = 2.6f, SideSpeed = 1.1f;
    /// Upward speed he may have when handed to physics, and how fast a limb inside the ground is
    /// pushed out of it, m/s.
    private const float MaxLift = 0.3f, MaxDepenetration = 1f;
    /// Crossfade back to Idle this far through the clip.
    private const float EndAt = 0.96f;

    private enum State { Animated, Down, GettingUp }
    private State state = State.Animated;

    private Animator anim;
    private HumanoidReach reach;
    private Rigidbody[] bodies;
    private Collider[] colliders;
    private Transform hips, head;
    private Rigidbody hipsBody;
    private Transform[] bones;
    private Quaternion[] snapshot;
    private Vector3[] restLocal;
    private Vector3 snapshotHips;
    private Vector3 hipsForwardLocal;
    private float downAt, stillFor, blendFrom, restAnkle;
    private string playing;

    /// Down or getting up: the fielder's own code leaves him alone.
    public bool Busy => state != State.Animated;
    public bool Ready => bodies != null && bodies.Length > 0;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
            return;
        hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        head = anim.GetBoneTransform(HumanBodyBones.Head);
        bodies = hips.GetComponentsInChildren<Rigidbody>(true);
        if (bodies.Length > 0)
            AddHandAndFootColliders();   // before the colliders are gathered
        colliders = hips.GetComponentsInChildren<Collider>(true);
        if (bodies.Length > 0)
            UseRagdollLayer();
        hipsBody = hips.GetComponent<Rigidbody>();
        // A falling limb at a few m/s covers more than its own thickness in a physics step: swept,
        // it cannot step through the ground's (one-sided) mesh.
        foreach (Rigidbody b in bodies)
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        CacheGroundGuard();
        // Only the skeleton the Animator drives. Everything else under the hips - the keeper's
        // pads, helmet and gloves, extra rig bones - keeps its own local pose and simply follows
        // its bone. Blending every transform's world rotation wrote over those local poses and,
        // with nothing animating them back, left the kit twisted until the next fall.
        var animated = new System.Collections.Generic.List<Transform>();
        for (HumanBodyBones b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++)
        {
            Transform t = anim.GetBoneTransform(b);
            if (t != null && !animated.Contains(t))
                animated.Add(t);
        }
        bones = animated.ToArray();
        // His ankle's height standing (the bind pose stands flat on the ground).
        Transform foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        restAnkle = foot != null ? (foot.position.y - transform.position.y) / Mathf.Max(1e-4f, transform.lossyScale.y) : 0.07f;
        snapshot = new Quaternion[bones.Length];
        // The bones' own lengths. The Animator turns limb bones but does not place them, so a joint
        // that stretched a little under physics would have stayed stretched after he got up.
        restLocal = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            restLocal[i] = bones[i].localPosition;
        // Which way his front is, in the hips bone's own frame (for face up or down).
        hipsForwardLocal = Quaternion.Inverse(hips.rotation) * transform.forward;
        // The fielder's own root body: moved by code, never by physics. Left dynamic (as the scene
        // had it, with no gravity) the falling limbs nudged it and he floated off upward for good.
        Rigidbody own = GetComponent<Rigidbody>();
        if (own != null)
        {
            own.linearVelocity = Vector3.zero;
            own.isKinematic = true;
        }
        // His own limbs overlap at rest (arms against the chest, thighs against the hips): left to
        // collide with each other they burst apart when he fell (the hips jumped a metre).
        for (int i = 0; i < colliders.Length; i++)
            for (int j = i + 1; j < colliders.Length; j++)
                Physics.IgnoreCollision(colliders[i], colliders[j]);
        Animate(true);
    }

    /// Added by AnimatedFielder.Start, after this Awake: found when first needed.
    private HumanoidReach Reach
    {
        get
        {
            if (reach == null)
                reach = GetComponent<HumanoidReach>();
            return reach;
        }
    }

    private void Animate(bool animated)
    {
        // Interpolation only while falling: on a kinematic body the animation poses, it would draw
        // the limbs from the last physics steps instead (as it did the bat).
        foreach (Rigidbody b in bodies)
        {
            b.isKinematic = animated;
            b.interpolation = animated ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
        foreach (Collider c in colliders)
            c.enabled = !animated;
    }

    /// Hand him to physics, moving at `velocity` (and turning at `spin`, radians/s).
    public void Fall(Vector3 velocity, Vector3 spin)
    {
        if (!Ready || state == State.Down)
            return;
        // A body handed over at an absurd speed (a bad frame time, a stale last position) flies
        // apart - the joints explode and the bones go to infinity. A diver moves at a few m/s.
        if (!IsFinite(velocity)) velocity = Vector3.zero;
        if (!IsFinite(spin)) spin = Vector3.zero;
        velocity = Vector3.ClampMagnitude(velocity, MaxFallSpeed);
        spin = Vector3.ClampMagnitude(spin, 6f);
        state = State.Down;
        downAt = Time.time;
        stillFor = 0f;
        anim.enabled = false;
        if (Reach != null)
            Reach.enabled = false;
        Animate(false);
        // Handed over low, some limbs start just inside the grass: pushed out at full depenetration
        // speed, and with a spin on him, he bounced up into a cartwheel (filmed). Out gently, and
        // no more than a small lift from the dive itself.
        velocity.y = Mathf.Min(velocity.y, MaxLift);
        foreach (Rigidbody b in bodies)
        {
            b.maxDepenetrationVelocity = MaxDepenetration;
            b.linearVelocity = velocity;
            b.angularVelocity = spin;
        }
    }

    /// Straight back to normal (a new ball while he is down).
    public void Recover()
    {
        if (state == State.Animated)
            return;
        Animate(true);
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != hips)
                bones[i].localPosition = restLocal[i];
        anim.enabled = true;
        anim.speed = 1f;
        anim.applyRootMotion = false;
        anim.Play("0 Idle", 0, 0f);
        if (Reach != null)
        {
            Reach.enabled = true;
            Reach.ResetPose();
        }
        state = State.Animated;
    }

    private void Update()
    {
        if (state != State.Down)
            return;
        float since = Time.time - downAt;
        stillFor = hipsBody != null && hipsBody.linearVelocity.magnitude < StillSpeed ? stillFor + Time.deltaTime : 0f;
        if ((since > MinDown && stillFor >= SettleTime) || since > MaxDown)
            StartGetUp();
    }

    private void StartGetUp()
    {
        // The pose he lies in.
        for (int i = 0; i < bones.Length; i++)
            snapshot[i] = bones[i].rotation;
        snapshotHips = hips.position;
        Vector3 lyingDir = Flat(head.position - hips.position);
        Vector3 front = hips.rotation * hipsForwardLocal;
        float facing = Vector3.Dot(front, Vector3.up);
        playing = facing < -0.5f ? FaceDownState : facing > 0.5f ? FaceUpState : SideState;
        float speed = playing == FaceDownState ? FaceDownSpeed : playing == FaceUpState ? FaceUpSpeed : SideSpeed;

        Animate(true);
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != hips)
                bones[i].localPosition = restLocal[i];
        // Upright, then the clip's first frame, turned and moved to lie where he lies.
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        anim.enabled = true;
        anim.applyRootMotion = true;
        anim.speed = speed;
        anim.Play(playing, 0, 0f);
        anim.Update(0f);
        Vector3 clipDir = Flat(head.position - hips.position);
        if (clipDir.sqrMagnitude > 1e-4f && lyingDir.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(clipDir, lyingDir, Vector3.up), Vector3.up) * transform.rotation;
        anim.Update(0f);
        Vector3 p = transform.position + Flat(snapshotHips - hips.position);
        p.y = BallFlight.GroundY(p);
        transform.position = p;
        anim.Update(0f);

        blendFrom = Time.time;
        state = State.GettingUp;
    }

    private void LateUpdate()
    {
        if (state == State.GettingUp)
            GetUpPose();
        KeepAboveGround();
    }

    private void GetUpPose()
    {
        // Out of the ragdoll's pose into the clip's.
        float w = Mathf.Clamp01((Time.time - blendFrom) / BlendTime);
        if (w < 1f)
        {
            float s = w * w * (3f - 2f * w);
            for (int i = 0; i < bones.Length; i++)
                bones[i].rotation = Quaternion.Slerp(snapshot[i], bones[i].rotation, s);
            hips.position = Vector3.Lerp(snapshotHips, hips.position, s);
        }
        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(playing) && info.normalizedTime >= EndAt)
        {
            anim.speed = 1f;
            anim.applyRootMotion = false;
            anim.CrossFadeInFixedTime("0 Idle", 0.25f);
            if (Reach != null)
            {
                Reach.enabled = true;
                Reach.ResetPose();
            }
            state = State.Animated;
        }
    }

    /// Feet within this of standing height are stood on: fully at FootFull above it, not at all
    /// from FootNone (kneeling up on his toes, a foot lifted to step).
    private const float FootFull = 0.025f, FootNone = 0.055f;

    /// Getting up, the feet he stands on go flat on the ground. The Mixamo get-ups end standing
    /// with the heels dug 3 cm in and the toes 30 degrees up (measured against his idle); the clip
    /// alone drives him here, so nothing else put them down.
    private void OnAnimatorIK(int layer)
    {
        if (state != State.GettingUp)
            return;
        float rest = restAnkle * transform.lossyScale.y;
        PlantFoot(AvatarIKGoal.LeftFoot, rest);
        PlantFoot(AvatarIKGoal.RightFoot, rest);
    }

    private void PlantFoot(AvatarIKGoal goal, float rest)
    {
        Vector3 p = anim.GetIKPosition(goal);
        float ground = BallFlight.GroundY(p);
        float above = p.y - ground - rest;
        float w = Mathf.InverseLerp(FootNone, FootFull, above);
        if (w <= 0f)
            return;
        Vector3 forward = Flat(anim.GetIKRotation(goal) * Vector3.forward);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Flat(transform.forward);
        p.y = ground + rest;
        anim.SetIKPosition(goal, p);
        anim.SetIKRotation(goal, Quaternion.LookRotation(forward, Vector3.up));
        anim.SetIKPositionWeight(goal, w);
        anim.SetIKRotationWeight(goal, w);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static bool IsFinite(Vector3 v) =>
        !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
          float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    /// Fastest a body is handed to physics, m/s.
    private const float MaxFallSpeed = 12f;
}
