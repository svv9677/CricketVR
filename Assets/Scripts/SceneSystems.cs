using UnityEngine;

/// <summary>
/// The scene's "Systems" object. The game's per-scene systems (match flow, the over break, the ball
/// ledger, the career's match and nets...) are components on its children, so the Hierarchy shows
/// everything that runs. (Until 2026-11-15 each was created in code as the scene loaded.)
///
/// A child that applies only sometimes (a career match, not quick play) is saved switched off and is
/// listed here with when it applies; on Awake this switches on the ones that apply now. One left off
/// never runs at all (no Awake, no OnDestroy), exactly as when it was not created.
/// </summary>
public class SceneSystems : MonoBehaviour
{
    public enum eWhen
    {
        CareerMatch,
        /// A career match whose player watches his partner bat (NonStrikerView.Watching).
        CareerMatchWatching,
        /// Quick play: any match that is not a career one.
        NotCareerMatch,
        /// The career's nets session (not the free nets).
        CareerNets,
        /// The replay theatre is showing a clip.
        ReplayTheatre,
    }

    [System.Serializable]
    public struct Conditional
    {
        [Tooltip("A child saved switched off; switched on when it applies.")]
        public GameObject system;
        public eWhen when;
    }

    [Tooltip("Systems that run only sometimes. Every other child always runs.")]
    public Conditional[] onlyWhen = new Conditional[0];

    private void Awake()
    {
        foreach (Conditional c in onlyWhen)
            if (c.system != null && Applies(c.when))
                c.system.SetActive(true);
    }

    public static bool Applies(eWhen when)
    {
        switch (when)
        {
            case eWhen.CareerMatch: return CareerMatchSession.Active;
            case eWhen.CareerMatchWatching: return CareerMatchSession.Active && NonStrikerView.Watching(CareerSaves.Current);
            case eWhen.NotCareerMatch: return !CareerMatchSession.Active;
            case eWhen.CareerNets: return NetsSession.Career;
            case eWhen.ReplayTheatre: return ReplayTheatre.Active;
            default: return false;
        }
    }
}
