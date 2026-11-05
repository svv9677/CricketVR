using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// What the main menu adds beside Rao's MainMenuScreen (Quick Match, Settings), on the same
/// screen object, so his screen is used as it is: under Quick Match, the match it will start
/// (overs, bowling, pitch, who bats first); buttons to Match Setup, Customise, Career and the last
/// result; and the last match as a small scorecard - won or lost, both sides' scores. Coming home
/// from a match, its result is shown straight away (MatchResultScreen); coming home from a career
/// session in the nets, the career hub is (NetsSession.ReturnToHub). Built by Tools > CricketVR
/// > Build Menu Prefabs.
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
        if (MatchHistory.ResultPending)
            StartCoroutine(ShowResult());
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
                : $"<color=#98A2B3>LAST MATCH</color>   {(r.Won ? "<color=#22C55E>" : r.tied ? "<color=#3B82F6>" : r.hasAI || r.target > 0 ? "<color=#FF6B4A>" : "<color=#E5E7EB>")}{r.result}</color>";
        if (lastDetail != null)
            lastDetail.text = r == null ? ""
                : $"You {r.Score} ({r.OversText} ov)" + (r.hasAI ? $"   ·   AI {r.AIScore} ({r.AIOversText} ov)" : "") + $"   <color=#98A2B3>{r.when}</color>";
    }

    private void RefreshSetup()
    {
        if (setup == null)
            return;
        string who = MatchSetup.Type == eMatchType.Chase ? "AI bats first, you chase" : "You bat first, AI chases";
        int overs = GameSettings.Overs;
        setup.text = $"{overs} over{(overs == 1 ? "" : "s")} · {MatchSetup.Name(MatchSetup.Bowling)} bowling · " +
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

    private void Push(string name)
    {
        if (screen != null && screen.Root != null)
            screen.Root.Push(name);
    }

    public void OnMatchSetup() => Push("setup");
    public void OnCustomise() => Push("customise");
    public void OnCareer() => Push("career");
    public void OnResults() => Push("result");
}
