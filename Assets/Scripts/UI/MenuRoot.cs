using UnityEngine;

/// <summary>
/// The one world-space menu host, used in Splash, CricketVR and Nets. It owns everything the
/// individual panels used to each duplicate: placing itself in front of the player, registering
/// with OpenXRMenuInputModule so the laser (and, in the editor, the mouse) works, restoring a
/// panel the player has moved by hand, and switching between screens.
///
/// Splash uses it with MainMenuScreen as the root screen and the backdrop shown; the match
/// scenes use the same prefab with PauseScreen as root, closed until B is pressed.
///
/// Placement mirrors SettingsPanel and NextBallMenu: a fixed distance ahead of where the player
/// is looking, slightly below eye level, tilted up. The canvas faces -Z, so +Z points away.
/// </summary>
public class MenuRoot : MonoBehaviour
{
    private const float Distance = 1.45f;
    private const float BelowEyes = 0.1f;

    [Tooltip("The child that shows and hides. This component's GameObject stays active.")]
    [SerializeField] private GameObject panel;

    [Tooltip("Screen shown when the menu opens. \"main\" in Splash, \"pause\" in a match.")]
    [SerializeField] private string rootScreen = "main";

    [Tooltip("Every screen this root can show. Order does not matter.")]
    [SerializeField] private MenuScreen[] screens;

    [Tooltip("Optional. Shown only where a backdrop makes sense - Splash.")]
    [SerializeField] private MenuBackdrop backdrop;

    [Tooltip("Off in CricketVR and Nets: the real ground is the backdrop there.")]
    [SerializeField] private bool showBackdrop = true;

    private readonly MenuStack stack = new MenuStack();
    private bool registered;

    public static MenuRoot Instance { get; private set; }

    public bool IsOpen => stack.IsOpen;

    private void Awake()
    {
        Instance = this;
        if (screens != null)
            foreach (MenuScreen screen in screens)
            {
                if (screen == null)
                    continue;
                screen.Root = this;
                screen.gameObject.SetActive(false);
            }
        if (panel != null)
            panel.SetActive(false);
        if (backdrop != null)
            backdrop.Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---- Opening and closing --------------------------------------------------------------------

    /// Open at the root screen, placed in front of the player.
    public void Open()
    {
        if (!registered && OpenXRMenuInputModule.Instance != null && panel != null)
        {
            OpenXRMenuInputModule.RegisterPanel(panel);
            registered = true;
        }
        Place();
        stack.Reset(rootScreen);
        if (panel != null)
            panel.SetActive(true);
        if (backdrop != null && showBackdrop)
            backdrop.Show();
        Apply();
    }

    public void Close()
    {
        HideCurrent();
        stack.Clear();
        ApplyVisibility(null);
        if (panel != null)
            panel.SetActive(false);
        if (backdrop != null)
            backdrop.Hide();
    }

    public void Push(string screen)
    {
        HideCurrent();
        stack.Push(screen);
        Apply();
    }

    /// Back. Popping the root screen closes the menu rather than leaving a blank card.
    public void Back()
    {
        HideCurrent();
        stack.Pop();
        if (!stack.IsOpen)
        {
            Close();
            return;
        }
        if (panel != null)
            panel.SetActive(true);
        if (backdrop != null && showBackdrop)
            backdrop.Show();
        Apply();
    }

    // ---- Wired in the prefab ----------------------------------------------------------------------

    public void OnOpenSettings() => Push("settings");

    public void OnClose() => Close();

    // ---- Internals ----------------------------------------------------------------------------------

    /// Switch on whichever screen the stack says is current, and tell it to refresh.
    private void Apply()
    {
        string current = stack.Current;
        ApplyVisibility(current);
        MenuScreen screen = Find(current);
        if (screen == null)
            return;
        // A screen with its own canvas (the settings panel) replaces this card rather than
        // sitting inside it, so hide ours while it is up.
        if (panel != null)
            panel.SetActive(!screen.OwnsItsCanvas);
        if (backdrop != null && screen.OwnsItsCanvas)
            backdrop.Hide();
        screen.OnShow();
    }

    private void ApplyVisibility(string current)
    {
        if (screens == null)
            return;
        MenuScreen wanted = Find(current);
        foreach (MenuScreen screen in screens)
        {
            if (screen == null)
                continue;
            // Compare by reference, not by name: if the same screen name is registered twice,
            // only the first copy is ever shown rather than two panels overlapping.
            bool on = screen == wanted;
            if (screen.gameObject.activeSelf != on)
                screen.gameObject.SetActive(on);
        }
    }

    /// The first screen registered under `name`, or null.
    private MenuScreen Find(string name)
    {
        if (name == null || screens == null)
            return null;
        foreach (MenuScreen screen in screens)
            if (screen != null && screen.ScreenName == name)
                return screen;
        return null;
    }

    private void HideCurrent()
    {
        MenuScreen screen = Find(stack.Current);
        if (screen != null)
            screen.OnHide();
    }

    /// Pose the whole menu - card and backdrop together - in front of the player.
    ///
    /// This moves the root, not just the card: the backdrop is a second canvas alongside the
    /// card, and moving only the card left the backdrop behind at the origin.
    private void Place()
    {
        Camera head = Camera.main;
        if (head == null)
            return;
        Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.left;
        forward.Normalize();
        transform.SetPositionAndRotation(
            head.transform.position + forward * Distance + Vector3.down * BelowEyes,
            Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(8f, 0f, 0f));
        // Moved by hand before (grip): open where it was left instead.
        if (TryGetComponent(out GrabbablePanel grab))
            grab.Restore();
    }
}
