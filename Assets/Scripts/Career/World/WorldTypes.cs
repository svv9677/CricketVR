using System;
using System.Collections.Generic;

/// What a fixture asks of the batting side beyond "bat" (phase 7 fills these in; Normal until then).
public enum eSituation { Normal, Chase, RainShortened, Rescue, Final }

public enum eMessageKind { Info, Offer, Selection, Scout, Coach, Milestone }

/// <summary>One player in a squad: a name, how well he bats (0..99, the same scale as the career
/// player's ratings), and what he bowls. The career player himself is in his team's squad with
/// isYou set; his batting rating is his measured overall.</summary>
[Serializable]
public class SquadPlayer
{
    public string name = "";
    public float bat = 40f;
    public bool bowls;
    public eBowlerKind kind = eBowlerKind.Medium;
    public bool isYou;
}

/// <summary>A side in a league: its name (player-editable), kit colour (Appearance.KitColours index),
/// its squad of eleven, and its league record this season.</summary>
[Serializable]
public class Team
{
    public string id = "";
    public string name = "";
    /// Three or four letters for the scoreboard ("SRH" style, but fictional).
    public string shortName = "";
    public int kit;
    public List<SquadPlayer> players = new List<SquadPlayer>();

    public int played, won, lost, tied, points;
    /// For net run rate: runs and balls, for and against.
    public int runsFor, ballsFaced, runsAgainst, ballsBowled;

    public float NetRunRate =>
        (ballsFaced > 0 ? runsFor * 6f / ballsFaced : 0f) - (ballsBowled > 0 ? runsAgainst * 6f / ballsBowled : 0f);

    public SquadPlayer You => players.Find(p => p.isYou);
}

/// <summary>One fixture of a season. Between two AI sides it is simulated (LeagueBuilder); with the
/// career player's side it is played (or, if he is left out, simulated).</summary>
[Serializable]
public class Fixture
{
    public int round;
    public string home = "", away = "";
    public bool final;
    public bool played;
    /// "Harbour CC won by 12 runs".
    public string result = "";
    /// "145/6 (20)" each side, for the fixtures list.
    public string homeScore = "", awayScore = "";
    public string winner = "";
    /// Under lights; the pitch (ePitch); what is asked of the side (phase 7).
    public bool night;
    public ePitch pitch = ePitch.Flat;
    public eSituation situation = eSituation.Normal;
    /// A scout from the rung above is watching this one (phase 4).
    public bool scout;

    public bool Involves(string teamId) => home == teamId || away == teamId;
    public string Opponent(string teamId) => home == teamId ? away : home;
}

/// <summary>A season at one rung: the league's sides, the fixtures, and which comes next.</summary>
[Serializable]
public class Season
{
    public int number = 1;
    public eRung rung = eRung.Club;
    /// The id of the career player's side.
    public string yourTeam = "";
    public List<Team> teams = new List<Team>();
    public List<Fixture> fixtures = new List<Fixture>();
    /// The season is over (the final played, or the side out of it): time for the review.
    public bool finished;
    /// Coach goals for this season (phase 7).
    public List<SeasonGoal> goals = new List<SeasonGoal>();

    public Team Team(string id) => teams.Find(t => t.id == id);
    public Team Yours => Team(yourTeam);
}

/// One line of a batting card.
[Serializable]
public class BatterLine
{
    public string name = "";
    public int runs, balls, fours, sixes;
    public bool batted, isOut, isYou;
    /// "c Sharma b Khan", "b Khan", "not out".
    public string how = "";
}

/// <summary>A side's innings, simulated or played: the total, the over-by-over runs and wickets (the
/// result screen plays these back) and the batting card.</summary>
[Serializable]
public class InningsCard
{
    public string team = "";
    public int runs, wickets, balls, overs, target;
    public List<int> overRuns = new List<int>(), overWickets = new List<int>();
    public List<BatterLine> batting = new List<BatterLine>();

    public string Score => $"{runs}/{wickets}";
    public string OversText => $"{balls / 6}.{balls % 6}";
}

/// <summary>The career player's innings in one match: the line in his record book.</summary>
[Serializable]
public class InningsRecord
{
    public int season;
    public eRung rung;
    public string team = "", opponent = "";
    public int position;
    public int runs, balls, fours, sixes;
    public bool batted, isOut, retired;
    public string how = "";
    /// The team's total and the result ("Won by 4 wickets").
    public string teamScore = "", result = "";
    public bool won, tied;
    /// He did not play (left out of the side; phase 4).
    public bool leftOut;
    /// A scout was watching, and what he made of it (0..1; phase 4).
    public bool scouted;
    public float impression;
    public string date = "";

    public float StrikeRate => balls > 0 ? runs * 100f / balls : 0f;
}

/// Career batting figures, all rungs together.
[Serializable]
public class CareerStats
{
    public int matches, innings, notOuts, runs, balls, fours, sixes, fifties, hundreds, ducks;
    public int highScore;
    public bool highScoreNotOut;

    public float Average => innings - notOuts > 0 ? runs / (float)(innings - notOuts) : runs;
    public float StrikeRate => balls > 0 ? runs * 100f / balls : 0f;
}

/// One choice in a message (an offer's team, accept / decline).
[Serializable]
public class MessageOption
{
    public string label = "";
    /// What choosing it does, read by CareerProgress.Choose ("join:<teamName>:<rung>", "decline").
    public string action = "";
}

/// <summary>A message in the career inbox: an offer, a selection decision, a scout's note, the
/// coach, a milestone.</summary>
[Serializable]
public class CareerMessage
{
    public eMessageKind kind;
    public string from = "", title = "", body = "";
    public bool read;
    public List<MessageOption> options = new List<MessageOption>();
    /// A choice has been made (options are then shown as done).
    public bool answered;
    public string date = "";
}

/// A coach goal for the season (phase 7).
[Serializable]
public class SeasonGoal
{
    public string id = "", text = "";
    public float target, progress;
    public bool done;
}
