using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The break between overs. When an over is complete and the game is ready for the next ball, a
/// card comes up in front of the player, level with their eyes and a little way down the pitch
/// toward the bowler, announcing who bowls next (name, type, arm, side) with one button, Next.
/// Only after Next - the laser, or A - does the usual next-ball card (NextBallMenu) come up, and
/// no ball can be started until then (Main.StartNextBall).
///
/// Built from the next-ball card's own panel, so it looks the same and its buttons work with the
/// laser pointer the same way; built when the match scene loads, nothing in the scene changes.
/// </summary>
public class OverBreak : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// Metres ahead of the player's fixed eye point, toward the bowler's end, and below eye level.
    private const float Ahead = 1.5f, Below = 0.1f;

    public static bool Showing { get; private set; }
    private static int closedFrame = -1;
    /// True while the card is up and for the frame it was dismissed in, so the A that closes it
    /// cannot also start the next ball.
    public static bool Blocking => Showing || Time.frameCount <= closedFrame;

    private GameObject card;
    private TMP_Text heading, body;
    private Vector3 baseScale;
    /// An announcement, bigger than the next-ball card it is copied from.
    private const float Bigger = 1.3f;
    private int shownForOver = -1;
    private bool subscribed;

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
        Showing = false;
        if (scene.name == MatchScene && FindFirstObjectByType<OverBreak>() == null)
            SceneManager.MoveGameObjectToScene(new GameObject("Over Break", typeof(OverBreak)), scene);
    }

    private void Start()
    {
        if (Main.Instance != null)
        {
            Main.Instance.onGameStateChanged += OnGameState;
            subscribed = true;
        }
    }

    private void OnDestroy()
    {
        Showing = false;
        if (subscribed && Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
    }

    private void OnGameState()
    {
        Main main = Main.Instance;
        if (main.gameState == eGameState.InGame_SelectDelivery && Showing)
        {
            Hide();     // a ball was started some other way
            return;
        }
        HUD hud = main.theHUD;
        if (main.gameState != eGameState.InGame_Ready || hud == null || hud.CurrentBowler == null)
            return;
        // A new over: the last ball of one is in the book, none of the next bowled.
        bool overDone = hud.Balls == 0 && hud.Overs > 0 && hud.Overs < hud.TotalOvers;
        if (overDone && hud.Overs != shownForOver)
        {
            shownForOver = hud.Overs;
            Show(hud);
        }
    }

    private void Update()
    {
        if (Showing && Main.Instance != null && Main.Instance.GetButton(XRButton.A))
            Close();
    }

    private void Show(HUD hud)
    {
        if (!BuildCard())
            return;
        string type = UIFormat.BowlerType(hud.CurrentBowler.Type);
        // Every bowling action in the game is right-arm, over the wicket.
        body.text = $"<b>{hud.CurrentBowler.Name}</b>  <color=#98A2B3>{type}</color>\n" +
                    "<size=70%><color=#98A2B3>Right-arm, over the wicket</color></size>\n\n" +
                    $"<size=60%><color=#98A2B3>Over {hud.Overs} complete   {hud.Runs}/{hud.Wickets}</color></size>";
        Place();
        card.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
        Showing = true;
    }

    private void Close()
    {
        closedFrame = Time.frameCount;
        Hide();
    }

    private void Hide()
    {
        Showing = false;
        if (card != null)
            card.SetActive(false);
    }

    /// Level with the player's eyes, a little toward the bowler, facing them.
    private void Place()
    {
        Vector3 eye, forward;
        if (!MenuLayout.TryGetEye(out eye, out forward))
        {
            Transform head = Camera.main.transform;
            eye = head.position;
            forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.left;
        }
        // Canvas text faces -Z: point +Z away from the player.
        card.transform.SetPositionAndRotation(eye + forward * Ahead + Vector3.down * Below, Quaternion.LookRotation(forward, Vector3.up));
        card.transform.localScale = baseScale * Bigger;
    }

    /// A copy of the next-ball card's panel with only the announcement and one button left.
    private bool BuildCard()
    {
        if (card != null)
            return true;
        NextBallMenu menu = FindFirstObjectByType<NextBallMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.Panel == null)
            return false;
        card = Instantiate(menu.Panel, transform);
        card.name = "Next Over Card";
        baseScale = card.transform.localScale;
        card.SetActive(false);
        Transform replay = card.transform.Find("Replay");
        if (replay != null)
            Destroy(replay.gameObject);
        Transform play = card.transform.Find("Play");
        if (play == null)
            return false;
        Transform labelRow = null;
        for (int i = play.childCount - 1; i >= 0; i--)
        {
            Transform child = play.GetChild(i);
            if (child.name.StartsWith("Bowl"))
                WireNextButton(child);
            else if (child.GetComponent<TMP_Text>() != null)
                heading = child.GetComponent<TMP_Text>();
            else if (labelRow == null && child.GetComponentInChildren<TMP_Text>(true) != null && child.GetComponentInChildren<Button>(true) != null &&
                     child.GetComponentInChildren<Button>(true).name == "Change")
                labelRow = child;
            else
                Destroy(child.gameObject);      // Settings / Bat grip
        }
        if (labelRow != null)
        {
            // The bowler's name and type: its own text, the Change button gone.
            foreach (Button b in labelRow.GetComponentsInChildren<Button>(true))
                Destroy(b.gameObject);
            body = labelRow.GetComponentInChildren<TMP_Text>(true);
            body.enableWordWrapping = false;
            body.alignment = TextAlignmentOptions.TopLeft;
            body.fontSize = 36f;
            var layout = body.GetComponent<LayoutElement>();
            if (layout != null)
                layout.preferredHeight = 150f;
        }
        if (heading != null)
            heading.text = "<color=#3B82F6>NEXT OVER</color>";
        OpenXRMenuInputModule.RegisterPanel(card);
        return body != null;
    }

    private void WireNextButton(Transform bowl)
    {
        Button button = bowl.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();   // not the next-ball card's calls
            button.onClick.AddListener(Close);
        }
        TMP_Text label = bowl.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "Next  <size=75%>(A)</size>";
    }
}
