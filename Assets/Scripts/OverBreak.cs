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
    // The next-ball card's layout was not one BuildCard knows: no next-over card this scene.
    private bool unbuildable;
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
        HUD.SimulatedBallScored += OnSimulatedBall;
    }

    private void OnDestroy()
    {
        Showing = false;
        if (subscribed && Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
        HUD.SimulatedBallScored -= OnSimulatedBall;
    }

    /// A career match's simulated ball that finished an over: its catch-up card announces that
    /// over, so this card must not come up for it later (a wide or dead first ball of the next over
    /// leaves Balls at 0 at the next Ready).
    private void OnSimulatedBall()
    {
        HUD hud = HUD.Instance;
        if (hud != null && hud.Balls == 0 && hud.Overs > 0)
            shownForOver = hud.Overs;
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
        string type = BowlerRepertoire.KindName(hud.CurrentBowler.Kind);
        // Every bowling action in the game is right-arm, over the wicket.
        body.text = $"<b>{hud.CurrentBowler.Name}</b>\n<size=70%><color=#98A2B3>{type}</color></size>\n" +
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
        if (unbuildable)
            return false;
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
        // The bowler's line: the copy of the card's own (it sits on its own line in the Play card,
        // with Change bowler, Settings and Bat grip in a row below it).
        Transform label = menu.BowlerLabel != null ? Twin(menu.BowlerLabel.transform, menu.Panel.transform, card.transform) : null;
        if (play == null || label == null)
        {
            // Not a card we know how to dress: never show it half-built (or try again each over).
            Destroy(card);
            card = null;
            unbuildable = true;
            return false;
        }
        for (int i = play.childCount - 1; i >= 0; i--)
        {
            Transform child = play.GetChild(i);
            if (child == label)
                continue;
            if (child.name.StartsWith("Bowl"))
                WireNextButton(child);
            else if (child.GetComponent<TMP_Text>() != null)
                heading = child.GetComponent<TMP_Text>();
            else
                Destroy(child.gameObject);      // Change bowler / Settings / Bat grip
        }
        body = label.GetComponent<TMP_Text>();
        // Its own lines (the card's is one line, cut off with an ellipsis): a long name wraps
        // rather than taking the rest of the card with it.
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSize = 36f;
        var layout = body.GetComponent<LayoutElement>();
        if (layout != null)
            layout.preferredHeight = 175f;
        if (heading != null)
            heading.text = "<color=#3B82F6>NEXT OVER</color>";
        OpenXRMenuInputModule.RegisterPanel(card);
        return true;
    }

    /// The transform in `copy` at the same child-index path that `t` has under `original`.
    private static Transform Twin(Transform t, Transform original, Transform copy)
    {
        var path = new System.Collections.Generic.List<int>();
        for (Transform x = t; x != original; x = x.parent)
        {
            if (x == null)
                return null;
            path.Add(x.GetSiblingIndex());
        }
        Transform at = copy;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            if (path[i] >= at.childCount)
                return null;
            at = at.GetChild(path[i]);
        }
        return at;
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
