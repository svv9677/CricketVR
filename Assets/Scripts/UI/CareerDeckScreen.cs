/// <summary>
/// A career screen laid out as separate floating panels (2026-11-06: "too cluttered... separate
/// screens and simpler info on each screen... multiple floating panels with different infos"):
/// the screen's main panel in the middle, one or two side panels angled toward the player, and a
/// tab bar across the top - Home, Training, Season, Ratings, Inbox, Goals, Locker - that is the same
/// on every career screen. Built by CareerDeckBuilder.
///
/// The panels are the screen's own canvases beside MenuRoot's card (OwnsItsCanvas, as Settings
/// does), so MenuRoot hides its card while one is up; they are registered with the laser here, as
/// they are not under the card MenuRoot registers. They move with the menu (grip), being children
/// of MenuRoot.
///
/// The tabs keep the stack shallow: Home is the hub at the bottom of the career stack, every other
/// tab is pushed straight onto it (whatever was over the hub is popped first), so Back from any tab
/// is the hub, and Back from the hub the career slots.
/// </summary>
public abstract class CareerDeckScreen : MenuScreen
{
    public const string Hub = "careerHub", Training = "careerDrills", SeasonTab = "careerSeason", RatingsTab = "careerRatings",
                        InboxTab = "careerInbox", GoalsTab = "careerGoals", LockerTab = "careerLocker";

    public override bool OwnsItsCanvas => true;

    private bool registered;

    /// Subclasses that override OnShow call this first.
    public override void OnShow()
    {
        // MenuRoot puts the backdrop away for a screen with its own canvas (Settings sits in the
        // match); in Splash the career panels float in front of it as the card did.
        if (Root != null && gameObject.scene.name == NetsSession.SplashScene &&
            Root.GetComponentInChildren<MenuBackdrop>(true) is MenuBackdrop backdrop)
            backdrop.Show();
        if (registered || OpenXRMenuInputModule.Instance == null)
            return;
        OpenXRMenuInputModule.RegisterPanel(gameObject);
        registered = true;
    }

    // ---- The tab bar (wired in the prefab) ------------------------------------------------------

    public void OnTabHome() => GoTo(Hub);
    public void OnTabTraining() => GoTo(Training);
    public void OnTabSeason() => GoTo(SeasonTab);
    public void OnTabRatings() => GoTo(RatingsTab);
    public void OnTabGoals() => GoTo(GoalsTab);
    public void OnTabLocker() => GoTo(LockerTab);

    public void OnTabInbox()
    {
        InboxScreen.StartAtNewest();
        GoTo(InboxTab);
    }

    /// The tab's screen: back down to the hub (however deep - Records over Ratings, a message over
    /// the inbox), then pushed over it; Home stops at the hub. Nothing for the tab already showing.
    protected void GoTo(string screen)
    {
        if (Root == null || screen == ScreenName)
            return;
        // At most a few screens deep; bounded so a stack without the hub can never loop.
        for (int i = 0; i < 8 && Showing(Root) != Hub && Showing(Root) != null; i++)
            Root.Back();
        if (screen != Hub && Showing(Root) == Hub)
            Root.Push(screen);
    }

    /// The screen MenuRoot is showing: the one active screen under it.
    private static string Showing(MenuRoot root)
    {
        foreach (MenuScreen s in root.GetComponentsInChildren<MenuScreen>(false))
            if (s.isActiveAndEnabled)
                return s.ScreenName;
        return null;
    }

    /// Which tab a screen lights up on the bar (the hub's Home; a screen that is not a tab, none).
    public static string TabOf(string screen) =>
        screen == Hub || screen == Training || screen == SeasonTab || screen == RatingsTab || screen == InboxTab ||
        screen == GoalsTab || screen == LockerTab ? screen : null;
}
