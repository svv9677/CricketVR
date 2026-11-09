using TMPro;
using UnityEngine;

/// <summary>
/// Before a career match, one thing at a time (2026-11-06: "I just need to know what the match is
/// against and then have everything sequential... not too much information on the same page").
/// The hub says only who the match is against (and a scout, when one is coming); Play match opens
/// this, two short steps on one floating panel:
///
///   1  The match     who against, the round and the overs, the ground and the time of day, and
///                    any situation it is played in (rain, a chase) - Next.
///   2  Your innings  where you bat, and whether to watch your partner's innings - Play match.
///
/// Back on the second step goes to the first; on the first, to the hub (the menu's own Back).
/// Built by CareerDeckPrematch.
/// </summary>
public class CareerPrematchScreen : CareerDeckScreen
{
    public const string Name = "careerPrematch";

    [SerializeField] private GameObject stepMatch;
    [SerializeField] private GameObject stepInnings;
    [SerializeField] private TextMeshProUGUI opponent;
    [SerializeField] private TextMeshProUGUI matchLine;
    [SerializeField] private TextMeshProUGUI placeLine;
    [SerializeField] private TextMeshProUGUI situationLine;
    [SerializeField] private TextMeshProUGUI slotLine;
    [SerializeField] private SegmentedControl watchPartner;

    public override void OnShow()
    {
        base.OnShow();
        ShowStep(1);
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        Season s = p?.season;
        Fixture next = p != null ? CareerProgress.NextFixture(p) : null;
        if (s == null || next == null)
            return;
        MatchPlan preview = CareerProgress.PlanNext(p, CareerSituations.PlanRng(p));
        Set(opponent, $"vs {CareerHubScreen.TeamName(s, next.Opponent(s.yourTeam))}");
        Set(matchLine, MatchLine(next, CareerHubScreen.Rounds(s), preview != null ? preview.overs : Ladder.Overs(s.rung)));
        Set(placeLine, $"{CareerHubScreen.VenueName(Ladder.Venue(s.rung))}  ·  {(next.night ? "Night" : "Afternoon")}");
        string situation = preview != null && CareerSituations.Of(next) != eSituation.Normal
            ? CareerSituationText.HubLine(preview, preview.slot + 1) : "";
        if (situationLine != null)
        {
            situationLine.gameObject.SetActive(!string.IsNullOrEmpty(situation));
            situationLine.text = situation;
        }
        Set(slotLine, CareerHubScreen.SlotLine(preview != null ? preview.slot + 1 : p.battingSlot, next.final));
        if (watchPartner != null)
            watchPartner.SetValueWithoutNotify(CareerHubScreen.WatchIndex(p.watchPartner));
    }

    /// "Final · 20 overs", "Round 3 of 7 · 6 overs".
    public static string MatchLine(Fixture f, int rounds, int overs)
    {
        string when = f.final ? "Final" : rounds > 0 ? $"Round {f.round} of {rounds}" : $"Round {f.round}";
        return $"{when}  ·  {overs} overs";
    }

    private void ShowStep(int step)
    {
        if (stepMatch != null) stepMatch.SetActive(step == 1);
        if (stepInnings != null) stepInnings.SetActive(step == 2);
    }

    private static void Set(TextMeshProUGUI t, string s)
    {
        if (t != null) t.text = s;
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    public void OnNext() => ShowStep(2);

    public void OnBackStep() => ShowStep(1);

    public void OnWatchPartner(int index)
    {
        CareerProfile p = CareerSaves.Current;
        bool watch = index == 0;
        if (p == null || p.watchPartner == watch)
            return;
        p.watchPartner = watch;
        CareerSaves.SaveCurrent();
    }

    public void OnPlay() => CareerHubScreen.PlayNextMatch();
}
