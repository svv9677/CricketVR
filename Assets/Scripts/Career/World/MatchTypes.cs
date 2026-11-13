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
    /// Phase 7: the target cut by rain (0: none); a collapse's scripted CareerSim codes, played before he comes in.
    public int revisedTarget;
    public List<int> collapse = new List<int>();
    public int seed;

    public int Target => !batFirst && theirs != null ? (revisedTarget > 0 ? revisedTarget : theirs.runs + 1) : 0;
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
    /// Phase 7 records: copied from MatchState (CareerMatchLogic.Build).
    public int ballsToFifty, ballsToHundred;
    public float longestSix;
    public string sixStamp = "";
    /// Records this match broke (CareerRecords.AfterMatch; empty on a first innings).
    public List<eRecord> bests = new List<eRecord>();
}

/// <summary>A career match begun and not yet recorded (CareerMatchRecovery): the plan, and the
/// innings as it stood when last saved, so a match the app was killed in can still be recorded.</summary>
[Serializable]
public class PendingMatch
{
    public bool active;
    public MatchPlan plan;
    public int runs, balls, fours, sixes, teamRuns, teamWickets, teamBalls;
    public bool batted, isOut;
    public string how = "";
    public int ballsToFifty, ballsToHundred;
    public float longestSix;
    public string sixStamp = "";
    /// It is continued, not retired (2026-11-09: "Career manager, I should also be able to resume"):
    /// the hub offers Continue match, which plays on from `resume`. False only in saves from before
    /// then, which CareerMatchRecovery still records as retired out.
    public bool resumable;
    public CareerResumeState resume = new CareerResumeState();
}

/// <summary>A career match as it stood after the last ball, to play on from (CareerMatch's resume):
/// the scoreboard and the match (MatchSnapshot), and what the career keeps beside it - each batter's
/// fours and sixes, whether he is out and the rest simulated, the collapse's script and the
/// situation card, and the over-by-over record.</summary>
[Serializable]
public class CareerResumeState
{
    /// A scoreboard was saved; false: the match is played from its first ball.
    public bool hasMatch;
    public MatchSnapshot match = new MatchSnapshot();
    public List<int> batterFours = new List<int>(), batterSixes = new List<int>();
    public bool youOut, restDone, introShown;
    public int scriptAt;
    public List<int> overRuns = new List<int>(), overWickets = new List<int>();
    public int overStartRuns, overStartWickets;
}
