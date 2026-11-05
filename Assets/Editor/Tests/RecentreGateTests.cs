using NUnit.Framework;

/// <summary>
/// RecentreGate: which tracking-origin updates XRRigSetup treats as the player's recentre (take a
/// new down-the-pitch from the head) and which are start-up noise (keep the saved one). Pure; the
/// frame numbers stand in for Time.frameCount. Run: Window > General > Test Runner > EditMode >
/// RecentreGateTests.
/// </summary>
public class RecentreGateTests
{
    /// Frames 0 to `last`, the origin becoming ready at `readyAt`, as XRRigSetup notes it.
    private static RecentreGate RunTo(int readyAt, int last)
    {
        var gate = new RecentreGate();
        for (int f = 0; f <= last; f++)
            gate.Note(f >= readyAt, f);
        return gate;
    }

    [Test]
    public void AnUpdateBeforeTheOriginIsReadyIsNotARecentre()
    {
        var gate = RunTo(readyAt: 100, last: 50);
        Assert.IsFalse(gate.OnOriginUpdated(50));
        Assert.IsFalse(gate.RecapturePending, "a scene load keeps the saved direction");
    }

    /// The floor origin request at start-up may report an update a few frames after it is in.
    [Test]
    public void AnUpdateWhileTheOriginSettlesIsNotARecentre()
    {
        const int ready = 5;
        var gate = RunTo(ready, ready + RecentreGate.SettleFrames - 1);
        Assert.IsFalse(gate.OnOriginUpdated(ready + 1));
        Assert.IsFalse(gate.OnOriginUpdated(ready + RecentreGate.SettleFrames - 1), "last unsettled frame");
        Assert.IsFalse(gate.RecapturePending);
    }

    [Test]
    public void AnUpdateOnceSettledIsARecentreUntilCaptured()
    {
        const int ready = 5;
        int settled = ready + RecentreGate.SettleFrames;
        var gate = RunTo(ready, settled);
        Assert.IsTrue(gate.OnOriginUpdated(settled), "first settled frame");
        Assert.IsTrue(gate.RecapturePending);
        gate.Captured();
        Assert.IsFalse(gate.RecapturePending);
        Assert.IsTrue(gate.OnOriginUpdated(settled + 500), "and every recentre after");
        Assert.IsTrue(gate.RecapturePending);
    }

    /// The settle count starts the first frame the origin is ready and is not pushed back by
    /// later frames (Note runs every frame).
    [Test]
    public void TheSettleCountStartsOnceOnly()
    {
        var gate = new RecentreGate();
        gate.Note(true, 10);
        gate.Note(true, 20);
        gate.Note(true, 35);
        Assert.IsTrue(gate.Settled(10 + RecentreGate.SettleFrames));
        Assert.IsFalse(gate.Settled(10 + RecentreGate.SettleFrames - 1));
    }

    /// No floor origin ever (retries run out): the origin counts as ready then, so a recentre
    /// still re-takes the direction.
    [Test]
    public void GivenUpRetriesStillSettle()
    {
        const int givenUpAt = 120;
        var gate = RunTo(givenUpAt, givenUpAt + RecentreGate.SettleFrames);
        Assert.IsTrue(gate.OnOriginUpdated(givenUpAt + RecentreGate.SettleFrames));
    }

    /// Each scene has its own rig and gate: a settled gate in one scene does not make the next
    /// scene's start-up update a recentre.
    [Test]
    public void ANewSceneStartsUnsettled()
    {
        var first = RunTo(0, 200);
        Assert.IsTrue(first.Settled(200));
        var next = RunTo(readyAt: 2, last: 4);
        Assert.IsFalse(next.OnOriginUpdated(4));
    }
}
