using TMPro;
using UnityEngine;

/// <summary>
/// The broadcast graphics over the replay: a "REPLAY" tag at the top left, and the score bar along
/// the bottom - the batting side's score and overs, both batters' runs (balls), the bowler and his
/// figures, and the delivery's speed. A screen-space canvas on the replay camera, so it is drawn
/// into the replay's picture and nowhere else; its text changes once per replay, not per frame.
/// The canvas, bars and texts are the ReplayOverlay prefab (CricketVR > Scene Objects > Place Replay
/// Overlay puts it under the replay camera in Main); until 2026-11-15 this class built them in code.
/// </summary>
public sealed class ReplayOverlay : MonoBehaviour
{
    /// The batting side's name on the score bar. A career match puts the player's own side's name
    /// here for the match and puts this back afterwards.
    public static string Team = "INDIA";

    [Tooltip("The overlay's canvas. Off until a replay shows.")]
    [SerializeField]
    private Canvas canvas;
    [SerializeField]
    private TMP_Text score, batters, bowler;

    /// The scorecard as it stands now, and this delivery's speed (km/h, 0 if unknown).
    public void Show(float speedKmh)
    {
        HUD hud = HUD.Instance;
        if (hud != null && hud.CurrentBatsman != null)
        {
            score.text = $"<size=60%>{Team}</size>  <b>{hud.Runs}/{hud.Wickets}</b>\n<size=55%>{hud.Overs}.{hud.Balls} OVERS   RR {hud.RunRate:F2}</size>";
            batters.text = $"<b>{hud.CurrentBatsman.Name}*</b><pos=62%>{HUD.RunsAndBalls(hud.CurrentBatsman)}\n" +
                           $"<color=#B3BDD1>{hud.CurrentRunner.Name}<pos=62%>{HUD.RunsAndBalls(hud.CurrentRunner)}</color>";
            string speed = speedKmh > 1f ? $"{Mathf.RoundToInt(speedKmh)} km/h" : "";
            bowler.text = $"<b>{hud.CurrentBowler.Name}</b><pos=62%>{HUD.Figures(hud.CurrentBowler)}\n<color=#B3BDD1>{speed}</color>";
        }
        canvas.enabled = true;
    }

    public void Hide() => canvas.enabled = false;
}
