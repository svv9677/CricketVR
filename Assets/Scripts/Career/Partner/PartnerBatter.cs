using UnityEngine;

/// <summary>
/// The AI partner the career player watches from the non-striker's end (phase 3): a body with a
/// helmet and a bat in both hands, standing at whichever end he is batting from, playing the shot
/// PartnerDirector planned. Built only for a career match.
///
/// He is the fielders' "David" on the fielders' idle (as the umpire is). He is the prefab
/// Prefabs/Scene/Partner, placed in CricketVR (PartnerBatterBuilder) and saved switched off; the
/// scene's Systems switches him on for a career match (SceneSystems.eWhen.CareerMatch). He is set
/// up before any other Start, so the replay (ReplayActors) records him as a player and his bat as
/// a prop. The bat ("Partner Bat": the game bat's mesh, a kinematic body, no collider) is let go of
/// to be a root object of its own in Awake, and posed every frame from the swing;
/// PartnerBatterBody bends him onto it. (Until 2026-11-15 all of it was built in code.)
/// Nothing of his touches the ball: the real ball meets his bat only through PartnerContact.
///
/// He does not follow the game on his own. NonStrikerView puts him at an end (TakeStrike,
/// StandAtNonStriker), PartnerDirector starts his swing (Play) and brings him back (BackToStance).
/// He bats the same hand as the player, because the field, the wide lines and the bowlers' lines
/// all follow the player's batting hand (Main.BattingStyle; right-handed by default).
/// </summary>
[DefaultExecutionOrder(-200)]
public class PartnerBatter : MonoBehaviour
{
    public const string ObjectName = "Partner";
    /// Never "Helmet": ReplayActors takes the first mesh under a "Helmet" for the player's own
    /// replay helmet, and this one faces the other way.
    public const string HelmetName = "Partner Helmet";
    public const string BatName = "Partner Bat";
    /// The top of his helmet above his head bone (the bone is at the base of the skull).
    public const float HeadTopAboveBone = 0.20f;
    /// Frames held at guard before his head is sampled (the IK has settled by then).
    public const int StanceSampleFrames = 20;
    /// Easing back to guard (or to backing up) after a ball, seconds.
    public const float BackToStanceSeconds = 0.6f;
    /// The swing clock stops this long after contact: the follow-through is held from there.
    public const float SwingHold = 1.0f;

    public static PartnerBatter Instance { get; private set; }
    public bool Ready { get; private set; }
    public Transform Head { get; private set; }
    public Transform Hips { get; private set; }
    public Transform BatTransform { get; private set; }
    public bool OnStrike { get; private set; }
    /// The top of his head in his stance (head bone in stance + 0.20 m, sampled once).
    public float StanceHeadTopY { get; private set; } = 1.75f;
    /// Where across the crease he takes guard (world z): his head at guard, sampled once with
    /// StanceHeadTopY (the player's own is his head's z, WideCollider).
    public float StanceZ { get; private set; }
    public BatGrip Grip { get; private set; }
    /// +1 right-handed, -1 left-handed (off is +z for a right-hander).
    public float OffSign { get; private set; } = 1f;
    /// His hips at guard (world): the swing's stance (PartnerSwing.Pose).
    public Vector3 StanceHips { get; private set; }
    /// A swing is playing (Play, until BackToStance).
    public bool Swinging => playing;

    [SerializeField] private Animator animator;
    [SerializeField] private PartnerBatterBody body;
    private SwingPose rest, current, easeFrom;
    private ShotPlan plan;
    private bool playing, easing, sampled;
    private float contactAt, easeStart;
    private int stanceFrames;

    // ---- The placed partner ------------------------------------------------------------------

