using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small panel that comes up in front of the player between balls: Next Ball, Change Bowler,
/// Settings, Calibrate Grip. It appears where the player is looking whenever the game is ready
/// for the next delivery and goes away the moment the ball is on its way. The laser pointer
/// clicks it (OpenXRMenuInputModule.RegisterPanel); A still bowls the next ball as before.
///
/// Built in code, so it needs no scene wiring - Main adds it at start.
/// </summary>
public class NextBallMenu : MonoBehaviour
{
    private const float Distance = 1.0f;
    /// Below eye level, so the pitch stays in view over the top of it.
    private const float BelowEyes = 0.35f;
    private const float PixelsPerMetre = 1000f;

    private GameObject panel;
    private TextMeshProUGUI bowlerLabel;
    private bool shown;

    private void Start()
    {
        Build();
        panel.SetActive(false);
    }

    private void Update()
    {
        Main inst = Main.Instance;
        bool want = inst != null && inst.gameState == eGameState.InGame_Ready &&
                    !inst.SettingsOpen && !inst.CalibratingGrip;
        if (want && !shown)
            Show();
        else if (!want && shown)
            Hide();
        if (shown && bowlerLabel != null && inst.theHUD != null && inst.theHUD.CurrentBowler != null)
            bowlerLabel.text = $"Change Bowler  <size=70%>({inst.theHUD.CurrentBowler.Name}, {inst.theHUD.CurrentBowler.Type})</size>";
    }

    private void Show()
    {
        if (OpenXRMenuInputModule.Instance != null)
            OpenXRMenuInputModule.RegisterPanel(panel);

        Transform head = Camera.main.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.left;
        forward.Normalize();
        Vector3 position = head.position + forward * Distance + Vector3.down * BelowEyes;
        // Canvas text faces -Z, so point +Z away from the player; tilt it up toward the eyes.
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(20f, 0f, 0f);
        panel.transform.SetPositionAndRotation(position, facing);
        panel.SetActive(true);
        shown = true;
    }

    private void Hide()
    {
        panel.SetActive(false);
        shown = false;
    }

    private void Build()
    {
        panel = new GameObject("NextBallMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        panel.transform.SetParent(transform, false);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        panel.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460f, 330f);
        rect.localScale = Vector3.one / PixelsPerMetre;

        var background = panel.AddComponent<Image>();
        background.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);

        var column = panel.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(20, 20, 20, 20);
        column.spacing = 12f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandHeight = true;

        AddButton("Next Ball  <size=70%>(A)</size>", () => Main.Instance.StartNextBall(), new Color(0.15f, 0.55f, 0.25f));
        bowlerLabel = AddButton("Change Bowler", () => Main.Instance.theHUD.ChangeBowler(), new Color(0.2f, 0.3f, 0.5f));
        AddButton("Settings", () => Main.Instance.OpenSettings(), new Color(0.25f, 0.25f, 0.3f));
        AddButton("Calibrate Grip", () => Main.Instance.onCalibrateGrip(), new Color(0.25f, 0.25f, 0.3f));
    }

    private TextMeshProUGUI AddButton(string label, UnityEngine.Events.UnityAction onClick, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel.transform, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 30f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
