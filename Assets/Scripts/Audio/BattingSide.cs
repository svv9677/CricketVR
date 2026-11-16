using UnityEngine;

/// <summary>
/// The batting side's team-mates, watching from the boundary in front of the pavilion (2026-11-08:
/// "have them actually be in the dugout or something and watching, and they can also clap from that
/// distance"). Five of them stand between the rope and the pavilion - at the club ground in front of
/// its pavilion, at the stadium in front of the stand on the same side - facing the middle, in the
/// side's kit, one padded up and in his helmet as the next man in. TeamTalk has them clap and call
/// out a good shot; each claps with his own hands (PlayerClap) and calls in his own voice.
///
/// They are the prefab Prefabs/Scene/Batting Side, placed in CricketVR (BattingSideBuilder) at the
/// same spots for the stadium and the club ground, so nothing is moved for the venue. Each
/// has his own skin tint and a pre-made kit material for every colour; this only picks the side's
/// colour. Not in the nets. No colliders: a ball over the rope goes past them.
/// (Until 2026-11-15 they were built in code as the match scene loaded.)
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
    public const string ObjectName = "Batting Side";

    [SerializeField] private Transform[] members = new Transform[0];
    [SerializeField] private PlayerClap[] hands = new PlayerClap[0];
    [SerializeField] private HumanoidReach[] reaches = new HumanoidReach[0];
    [SerializeField] private Animator[] animators = new Animator[0];
    [SerializeField] private PartnerKit[] kits = new PartnerKit[0];

    public static BattingSide Instance { get; private set; }
    public System.Collections.Generic.IReadOnlyList<Transform> Members => members;
    public System.Collections.Generic.IReadOnlyList<PlayerClap> Hands => hands;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        int kit = KitIndex();
        for (int i = 0; i < members.Length; i++)
        {
            // Not all breathing in step.
            animators[i].Update(Random.Range(0f, 2f));
            kits[i].Apply(kit);
            reaches[i].look = true;
            reaches[i].lookTarget = new Vector3(BallDelivery.BatsmanStumpsX, 1.4f, 0f);
        }
    }

    /// The side's kit: a career's own colours, whites for a quick-play Test, else royal blue.
    private static int KitIndex()
    {
        if (CareerMatchSession.Active)
            return PartnerBatter.KitIndex();
        return TestLook.Applies ? PartnerKit.Whites : 0;
    }

    /// Where team-mate `i` (0-based) stands: along the boundary either side of the pavilion's bearing.
    public static Vector3 Position(int i)
    {
        float bearing = Mathf.Atan2(Toward.y, Toward.x);
        float a = bearing + (i - (Count - 1) * 0.5f) * Spacing / Radius;
        return new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius);
    }
}
