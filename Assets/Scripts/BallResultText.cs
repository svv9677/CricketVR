using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What happened on the last ball - "0", "1", "WIDE", "FOUR", "OUT" - in big letters hovering
/// over the bowler's stumps, where the batter is looking. It pops up when the ball is scored
/// (HUD.BallScored), then floats there bobbing gently and pulsing, turned to face the player,
/// until the bowler starts his next run-up. Built when the match scene loads (no scene change).
/// </summary>
public class BallResultText : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    private const float Height = 3.4f, PopTime = 0.45f, HideTime = 0.3f;
    private const float Bob = 0.12f, BobRate = 0.55f, Pulse = 0.06f, PulseRate = 1.1f;

    private TextMeshPro text;
    private Vector3 home;
    private float shownAt = -100f, hiddenAt = -100f;
    private bool showing;

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
        if (scene.name != MatchScene || FindFirstObjectByType<BallResultText>() != null)
            return;
        var go = new GameObject("Ball Result Text");
        SceneManager.MoveGameObjectToScene(go, scene);
        GameObject end = GameObject.Find("Main/Stumps_BowlingEnd");
        float x = end != null ? end.transform.position.x : -10.4f;
        go.transform.position = new Vector3(x, Height, 0f);
        go.AddComponent<BallResultText>();
    }

    private void Awake()
    {
        home = transform.position;
        text = gameObject.AddComponent<TextMeshPro>();
        text.fontSize = 14f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(12f, 3f);
        text.outlineWidth = 0.22f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        text.text = "";
        transform.localScale = Vector3.zero;
    }

    private void OnEnable() => HUD.BallScored += Show;
    private void OnDisable() => HUD.BallScored -= Show;

    private void Start()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged += OnGameState;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
    }

    private void OnGameState()
    {
        if (showing && Main.Instance.gameState == eGameState.InGame_SelectDeliveryLoop)
        {
            showing = false;
            hiddenAt = Time.time;
        }
    }

    private void Show(BallOutcome outcome)
    {
        text.text = outcome.Label;
        text.color = outcome.Color;
        shownAt = Time.time;
        showing = true;
    }

    private void LateUpdate()
    {
        float scale;
        if (showing)
        {
            float t = Time.time - shownAt;
            scale = t < PopTime ? Overshoot(t / PopTime) : 1f + Pulse * Mathf.Sin((t - PopTime) * Mathf.PI * 2f * PulseRate);
            transform.position = home + Vector3.up * (Bob * Mathf.Sin(t * Mathf.PI * 2f * BobRate));
        }
        else
        {
            float t = (Time.time - hiddenAt) / HideTime;
            if (t >= 1f)
            {
                if (transform.localScale != Vector3.zero)
                    transform.localScale = Vector3.zero;
                return;
            }
            scale = 1f - Mathf.SmoothStep(0f, 1f, t);
        }
        transform.localScale = Vector3.one * scale;
        Face();
    }

    /// Turned about the vertical to face the player (text reads from its -Z side).
    private void Face()
    {
        Camera eye = Camera.main;
        if (eye == null)
            return;
        Vector3 away = transform.position - eye.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    /// 0 to 1 with a springy overshoot (ease-out-back).
    private static float Overshoot(float t)
    {
        const float s = 1.9f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }
}
