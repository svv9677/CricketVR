using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// What the player is doing, for the menus: each mode shows only what belongs to it.
public enum ePlayMode { Menu, QuickPlay, CareerMatch, NetsTrial, NetsDrill, NetsPractice, FreeNets }

/// <summary>
/// The menus' rules per mode (pure, tested in ModeMenusTests), and the pause card's per-mode words
/// and leaving (PauseModeAdapter, below).
///
///   Quick Play     Resume / Settings / Main Menu (asks first; the match is saved in its slot); no Change bowler (the captain picks,
///                  2026-11-06: "that shouldn't be there"); settings without the bowling, fielding or
///                  match pages (they are set before the match, in Match Setup, or the nets').
///   Career match   Resume / Settings / Leave match (asks first; the match is kept to continue
///                  from the hub, CareerMatchResume); no Change bowler (the captain picks);
///                  settings without hand, difficulty, bowling, fielding, overs or restart.
///   Career nets    Resume / Settings / End session (the trial and a drill ask first, practice
///                  does not), ended by NetsTrial.TryEndFromPause; career settings as above;
///                  Change bowler in practice only (trial and drills bowl their plan).
///   Free nets      Resume / Settings / Main Menu (no asking); every setting; Change bowler.
/// </summary>
public static class ModeMenus
{
    public static ePlayMode Current =>
        For(SceneManager.GetActiveScene().name, CareerMatchSession.Active, NetsSession.Career, NetsSession.Mode);

    public static ePlayMode For(string scene, bool careerMatch, bool careerNets, eNetsMode nets)
    {
        if (scene == NetsSession.NetsScene)
        {
            if (!careerNets) return ePlayMode.FreeNets;
            return nets == eNetsMode.Trial ? ePlayMode.NetsTrial
                 : nets == eNetsMode.Drill ? ePlayMode.NetsDrill : ePlayMode.NetsPractice;
        }
        if (scene == CareerMatchSession.MatchScene)
            return careerMatch ? ePlayMode.CareerMatch : ePlayMode.QuickPlay;
        return ePlayMode.Menu;
    }

    public static bool IsCareer(ePlayMode m) =>
        m == ePlayMode.CareerMatch || m == ePlayMode.NetsTrial || m == ePlayMode.NetsDrill || m == ePlayMode.NetsPractice;

    public static bool IsCareerNets(ePlayMode m) =>
        m == ePlayMode.NetsTrial || m == ePlayMode.NetsDrill || m == ePlayMode.NetsPractice;

    public static bool IsNets(ePlayMode m) => IsCareerNets(m) || m == ePlayMode.FreeNets;

    /// Choosing the bowler: only in the nets' practice - never in a match (the captain picks) or a
    /// planned nets session.
    public static bool CanChangeBowler(ePlayMode m) =>
        m == ePlayMode.NetsPractice || m == ePlayMode.FreeNets;

    /// The bowling, fielding and match pages of the settings: the free nets' only. A match plays to
    /// what was set before it (Match Setup, the career's own), not to sliders mid-innings.
    public static bool ShowsMatchTuning(ePlayMode m) => m == ePlayMode.FreeNets || m == ePlayMode.Menu;

    /// The pause card's leave button.
    public static string LeaveLabel(ePlayMode m) =>
        m == ePlayMode.CareerMatch ? "Leave match" : IsCareerNets(m) ? "End session" : "Main Menu";

    /// Whether leaving asks first, and what it says.
    public static bool AsksBeforeLeaving(ePlayMode m) =>
        m == ePlayMode.QuickPlay || m == ePlayMode.CareerMatch || m == ePlayMode.NetsTrial || m == ePlayMode.NetsDrill;

