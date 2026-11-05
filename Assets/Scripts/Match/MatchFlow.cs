using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The match as a match: it starts with the overs and the target (a chase) from Match Setup, and
/// ends - when the overs are bowled, the batting side is all out, or the target is reached - with
/// a result card (the score, the result, the boundaries) and then home to the Splash menu, where
/// the full result is shown (MatchResultScreen). No more balls can be started once it is over
/// (Main.StartNextBall, NextBallMenu).
///
/// The other side is the AI (AIInnings): in a chase it has batted first and set the target; setting
/// a total, it chases what the player made once the innings is over, and the result is decided by
/// that chase (shown over by over on the result screen at home).
///
/// HUD shows the state of the match beside the run rate (Status): "Need 23 off 14 · RRR 9.9" in a
/// chase, "Over 3 of 5 · AI chases" otherwise.
///
/// Built when the match scene loads, like OverBreak; its card is a copy of the next-ball card's
/// panel so it looks and works the same (laser or A).
/// </summary>
public class MatchFlow : MonoBehaviour
{
    private const string MatchScene = "CricketVR", HomeScene = "Splash";
    private const float Ahead = 1.5f, Below = 0.1f, Bigger = 1.25f;
    /// Seconds the result card stays up before going home on its own.
    private const float HomeAfter = 12f;

    public static bool Over { get; private set; }
    /// The target in a chase; 0 when setting a total.
    public static int Target { get; private set; }
    public static string Status { get; private set; }
    /// The AI's first innings, in a chase (null when the player bats first).
    public static AIInnings.Result AIFirst { get; private set; }
    private System.Random rng;

