/// The four rungs of the career, bottom to top (2026-11-05, agreed with the user).
public enum eRung { Club, State, ProLeague, International }

/// Where a career match is played: the club ground (Club and State) or the big stadium.
public enum eVenue { ClubGround, Stadium }

/// <summary>
/// What each rung of the career is: the standard of bowling (BowlingLevels), the length of a match,
/// the ground and the crowd, how good the other batters are, and what it takes to be noticed by the
/// rung above. One table, so balancing the career is done here and nowhere else.
/// </summary>
public static class Ladder
{
    public const int Rungs = 4;

    public static string Name(eRung r) => r switch
    {
        eRung.Club => "Club cricket",
        eRung.State => "State cricket",
        eRung.ProLeague => "Pro League",
        _ => "International",
    };

    /// The short form for a line of text ("Club", "State", "Pro League", "International").
    public static string Short(eRung r) => r switch
    {
        eRung.Club => "Club",
        eRung.State => "State",
        eRung.ProLeague => "Pro League",
        _ => "International",
    };

    public static eBowlingLevel Bowling(eRung r) => r switch
    {
        eRung.Club => eBowlingLevel.Easy,
        eRung.State => eBowlingLevel.Medium,
        eRung.ProLeague => eBowlingLevel.Hard,
        _ => eBowlingLevel.Pro,
    };

    /// Overs a side: short at the bottom (a quick session in the headset), full T20 at the top.
    public static int Overs(eRung r) => r switch
    {
        eRung.Club => 6,
        eRung.State => 10,
        _ => 20,
    };

    public static eVenue Venue(eRung r) => r <= eRung.State ? eVenue.ClubGround : eVenue.Stadium;

    public static eCrowd Crowd(eRung r) => r switch
    {
        eRung.Club => eCrowd.Empty,
        eRung.State => eCrowd.Half,
        _ => eCrowd.Packed,
    };

    /// Chance a fixture is played under lights (Pro League is a night competition, mostly).
    public static float NightChance(eRung r) => r switch
    {
        eRung.Club => 0f,
        eRung.State => 0.1f,
        eRung.ProLeague => 0.55f,
        _ => 0.35f,
    };

    /// The typical batting rating (0..99) of the other players at this rung, and its spread.
    public static float SquadMean(eRung r) => r switch
    {
        eRung.Club => 32f,
        eRung.State => 46f,
        eRung.ProLeague => 61f,
        _ => 74f,
    };

    public const float SquadSpread = 11f;

    /// Overall rating (and form) a scout from the rung above wants to see before an offer.
    public static float PromotionRating(eRung r) => r switch
    {
        eRung.Club => 42f,
        eRung.State => 56f,
        eRung.ProLeague => 69f,
        _ => 999f,
    };

    /// Overall below this (with poor form over a season) and a side drops you a rung.
    public static float RelegationRating(eRung r) => r switch
    {
        eRung.Club => -1f,
        eRung.State => 30f,
        eRung.ProLeague => 44f,
        _ => 57f,
    };

    public static bool Top(eRung r) => r == eRung.International;
    public static eRung Above(eRung r) => r == eRung.International ? r : r + 1;
    public static eRung Below(eRung r) => r == eRung.Club ? r : r - 1;

    /// Teams in a league at this rung; each plays the others once, then the top two meet in a final.
    public const int TeamsInLeague = 8;
}
