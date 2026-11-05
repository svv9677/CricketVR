using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The batting card in a match: the side's name, score and overs, a line for each of the eleven
/// (how he got out, or not out, or yet to bat; runs and balls; a star for the striker; "You" for
/// the career player), then the extras and the total. It stands where the last-shot card stands
/// in the nets (ShotCard: off to the batter's off side, turned to the crease, mirrored for a
/// left-hander, carried to the non-striker's end with the player: PlayerEnd, EndPose), and the
/// shot card stands down in any scene that has one of these (ShownInstead).
///
/// A prefab (Assets/Resources/Prefabs/UI/ScorecardPanel.prefab, built and placed in CricketVR.unity
/// by Tools > CricketVR > Build Scorecard Panel). Display only: no raycasters, so the laser passes
/// through it. Only between balls, as the shot card is.
///
/// The card comes from BattingCardFeed (HUD's events, and how each batter got out). Nothing is
/// built per frame: an event marks the card dirty, and it is built once, the next time it is
/// shown or the next frame it is up.
/// </summary>
public class ScorecardPanel : MonoBehaviour
{
    /// One batter's line: its row (hidden when the side has fewer than eleven) and four cells.
    [System.Serializable]
    public class RowView
    {
        public GameObject root;
        public TMP_Text name;
        public TMP_Text howOut;
        public TMP_Text runs;
        public TMP_Text balls;
    }

    public static ScorecardPanel Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [Header("Heading")]
    [SerializeField] private TMP_Text team;
    [SerializeField] private TMP_Text score;
    [SerializeField] private TMP_Text overs;
    [Header("Batters")]
    [SerializeField] private RowView[] rows = new RowView[0];
    [Header("Foot")]
    [SerializeField] private TMP_Text extras;
    [SerializeField] private TMP_Text total;
    [Header("Style")]
    [SerializeField] private Color textColor = new Color(0.953f, 0.961f, 0.973f);
    [SerializeField] private Color mutedColor = new Color(0.596f, 0.635f, 0.702f);
    [SerializeField] private Color youColor = new Color(0.54f, 0.72f, 1f);
    [Header("Placement (right-handed batter; mirrored for left) - as ShotCard's")]
    [SerializeField] private Vector3 rightHandedPosition = new Vector3(7.0f, 1.8f, 2.8f);
    [SerializeField] private Vector3 eyePosition = new Vector3(9.8f, 1.6f, 0f);

    private readonly BattingCardFeed feed = new BattingCardFeed();
    private eBattingStyle placedFor = eBattingStyle.None;
    private bool placedAtNonStriker;
    private bool dirty = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    /// True when a batting card stands in `scene`: the shot card there hides (ShotCard.Update), so
    /// a match shows the card and the nets, which have none, keep the last shot.
    public static bool ShownInstead(Scene scene) =>
        Instance != null && Instance.isActiveAndEnabled && Instance.gameObject.scene == scene;

    private void Awake()
    {
        Instance = this;
        feed.Changed += MarkDirty;
        // Here, not OnEnable: a wicket while the object was off would be lost for good.
        feed.Attach();
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnDestroy()
    {
        feed.Changed -= MarkDirty;
        feed.Detach();
        if (Instance == this)
            Instance = null;
    }

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (panel == null)
            return;
        Main main = Main.Instance;
        eBattingStyle style = main != null ? main.BattingStyle : eBattingStyle.RightHanded;
        if (style != placedFor || PlayerEnd.NonStriker != placedAtNonStriker)
            Place(style);
        // Only between balls, alongside the next-ball options (as ShotCard).
        bool between = main == null || main.gameState == eGameState.InGame_Ready;
        if (panel.activeSelf != between)
            panel.SetActive(between);
        if (between && dirty)
            Refresh();
    }

    /// ShotCard's spot: placed for the crease, then carried to the player's end.
    private void Place(eBattingStyle style)
    {
        placedFor = style;
        placedAtNonStriker = PlayerEnd.NonStriker;
        float side = style == eBattingStyle.LeftHanded ? -1f : 1f;
        Vector3 position = EndPose.FromCrease(new Vector3(rightHandedPosition.x, rightHandedPosition.y, rightHandedPosition.z * side),
                                              placedAtNonStriker);
        Vector3 eye = EndPose.FromCrease(eyePosition, placedAtNonStriker);
        Vector3 away = Vector3.ProjectOnPlane(position - eye, Vector3.up);
        if (away.sqrMagnitude < 1e-4f)
            away = placedAtNonStriker ? Vector3.right : Vector3.left;
        // Canvas text faces -Z: point +Z away from the batter.
        panel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(away.normalized, Vector3.up));
    }

    /// The card from the scorebook into the texts. Left dirty while there is no scorebook yet.
    private void Refresh()
    {
        HUD hud = HUD.Instance;
        if (hud == null || hud.Batsmen == null)
            return;
        dirty = false;
        MatchPlan plan = CareerMatchSession.Active ? CareerMatchSession.Plan : null;
        BattingCard card = feed.Build(hud, BattingCard.TeamName(plan?.yourTeamName, ReplayOverlay.Team));
        Set(team, card.Team);
        Set(score, card.Score);
        Set(overs, card.Overs);
        Set(extras, card.Extras);
        Set(total, card.Total);
        for (int i = 0; i < rows.Length; i++)
        {
            RowView view = rows[i];
            if (view == null || view.root == null)
                continue;
            bool used = i < card.Rows.Count;
            if (view.root.activeSelf != used)
                view.root.SetActive(used);
            if (used)
                Draw(view, card.Rows[i]);
        }
    }

    /// Batters in are bright and bold, the gone plain, those still to come grey; the career player
    /// in the accent colour, whatever his state.
    private void Draw(RowView view, BattingCardRow row)
    {
        Color main = row.State == eBatterState.YetToBat ? mutedColor : textColor;
        Set(view.name, row.Name, row.You ? youColor : main, row.State == eBatterState.NotOut);
        Set(view.howOut, row.HowOut, row.State == eBatterState.Out ? textColor : mutedColor, false);
        Set(view.runs, row.Runs, main, row.State == eBatterState.NotOut);
        Set(view.balls, row.Balls, mutedColor, false);
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static void Set(TMP_Text text, string value, Color color, bool bold)
    {
        if (text == null)
            return;
        Set(text, value);
        text.color = color;
        FontStyles style = bold ? text.fontStyle | FontStyles.Bold : text.fontStyle & ~FontStyles.Bold;
        if (text.fontStyle != style)
            text.fontStyle = style;
    }
}
