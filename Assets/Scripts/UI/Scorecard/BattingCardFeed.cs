using System.Collections.Generic;

/// <summary>
/// Keeps the batting card (BattingCard) current from HUD's events, for the match panel
/// (ScorecardPanel). HUD keeps every batter's runs and balls but not how he got out, so this
/// does: HUD.BatterOut comes for every wicket, off a real ball (its dismissal line, "Ravi c Point
/// b Khan  34 (22)", from where the catch was taken and who bowled it) and off a career match's
/// simulated one (CareerMatch's "c Khan b Lee", "lbw b Lee", "run out"), so one listener sees both.
///
/// Changed is raised for anything that can move the card: a wicket, a change of strike (which
/// also comes for HUD.Reset and the career's roster going in), every real ball (HUD.BallScored)
/// and every simulated one (HUD.SimulatedBallScored). A batch of simulated balls raises it dozens
/// of times in one frame, so the panel only marks itself dirty and builds the card once, later.
/// </summary>
public sealed class BattingCardFeed
{
    /// Something on the card may have changed. Do not build the card from inside it.
    public event System.Action Changed;

    private readonly Dictionary<Batsman, string> outs = new Dictionary<Batsman, string>();
    private List<Batsman> book;
    private bool attached;

    /// How each dismissed batter went ("c Point b Khan"), keyed by HUD's Batsman.
    public IDictionary<Batsman, string> Outs => outs;

    /// Listen to HUD. Safe to call twice.
    public void Attach()
    {
        if (attached)
            return;
        attached = true;
        HUD.BatterOut += OnBatterOut;
        HUD.StrikeChanged += OnStrikeChanged;
        HUD.BallScored += OnBallScored;
        HUD.SimulatedBallScored += OnChanged;
    }

    public void Detach()
    {
        if (!attached)
            return;
        attached = false;
        HUD.BatterOut -= OnBatterOut;
        HUD.StrikeChanged -= OnStrikeChanged;
        HUD.BallScored -= OnBallScored;
        HUD.SimulatedBallScored -= OnChanged;
    }

    /// A batter is out: how, from HUD's dismissal line without his name or score
    /// (CareerMatchLogic.CleanHow, as the career's own cards write it). Public for the tests.
    public void Record(Batsman b, string dismissal)
    {
        if (b == null)
            return;
        outs[b] = CareerMatchLogic.CleanHow(b.Name, dismissal);
        Changed?.Invoke();
    }

    /// HUD.Reset makes a new list of new batters: the dismissals of the old ones are forgotten.
    /// Only theirs - a wicket in the new list that came before the card was next built (the
    /// panel builds only while it is shown) is kept. Called with HUD's list each time the card is
    /// built. Public for the tests.
    public void UseBook(List<Batsman> batsmen)
    {
        if (ReferenceEquals(batsmen, book))
            return;
        book = batsmen;
        var gone = new List<Batsman>();
        foreach (Batsman b in outs.Keys)
            if (batsmen == null || !batsmen.Contains(b))
                gone.Add(b);
        foreach (Batsman b in gone)
            outs.Remove(b);
    }

    /// The card as the scorebook stands now.
    public BattingCard Build(HUD hud, string team)
    {
        UseBook(hud.Batsmen);
        return BattingCard.Build(team, hud.Batsmen, hud.CurrentBatsman, hud.CurrentRunner, outs,
                                 hud.Runs, hud.Wickets, hud.Overs, hud.Balls);
    }

    private void OnBatterOut(Batsman b, string dismissal) => Record(b, dismissal);
    private void OnStrikeChanged(Batsman b) => OnChanged();
    private void OnBallScored(BallOutcome o) => OnChanged();
    private void OnChanged() => Changed?.Invoke();
}
