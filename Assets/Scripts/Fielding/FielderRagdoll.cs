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
public class FielderRagdoll : MonoBehaviour
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
    private Vector3 snapshotHips;
    private Vector3 hipsForwardLocal;
    private float downAt, stillFor, blendFrom;
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
        colliders = hips.GetComponentsInChildren<Collider>(true);
        hipsBody = hips.GetComponent<Rigidbody>();
        bones = hips.GetComponentsInChildren<Transform>(true);
        snapshot = new Quaternion[bones.Length];
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
        if (state != State.GettingUp)
            return;
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

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
