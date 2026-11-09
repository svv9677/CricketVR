using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A quick-play match kept in its slot as it is played (QuickPlaySlots): after every ball, and once
/// at the start, so leaving at any point - the pause card's Main Menu, taking the headset off -
/// loses at most the ball in play. A match continued from its slot is put back here first, once
/// HUD and MatchFlow have set up their own (one frame in). When the match is over the slot keeps
/// its result line and no match, so the screen offers a new one.
///
/// Built with the match scene; not in a career match (the career keeps its own) or the nets.
/// </summary>
public class QuickPlayAutosave : MonoBehaviour
{
    private QuickPlaySlot slot;
    private readonly Dictionary<Batsman, string> outs = new Dictionary<Batsman, string>();
    private bool subscribed, finished;

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
        if (scene.name != MainMenuScreen.CricketScene || CareerMatchSession.Active ||
            FindFirstObjectByType<QuickPlayAutosave>() != null)
            return;
        var go = new GameObject("Quick Play Autosave");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<QuickPlayAutosave>();
    }

    private IEnumerator Start()
    {
        HUD.BatterOut += OnOut;
        HUD.StrikeChanged += OnStrike;
        yield return null;          // HUD.Start (its Reset) and MatchFlow.Start have run
        while (HUD.Instance == null || Main.Instance == null)
            yield return null;
        slot = QuickPlaySlots.Load(MatchFormat.Current);
        MatchSnapshot resume = QuickPlaySlots.PendingResume;
        QuickPlaySlots.PendingResume = null;
        if (resume != null)
        {
            bool ok = HUD.Instance.Restore(resume) && MatchFlow.Restore(resume);
            Debug.Log(ok ? $"[QuickPlay] continued {MatchFormat.Label()} at {resume.ScoreText}"
                         : $"[QuickPlay] the saved {MatchFormat.Label()} match did not fit: a new one");
        }
        Main.Instance.onGameStateChanged += OnGameState;     // after MatchFlow's: it has checked the end
        subscribed = true;
        Save();
    }

    private void OnDestroy()
    {
        HUD.BatterOut -= OnOut;
        HUD.StrikeChanged -= OnStrike;
        if (subscribed && Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
    }

    private void OnOut(Batsman b, string how)
    {
        if (b != null) outs[b] = how;
    }

    /// A new innings (a Test's, HUD.Reset) brings a fresh eleven: the old dismissals go.
    private void OnStrike(Batsman b)
    {
        if (HUD.Instance != null && HUD.Instance.Wickets == 0 && HUD.Instance.Overs == 0 && HUD.Instance.Balls == 0)
            outs.Clear();
    }

    private void OnGameState()
    {
        if (Main.Instance.gameState == eGameState.InGame_Ready)
            Save();
    }

    private void Save()
    {
        if (slot == null || finished || HUD.Instance == null)
            return;
        if (!slot.hasSetup)
        {
            slot.setup = QuickPlaySetup.FromCurrent();
            slot.hasSetup = true;
        }
        if (MatchFlow.Over)
        {
            finished = true;
            slot.inProgress = false;
            slot.match = null;
            slot.lastResult = string.IsNullOrEmpty(MatchFlow.Status) ? "" : $"Last match: {MatchFlow.Status}";
            QuickPlaySlots.Save(slot);
            return;
        }
        var m = new MatchSnapshot();
        HUD.Instance.Capture(m, outs);
        MatchFlow.Capture(m);
        slot.match = m;
        slot.inProgress = true;
        QuickPlaySlots.Save(slot);
    }
}
