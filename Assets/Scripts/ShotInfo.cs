/// <summary>
/// What the bat reports for each struck ball (Bat.ShotStruck). Speeds are in m/s; quality runs
/// 0..1, 1 being the middle of the bat; contactLabel is the word shown to the player ("Middled",
/// "Thick edge", "Toe"); edge is true for any edge.
/// </summary>
public struct ShotInfo { public float ballSpeedIn, batSpeed, exitSpeed, quality; public string contactLabel; public bool edge; }
