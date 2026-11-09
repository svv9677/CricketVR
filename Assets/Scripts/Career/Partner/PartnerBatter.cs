using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The AI partner the career player watches from the non-striker's end (phase 3): a body with a
/// helmet and a bat in both hands, standing at whichever end he is batting from, playing the shot
/// PartnerDirector planned. Built only for a career match.
///
/// He is the fielders' "David" on the fielders' idle (as the umpire is), built when the match scene
/// loads and before any Start, so the replay (ReplayActors) records him as a player and his bat as
/// a prop. The bat is a root object of its own ("Partner Bat": the game bat's mesh, a kinematic
/// body, no collider), posed every frame from the swing; PartnerBatterBody bends him onto it.
/// Nothing of his touches the ball: the real ball meets his bat only through PartnerContact.
///
/// He does not follow the game on his own. NonStrikerView puts him at an end (TakeStrike,
/// StandAtNonStriker), PartnerDirector starts his swing (Play) and brings him back (BackToStance).
/// He bats the same hand as the player, because the field, the wide lines and the bowlers' lines
/// all follow the player's batting hand (Main.BattingStyle; right-handed by default).
/// </summary>
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

    private Animator animator;
    private PartnerBatterBody body;
    private SwingPose rest, current, easeFrom;
    private ShotPlan plan;
    private bool playing, easing, sampled;
    private float contactAt, easeStart;
    private int stanceFrames;

    // ---- Built with the match scene ----------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != CareerMatchSession.MatchScene || !CareerMatchSession.Active)
            return;
        if (Instance != null || FindFirstObjectByType<PartnerBatter>() != null)
            return;
        GameObject model = Resources.Load<GameObject>("Models/David");
        var controller = Resources.Load<RuntimeAnimatorController>("Animations/Fielder Controller");
        if (model == null || controller == null)
            return;
        GameObject who = Instantiate(model);
        who.name = ObjectName;
        SceneManager.MoveGameObjectToScene(who, scene);
        Animator a = who.GetComponent<Animator>();
        if (a == null || !a.isHuman)
        {
            Destroy(who);
            return;
        }
        a.runtimeAnimatorController = controller;   // Action 0: the idle, under the IK
        a.applyRootMotion = false;
        a.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the replay records him
        // The ball goes through him to the stumps: nothing of his may have a collider.
        Collider[] colliders = who.GetComponentsInChildren<Collider>(true);
        Debug.Assert(colliders.Length == 0, "[Partner] David has colliders; removing them");
        foreach (Collider c in colliders)
            Destroy(c);
        who.AddComponent<PartnerBatter>().Setup(a, scene);
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (BatTransform != null)
            Destroy(BatTransform.gameObject);
    }

    private void Setup(Animator a, Scene scene)
    {
        animator = a;
        body = a.gameObject.AddComponent<PartnerBatterBody>();
        body.Init(a);
        Head = a.GetBoneTransform(HumanBodyBones.Head);
        Hips = a.GetBoneTransform(HumanBodyBones.Hips);
        PartnerKit.Dress(a, KitColour());
        Main main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        BatTransform = BuildBat(main, scene, out BatGrip grip);
        Grip = grip;
        Ready = Head != null && Hips != null && BatTransform != null;
        if (!Ready)
        {
            Debug.LogWarning("[Partner] not built (no head, hips or bat): partners' balls stay simulated");
            return;
        }
        StandAtNonStriker();
    }

    /// The career player's side's kit colour (the season's team, else the player's own look).
    public static Color KitColour()
    {
        MatchPlan plan = CareerMatchSession.Plan;
        CareerProfile p = CareerSaves.Current;
        Team yours = plan != null && p != null && p.season != null ? p.season.Team(plan.yourTeam) : null;
        int kit = yours != null ? yours.kit : p != null && p.look != null ? p.look.kit : 0;
        return Appearance.KitColours[Mathf.Clamp(kit, 0, Appearance.KitColours.Length - 1)];
    }

    /// His bat: the game bat's mesh and materials at its size, a kinematic body with no collider
    /// (ReplayActors.FindProps records it), and the grip from its markers.
    private static Transform BuildBat(Main main, Scene scene, out BatGrip grip)
    {
        grip = default;
        MeshFilter source = main != null && main.theBat != null ? main.theBat.GetComponent<MeshFilter>() : null;
        if (source == null || source.sharedMesh == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Bat");
            source = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
        }
        MeshRenderer look = source != null ? source.GetComponent<MeshRenderer>() : null;
        if (source == null || source.sharedMesh == null || look == null)
            return null;
        var bat = new GameObject(BatName);
        SceneManager.MoveGameObjectToScene(bat, scene);
        bat.transform.localScale = source.transform.lossyScale;
        bat.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        bat.AddComponent<MeshRenderer>().sharedMaterials = look.sharedMaterials;
        Rigidbody body = bat.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = false;
        grip = PartnerStance.GripFrom(source.GetComponent<BatGeometry>(), source.sharedMesh, source.transform.lossyScale.x);
        return bat.transform;
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
/// The partner's kit, in simple shapes on his bones: his side's colours on his clothes, a helmet
/// (a shell, a peak and a grille) under "Partner Helmet", pads on his shins and batting gloves.
/// </summary>
public static class PartnerKit
{
    /// The helmet's shell, centred this far below the top of his head, and its size.
    public const float ShellDrop = 0.075f;
    public static readonly Vector3 ShellSize = new Vector3(0.25f, 0.23f, 0.27f);
    /// The peak: out in front of the shell, its size, tipped down (degrees).
    public const float PeakForward = 0.135f, PeakTilt = 8f;
    public static readonly Vector3 PeakSize = new Vector3(0.21f, 0.012f, 0.09f);
    /// The grille: three bars across the face from this far below the top, this far apart, this
    /// far in front; each this thick and this long.
    public const float GrilleTop = 0.13f, GrilleGap = 0.04f, GrilleForward = 0.13f, BarThickness = 0.012f, BarLength = 0.22f;
    /// Pads: this thick (across, front to back), this much longer than the shin, this far in front of it.
    public const float PadWidth = 0.15f, PadDepth = 0.12f, PadLength = 1.1f, PadForward = 0.04f;
    /// Gloves: a ball this big round each hand, this far toward the fingers.
    public const float GloveSize = 0.10f, GloveForward = 0.04f;
    /// No head mesh to measure: the top of the head above the head bone.
    public const float DefaultHeadTop = 0.17f;
    /// The top of his head is the highest vertex within this distance (across the ground) of his head bone.
    public const float HeadSearchRadius = 0.2f;
    /// His trousers are his shirt's colour darkened by this much toward black.
    public const float TrouserShade = 0.3f;

    private static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);
    private static readonly Color Whites = new Color(0.93f, 0.93f, 0.9f);
    private static readonly Color Shoes = new Color(0.9f, 0.9f, 0.9f);

    public static void Dress(Animator a, Color kit)
    {
        Clothes(a, kit);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return;
        Material shell = Plain(lit, "Partner Helmet", kit), metal = Plain(lit, "Partner Grille", Metal), white = Plain(lit, "Partner Pads", Whites);
        Helmet(a, shell, metal);
        Pads(a, white);
        Gloves(a, white);
    }

    private static Material Plain(Shader lit, string name, Color c)
    {
        var m = new Material(lit) { name = name };
        m.SetColor("_BaseColor", c);
        return m;
    }

    /// Shirt, trousers and shoes only (no helmet, pads or gloves): a team-mate off the field (BattingSide).
    public static void DressClothes(Animator a, Color kit) => Clothes(a, kit);

    /// Shirt and trousers in his side's colour, as the umpire is dressed (Umpire.Dress).
    private static void Clothes(Animator a, Color kit)
    {
        Color trousers = Color.Lerp(kit, Color.black, TrouserShade);
        foreach (SkinnedMeshRenderer skin in a.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string n = skin.name;
            Color? plain = n.Contains("Hoody") ? kit : n.Contains("Pants") ? trousers : n.Contains("Sneakers") ? Shoes : (Color?)null;
            if (plain == null)
                continue;
            var material = new Material(skin.sharedMaterial) { name = "Partner " + n };
            material.mainTexture = null;        // one flat colour: the atlas would show through
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", plain.Value);
            if (material.HasProperty("_Color")) material.SetColor("_Color", plain.Value);
            skin.sharedMaterial = material;
        }
    }

    /// Built as he stands at build (upright, facing his root's forward), then carried by his head.
    private static void Helmet(Animator a, Material shell, Material metal)
    {
        Transform head = a.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;
        Vector3 f = a.transform.forward, up = Vector3.up;
        Quaternion facing = Quaternion.LookRotation(f, up);
        float top = TopOfHead(a, head);
        Transform helmet = new GameObject(PartnerBatter.HelmetName).transform;
        helmet.SetPositionAndRotation(head.position, facing);
        helmet.SetParent(head, true);
        Vector3 centre = head.position + up * (top - ShellDrop);
        Part(PrimitiveType.Sphere, helmet, shell, "Shell", centre, facing, ShellSize);
        Part(PrimitiveType.Cube, helmet, shell, "Peak", centre + f * PeakForward,
             facing * Quaternion.Euler(PeakTilt, 0f, 0f), PeakSize);
        Quaternion across = facing * Quaternion.Euler(0f, 0f, 90f);     // a cylinder's length is its y
        var bar = new Vector3(BarThickness, BarLength * 0.5f, BarThickness);
        for (int i = 0; i < 3; i++)
            Part(PrimitiveType.Cylinder, helmet, metal, "Grille", head.position + up * (top - GrilleTop - i * GrilleGap) + f * GrilleForward, across, bar);
    }

    private static void Pads(Animator a, Material white)
    {
        Pad(a, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, white);
        Pad(a, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, white);
    }

    private static void Pad(Animator a, HumanBodyBones shin, HumanBodyBones foot, Material white)
    {
        Transform knee = a.GetBoneTransform(shin), ankle = a.GetBoneTransform(foot);
        if (knee == null || ankle == null)
            return;
        Vector3 along = ankle.position - knee.position;
        if (along.sqrMagnitude < 1e-4f)
            return;
        Quaternion turn = Quaternion.LookRotation(a.transform.forward, along);
        Vector3 at = (knee.position + ankle.position) * 0.5f + a.transform.forward * PadForward;
        // A capsule is 2 long and 1 across: half the length in y.
        Part(PrimitiveType.Capsule, knee, white, "Pad", at, turn, new Vector3(PadWidth, along.magnitude * PadLength * 0.5f, PadDepth));
    }

    private static void Gloves(Animator a, Material white)
    {
        foreach (var (hand, finger) in new[] { (HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal), (HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal) })
        {
            Transform h = a.GetBoneTransform(hand), m = a.GetBoneTransform(finger);
            if (h == null)
                continue;
            Vector3 toward = m != null ? (m.position - h.position).normalized : Vector3.zero;
            Part(PrimitiveType.Sphere, h, white, "Glove", h.position + toward * GloveForward, Quaternion.identity, Vector3.one * GloveSize);
        }
    }

    private static void Part(PrimitiveType shape, Transform parent, Material material, string name, Vector3 at, Quaternion turn, Vector3 size)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.Destroy(part.GetComponent<Collider>());      // the ball passes through him
        part.name = name;
        part.transform.SetPositionAndRotation(at, turn);
        part.transform.localScale = size;
        part.transform.SetParent(parent, true);
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// The top of his head (hair included) above the head bone, from the skinned mesh itself, as
    /// the umpire's hat is fitted (the renderers' bounds are padded).
    private static float TopOfHead(Animator a, Transform head)
    {
        float top = float.NegativeInfinity;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer skin in a.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body") && !skin.name.Contains("Hair"))
                continue;
            skin.BakeMesh(baked, true);
            Vector3 at = skin.transform.position;
            Quaternion turn = skin.transform.rotation;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = at + turn * v;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(head.position.x, head.position.z)) < HeadSearchRadius)
                    top = Mathf.Max(top, w.y);
            }
        }
        Object.Destroy(baked);
        return float.IsNegativeInfinity(top) ? DefaultHeadTop : top - head.position.y;
    }
}
