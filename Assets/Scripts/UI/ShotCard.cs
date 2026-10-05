using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The last-shot card: how well it was struck ("Middled", "Thick edge", "Toe") with a quality
/// bar, ball / bat / exit speed, how far it carried (or went, for a six), and the score. It stands
/// off to the batter's off side, well out of the line to the bowler, and turns to face the
/// crease; it moves across when the batting hand changes.
///
/// A prefab (Assets/Resources/Prefabs/UI/ShotCard.prefab, built by Tools > CricketVR > Build Player
/// UI). Fed by HUD.ShowShot (Bat.ShotStruck), ShotDistance (carry / six distance) and HUD.UpdateUI
/// (score). Display only: no raycasters, so the laser passes through it.
/// </summary>
public class ShotCard : MonoBehaviour
{
    public static ShotCard Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [Header("Shot")]
    [SerializeField] private TMP_Text contact;
    [SerializeField] private RectTransform qualityFill;
    [SerializeField] private TMP_Text ballSpeed;
    [SerializeField] private TMP_Text batSpeed;
    [SerializeField] private TMP_Text exitSpeed;
    [SerializeField] private TMP_Text distance;
    [SerializeField] private TMP_Text distanceCaption;
    [Header("Score")]
    [SerializeField] private TMP_Text score;
    [SerializeField] private TMP_Text overs;
    [SerializeField] private TMP_Text recent;
    [Header("Style")]
    [SerializeField] private Color goodColor = new Color(0.54f, 0.72f, 1f);
    [SerializeField] private Color okColor = Color.white;
    [SerializeField] private Color poorColor = new Color(0.6f, 0.64f, 0.7f);
    [Header("Placement (right-handed batter; mirrored for left)")]
    [SerializeField] private Vector3 rightHandedPosition = new Vector3(7.0f, 1.8f, 2.8f);
    [SerializeField] private Vector3 eyePosition = new Vector3(9.8f, 1.6f, 0f);

    private const int RecentBallCount = 6;
    private eBattingStyle placedFor = eBattingStyle.None;
    private readonly StringBuilder builder = new StringBuilder(32);

    private TMP_Text scorecard;

    private void Awake()
    {
        Instance = this;
        BuildScorecard();
        Clear();
    }

    /// The batters and the bowler, under the score, built here so the prefab needs no rebuild: a
    /// divider like the one above the score, then one line per player in the recent-balls style.
    private void BuildScorecard()
    {
        if (panel == null || recent == null)
            return;
        Transform divider = panel.transform.Find("Divider");
        if (divider != null)
            Instantiate(divider.gameObject, panel.transform).name = "Scorecard Divider";
        GameObject go = Instantiate(recent.gameObject, panel.transform);
        go.name = "Scorecard";
        scorecard = go.GetComponent<TMP_Text>();
        scorecard.fontSize = 22f;
        scorecard.alignment = TextAlignmentOptions.TopLeft;
        scorecard.richText = true;
        var layout = go.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layout != null)
            layout.preferredHeight = 128f;
    }

    /// Batters' runs (balls) and strike rate, the bowler's figures and the run rate.
    private void SetScorecard()
    {
        HUD hud = HUD.Instance;
        if (scorecard == null || hud == null || hud.CurrentBatsman == null)
            return;
        builder.Clear();
        AppendBatter(hud.CurrentBatsman, true);
        AppendBatter(hud.CurrentRunner, false);
        builder.Append("<color=#98A2B3>Bowler</color>  ").Append(hud.CurrentBowler.Name)
               .Append("<pos=62%>").Append(HUD.Figures(hud.CurrentBowler)).Append('\n');
        builder.Append("<color=#98A2B3>Run rate</color>  ").Append(hud.RunRate.ToString("F2"));
        scorecard.text = builder.ToString();
    }

    private void AppendBatter(Batsman b, bool striker)
    {
        builder.Append("<color=#F3F5F8>").Append(b.Name).Append(striker ? "*" : "").Append("</color>")
               .Append("<pos=48%>").Append(HUD.RunsAndBalls(b))
               .Append("<pos=74%><color=#98A2B3>SR</color> ").Append(HUD.StrikeRate(b)).Append('\n');
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Main main = Main.Instance;
        eBattingStyle style = main != null ? main.BattingStyle : eBattingStyle.RightHanded;
        if (style != placedFor)
            Place(style);
        // Only between balls, alongside the next-ball options: while a ball is being bowled or
        // is in play it is in the way.
        bool between = main == null || main.gameState == eGameState.InGame_Ready;
        if (panel.activeSelf != between)
            panel.SetActive(between);
    }

    private void Place(eBattingStyle style)
    {
        placedFor = style;
        float side = style == eBattingStyle.LeftHanded ? -1f : 1f;
        Vector3 position = new Vector3(rightHandedPosition.x, rightHandedPosition.y, rightHandedPosition.z * side);
        Vector3 away = Vector3.ProjectOnPlane(position - eyePosition, Vector3.up);
        if (away.sqrMagnitude < 1e-4f)
            away = Vector3.left;
        // Canvas text faces -Z: point +Z away from the batter.
        panel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(away.normalized, Vector3.up));
    }

    public void Clear()
    {
        Set(contact, "Ready");
        if (contact != null) contact.color = poorColor;
        SetQuality(0f);
        Set(ballSpeed, "-");
        Set(batSpeed, "-");
        Set(exitSpeed, "-");
        Set(distance, "-");
        Set(distanceCaption, "Carry");
    }

    /// A struck ball. Distance is left to SetDistance, which may arrive before or after this.
    public void ShowShot(ShotInfo info)
    {
        float q = Mathf.Clamp01(info.quality);
        Set(contact, string.IsNullOrEmpty(info.contactLabel) ? UIFormat.Contact(q, info.edge) : info.contactLabel);
        if (contact != null)
            contact.color = info.edge ? poorColor : q >= 0.7f ? goodColor : q >= 0.35f ? okColor : poorColor;
        SetQuality(q);
        Set(ballSpeed, UIFormat.Kmh(info.ballSpeedIn));
        Set(batSpeed, UIFormat.Kmh(info.batSpeed));
        Set(exitSpeed, UIFormat.Kmh(info.exitSpeed));
    }

    /// Where the ball first lands (carry), or where a six would have come down.
    public void SetDistance(float metres, bool six)
    {
        Set(distanceCaption, six ? "Six" : "Carry");
        Set(distance, UIFormat.WithUnit(Mathf.RoundToInt(metres).ToString(), "m"));
    }

    public void SetScore(int runs, int wickets, int completedOvers, int balls, IReadOnlyList<string> bowled)
    {
        Set(score, $"{runs}/{wickets}");
        Set(overs, $"{completedOvers}.{balls} ov");
        if (recent == null)
            return;
        // Built from the same builder below, so first.
        SetScorecard();
        builder.Clear();
        int count = bowled != null ? bowled.Count : 0;
        for (int i = Mathf.Max(0, count - RecentBallCount); i < count; i++)
        {
            if (builder.Length > 0) builder.Append("   ");
            builder.Append(bowled[i] == "*" ? "0" : bowled[i]);   // "*" is HUD's dot ball
        }
        recent.text = builder.Length > 0 ? builder.ToString() : " ";
    }

    private void SetQuality(float q)
    {
        if (qualityFill == null)
            return;
        qualityFill.anchorMin = Vector2.zero;
        qualityFill.anchorMax = new Vector2(q, 1f);
        qualityFill.offsetMin = qualityFill.offsetMax = Vector2.zero;
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
