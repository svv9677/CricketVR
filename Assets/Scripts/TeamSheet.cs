using UnityEngine;

/// <summary>
/// The quick-play sides: the batting order and the bowlers with their kind. An asset
/// (Assets/Resources/Data/QuickPlayTeams.asset) on the HUD's Teams field, so the names are edited in
/// the inspector. (Until 2026-11-15 they were arrays in HUD.Reset.) A career match brings its own
/// roster and does not use this.
/// </summary>
[CreateAssetMenu(menuName = "CricketVR/Team Sheet", fileName = "TeamSheet")]
public class TeamSheet : ScriptableObject
{
    [System.Serializable]
    public struct BowlerEntry
    {
        public string name;
        public eBowlerKind kind;
    }

    [Tooltip("Eleven batters, in batting order.")]
    public string[] batters = new string[0];

    [Tooltip("Who bowls when is the captain's (BowlingAttack.PickNextBowler), not this order.")]
    public BowlerEntry[] bowlers = new BowlerEntry[0];
}
