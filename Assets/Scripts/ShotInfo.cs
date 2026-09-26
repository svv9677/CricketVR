/// <summary>
/// One bat-ball contact, as Bat reports it through Bat.ShotStruck: speeds in m/s, quality 1 = middled.
/// </summary>
public struct ShotInfo { public float ballSpeedIn, batSpeed, exitSpeed, quality; public string contactLabel; public bool edge; }
