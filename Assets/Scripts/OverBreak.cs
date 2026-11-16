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
/// The card is the NextOverCard prefab (CardPrefabBuilder), in the next-ball card's look, its button
/// working with the laser pointer the same way. On CricketVR's Systems > Over Break.
/// </summary>
public class OverBreak : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    private const string CardResource = "Prefabs/UI/NextOverCard";
    /// Metres ahead of the player's fixed eye point, toward the bowler's end, and below eye level.
    private const float Ahead = 1.5f, Below = 0.1f;

    public static bool Showing { get; private set; }
    private static int closedFrame = -1;
    /// True while the card is up and for the frame it was dismissed in, so the A that closes it
    /// cannot also start the next ball.
    public static bool Blocking => Showing || Time.frameCount <= closedFrame;

    private GameObject card;
    private TMP_Text heading, body;
    // The card prefab is missing: no next-over card this scene.
    private bool unbuildable;
    private Vector3 baseScale;
    /// An announcement, bigger than the next-ball card it is copied from.
    private const float Bigger = 1.3f;
    private int shownForOver = -1;
    private bool subscribed;

    // Per-scene static state, reset as each scene loads. The system itself is a component under the
    // scene's Systems object (SceneSystem).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        Showing = false;
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
        // His arm, as the scorecard says it (half the bowlers are left-arm since 2026-11-06).
        bool leftArm = AnimatedBowler.Instance != null && AnimatedBowler.Instance.WillBowlLeftArm(hud.CurrentBowler);
        string type = BowlerArm.KindName(hud.CurrentBowler.Kind, leftArm);
        CardText.Set(body, $"{CardText.Title(hud.CurrentBowler.Name)}\n{type}\n" +
                           CardText.Caption(leftArm ? "Over the wicket" : "Right-arm, over the wicket") + "\n\n" +
                           CardText.Caption($"Over {hud.Overs} complete   {hud.Runs}/{hud.Wickets}"));
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

    /// The next-over card prefab (CardPrefabBuilder), made on the first over break: an announcement
    /// line and one button, Next.
    private bool BuildCard()
    {
        if (card != null)
            return true;
        if (unbuildable)
            return false;
        CardView view = CardView.Create(CardResource, transform);
        if (view == null)
        {
            unbuildable = true;
            return false;
        }
        card = view.gameObject;
        baseScale = card.transform.localScale;
        heading = view.Heading;
        body = view.Body;
        view.Clicked += Close;
        CardText.Set(heading, "Next over");
        OpenXRMenuInputModule.RegisterPanel(card);
        return true;
    }
}
