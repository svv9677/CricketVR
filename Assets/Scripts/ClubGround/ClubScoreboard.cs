using TMPro;
using UnityEngine;

/// <summary>
/// The score hut's board at the club ground: the home side's name across the top (header), and
/// under it the total, wickets and overs, the two batters, the last wicket and, when chasing, the
/// target and what is still needed, each figure on its tin (ClubScoreboardFigures, laid out by
/// ClubScoreboardLayout). The labels and figures are one rich-text string on one TMP object, so
/// the board costs two draws in all, the header and the rest.
///
/// It reads HUD (the match's scorebook, which keeps scoring with the stadium's scorecard hidden) and
/// MatchFlow.Target five times a second rather than listening for events: the career's simulated
/// balls change the score without a delivery, and polling sees those too. The text is set only when
/// the figures change, so TMP rebuilds its mesh a few times an over, not every poll.
///
/// The scorebook keeps no fall of wickets, so the board works out the last one each poll from its
/// ball list (ClubScoreboardFigures.LastWicket): the total less the runs since the last "W". That
/// stays right when a batch of simulated balls scores runs after a wicket in the same frame, which
/// the total at the poll would not.
/// </summary>
public class ClubScoreboard : MonoBehaviour
{
    [Tooltip("The board's labels and figures: one TMP text, filled in from the match's score.")]
    [SerializeField] private TMP_Text figures;
    [Tooltip("The home side's name across the top of the board.")]
    [SerializeField] private TMP_Text header;

    private const float Interval = 0.2f;
    private float nextPoll;
    private BoardState shown;
    private bool hasShown;

    /// The name across the top of the board, in capitals as signwritten (ClubGround.SetClubName).
    public void SetHomeName(string name)
    {
        if (header != null)
            header.SetText(string.IsNullOrWhiteSpace(name) ? "" : name.Trim().ToUpperInvariant());
    }

    private void Update()
    {
        if (figures == null || Time.unscaledTime < nextPoll)
            return;
        nextPoll = Time.unscaledTime + Interval;
        HUD hud = HUD.Instance;
        if (hud == null)
            return;
        BoardState now = Read(hud, ClubScoreboardFigures.LastWicket(hud.BowledBalls, hud.Runs, hud.Wickets));
        if (hasShown && now.Equals(shown))
            return;
        shown = now;
        hasShown = true;
        figures.SetText(ClubScoreboardFigures.Compose(now));
    }

    /// The board as the scorebook stands: HUD's totals and its striker (CurrentBatsman) and the
    /// batter at the other end (CurrentRunner), by their place in the batting order.
    private static BoardState Read(HUD hud, int lastWicket)
    {
        int strikerIndex = Index(hud, hud.CurrentBatsman), otherIndex = Index(hud, hud.CurrentRunner);
        int strikerRuns = hud.CurrentBatsman != null ? hud.CurrentBatsman.Runs : 0;
        int otherRuns = hud.CurrentRunner != null ? hud.CurrentRunner.Runs : 0;
        return ClubScoreboardFigures.From(hud.Runs, hud.Wickets, hud.Overs, hud.Balls, MatchFlow.Target,
                                          strikerIndex, strikerRuns, otherIndex, otherRuns, lastWicket);
    }

    private static int Index(HUD hud, Batsman b) => b != null && hud.Batsmen != null ? hud.Batsmen.IndexOf(b) : -1;
}
