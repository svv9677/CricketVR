/// <summary>
/// Tells the player's recentre (the Oculus button held) apart from the tracking-origin updates
/// that start-up itself may report, for XRRigSetup.
///
/// Both arrive as the same XRInputSubsystem.trackingOriginUpdated event. A recentre redraws
/// tracking space, so the room's saved down-the-pitch (RoomFacing) must be taken again from the
/// head. But XRRigSetup asks for the floor origin on every scene load, and if that request reports
/// an update of its own, treating it as a recentre would throw the saved direction away on every
/// launch and every scene change - the very thing RoomFacing is for. So an update only counts once
/// the origin has been in (or its retries given up) for SettleFrames frames.
///
/// The cost: a real recentre in the first SettleFrames frames after a load still re-places the
/// player (XRRigSetup does that on any update) but keeps the old direction.
///
/// Plain C# with the frame number passed in, so RecentreGateTests run without the engine. One per
/// rig, so each scene load starts unsettled.
/// </summary>
public sealed class RecentreGate
{
    /// About half a second at 72 Hz: long enough for a start-up origin request to report.
    public const int SettleFrames = 30;

    private int _settledAt = -1;

    /// A recentre has happened and the head's direction has not been taken yet.
    public bool RecapturePending { get; private set; }

    /// True from SettleFrames frames after the origin was first ready.
    public bool Settled(int frame) => _settledAt >= 0 && frame >= _settledAt;

    /// Called every frame. The first frame `originReady` is true (the floor origin is in, or its
    /// retries have run out) starts the settle count; later calls change nothing.
    public void Note(bool originReady, int frame)
    {
        if (_settledAt < 0 && originReady)
            _settledAt = frame + SettleFrames;
    }

    /// A tracking-origin update at `frame`. True (and a recapture pending) when it counts as the
    /// player's recentre; false while the origin is still settling.
    public bool OnOriginUpdated(int frame)
    {
        if (!Settled(frame))
            return false;
        RecapturePending = true;
        return true;
    }

    /// The new direction has been taken and saved.
    public void Captured() => RecapturePending = false;
}
