using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The batting side's team-mates, watching from the boundary in front of the pavilion (2026-11-08:
/// "have them actually be in the dugout or something and watching, and they can also clap from that
/// distance"). Five of them stand between the rope and the pavilion - at the club ground in front of
/// its pavilion, at the stadium in front of the stand on the same side - facing the middle, in the
/// side's kit, one padded up and in his helmet as the next man in. TeamTalk has them clap and call
/// out a good shot; each claps with his own hands (PlayerClap) and calls in his own voice.
///
/// Built with each match scene; not in the nets. No colliders: a ball over the rope goes past them.
/// </summary>
public class BattingSide : MonoBehaviour
{
    /// Between the rope (56.64) and the pavilion's front (~62.5) or the stand's wall (62.6).
    public const float Radius = 60.2f;
    /// On the pavilion's bearing (ClubGroundLayout.Pavilion, (-60, 30)): 23 degrees off the
    /// batter's line, clear of the sightscreen.
    public static readonly Vector2 Toward = new Vector2(-60f, 30f);
    /// How many, and how far apart along the boundary (m).
    public const int Count = 5;
    public const float Spacing = 1.6f;

    private static readonly Color Whites = new Color(0.93f, 0.93f, 0.9f);
    /// Skin tints over the model's own skin, so five copies of one man read as five men.
    private static readonly Color[] Skin =
    {
        Color.white, new Color(0.86f, 0.78f, 0.72f), new Color(0.7f, 0.6f, 0.52f), new Color(0.95f, 0.9f, 0.86f),
        new Color(0.6f, 0.5f, 0.43f),
    };

    private readonly List<Transform> members = new List<Transform>();
    private readonly List<PlayerClap> hands = new List<PlayerClap>();

    public static BattingSide Instance { get; private set; }
    public IReadOnlyList<Transform> Members => members;
    public IReadOnlyList<PlayerClap> Hands => hands;

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
        if (scene.name != CareerMatchSession.MatchScene || Instance != null)
            return;
        var go = new GameObject("Batting Side");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<BattingSide>();
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        GameObject model = Resources.Load<GameObject>("Models/David");
        var controller = Resources.Load<RuntimeAnimatorController>("Animations/Fielder Controller");
        if (model == null || controller == null)
            return;
        Color kit = Kit();
        float bearing = Mathf.Atan2(Toward.y, Toward.x);
        for (int i = 0; i < Count; i++)
        {
            // Along the boundary either side of the bearing.
            float a = bearing + (i - (Count - 1) * 0.5f) * Spacing / Radius;
            var at = new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius);
            Make(model, controller, at, kit, i);
        }
    }

    private void Make(GameObject model, RuntimeAnimatorController controller, Vector3 at, Color kit, int i)
    {
        GameObject who = Instantiate(model, transform);
        who.name = "Team-mate " + (i + 1);
        Vector3 toMiddle = -at;
        toMiddle.y = 0f;
        who.transform.SetPositionAndRotation(at, Quaternion.LookRotation(toMiddle.normalized, Vector3.up));
        who.transform.localScale = Vector3.one * (0.96f + 0.02f * ((i * 7) % 5));
        foreach (Collider c in who.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        Animator anim = who.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Destroy(who);
            return;
        }
        anim.runtimeAnimatorController = controller;      // Action 0: the idle
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        // Not all breathing in step.
        anim.Update(Random.Range(0f, 2f));
        // The middle one is next in: padded up, helmet on.
        if (i == Count / 2)
            PartnerKit.Dress(anim, kit);
        else
            PartnerKit.DressClothes(anim, kit);
        Tint(who, Skin[i % Skin.Length]);
        var reach = who.AddComponent<HumanoidReach>();
        reach.look = true;
        reach.lookTarget = new Vector3(BallDelivery.BatsmanStumpsX, 1.4f, 0f);
        members.Add(who.transform);
        hands.Add(who.AddComponent<PlayerClap>());
    }

    /// The side's kit: a career's own colours, whites for a quick-play Test, else royal blue.
    private static Color Kit()
    {
        if (CareerMatchSession.Active)
            return PartnerBatter.KitColour();
        return TestLook.Applies ? Whites : Appearance.KitColours[0];
    }

    private static void Tint(GameObject who, Color tint)
    {
        foreach (SkinnedMeshRenderer skin in who.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body"))
                continue;
            var m = new Material(skin.sharedMaterial) { name = "Team-mate Skin" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            skin.sharedMaterial = m;
        }
    }
}
