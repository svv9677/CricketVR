using TMPro;
using UnityEngine;

/// <summary>
/// Customise, from the Splash main menu: the ground for the next match - the crowd, the pitch and
/// the time of day (StadiumConditions and StadiumLighting put them on the ground when it loads). Built by
/// Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CustomiseScreen : MenuScreen
{
    [SerializeField] private SegmentedControl crowd;
    [SerializeField] private SegmentedControl pitch;
    [SerializeField] private TextMeshProUGUI pitchHint;
    [SerializeField] private SegmentedControl timeOfDay;
    [SerializeField] private TextMeshProUGUI timeHint;

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        if (crowd != null) crowd.SetValueWithoutNotify((int)MatchSetup.Crowd);
        if (pitch != null) pitch.SetValueWithoutNotify((int)MatchSetup.Pitch);
        if (pitchHint != null) pitchHint.text = MatchSetup.Hint(MatchSetup.Pitch);
        if (timeOfDay != null) timeOfDay.SetValueWithoutNotify((int)MatchSetup.TimeOfDay);
        if (timeHint != null) timeHint.text = StadiumLighting.Hint(MatchSetup.TimeOfDay);
    }

    public void OnCrowd(int i) { MatchSetup.Crowd = (eCrowd)i; Refresh(); }
    public void OnPitch(int i) { MatchSetup.Pitch = (ePitch)i; Refresh(); }
    public void OnTimeOfDay(int i) { MatchSetup.TimeOfDay = (eTimeOfDay)i; Refresh(); }
}
