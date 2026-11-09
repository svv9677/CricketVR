using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A partner's balls while the career player is not watching, shown ball by ball (2026-11-06:
/// "instead of just saying after this many overs is the result, show me ball and then the result,
/// and keep adding up until it's my turn again"). They used to be simulated all at once with one
/// summary card; now one is simulated every BallSeconds and its line added to the card - "6.3
/// Ravi 1 run" - the newest few showing over the score, until the career player is on strike (or
/// the innings is over). Then the card stays up a moment with how the partner stands, as before.
/// Each ball is CareerSim's, scored by HUD (SimulateOne), exactly as the batch was. A skips the
/// rest: every ball left at once, straight to the closing card ("You come in at 45/4").
/// The rest of an innings after he is out is still simulated at once (FinishInnings).
/// </summary>
public partial class CareerMatch
{
    /// Seconds between simulated balls on the card.
    public const float BallSeconds = 0.7f;

    private Batch stepBatch;
    private readonly List<string> ballLines = new List<string>();
    private float nextBallAt;
    private int stepCount, stepCap;
    private bool stepWasIn;

    /// A partner is on strike: his balls begin, one at a time (StepBallByBall).
    private void CatchUp(HUD hud)
    {
        stepBatch = new Batch();
        ballLines.Clear();
        stepCount = 0;
        stepCap = CareerMatchLogic.BatchCap(hud.TotalOvers * 6, Bowled(hud));
        stepWasIn = YouAtCrease(hud);
        nextBallAt = Time.time + BallSeconds;
        stepStartedAt = Time.time;
        Hold(true);
        ShowSteps(hud);
    }

    /// While a partner's balls are being shown: the next one when it is due, the closing card when
    /// they are done. True while it has the game (Update does nothing else).
    private bool StepBallByBall(HUD hud)
    {
        if (stepBatch == null)
            return false;
        // Not on under the pause menu: the next ball waits for it to close.
        if (MenuRoot.Instance != null && MenuRoot.Instance.IsOpen)
        {
            nextBallAt = Mathf.Max(nextBallAt, Time.time + BallSeconds);
            return true;
        }
        // A: skip the rest - to when he comes in, or to his strike (2026-11-06: "add an option to
        // skip to when I come in so that I don't have to wait through everyone").
        if (main != null && main.GetButton(XRButton.A) && Time.time - stepStartedAt > SkipAfter)
        {
            SkipSteps(hud);
            return true;
        }
        if (Time.time < nextBallAt)
            return true;
        if (CareerMatchLogic.StopBatch(YourTurn(hud), youOut, InningsOver(hud), stepCount, stepCap) || hud.CurrentBatsman == null)
        {
            EndSteps(hud);
            return true;
        }
        StepOne(hud);
        nextBallAt = Time.time + BallSeconds;
        ShowSteps(hud);
        return true;
    }

    /// Seconds into the list before A skips (the A that started the last ball must not).
    private const float SkipAfter = 0.5f;
    private float stepStartedAt;

    /// Every ball left at once, then the closing card.
    private void SkipSteps(HUD hud)
    {
        while (!CareerMatchLogic.StopBatch(YourTurn(hud), youOut, InningsOver(hud), stepCount, stepCap) && hud.CurrentBatsman != null)
            StepOne(hud);
        EndSteps(hud);
    }

    private void StepOne(HUD hud)
    {
        Batsman b = hud.CurrentBatsman;
        int entries = hud.BowledBalls != null ? hud.BowledBalls.Count : 0;
        SimulateOne(hud, stepBatch);
        stepCount++;
        if (!stepWasIn && YouAtCrease(hud))
        {
            stepWasIn = stepBatch.cameIn = true;
            stepBatch.inRuns = hud.Runs; stepBatch.inWickets = hud.Wickets; stepBatch.inBalls = Bowled(hud);
        }
        string entry = hud.BowledBalls != null && hud.BowledBalls.Count > entries ? hud.BowledBalls[hud.BowledBalls.Count - 1] : "*";
        string how = entry == "W" && outs.TryGetValue(b, out string h) ? h : null;
        string number = CareerMatchLogic.BallNumber(Bowled(hud), CareerMatchLogic.IsLegal(entry));
        ballLines.Add(CareerMatchLogic.BallLine(number, b.Name, entry, how));
    }

    private void ShowSteps(HUD hud)
    {
        string heading = stepWasIn ? CareerMatchLogic.AtTheOtherEnd : "Before you come in";
        string skip = stepWasIn ? "A: skip to your strike" : "A: skip to when you come in";
        ui.Show(heading, CareerMatchLogic.BallByBallBody(ballLines, new[] { $"<size=70%><color=#98A2B3>{skip}</color></size>" },
                                                         hud.Runs, hud.Wickets, Bowled(hud)));
    }

    /// The partner's balls are done: how he stands and whose over is next, held a moment.
    private void EndSteps(HUD hud)
    {
        Batch batch = stepBatch;
        stepBatch = null;
        bool over = InningsOver(hud);
        if (stepCount == 0)
        {
            ui.Hide();
            Hold(false);
            if (over)
                MatchFlow.CheckFinished();
            return;
        }
        MatchFlow.RefreshStatus();   // the "Need 18 off 18" line, now, not after his next ball
        Debug.Log($"[CareerMatch] simulated {stepCount} ball{(stepCount == 1 ? "" : "s")} ball by ball: {hud.Runs}/{hud.Wickets} after {CareerMatchLogic.OversText(Bowled(hud))}");
        var closing = new List<string>();
        Batsman partner = StrikerIsYou(hud) ? hud.CurrentRunner : null;
        if (partner != null && !partner.IsHuman && partner.Balls > 0 && !batch.fell.Contains(partner))
            closing.Add(CareerMatchLogic.BatterText(partner.Name, partner.Runs, partner.Balls, false, null));
        // Simulated balls change no game state, so OverBreak never announces their new over: the card does.
        if (!over && hud.Balls == 0 && Bowled(hud) > 0 && hud.CurrentBowler != null)
            closing.Add(CareerMatchLogic.NextOver(hud.CurrentBowler.Name));
        string heading = batch.cameIn ? CareerMatchLogic.ComeIn(batch.inRuns, batch.inWickets, batch.inBalls) : CareerMatchLogic.AtTheOtherEnd;
        ShowCard(heading, CareerMatchLogic.BallByBallBody(ballLines, closing, hud.Runs, hud.Wickets, Bowled(hud)),
                 batch.cameIn ? ComeInSeconds : CatchUpSeconds, over);
    }
}
