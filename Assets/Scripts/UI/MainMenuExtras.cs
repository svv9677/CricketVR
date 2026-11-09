using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// What the main menu adds beside Rao's MainMenuScreen (Quick Match, Settings), on the same
/// screen object, so his screen is used as it is: under Quick Match, the match it will start
/// (overs, bowling, pitch, who bats first); buttons to Match Setup, Customise, Career and the last
/// result; and the last match as a small scorecard - won or lost, both sides' scores. Coming home
/// from a match, its result is shown straight away (MatchResultScreen); coming home from a career
/// session in the nets, the career hub is (NetsSession.ReturnToHub); from a career match, its
/// result over the hub (CareerMatchSession.ReturnToHub, CareerResultScreen); from the replay
/// theatre, the Records screen (ReplayTheatre.ReturnToRecords). Built by Tools > CricketVR > Build
/// Menu Prefabs.
/// </summary>
public class MainMenuExtras : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lastMatch;
    [SerializeField] private TextMeshProUGUI lastDetail;
    [SerializeField] private TextMeshProUGUI setup;

    private MenuScreen screen;

    private void Awake() => screen = GetComponent<MenuScreen>();

    private void OnEnable()
    {
        Refresh();
        // Back from a career match comes first of all: its result over the hub (the same rules
        // for the flag as the nets', below).
        if (CareerMatchSession.ReturnToHub)
        {
            if (CareerSaves.Current != null)
            {
                StartCoroutine(ShowCareerResult());
                return;
            }
            CareerMatchSession.ReturnToHub = false;
        }
        // Back from the replay theatre (phase 7): the Records screen it was opened from, with the
        // same rules for the flag as the nets', below.
        if (ReplayTheatre.ReturnToRecords)
        {
            if (CareerSaves.Current != null)
            {
                StartCoroutine(ShowCareerRecords());
                return;
            }
            ReplayTheatre.ReturnToRecords = false;
        }
        // Back from a career session in the nets: straight to the career hub, ahead of any match
        // result. The flag is only cleared once the hub is actually pushed (ShowCareerHub): this
        // screen can be switched on and off again while the menu sets itself up, which stops the
        // coroutine, and the next OnEnable must still see the flag. With no career loaded the
        // flag is stale, so it is dropped here and cannot fire on a later, unrelated return.
        // A pending match result is put off, not discarded: it shows the next time this screen
        // opens.
        if (NetsSession.ReturnToHub)
        {
            if (CareerSaves.Current != null)
            {
                StartCoroutine(ShowCareerHub());
                return;
            }
            NetsSession.ReturnToHub = false;
        }
        // The results page is gone (2026-11-06: "the results page is completely blank... just take
        // it off"): the match's own card says how it went; the last match is on this screen.
        if (MatchHistory.ResultPending)
            MatchHistory.ResultPending = false;
    }

    private void Update()
    {
        // The overs stepper on Rao's Quick Match row changes the match under it.
        if (Time.frameCount % 15 == 0)
            RefreshSetup();
    }

    private void Refresh()
    {
        RefreshSetup();
        MatchHistory.Record r = MatchHistory.Latest;
        if (lastMatch != null)
            lastMatch.text = r == null ? "<color=#98A2B3>No matches played yet</color>"
                : $"<color=#98A2B3>LAST MATCH</color>   {(r.Won ? "<color=#22C55E>" : r.tied || r.drawn ? "<color=#3B82F6>" : r.hasAI || r.target > 0 || r.test ? "<color=#FF6B4A>" : "<color=#E5E7EB>")}{r.result}</color>";
        if (lastDetail != null)
            lastDetail.text = r == null ? ""
                : r.test ? $"Test   ·   {r.summary}   <color=#98A2B3>{r.when}</color>"
                : $"You {r.Score} ({r.OversText} ov)" + (r.hasAI ? $"   ·   AI {r.AIScore} ({r.AIOversText} ov)" : "") + $"   <color=#98A2B3>{r.when}</color>";
    }

    private void RefreshSetup()
    {
        if (setup == null)
            return;
        string who = MatchSetup.Type == eMatchType.Chase ? "AI bats first, you chase" : "You bat first, AI chases";
        setup.text = $"{MatchFormat.Label()} · {MatchSetup.Name(MatchSetup.Bowling)} bowling · " +
                     $"{MatchSetup.Name(MatchSetup.Pitch)} pitch · {who}";
    }

    /// After the menu has finished opening (this runs as the main screen is switched on).
    private IEnumerator ShowResult()
    {
        yield return null;
        Push("result");
    }

    /// The hub over the slots screen, so Back from the hub goes to the slots, then here.
    private IEnumerator ShowCareerHub()
    {
        yield return null;
        NetsSession.ReturnToHub = false;
        Push("career");
        Push("careerHub");
    }

    /// The career match's result over the hub over the slots: Continue goes to the hub, Back from
    /// there to the slots, then here. A pending quick-match result can only be this same match's
    /// (the match scene's MatchFlow.End adds every match it plays to MatchHistory, and
    /// MatchResultScreen clears the flag whenever it is shown, so an older one was already seen
    /// on the way into the career), so it is dropped rather than put off: the career result is
    /// this match's scorecard. A nets return asked for earlier is met by the hub this opens.
    private IEnumerator ShowCareerResult()
    {
        yield return null;
        CareerMatchSession.ReturnToHub = false;
        NetsSession.ReturnToHub = false;
        if (MatchHistory.ResultPending)
            MatchHistory.ResultPending = false;
        Push("career");
        Push("careerHub");
        Push("careerResult");
    }

    /// Records over Ratings over the hub over the slots, so Back walks the way he came. The theatre
    /// plays no match into MatchHistory, so a pending result is an older one, already seen on the
    /// way into the career (as in ShowCareerResult): dropped.
    private IEnumerator ShowCareerRecords()
    {
        yield return null;
        ReplayTheatre.ReturnToRecords = false;
        if (MatchHistory.ResultPending)
            MatchHistory.ResultPending = false;
        Push("career");
        Push("careerHub");
        Push("careerRatings");
        Push("careerRecords");
    }

    private void Push(string name)
    {
        if (screen != null && screen.Root != null)
            screen.Root.Push(name);
    }

    /// The main menu's Quick Play (2026-11-09: "one of the screens could be quick play, one ...
    /// career mode, and one ... settings"): the slots, one per match length.
    public void OnQuickPlay() => Push("quickplay");
    public void OnMatchSetup() => Push("setup");
    public void OnCustomise() => Push("customise");
    public void OnCareer() => Push("career");
    public void OnResults() => Push("result");
}
