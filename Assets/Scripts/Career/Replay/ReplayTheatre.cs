/// <summary>
/// The record replay theatre (phase 7): a saved record clip played back at the ground it was hit at,
/// from the Records screen. Not yet: Begin always says no, so Watch stays off.
/// </summary>
public static class ReplayTheatre
{
    public static bool Active { get; private set; }

    public static eVenue Venue { get; private set; } = eVenue.Stadium;

    /// Back at Splash from the theatre: open Records (MainMenuExtras clears it once pushed).
    public static bool ReturnToRecords;

    /// Play `which`'s clip from `slot`. False when it cannot.
    public static bool Begin(int slot, eRecord which) => false;
}
