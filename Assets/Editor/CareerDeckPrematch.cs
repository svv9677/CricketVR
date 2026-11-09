using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pre-match steps (CareerPrematchScreen) on one floating panel in the middle, one step showing
/// at a time, no tab bar (it is a sequence, not a place):
///
///   THE MATCH                     YOUR INNINGS
///   vs Marston Green CC           You bat at 2
///   Round 7 of 7 · 4 overs        Partner's innings  [Watch | Skip]
///   Club ground · Afternoon       [ Play match ]
///   RAIN · 35 to win off 4        Back
///   [ Next ]
/// </summary>
public static class CareerDeckPrematch
{
    public static MenuScreen Build(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "CareerPrematchScreen");
        CareerPrematchScreen screen = go.AddComponent<CareerPrematchScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerPrematchScreen.Name);
        var so = new SerializedObject(screen);
        Transform panel = CareerDeckBuilder.Panel(go, "Pre-match", CareerDeckBuilder.Slot.Centre);
        Match(Step(panel, "Step The Match"), screen, so);
        Innings(Step(panel, "Step Your Innings"), screen, so);
        so.ApplyModifiedProperties();
        return screen;
    }

    private static Transform Step(Transform panel, string name)
    {
        Transform step = WorldPanelBuilder.Column(panel, true, UIStyle.Gap);
        step.name = name;
        return step;
    }

    private static void Match(Transform step, CareerPrematchScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Bind(so, "stepMatch", step.gameObject);
        CareerDeckBuilder.Heading(step, "The match");
        TextMeshProUGUI title = WorldPanelBuilder.Line(step, "", UIStyle.TitleSize + 8f, FontStyles.Bold, UIStyle.Text, 76f,
                                                       TextAlignmentOptions.Left);
        title.enableAutoSizing = true;
        title.fontSizeMin = UIStyle.LabelSize;
        title.fontSizeMax = UIStyle.TitleSize + 8f;
        CareerDeckBuilder.Bind(so, "opponent", title);
        CareerDeckBuilder.Bind(so, "matchLine", CareerDeckBuilder.Body(step, "", UIStyle.Text, 40f));
        CareerDeckBuilder.Bind(so, "placeLine", CareerDeckBuilder.Body(step, "", UIStyle.TextMuted, 40f));
        TextMeshProUGUI situation = CareerDeckBuilder.Body(step, "", UIStyle.AccentText, 40f, 0f, FontStyles.Bold);
        situation.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "situationLine", situation);
        Button next = UIControls.ActionButton(step, "Next", screen.OnNext, true, 96f);
        next.colors = UIStyle.MenuColors(true);
    }

    private static void Innings(Transform step, CareerPrematchScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Bind(so, "stepInnings", step.gameObject);
        CareerDeckBuilder.Heading(step, "Your innings");
        TextMeshProUGUI slot = WorldPanelBuilder.Line(step, "", UIStyle.TitleSize + 4f, FontStyles.Bold, UIStyle.Text, 70f,
                                                      TextAlignmentOptions.Left);
        CareerDeckBuilder.Bind(so, "slotLine", slot);
        Transform row = CareerMenuBuilder.Row(step, UIStyle.Gap);
        WorldPanelBuilder.Line(row, "Partner's innings", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.TextMuted, UIStyle.Control,
                               TextAlignmentOptions.Left, 240f);
        SegmentedControl watch = UIControls.Segmented(row, CareerHubScreen.WatchOptions, screen.OnWatchPartner);
        watch.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CareerDeckBuilder.Bind(so, "watchPartner", watch);
        Button play = UIControls.ActionButton(step, "Play match", screen.OnPlay, true, 96f);
        play.colors = UIStyle.MenuColors(true);
        UIControls.ActionButton(step, "Back", screen.OnBackStep, false);
        step.gameObject.SetActive(false);
    }
}
