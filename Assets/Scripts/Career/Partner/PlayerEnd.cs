using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Which end the career player is standing at (phase 3): the striker's crease, as in every other
/// mode, or the non-striker's spot while he watches his partner bat. NonStrikerView moves him; the
/// rig (XRRigSetup), the menu spots (MenuLayout), the shot card, the ball-result text and the saved
/// panel poses (GrabbablePanel) follow Changed through EndPose.
///
/// Static, so it is put back to the crease on every scene load: a Quick Match after a career match
/// must not start at the far end.
/// </summary>
public static class PlayerEnd
{
    public static bool NonStriker { get; private set; }
    public static event System.Action Changed;

    public static void Set(bool nonStriker)
    {
        if (NonStriker == nonStriker) return;
        NonStriker = nonStriker;
        Changed?.Invoke();
    }

    /// Before the first scene: nothing is listening yet (with domain reload off, listeners from the
    /// last play session are gone with their objects).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        NonStriker = false;
        Changed = null;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    /// Back at the crease. Raised, not set quietly: the new scene's listeners subscribed in their
    /// Awake, before this, and may have read the old end (the old scene's are destroyed by now).
    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Reset();

    /// Back to the striker's end (scene loads, and tests).
    public static void Reset() => Set(false);
}
