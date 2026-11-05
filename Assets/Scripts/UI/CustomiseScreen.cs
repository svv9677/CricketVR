using TMPro;
using UnityEngine;

/// <summary>
/// Customise, from the Splash main menu: the ground for the next match - the crowd and the pitch
/// (StadiumConditions puts them on the ground when the match loads). Built by
/// Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CustomiseScreen : MenuScreen
{
    [SerializeField] private SegmentedControl crowd;
    [SerializeField] private SegmentedControl pitch;
    [SerializeField] private TextMeshProUGUI pitchHint;

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        if (crowd != null) crowd.SetValueWithoutNotify((int)MatchSetup.Crowd);
        if (pitch != null) pitch.SetValueWithoutNotify((int)MatchSetup.Pitch);
        if (pitchHint != null) pitchHint.text = MatchSetup.Hint(MatchSetup.Pitch);
    }

    public void OnCrowd(int i) { MatchSetup.Crowd = (eCrowd)i; Refresh(); }
    public void OnPitch(int i) { MatchSetup.Pitch = (ePitch)i; Refresh(); }
}
