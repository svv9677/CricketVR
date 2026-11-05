using TMPro;
using UnityEngine;

/// <summary>
/// The score hut's board at the club ground: total, wickets, overs, the target and the two batters,
/// as the hut's plates would show them (ClubScoreboardFigures). One rich-text string on one TMP
/// object, so the whole board is a single draw.
///
/// It reads HUD (the match's scorebook, which keeps scoring with the stadium's scorecard hidden) and
/// MatchFlow.Target five times a second rather than listening for events: the career's simulated
/// balls change the score without a delivery, and polling sees those too. The text is set only when
/// the figures change, so TMP rebuilds its mesh a few times an over, not every poll.
/// </summary>
public class ClubScoreboard : MonoBehaviour
{
    [Tooltip("The board's figures: one TMP text, filled in from the match's score.")]
    [SerializeField] private TMP_Text figures;

    private const float Interval = 0.2f;
    private float nextPoll;
    private BoardState shown;
    private bool hasShown;

    private void Update()
    {
        if (figures == null || Time.unscaledTime < nextPoll)
            return;
        nextPoll = Time.unscaledTime + Interval;
        HUD hud = HUD.Instance;
        if (hud == null)
            return;
        BoardState now = Read(hud);
        if (hasShown && now.Equals(shown))
            return;
        shown = now;
        hasShown = true;
        figures.SetText(ClubScoreboardFigures.Compose(now));
    }

    /// The board as the scorebook stands: HUD's totals and its striker (CurrentBatsman) and the
    /// batter at the other end (CurrentRunner), by their place in the batting order.
    private static BoardState Read(HUD hud)
    {
        int strikerIndex = Index(hud, hud.CurrentBatsman), otherIndex = Index(hud, hud.CurrentRunner);
        int strikerRuns = hud.CurrentBatsman != null ? hud.CurrentBatsman.Runs : 0;
        int otherRuns = hud.CurrentRunner != null ? hud.CurrentRunner.Runs : 0;
        return ClubScoreboardFigures.From(hud.Runs, hud.Wickets, hud.Overs, hud.Balls, MatchFlow.Target,
                                          strikerIndex, strikerRuns, otherIndex, otherRuns);
    }

    private static int Index(HUD hud, Batsman b) => b != null && hud.Batsmen != null ? hud.Batsmen.IndexOf(b) : -1;
}
