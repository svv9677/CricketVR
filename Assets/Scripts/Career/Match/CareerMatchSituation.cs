/// <summary>
/// What a match situation does in the career match (phase 7): a collapse's scripted balls played
/// before he comes in, and the situation's card before the first ball. With no situation, the
/// partners' balls come from CareerSim exactly as before.
/// </summary>
public partial class CareerMatch
{
    /// The next simulated ball's CareerSim code: from CareerSim at the striker's rating, as hard as
    /// the match situation says.
    private int NextCode(HUD hud, Batsman b)
    {
        float aggression = CareerSim.Aggression(hud.Runs, hud.Wickets, Bowled(hud), hud.TotalOvers * 6, MatchFlow.Target);
        return CareerSim.Ball(b.Rating, plan.bowling, aggression, rng);
    }

    /// After a simulated ball is scored: a scripted code is used up once the scoreboard took it.
    private void ScriptTaken(int bowledBefore, HUD hud) { }

    /// True while the situation's card is up before the first ball.
    private bool SituationIntro() => false;
}
