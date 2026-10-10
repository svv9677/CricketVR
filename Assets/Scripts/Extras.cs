using UnityEngine;

/// <summary>
/// Byes: runs off a ball that missed the bat (and the batter) and got past the keeper.
///
/// Per the Laws (Law 23): a ball that passes the striker without touching bat or person is still
/// in play; runs the batters take off it, or a boundary it reaches, are byes - extras to the team,
/// not the batter's, and not charged to the bowler. The ball still counts in the over. On a wide,
/// runs taken are added to the wide instead (Law 22), not scored as byes.
///
/// So once a ball beats the keeper (KeeperCollider behind him, or it spills out of his gloves)
/// it is played on like a hit ball - the field goes after it, the rope counts, the time until it
/// is fielded gives the runs - and HUD scores the result as byes (or wides).
/// </summary>
public static class Extras
{
    /// A ball the keeper has let through is being played on.
    public static bool InPlay { get; private set; }
    /// ...off a wide, so its runs are wides, not byes.
    public static bool OnWide { get; private set; }

    public static void Begin(bool onWide)
    {
        Main inst = Main.Instance;
        if (InPlay || inst == null)
            return;
        InPlay = true;
        OnWide = onWide;
        // It pitched on the way down: never a six, and nobody "catches" a bye.
        inst.theBallScript.bounced = true;
        inst.theBatScript.hasHitBall = true;   // the bat cannot meet it now
        inst.resetDelay = Main.NextBallDelay;
        Debug.Log($"[Extras] Past the keeper{(onWide ? " off a wide" : "")}: played on for byes");
        inst.gameState = eGameState.InGame_BallHit;
    }

    public static void Clear()
    {
        InPlay = false;
        OnWide = false;
    }
}