    public static string LeaveQuestion(ePlayMode m) => m switch
    {
        // Kept in the career to continue from the hub (CareerMatchResume; it was retiring out).
        ePlayMode.CareerMatch => "Leave the match? It is saved: continue it from the career hub.",
        ePlayMode.NetsTrial => "End the trial? It will not count - you can take it again.",
        ePlayMode.NetsDrill => "End the drill? This attempt will not be scored.",
        // Kept in its Quick Play slot after every ball (QuickPlayAutosave).
        ePlayMode.QuickPlay => "Leave the match? It is saved: continue it from Quick Play.",
        _ => "Leave match? Your score will be lost.",
    };

    /// The next-ball card's heading in the nets, where there are no overs to announce.
    public static string NetsHeading(ePlayMode m) => m switch
    {
        ePlayMode.NetsTrial => "<color=#3B82F6>NETS TRIAL</color>",
        ePlayMode.NetsDrill => "<color=#3B82F6>DRILL</color>",
        ePlayMode.NetsPractice => "<color=#3B82F6>PRACTICE</color>",
        _ => "<color=#3B82F6>NETS</color>",
    };

    // ---- The pause card ------------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    /// Every pause card in a match or nets scene gets the adapter (PauseScreen itself is unchanged).
    private static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != CareerMatchSession.MatchScene && scene.name != NetsSession.NetsScene)
            return;
        foreach (PauseScreen p in Object.FindObjectsByType<PauseScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (p.GetComponent<PauseModeAdapter>() == null)
                p.gameObject.AddComponent<PauseModeAdapter>();
    }
}

/// <summary>
/// The pause card's words and leaving for this mode (ModeMenus), each time it is shown: the leave
/// button reads "Leave match" or "End session" where it is not "Main Menu", the question says what
/// leaving does, and in the career nets the session is ended (NetsTrial.TryEndFromPause) instead
/// of the plain trip to Splash. Built beside PauseScreen rather than in it: its buttons are found
/// by their names and their own click handlers switched off where this one takes over.
/// </summary>
public class PauseModeAdapter : MonoBehaviour
{
    private Button leave, confirmLeave;
    private TMP_Text leaveLabel, question;
    private GameObject actionRow, confirmRow;
    private bool wired;

    private void OnEnable() => Apply();

    private void Apply()
    {
        if (!Find())
            return;
        ePlayMode m = ModeMenus.Current;
        if (leaveLabel != null) leaveLabel.text = ModeMenus.LeaveLabel(m);
        if (question != null) question.text = ModeMenus.LeaveQuestion(m);
        if (!ModeMenus.IsCareerNets(m) || wired)
            return;
        // The career nets: our handlers in place of PauseScreen's own (its OnMainMenu would go
        // straight to Splash, as Nets has no confirm).
        Mute(leave);
        Mute(confirmLeave);
        leave.onClick.AddListener(OnLeave);
        confirmLeave.onClick.AddListener(EndSession);
        wired = true;
    }

    private void OnLeave()
    {
        if (ModeMenus.AsksBeforeLeaving(ModeMenus.Current) && actionRow != null && confirmRow != null)
        {
            actionRow.SetActive(false);
            confirmRow.SetActive(true);
        }
        else
            EndSession();
    }

    private void EndSession()
    {
        Time.timeScale = 1f;
        if (!NetsTrial.TryEndFromPause())
            SceneFader.Load(PauseScreen.SplashScene);
    }

    private static void Mute(Button b)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            b.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }

    /// The card's parts, by the names its builder gives them (MenuPrefabBuilder.BuildPause).
    private bool Find()
    {
        if (leave != null)
            return true;
        foreach (Button b in GetComponentsInChildren<Button>(true))
        {
            if (b.name == "Main Menu") leave = b;
            else if (b.name == "Leave") confirmLeave = b;
        }
        if (leave == null || confirmLeave == null)
            return false;
        leaveLabel = leave.GetComponentInChildren<TMP_Text>(true);
        actionRow = leave.transform.parent.gameObject;
        confirmRow = confirmLeave.transform.parent.parent.gameObject;
        question = confirmRow.GetComponentInChildren<TMP_Text>(true);
        return true;
    }
}
