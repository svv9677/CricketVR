using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The result of the match just played, shown in the Splash menu when the player comes home
/// from one (MainMenuExtras pushes it): the score, the result, the boundaries and dots, the
/// conditions; the AI's innings (AIInnings) - its chase of the player's total played out over by
/// over, the asking rate with it, and the result only once it is done; or the total it set
/// batting first - and the last few matches. Play Again starts another with the same setup.
/// Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class MatchResultScreen : MenuScreen
{
    [SerializeField] private TextMeshProUGUI heading;
    [SerializeField] private TextMeshProUGUI score;
    [SerializeField] private TextMeshProUGUI result;
    [SerializeField] private TextMeshProUGUI details;
    [SerializeField] private TextMeshProUGUI aiInnings;
    [SerializeField] private TextMeshProUGUI history;

    private const int HistoryLines = 3;
    /// Seconds per over as the AI's chase is played out, and before it starts.
    private const float PerOver = 0.45f, BeforeChase = 0.8f;
    private const string Muted = "#98A2B3", Good = "#22C55E", Bad = "#FF6B4A", Blue = "#3B82F6";

    public override void OnShow()
    {
        StopAllCoroutines();
        MatchHistory.ResultPending = false;
        MatchHistory.Record r = MatchHistory.Latest;
        if (r == null)
        {
            Set(heading, "No matches yet");
            Set(score, "");
            Set(result, "Play a match and your scorecard will be here.");
            Set(details, "");
            Set(aiInnings, "");
            Set(history, "");
            return;
        }
        Set(details, $"Run rate {r.RunRate:0.00}   Fours {r.fours}   Sixes {r.sixes}   Dots {r.dots}   Wides {r.wides}\n" +
                     $"<color={Muted}>{MatchSetup.Name((eBowlingLevel)r.bowling)} bowling · {MatchSetup.Name((ePitch)r.pitch)} pitch · {r.when}</color>");
        Set(history, History());
        if (r.hasAI && r.aiChased && isActiveAndEnabled)
        {
            StartCoroutine(PlayChase(r));
            return;
        }
        Final(r);
        if (r.hasAI)
            Set(aiInnings, $"<color={Muted}>AI batted first</color>  <b>{r.AIScore}</b> <color={Muted}>({r.AIOversText} ov)</color>\n" + OverRow(r, r.aiOverRuns.Count));
        else
            Set(aiInnings, "");
    }

    private void Final(MatchHistory.Record r)
    {
        Set(heading, r.Won ? $"<color={Good}>YOU WON</color>" : r.tied ? $"<color={Blue}>MATCH TIED</color>"
                   : r.hasAI || r.target > 0 ? $"<color={Bad}>MATCH LOST</color>" : "INNINGS COMPLETE");
        Set(score, YourScore(r) + (r.hasAI && r.aiChased ? $"<size=55%>  <color={Muted}>v</color> AI {r.AIScore} <color={Muted}>({r.AIOversText})</color></size>" : ""));
        Set(result, r.target > 0 ? $"{r.result}  <color={Muted}>· target {r.target}</color>" : r.result);
    }

    private static string YourScore(MatchHistory.Record r) =>
        $"{r.Score}<size=55%> <color={Muted}>({r.OversText} ov)</color></size>";

    /// The AI's chase, over by over, then the result.
    private IEnumerator PlayChase(MatchHistory.Record r)
    {
        int target = r.aiTarget > 0 ? r.aiTarget : r.runs + 1;
        Set(heading, $"<color={Blue}>THE AI CHASES {target}</color>");
        Set(score, YourScore(r));
        Set(result, $"<color={Muted}>Your innings is done. Can they get there?</color>");
        Set(aiInnings, $"AI  <b>0/0</b>  <color={Muted}>· need {target} off {r.overs * 6} · RRR {target / Mathf.Max(1f, r.overs):0.0}</color>");
        yield return new WaitForSeconds(BeforeChase);
        int runs = 0, wickets = 0;
        for (int o = 0; o < r.aiOverRuns.Count; o++)
        {
            runs += r.aiOverRuns[o];
            wickets += o < r.aiOverWickets.Count ? r.aiOverWickets[o] : 0;
            bool last = o == r.aiOverRuns.Count - 1;
            int ballsGone = last ? r.aiBalls : (o + 1) * 6;
            int need = Mathf.Max(0, target - runs), left = r.overs * 6 - ballsGone;
            string state = need == 0 ? $"<color={Bad}>got there</color>"
                         : (left <= 0 || wickets >= 10) && need == 1 ? $"<color={Blue}>scores level</color>"
                         : left <= 0 || wickets >= 10 ? $"<color={Good}>fell {need - 1} short</color>"
                         : $"<color={Muted}>need {need} off {left} · RRR {need * 6f / left:0.0}</color>";
            Set(aiInnings, $"AI  <b>{runs}/{wickets}</b> <color={Muted}>({ballsGone / 6}.{ballsGone % 6} ov)</color>  {state}\n" + OverRow(r, o + 1));
            yield return new WaitForSeconds(PerOver);
        }
        Final(r);
    }

    /// The AI's overs so far as runs, a wicket in an over marked in red.
    private static string OverRow(MatchHistory.Record r, int upTo)
    {
        var sb = new StringBuilder($"<size=80%><color={Muted}>Overs</color>  ");
        for (int i = 0; i < upTo && i < r.aiOverRuns.Count; i++)
        {
            int w = i < r.aiOverWickets.Count ? r.aiOverWickets[i] : 0;
            sb.Append(w > 0 ? $"<color={Bad}>{r.aiOverRuns[i]}{new string('•', Mathf.Min(w, 3))}</color>" : r.aiOverRuns[i].ToString()).Append("  ");
        }
        return sb.Append("</size>").ToString();
    }

    private static string History()
    {
        var sb = new StringBuilder();
        var all = MatchHistory.All;
        for (int i = 1; i < all.Count && i <= HistoryLines; i++)
        {
            MatchHistory.Record h = all[i];
            string tag = h.Won ? $"<color={Good}>W</color>" : h.tied ? $"<color={Blue}>T</color>" : h.hasAI || h.target > 0 ? $"<color={Bad}>L</color>" : "-";
            sb.Append(i == 1 ? $"<color={Muted}>Earlier</color>\n" : "").Append(tag).Append("  ").Append(MatchHistory.Line(h)).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    private static void Set(TextMeshProUGUI t, string s)
    {
        if (t != null) t.text = s;
    }

    public void OnPlayAgain() => SceneFader.Load(MainMenuScreen.CricketScene);
}
