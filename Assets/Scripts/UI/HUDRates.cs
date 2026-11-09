using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A bar across the bottom of the floating scorecard with the rates (2026-11-06):
///   chasing        "RRR 9.40   CRR 7.20   ·   47 needed off 30 balls"
///   batting first  "CRR 7.20   ·   at 7.2: 144   at 9.2: 164   at 5.2: 124" - the total if the
///                  innings goes on at the current rate, two an over quicker, and two slower.
/// Nothing in a Test (no over limit to project to) or in the nets.
///
/// Made at run time from the scorecard's own "this over" row (HUD/Score/RowBalls), so it looks like
/// the rest of the board, in a band the board grows at its bottom (GrowBoard), under both columns;
/// the HUD prefab is left as it is.
/// </summary>
public class HUDRates : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// The board grows this much at the bottom for the bar (its rows stay where they were), and
    /// the bar sits in that band, in the Score panel's units.
    private const float Grow = 300f, BarY = -1105f, BarWidth = 4400f, BarHeight = 250f, Refresh = 0.2f;

    private TMP_Text text;
    private GameObject bar;
    private float nextAt;

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
        if (scene.name != MatchScene)
            return;
        HUD hud = FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
        if (hud != null && hud.GetComponent<HUDRates>() == null)
            hud.gameObject.AddComponent<HUDRates>();
    }

    private void Start()
    {
        Transform score = transform.Find("Score");
        Transform row = score != null ? score.Find("RowBalls") : null;
        TMP_Text style = row != null ? row.GetComponentInChildren<TMP_Text>(true) : null;
        if (style == null)
        {
            enabled = false;
            return;
        }
        GrowBoard((RectTransform)transform, (RectTransform)score);
        Backing(score);
        bar = Instantiate(row.gameObject, score);
        bar.name = "RowRates";
        for (int i = bar.transform.childCount - 1; i >= 0; i--)
            Destroy(bar.transform.GetChild(i).gameObject);
        var rect = (RectTransform)bar.transform;
        rect.anchoredPosition = new Vector2(0f, BarY);
        rect.sizeDelta = new Vector2(BarWidth, BarHeight);
        text = Instantiate(style, bar.transform);
        text.name = "txtRates";
        var t = text.rectTransform;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = new Vector2(80f, 0f);
        t.offsetMax = new Vector2(-80f, 0f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = style.fontSize * 1.1f;
        text.enableAutoSizing = true;
        text.fontSizeMin = style.fontSize * 0.6f;
        text.fontSizeMax = style.fontSize * 1.1f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        bar.SetActive(false);
    }

    /// The board's backing (the HUD canvas's own image) Grow taller at the bottom: its top edge and
    /// every row stay put in the world.
    private static void GrowBoard(RectTransform board, RectTransform rows)
    {
        // Metres per unit up the board, measured (its parent screen is rotated and scaled unevenly).
        var corners = new Vector3[4];
        board.GetWorldCorners(corners);
        float unit = (corners[1] - corners[0]).magnitude / Mathf.Max(1f, board.sizeDelta.y);
        board.sizeDelta += new Vector2(0f, Grow);
        board.position -= board.up * (Grow * 0.5f * unit);
        rows.anchoredPosition += new Vector2(0f, Grow * 0.5f);
        // The board's bottom edge sits on the stand: the new band would be behind its crowd. The
        // whole display goes up by the band instead, so it ends where the board used to.
        if (board.parent != null)
            board.parent.position += Vector3.up * (Grow * unit);
    }

    /// The board's screen is a fixed plane in the stadium (Scorecard Display, flat grey): a band of
    /// its colour behind the bar carries it down, under both columns.
    private static void Backing(Transform score)
    {
        var go = new GameObject("RatesBacking", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(score, false);
        go.transform.SetAsFirstSibling();
        var r = (RectTransform)go.transform;
        r.anchoredPosition = new Vector2(0f, BarY - 10f);
        r.sizeDelta = new Vector2(BoardWidth, Grow + 20f);
        Image img = go.GetComponent<Image>();
        img.color = ScreenGrey;
        img.raycastTarget = false;
    }

    private const float BoardWidth = 4500f;
    private static readonly Color ScreenGrey = new Color(0.094f, 0.094f, 0.094f, 1f);

    private void LateUpdate()
    {
        if (text == null || Time.time < nextAt)
            return;
        nextAt = Time.time + Refresh;
        HUD hud = HUD.Instance;
        string line = hud != null ? Line(hud.Runs, hud.Overs * 6 + hud.Balls, hud.TotalOvers, MatchFlow.Target) : null;
        bar.SetActive(!string.IsNullOrEmpty(line));
        if (!string.IsNullOrEmpty(line))
            text.text = line;
    }

    /// The bar's line, or null for none: a chase's required and current rates and the equation;
    /// setting a total, the current rate and the total at it, two quicker and two slower. Pure.
    public static string Line(int runs, int balls, int totalOvers, int target)
    {
        if (totalOvers <= 0 || FormatRules.IsTest(totalOvers))
            return null;
        int total = totalOvers * 6, left = Mathf.Max(0, total - balls);
        float crr = balls > 0 ? runs * 6f / balls : 0f;
        const string Grey = "<color=#98A2B3>", End = "</color>";
        if (target > 0)
        {
            int need = Mathf.Max(0, target - runs);
            string rrr = left > 0 ? (need * 6f / left).ToString("0.00") : "-";
            return $"{Grey}RRR{End} {rrr}   {Grey}CRR{End} {crr:0.00}   {Grey}·{End}   {need} needed off {left} ball{(left == 1 ? "" : "s")}";
        }
        if (balls < 6)
            return $"{Grey}CRR{End} {crr:0.00}   {Grey}·   projected totals after the first over{End}";
        float overs = left / 6f;
        string At(float rate) => $"{Grey}at {Mathf.Max(0f, rate):0.0}:{End} {Mathf.RoundToInt(runs + Mathf.Max(0f, rate) * overs)}";
        return $"{Grey}CRR{End} {crr:0.00}   {Grey}·{End}   {At(crr)}   {At(crr + 2f)}   {At(crr - 2f)}";
    }
}
