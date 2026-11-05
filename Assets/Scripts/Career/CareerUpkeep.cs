/// <summary>
/// Phase 7 upkeep, run when the hub opens and after a new season: the coach's goals made and brought
/// up to date, and the locker kit he has earned granted. True when anything changed (save it).
/// </summary>
public static class CareerUpkeep
{
    public static bool Run(CareerProfile p)
    {
        if (p == null)
            return false;
        bool c = CoachGoals.Ensure(p);
        c |= CoachGoals.Update(p).Count > 0;
        c |= Cosmetics.Grant(p).Count > 0;
        return c;
    }
}
