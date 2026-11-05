using System;
using System.Collections.Generic;

/// <summary>
/// Makes the world the career is played in: fictional sides with fictional squads at each rung
/// (names from NameBank - never real teams, leagues or players), a season's fixtures (each side
/// plays the others once, then the top two meet in a final), the league table, and the results of
/// the fixtures the career player is not in. Pure and seeded.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these signatures.
/// </summary>
public static class LeagueBuilder
{
    /// A new season at `rung`: Ladder.TeamsInLeague sides. The career player's side keeps its name
    /// and squad when `keep` is given (the same side another season), else a new side is made;
    /// `you` goes into its squad (isYou). At International, "your side" is your nation's.
    public static Season NewSeason(eRung rung, int number, string nation, Team keep, SquadPlayer you, Random rng) =>
        new Season { rung = rung, number = number };

    /// A side at `rung` with a fictional name and eleven players (batting ratings around
    /// Ladder.SquadMean, the order strongest-first for the top six, bowlers at the tail).
    public static Team NewTeam(eRung rung, string name, string nation, Random rng) => new Team { name = name };

    /// Round robin: each side plays every other once, spread over rounds, each side once a round.
    public static List<Fixture> RoundRobin(List<Team> teams, Random rng) => new List<Fixture>();

    /// Play an AI-vs-AI fixture by simulation and put it in the table.
    public static void Simulate(Season s, Fixture f, Random rng) { }

    /// Put a played fixture's result into both sides' table rows (2 points a win, 1 a tie).
    public static void Record(Season s, Fixture f, InningsCard home, InningsCard away) { }

    /// The table: points, then net run rate.
    public static List<Team> Table(Season s) => new List<Team>(s.teams);

    /// After the last league round: the final between the top two (added once, false if it exists).
    public static bool AddFinal(Season s) => false;
}
