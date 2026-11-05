/// <summary>
/// What the career mode needs from the scorekeeper. Kept apart from HUD.cs so the career's additions
/// are in one place.
/// </summary>
public partial class HUD
{
    /// Bring on the first bowler of this kind (the nets trial: each planned ball is bowled by the
    /// kind of bowler it is planned for, so the run-up, the action and the keeper match the ball).
    /// SelectBowlerOfType cannot do it: it goes by eSwingType, and the mystery spinner shares the
    /// off-spinner's, so it could never bring him on. Through SetBowler, so the bowling machine's
    /// view (Main.swingType), the keeper and AnimatedBowler's run-up all follow, as for any change.
    /// True when there is a bowler of that kind (whether or not he was on already).
    public bool SelectBowlerOfKind(eBowlerKind kind)
    {
        if (Bowlers == null)
            return false;
        int index = -1;
        for (int i = 0; i < Bowlers.Count; i++)
        {
            if (Bowlers[i] != null && Bowlers[i].Kind == kind)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
            return false;
        if (Bowlers[index] == CurrentBowler)
            return true;
        NextBowlerIndex = index;
        SetBowler(index);
        UpdateUI();
        return true;
    }

    /// Move the keeper for the bowler now, if a change is still waiting for it. HUD does this itself
    /// when the next ball starts (InGame_SelectDelivery), but a bowler brought on by a listener that
    /// runs after HUD's on that same state would otherwise keep the old keeper position for a ball.
    /// Only call it while the ball is parked (the start of a delivery), as HUD does.
    public void MoveKeeperIfPending()
    {
        if (!keeperMovePending)
            return;
        keeperMovePending = false;
        UpdateKeeperPosition();
    }
}
