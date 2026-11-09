using TMPro;
using UnityEngine;

/// <summary>
/// The ground for the match - the stadium or the club ground (ClubGround), the crowd, the pitch and
/// the time of day (StadiumConditions and StadiumLighting put them on the ground when it loads).
/// Since 2026-11-09 it is step 2 of Quick Play's New Game ("those customize and setup menus won't
/// be separate menus anymore"): Start Match makes the two steps the slot's setup and plays it
/// (QuickPlaySlots.StartNew). Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CustomiseScreen : MenuScreen
{
    [SerializeField] private SegmentedControl venue;
    [SerializeField] private SegmentedControl crowd;
    [SerializeField] private SegmentedControl pitch;
    [SerializeField] private TextMeshProUGUI pitchHint;
    [SerializeField] private SegmentedControl timeOfDay;
    [SerializeField] private TextMeshProUGUI timeHint;
    [SerializeField] private TextMeshProUGUI stepTitle;

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        if (venue != null) venue.SetValueWithoutNotify(VenueRules.QuickIndex(MatchSetup.Venue));
        if (crowd != null) crowd.SetValueWithoutNotify((int)MatchSetup.Crowd);
        if (pitch != null) pitch.SetValueWithoutNotify((int)MatchSetup.Pitch);
        if (pitchHint != null) pitchHint.text = MatchSetup.Hint(MatchSetup.Pitch);
        if (timeOfDay != null) timeOfDay.SetValueWithoutNotify((int)MatchSetup.TimeOfDay);
        if (timeHint != null) timeHint.text = StadiumLighting.Hint(MatchSetup.TimeOfDay);
        if (stepTitle != null) stepTitle.text = $"New {MatchFormat.MatchName(MatchFormat.Current)}";
    }

    public void OnStartMatch() => QuickPlaySlots.StartNew();

    public void OnVenue(int i) { MatchSetup.Venue = VenueRules.FromQuickIndex(i); Refresh(); }
    public void OnCrowd(int i) { MatchSetup.Crowd = (eCrowd)i; Refresh(); }
    public void OnPitch(int i) { MatchSetup.Pitch = (ePitch)i; Refresh(); }
    public void OnTimeOfDay(int i) { MatchSetup.TimeOfDay = (eTimeOfDay)i; Refresh(); }
}
