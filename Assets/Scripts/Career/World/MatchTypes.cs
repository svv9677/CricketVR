using System;
using System.Collections.Generic;

/// <summary>
/// Everything a career match needs to be set up, decided at home before the match scene loads
/// (CareerProgress.PlanNext) and read in the match (CareerMatch): who the two sides are, the format,
/// the conditions, who bats first, and where the career player bats.
/// </summary>
[Serializable]
public class MatchPlan
{
    /// Index into the season's fixtures.
    public int fixture;
    public eRung rung;
    public string yourTeam = "", opponent = "";
    public string yourTeamName = "", opponentName = "";
    public string yourShort = "", opponentShort = "";
    public int overs = 6;
    public eBowlingLevel bowling = eBowlingLevel.Easy;
    public eCrowd crowd = eCrowd.Empty;
    public eTimeOfDay time = eTimeOfDay.Afternoon;
    public ePitch pitch = ePitch.Flat;
    public eVenue venue = eVenue.ClubGround;
    /// True: your side bats first and sets a total; false: the other side has batted and you chase
    /// (their innings is in `theirs`, simulated before the match).
    public bool batFirst = true;
    public eSituation situation = eSituation.Normal;
    /// Your side's batting order (11 names, the order they bat in); the career player is at `slot`
    /// (0-based index into it). The ratings, same order, for the partners' simulated balls.
    public List<string> order = new List<string>();
    public List<float> orderRatings = new List<float>();
    public int slot = 6;
    /// The other side's bowlers (names, by kind as HUD's attack is: two quicks, two medium pacers,
    /// an off-spinner, two leg-spinners, a mystery spinner) for the scoreboard.
    public List<string> bowlers = new List<string>();
    /// Chasing: the other side's innings already played (target = runs + 1).
    public InningsCard theirs;
    /// A scout from the rung above is watching (phase 4), and which rung he is from.
    public bool scout;
    public eRung scoutFrom;
    /// Phase 7 situations: start the innings at this score (a collapse to rescue), overs cut by rain.
    public int startRuns, startWickets;
    public int seed;

    public int Target => theirs != null && !batFirst ? theirs.runs + 1 : 0;
}

/// <summary>How a career match went, from the match scene (CareerMatch) to CareerProgress.</summary>
[Serializable]
public class CareerMatchResult
{
    /// The career player's innings.
    public int runs, balls, fours, sixes;
    public bool batted, isOut, retired;
    public string how = "";
    /// Both sides' innings (yours played, partly simulated; theirs simulated).
    public InningsCard yours, theirs;
    public bool won, tied;
    public string result = "";
    /// He left the match before it ended (the pause menu): his innings counts as retired out and
    /// the rest of the match is simulated.
    public bool abandoned;
}
