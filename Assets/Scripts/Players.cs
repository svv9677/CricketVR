/// The two sides' players as the scorebook knows them (HUD keeps the lists).
public class Batsman
{
    public string Name;
    public int Runs;
    public int Balls;   // faced (HUDStats)
    /// The player himself, in a career match (HUD.ApplyCareerRoster): his balls are bowled to him
    /// for real; everyone else's are simulated (HUD.ScoreSimulatedBall). False in every other match.
    public bool IsHuman;
    /// A career batter's batting rating (0-100), for the simulation of his balls. Read only by the
    /// career's simulation, never by the physics.
    public float Rating = 40f;

    public Batsman(string name, int runs=0)
    {
        Name = name;
        Runs = runs;
    }
}

public class Bowler
{
    public string Name;
    /// The rest of the game's view of him (keeper position, action, field plans): his kind's
    /// BowlerRepertoire.BaseType.
    public eSwingType Type;
    /// Fast, medium, off-spin, leg-spin or mystery: what he can bowl (BowlerRepertoire).
    public eBowlerKind Kind;
    public int Balls, Conceded, Wickets;   // HUDStats

    public Bowler(string name, eSwingType typ = eSwingType.None)
    {
        Name = name;
        Type = typ;
        Kind = typ == eSwingType.Pace ? eBowlerKind.Fast : typ == eSwingType.LegSpin ? eBowlerKind.LegSpin
             : typ == eSwingType.OffSpin ? eBowlerKind.OffSpin : eBowlerKind.Medium;
    }

    public Bowler(string name, eBowlerKind kind) : this(name, BowlerRepertoire.BaseType(kind))
    {
        Kind = kind;
    }
}