    /// His kit colours: the shirt, trousers and helmet shell for the side's colour (PartnerKit).
    [SerializeField] private PartnerKit kit;
    /// His bat, a child of the prefab until Awake lets it go (see Awake).
    [SerializeField] private Transform bat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    /// The bat becomes a root object of its own: the replay (ReplayActors.FindProps) takes a body
    /// with a mesh that is not part of a player as a prop, and he poses it in world space.
    private void Awake()
    {
        Instance = this;
        if (bat != null)
            bat.SetParent(null, true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (BatTransform != null)
            Destroy(BatTransform.gameObject);
    }

    /// Before any other Start: his Ready, Head and Grip are there for whoever starts next.
    private void Start()
    {
        body.Init(animator);
        Head = animator.GetBoneTransform(HumanBodyBones.Head);
        Hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        kit.Apply(KitIndex());
        Main main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        BatTransform = FitBat(main, out BatGrip grip);
        Grip = grip;
        Ready = Head != null && Hips != null && BatTransform != null;
        if (!Ready)
        {
            Debug.LogWarning("[Partner] not built (no head, hips or bat): partners' balls stay simulated");
            return;
        }
        StandAtNonStriker();
    }

    /// The index of the career player's side's kit colour (the season's team, else the player's own look).
    public static int KitIndex()
    {
        MatchPlan plan = CareerMatchSession.Plan;
        CareerProfile p = CareerSaves.Current;
        Team yours = plan != null && p != null && p.season != null ? p.season.Team(plan.yourTeam) : null;
        int index = yours != null ? yours.kit : p != null && p.look != null ? p.look.kit : 0;
        return Mathf.Clamp(index, 0, Appearance.KitColours.Length - 1);
    }

    /// The career player's side's kit colour.
    public static Color KitColour() => Appearance.KitColours[KitIndex()];

    /// His bat at the game bat's size (the placed bat already has its mesh and materials), and the
    /// grip from the game bat's markers. Null (no bat) when there is no game bat to measure.
    private Transform FitBat(Main main, out BatGrip grip)
    {
        grip = default;
        MeshFilter source = main != null && main.theBat != null ? main.theBat.GetComponent<MeshFilter>() : null;
        if (source == null || source.sharedMesh == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Bat");
            source = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
        }
        if (bat == null || source == null || source.sharedMesh == null || source.GetComponent<MeshRenderer>() == null)
            return null;
        bat.localScale = source.transform.lossyScale;
        grip = PartnerStance.GripFrom(source.GetComponent<BatGeometry>(), source.sharedMesh, source.transform.lossyScale.x);
        return bat;
    }


    // ---- The ends ----------------------------------------------------------------------------

    /// At the striker's crease: guard on middle stump, side-on, for his hand.
    public void TakeStrike()
    {
        OnStrike = true;
        if (!Ready)
            return;
        OffSign = PartnerStance.OffSign(BattingHand());
        StanceHips = PartnerStance.StanceHips(OffSign, body.StandingHipsY);
        if (!sampled)
        {
            // Until his head is sampled at guard: standing head height less the crouch, and his
            // head's z bent over the bat (StanceZ is always his head's z, never his hips').
            StanceHeadTopY = body.StandingHeadY - PartnerStance.Crouch + HeadTopAboveBone;
            StanceZ = PartnerStance.StanceHeadZ(OffSign, body.StandingHipsY, body.StandingHeadY);
        }
        transform.SetPositionAndRotation(new Vector3(StanceHips.x, 0f, StanceHips.z), PartnerStance.StrikerFacing(OffSign));
        PartnerStance.StanceFeetTurn(OffSign, out Quaternion left, out Quaternion right);
        body.Configure(PartnerBatterBody.Hold.BothHands, OffSign > 0f, PartnerStance.Lean, left, right);
        rest = PartnerStance.Stance(Grip, OffSign, body.StandingHipsY);
        Settle();
    }

    /// The side he last took at the non-striker's end (EndPose.NonStrikerSide).
    private float standingSide = -1f;

    /// A bowler of the other arm has come on: backing up, he crosses to the other side of the
    /// stumps, out of the new run-up (between balls only).
    public void KeepNonStrikerSide()
    {
        if (Ready && !OnStrike && !playing && standingSide != EndPose.NonStrikerSide)
            StandAtNonStriker();
    }

    /// At the non-striker's end: backing up, bat grounded, standing still, out of the bowler's way.
    public void StandAtNonStriker()
    {
        OnStrike = false;
        if (!Ready)
            return;
        Quaternion facing = PartnerStance.NonStrikerFacing;
        standingSide = EndPose.NonStrikerSide;
        transform.SetPositionAndRotation(new Vector3(PartnerStance.NonStrikerX, 0f, PartnerStance.NonStrikerZ), facing);
        body.Configure(PartnerBatterBody.Hold.RightHandOnTop, true, 0f, facing, facing);
        rest = PartnerStance.NonStriker(Grip, body.StandingHipsY);
        Settle();
    }

    /// Straight into the end's rest pose (a change of end happens behind the blink).
    private void Settle()
    {
        playing = false;
        easing = false;
        stanceFrames = 0;
        Apply(rest);
    }

    private static eBattingStyle BattingHand()
    {
        if (Main.Instance != null && Main.Instance.BattingStyle != eBattingStyle.None)
            return Main.Instance.BattingStyle;
        return GameSettings.BattingStyle;
    }

    // ---- The swing ---------------------------------------------------------------------------

    /// Start the swing so the bat meets the ball `contactTimeFromNow` seconds from now.
    public void Play(in ShotPlan plan, float contactTimeFromNow)
    {
        if (!Ready || !OnStrike)
        {
            Debug.LogWarning("[Partner] Play while not on strike: ignored");
            return;
        }
        this.plan = plan;
        contactAt = Time.time + contactTimeFromNow;
        playing = true;
        easing = false;
    }

    /// Ease back to this end's rest pose from wherever the swing left him.
    public void BackToStance()
    {
        if (!Ready)
            return;
        easeFrom = current;
        easeStart = Time.time;
        playing = false;
        easing = true;
    }

    private void Update()
    {
        if (!Ready)
            return;
        // Backing up between balls: across to the other side of the stumps if a bowler of the
        // other arm has come on (EndPose.NonStrikerSide), out of his run-up.
        Main m = Main.Instance;
        if (!OnStrike && m != null && (m.gameState == eGameState.InGame_Ready || m.gameState == eGameState.InGame_ResetToReadyLoop))
            KeepNonStrikerSide();
        SwingPose target = rest;
        if (playing)
        {
            // Scaled time, as the ball's: a slowed game slows the swing with it.
            float t = Mathf.Min(Time.time - contactAt, SwingHold);
            SwingPose p = PartnerSwing.Pose(plan, Grip, OffSign, StanceHips, t);
            if (PartnerStance.Valid(p))
            {
                PartnerStance.Complete(ref p, Grip, rest);
                target = p;
            }
        }
        else if (easing)
        {
            float k = Mathf.Clamp01((Time.time - easeStart) / BackToStanceSeconds);
            target = PartnerStance.Blend(easeFrom, rest, Mathf.SmoothStep(0f, 1f, k));
            easing = k < 1f;
        }
        Apply(target);
    }

    /// The bat where the pose has it; the body after it; the head on the ball while it is live.
    private void Apply(in SwingPose p)
    {
        current = p;
        BatTransform.SetPositionAndRotation(p.batPosition, p.batRotation);
        body.Pose(p);
        body.Look = BallInPlay(out Vector3 ball) ? ball
                  : p.look.sqrMagnitude > 1e-6f ? p.look
                  : OnStrike ? PartnerStance.LookAtBowler : PartnerStance.LookAtStriker;
    }

    private static bool BallInPlay(out Vector3 at)
    {
        at = default;
        Main m = Main.Instance;
        if (m == null || m.theBall == null)
            return false;
        eGameState s = m.gameState;
        bool live = s == eGameState.InGame_DeliverBallLoop || s == eGameState.InGame_BallHit || s == eGameState.InGame_BallHitLoop;
        if (live)
            at = m.theBall.transform.position;
        return live;
    }

    /// After the Animator: his head at guard, once, for the over-the-head wide and the wide lines.
    private void LateUpdate()
    {
        if (!Ready || sampled || !OnStrike || playing || easing)
            return;
        if (++stanceFrames < StanceSampleFrames)
            return;
        StanceHeadTopY = Head.position.y + HeadTopAboveBone;
        StanceZ = Head.position.z;
        sampled = true;
    }
}

/// <summary>
/// A team-mate's kit, as placed: the shirt, trousers and (for one who is padded up) the helmet shell
/// each with a pre-made material for every kit colour (Appearance.KitColours, then whites for a
/// Test). Apply picks one; nothing is made at runtime. Filled by the Scene Objects builders.
/// </summary>
[System.Serializable]
public class PartnerKit
{
    /// The kit index for whites (a quick-play Test): after the career kit colours.
    public static int Whites => Appearance.KitColours.Length;

    public Renderer[] shirts = new Renderer[0];
    public Renderer[] trousers = new Renderer[0];
    /// The helmet's shell; null for one who wears no helmet.
    public Renderer shell;
    public Material[] shirtMaterials = new Material[0];
    public Material[] trouserMaterials = new Material[0];
    public Material[] shellMaterials = new Material[0];

    public void Apply(int index)
    {
        Set(shirts, shirtMaterials, index);
        Set(trousers, trouserMaterials, index);
        if (shell != null && index >= 0 && index < shellMaterials.Length && shellMaterials[index] != null)
            shell.sharedMaterial = shellMaterials[index];
    }

    private static void Set(Renderer[] renderers, Material[] materials, int index)
    {
        if (index < 0 || index >= materials.Length || materials[index] == null)
            return;
        foreach (Renderer r in renderers)
            if (r != null)
                r.sharedMaterial = materials[index];
    }
}
