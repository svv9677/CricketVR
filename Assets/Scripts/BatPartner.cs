/// <summary>
/// The human bat's part in watching the partner bat (phase 3): while the player stands at the
/// non-striker's end his bat must not touch the ball, and the partner's contact reports its shot
/// through the same ShotStruck event a real hit does. CONTRACT STUB.
/// </summary>
public partial class Bat
{
    /// The player's bat makes no contact (he is at the non-striker's end). Reset on every scene load.
    public static bool ContactSuspended;

    /// The partner's contact raising the same event as a real hit.
    internal static void ReportShot(ShotInfo i) => ShotStruck?.Invoke(i);
}
