using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The broadcast graphics over the replay: a "REPLAY" tag at the top left, and the score bar along
/// the bottom - the batting side's score and overs, both batters' runs (balls), the bowler and his
/// figures, and the delivery's speed. A screen-space canvas on the replay camera, so it is drawn
/// into the replay's picture and nowhere else; its text changes once per replay, not per frame.
/// </summary>
public sealed class ReplayOverlay
{
    private const string Team = "INDIA";
    private static readonly Color Bar = new Color(0.04f, 0.06f, 0.12f, 0.88f);
    private static readonly Color Accent = new Color(0.86f, 0.12f, 0.2f, 0.95f);
    private static readonly Color Dim = new Color(0.7f, 0.74f, 0.82f);

    private readonly Canvas canvas;
    private readonly TMP_Text score, batters, bowler;

    public ReplayOverlay(Camera camera)
    {
        var root = new GameObject("Replay Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = camera.nearClipPlane + 0.05f;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        // The score bar: three blocks across the bottom.
        RectTransform bar = Box(root.transform, "Score Bar", Bar, new Vector2(0f, 0f), new Vector2(1f, 0f), 0f, 64f);
        RectTransform tag = Box(bar, "Team", Accent, new Vector2(0f, 0f), new Vector2(0.3f, 1f), 0f, 0f);
        score = Text(tag, "Score", 26f, TextAlignmentOptions.Center, Color.white);
        batters = Text(Region(bar, 0.31f, 0.66f), "Batters", 17f, TextAlignmentOptions.Left, Color.white);
        bowler = Text(Region(bar, 0.67f, 0.99f), "Bowler", 17f, TextAlignmentOptions.Left, Color.white);

        RectTransform replay = Box(root.transform, "Replay Tag", Accent, new Vector2(0f, 1f), new Vector2(0f, 1f), 0f, 0f);
        replay.sizeDelta = new Vector2(120f, 34f);
        replay.anchoredPosition = new Vector2(24f + 60f, -24f - 17f);
        Text(replay, "Replay", 20f, TextAlignmentOptions.Center, Color.white).text = "<b>REPLAY</b>";
        canvas.enabled = false;
    }

    /// The scorecard as it stands now, and this delivery's speed (km/h, 0 if unknown).
    public void Show(float speedKmh)
    {
        HUD hud = HUD.Instance;
        if (hud != null && hud.CurrentBatsman != null)
        {
            score.text = $"<size=60%>{Team}</size>  <b>{hud.Runs}/{hud.Wickets}</b>\n<size=55%>{hud.Overs}.{hud.Balls} OVERS   RR {hud.RunRate:F2}</size>";
            batters.text = $"<b>{hud.CurrentBatsman.Name}*</b><pos=62%>{HUD.RunsAndBalls(hud.CurrentBatsman)}\n" +
                           $"<color=#B3BDD1>{hud.CurrentRunner.Name}<pos=62%>{HUD.RunsAndBalls(hud.CurrentRunner)}</color>";
            string speed = speedKmh > 1f ? $"{Mathf.RoundToInt(speedKmh)} km/h" : "";
            bowler.text = $"<b>{hud.CurrentBowler.Name}</b><pos=62%>{HUD.Figures(hud.CurrentBowler)}\n<color=#B3BDD1>{speed}</color>";
        }
        canvas.enabled = true;
    }

    public void Hide() => canvas.enabled = false;

    private static RectTransform Box(Transform parent, string name, Color color, Vector2 min, Vector2 max, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, y);
        rt.offsetMax = new Vector2(0f, y + height);
        if (height <= 0f)
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static RectTransform Region(RectTransform parent, float from, float to)
    {
        var rt = (RectTransform)new GameObject("Region", typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 4f);
        rt.offsetMax = new Vector2(-10f, -4f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Truncate;   // the font has no ellipsis glyph
        text.raycastTarget = false;
        return text;
    }
}