    private GameObject card;
    private TMP_Text heading, body;
    private Vector3 baseScale;
    private float overAt;
    private bool subscribed, leaving;

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
        Over = false;
        Status = null;
        Target = 0;
        AIFirst = null;
        if (scene.name == MatchScene && FindFirstObjectByType<MatchFlow>() == null)
            SceneManager.MoveGameObjectToScene(new GameObject("Match Flow", typeof(MatchFlow)), scene);
    }

    private void Start()
    {
        rng = new System.Random(System.Environment.TickCount);
        if (MatchSetup.Type == eMatchType.Chase)
        {
            AIFirst = AIInnings.Play(GameSettings.Overs, 0, MatchSetup.Bowling, rng);
            Target = AIFirst.runs + 1;
        }
        else
            Target = 0;
        if (Main.Instance != null)
        {
            Main.Instance.onGameStateChanged += OnGameState;
            subscribed = true;
        }
        UpdateStatus();
    }

    private void OnDestroy()
    {
        if (subscribed && Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
    }

    private void OnGameState()
    {
        if (Over)
            return;
        eGameState s = Main.Instance.gameState;
        if (s == eGameState.InGame_ResetToReady || s == eGameState.InGame_Ready)
        {
            UpdateStatus();
            HUD hud = HUD.Instance;
            if (hud != null && Finished(hud, out string result))
                End(hud, result);
        }
    }

    /// The match is over, and how it ended.
    private bool Finished(HUD hud, out string result)
    {
        int balls = hud.Overs * 6 + hud.Balls, total = hud.TotalOvers * 6;
        bool chase = Target > 0;
        if (chase && hud.Runs >= Target)
        {
            int left = 10 - hud.Wickets;
            result = $"Won by {left} wicket{(left == 1 ? "" : "s")} ({total - balls} ball{(total - balls == 1 ? "" : "s")} left)";
            return true;
        }
        bool allOut = hud.Wickets >= 10, oversDone = total > 0 && balls >= total;
        if (!allOut && !oversDone)
        {
            result = null;
            return false;
        }
        if (chase)
        {
            int short_ = Target - 1 - hud.Runs;
            result = short_ == 0 ? "Match tied" : $"Lost by {short_} run{(short_ == 1 ? "" : "s")}";
        }
        else
            result = allOut ? "All out" : "Innings complete";
        return true;
    }

    private void UpdateStatus()
    {
        HUD hud = HUD.Instance;
        if (hud == null)
            return;
        int balls = hud.Overs * 6 + hud.Balls, total = hud.TotalOvers * 6;
        if (Target > 0)
        {
            int need = Mathf.Max(0, Target - hud.Runs), left = Mathf.Max(0, total - balls);
            Status = $"Need {need} off {left}" + (left > 0 ? $" · RRR {need * 6f / left:0.0}" : "");
        }
        else
            Status = total > 0 ? $"Over {Mathf.Min(hud.Overs + 1, hud.TotalOvers)} of {hud.TotalOvers} · AI chases" : null;
        hud.UpdateUI();
    }

    private void End(HUD hud, string result)
    {
        Over = true;
        overAt = Time.time;
        AIInnings.Result aiChase = null;
        if (Target == 0)
        {
            // Batted first: now the AI chases it.
            aiChase = AIInnings.Play(hud.TotalOvers, hud.Runs + 1, MatchSetup.Bowling, rng);
            result = AIInnings.ChaseResult(aiChase, out _, out _);
        }
        MatchHistory.Record r = Record(hud, result);
        AddAI(r, aiChase ?? AIFirst, aiChase != null);
        MatchHistory.Add(r);
        Status = result;
        hud.UpdateUI();
        Debug.Log($"[MatchFlow] {r.Score} in {r.OversText} overs: {result}");
        if (!BuildCard())
            return;
        if (aiChase != null)
        {
            // The chase is shown at home, over by over: here, only what the AI needs.
            heading.text = "<color=#3B82F6>INNINGS OVER</color>";
            body.text = $"<b><size=150%>{r.Score}</size></b>  <color=#98A2B3>({r.OversText} overs)</color>\n" +
                        $"The AI needs {r.runs + 1} to win\n" +
                        $"<size=70%><color=#98A2B3>Fours {r.fours}   Sixes {r.sixes}   Run rate {r.RunRate:0.00}   · watch the chase at home</color></size>";
        }
        else
        {
            heading.text = r.Won ? "<color=#22C55E>YOU WON</color>"
                         : !r.tied ? "<color=#FF6B4A>MATCH LOST</color>"
                         : "<color=#3B82F6>MATCH TIED</color>";
            body.text = $"<b><size=150%>{r.Score}</size></b>  <color=#98A2B3>({r.OversText} overs)</color>\n" +
                        $"{result}\n" +
                        $"<size=70%><color=#98A2B3>Target {Target}   Fours {r.fours}   Sixes {r.sixes}   Run rate {r.RunRate:0.00}</color></size>";
        }
        Place();
        card.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
    }

    /// The AI's innings onto the record, and who won.
    private static void AddAI(MatchHistory.Record r, AIInnings.Result ai, bool chased)
    {
        if (ai == null)
            return;
        r.hasAI = true;
        r.aiChased = chased;
        r.aiRuns = ai.runs; r.aiWickets = ai.wickets; r.aiBalls = ai.balls; r.aiTarget = ai.target;
        r.aiOverRuns.AddRange(ai.overRuns);
        r.aiOverWickets.AddRange(ai.overWickets);
        if (chased)
        {
            AIInnings.ChaseResult(ai, out r.won, out r.tied);
        }
        else
        {
            r.won = r.runs >= r.target;
            r.tied = r.runs == r.target - 1 && r.result == "Match tied";
        }
    }

    private static MatchHistory.Record Record(HUD hud, string result)
    {
        var r = new MatchHistory.Record
        {
            runs = hud.Runs, wickets = hud.Wickets, balls = hud.Overs * 6 + hud.Balls, overs = hud.TotalOvers,
            target = Target, bowling = (int)MatchSetup.Bowling, pitch = (int)MatchSetup.Pitch,
            result = result, when = System.DateTime.Now.ToString("d MMM, HH:mm"),
        };
        if (hud.BowledBalls != null)
            foreach (string b in hud.BowledBalls)
            {
                if (b == "4") r.fours++;
                else if (b == "6") r.sixes++;
                else if (b == "*") r.dots++;
                else if (b.EndsWith("wd")) r.wides++;
            }
        return r;
    }

    private void Update()
    {
        if (!Over || leaving)
            return;
        bool pressed = Main.Instance != null && Main.Instance.GetButton(XRButton.A) && Time.time - overAt > 1f;
        if (pressed || Time.time - overAt > HomeAfter)
            GoHome();
    }

    private void GoHome()
    {
        leaving = true;
        if (card != null)
            card.SetActive(false);
        SceneFader.Load(HomeScene);
    }

    private void Place()
    {
        if (!MenuLayout.TryGetEye(out Vector3 eye, out Vector3 forward))
        {
            Transform head = Camera.main.transform;
            eye = head.position;
            forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.left;
        }
        card.transform.SetPositionAndRotation(eye + forward * Ahead + Vector3.down * Below, Quaternion.LookRotation(forward, Vector3.up));
        card.transform.localScale = baseScale * Bigger;
    }

    /// A copy of the next-ball card's panel with the result and one button, Home.
    private bool BuildCard()
    {
        if (card != null)
            return true;
        NextBallMenu menu = FindFirstObjectByType<NextBallMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.Panel == null)
            return false;
        card = Instantiate(menu.Panel, transform);
        card.name = "Match Result Card";
        baseScale = card.transform.localScale;
        card.SetActive(false);
        Transform replay = card.transform.Find("Replay");
        if (replay != null)
            Destroy(replay.gameObject);
        Transform play = card.transform.Find("Play");
        if (play == null)
            return false;
        // The play card: a name line, a line under it, Bowl, and a row of small buttons (NextBallMenu,
        // UIPrefabBuilder.PlayCard). The name line is the heading, the line under it the body, Bowl
        // the Home button; the row goes.
        var doomed = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in play)
        {
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (child.name.StartsWith("Bowl"))
                WireHomeButton(child);
            else if (text != null && heading == null)
                heading = text;
            else if (text != null && body == null)
                body = text;
            else
                doomed.Add(child.gameObject);
        }
        foreach (GameObject g in doomed)
            Destroy(g);
        if (body != null)
        {
            body.textWrappingMode = TextWrappingModes.NoWrap;
            body.enableAutoSizing = false;
            body.overflowMode = TextOverflowModes.Overflow;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.fontSize = 36f;
            var layout = body.GetComponent<LayoutElement>();
            if (layout != null)
                layout.preferredHeight = 190f;
        }
        OpenXRMenuInputModule.RegisterPanel(card);
        return body != null && heading != null;
    }

    private void WireHomeButton(Transform bowl)
    {
        Button button = bowl.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(GoHome);
        }
        TMP_Text label = bowl.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "Home  <size=75%>(A)</size>";
    }
}
