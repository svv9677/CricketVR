using System;

/// <summary>
/// The simulation of everything the career player does not bat himself: his partners' balls, the
/// other side's innings, and the matches between the other sides in his league. Pure and seeded.
/// It never touches the player's own balls - those are played and measured.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these signatures.
/// </summary>
public static class CareerSim
{
    /// Scorebook codes, as HUD.AddRuns takes them.
    public const int Wicket = -1, Wide = 10;

    /// How hard the batting side is going (0 = survival, 0.5 = normal, 1 = all-out attack) given
    /// the score, the balls gone and left, and the target (0 when setting a total).
    public static float Aggression(int runs, int wickets, int ballsBowled, int totalBalls, int target) => 0.5f;

    /// One ball to a batter of `bat` rating (0..99) against an attack of `level`:
    /// Wicket, 0..6, or Wide.
    public static int Ball(float bat, eBowlingLevel level, float aggression, Random rng) => 0;

    /// How a simulated batter was out ("c Sharma b Khan", "b Khan", "lbw b Khan", "run out").
    public static string HowOut(string bowler, string fielder, Random rng) => "b " + bowler;

    /// A whole innings for `batting` against `bowling`: in order, each batter's balls from Ball(),
    /// strike rotating on odd runs and at the over, ending all out, overs done or target reached.
    /// `target` 0 when batting first. The card names every batter who batted.
    public static InningsCard Innings(Team batting, Team bowling, int overs, int target, eBowlingLevel level, Random rng) =>
        new InningsCard { team = batting?.name ?? "", overs = overs, target = target };

    /// The card as MatchFlow's AI innings (the result screen plays these back).
    public static AIInnings.Result ToAIResult(InningsCard c) => new AIInnings.Result();
}
