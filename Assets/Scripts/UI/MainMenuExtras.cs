using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// What the main menu adds beside Rao's MainMenuScreen (Quick Match, Settings), on the same
/// screen object, so his screen is used as it is: under Quick Match, the match it will start
/// (overs, bowling, pitch, who bats first); buttons to Match Setup, Customise, Career and the last
/// result; and the last match as a small scorecard - won or lost, both sides' scores. Coming home
/// from a match, its result is shown straight away (MatchResultScreen). Built by Tools > CricketVR
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
