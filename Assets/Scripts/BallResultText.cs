using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What happened on the last ball, over the bowler's stumps where the batter is looking - only when
/// it is worth saying (2026-11-05: "not so cluttered, and AI sloppy, make it professional"):
///   * a boundary, a wicket (with the batter's scorecard line), a wide, a no-ball / free hit:
///     a clean title with a small caption under it, soft shadow, no outline;
///   * 1, 2 or 3 runs: a small grey figure; a dot ball: nothing (the scoreboard has it).
/// It eases in when the ball is scored (HUD.BallScored), holds still - no bobbing, no pulsing -
/// turned to face the player, and fades as the bowler starts his next run-up. While the player
/// watches his partner from the non-striker's end (PlayerEnd) it is over the far stumps from him,
/// the striker's, where he is looking then.
/// </summary>
public class BallResultText : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    private const float Height = 3.4f, PopTime = 0.3f, HideTime = 0.3f;
    /// Runs alone are shown this much smaller than a headline.
    private const float RunsScale = 0.55f;

    private TextMeshPro text;
    private Vector3 home;
    /// Over the bowler's stumps, as the scene placed it.
    private Vector3 creaseHome;
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
        home = creaseHome = transform.position;
        text = gameObject.AddComponent<TextMeshPro>();
        text.fontSize = 14f;
        text.enableAutoSizing = true;       // a long result shrinks to its 12 m line, never past it
        text.fontSizeMax = 14f;
        text.fontSizeMin = 6f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(12f, 3f);
        // A soft shadow under the letters rather than a heavy black outline round them.
        text.outlineWidth = 0f;
        text.fontSharedMaterial = new Material(text.fontSharedMaterial);
        text.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        text.fontSharedMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
        text.fontSharedMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
        text.fontSharedMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
        text.fontSharedMaterial.SetFloat("_UnderlaySoftness", 0.5f);
        text.characterSpacing = 6f;
        text.text = "";
        transform.localScale = Vector3.zero;
    }

    private void OnEnable()
    {
        HUD.BallScored += Show;
        PlayerEnd.Changed += FollowEnd;
        FollowEnd();
    }

    private void OnDisable()
    {
        HUD.BallScored -= Show;
        PlayerEnd.Changed -= FollowEnd;
    }

    /// Over the stumps at the other end from the player.
    private void FollowEnd()
    {
        if (!PlayerEnd.NonStriker)
        {
            home = creaseHome;
            return;
        }
        Vector3 far = EndPose.FarStumps(true);
        home = new Vector3(far.x, Height, far.z);
    }

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

    private float size = 1f;

    private void Show(BallOutcome outcome)
    {
        string caption = null;
        size = 1f;
        switch (outcome.Signal)
        {
            case UmpireSignal.Out:
                caption = HUD.Instance != null ? HUD.Instance.LastDismissal : null;    // "Ishan b Shami  0 (1)"
                break;
            case UmpireSignal.NoBall:
                caption = "Free hit next ball";
                break;
            case UmpireSignal.None:
                // Runs off the bat: a small figure, and nothing at all for a dot.
                if (outcome.Label == "0")
                {
                    // Only a result still up fades; one already gone stays gone. Restarting the fade
                    // on a hidden one brought the last ball's "2" back to fade out again after a dot
                    // (2026-11-06).
                    if (showing)
                    {
                        showing = false;
                        hiddenAt = Time.time;
                    }
                    return;
                }
                size = RunsScale;
                break;
        }
        if (NoBalls.FreeHit && outcome.Signal != UmpireSignal.NoBall && outcome.Signal != UmpireSignal.Wide)
            caption = caption ?? "Free hit";
        text.text = string.IsNullOrEmpty(caption) ? outcome.Label
                  : $"{outcome.Label}\n<size=34%><color=#E5E7EB><cspace=0>{caption}</cspace></color></size>";
        text.color = outcome.Signal == UmpireSignal.None ? new Color(0.85f, 0.88f, 0.92f) : outcome.Color;
        shownAt = Time.time;
        showing = true;
    }

    private void LateUpdate()
    {
        float scale;
        if (showing)
        {
            float t = Time.time - shownAt;
            // Eased in (rising a little into place), then held still.
            float k = Mathf.Clamp01(t / PopTime);
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            scale = size * Mathf.Lerp(0.85f, 1f, e);
            transform.position = home + Vector3.down * (0.15f * (1f - e));
            text.alpha = e;
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
            scale = size * (1f - 0.15f * t);
            text.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
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
