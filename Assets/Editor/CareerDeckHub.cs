using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career hub (CareerHubScreen's fields), decluttered 2026-11-06 ("I just need to know what the
/// match is against and then have everything sequential"): one panel in the middle -
///
///   NEXT MATCH
///   vs Marston Green CC
///   A Pro League scout is watching           (only when one is)
///   An offer is waiting in your inbox        (only when one is)
///   [ Play match ]                            (the trial, Sim, the next season as before)
///   Main menu
///
/// and the tab bar over it: Home lit; Training, Season, Ratings, Inbox (with the unread count),
/// Goals, Locker. Play match opens the match's details step by step (CareerPrematchScreen). The
/// player card and the club news are no longer here (Ratings and Inbox have them).
/// </summary>
public static class CareerDeckHub
{
    public static MenuScreen Build(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "CareerHubScreen");
        CareerHubScreen screen = go.AddComponent<CareerHubScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerDeckScreen.Hub);
        var so = new SerializedObject(screen);

        NextMatch(CareerDeckBuilder.Panel(go, "Next Match", CareerDeckBuilder.Slot.Centre), screen, so);

        Dictionary<string, Button> tabs = CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.Hub);
        CareerDeckBuilder.Bind(so, "nets", tabs[CareerDeckScreen.Training]);
        CareerDeckBuilder.Bind(so, "inboxLabel", UIControls.LabelOf(tabs[CareerDeckScreen.InboxTab]));
        CareerDeckBuilder.Bind(so, "goals", tabs[CareerDeckScreen.GoalsTab]);
        CareerDeckBuilder.Bind(so, "goalsLabel", UIControls.LabelOf(tabs[CareerDeckScreen.GoalsTab]));
        CareerDeckBuilder.Bind(so, "locker", tabs[CareerDeckScreen.LockerTab]);
        so.ApplyModifiedProperties();
        return screen;
    }

    private static void NextMatch(Transform panel, CareerHubScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Next match");
        TextMeshProUGUI title = WorldPanelBuilder.Line(panel, "", UIStyle.TitleSize + 8f, FontStyles.Bold, UIStyle.Text, 76f,
                                                       TextAlignmentOptions.Left);
        title.enableAutoSizing = true;
        title.fontSizeMin = UIStyle.LabelSize;
        title.fontSizeMax = UIStyle.TitleSize + 8f;
        CareerDeckBuilder.Bind(so, "nextTitle", title);
        TextMeshProUGUI scout = CareerDeckBuilder.Body(panel, "", UIStyle.MenuPrimary, 40f, 0f, FontStyles.Bold);
        scout.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "nextScout", scout);
        TextMeshProUGUI offer = CareerDeckBuilder.Body(panel, "", UIStyle.AccentText, 40f, 0f, FontStyles.Bold);
        offer.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "offerLine", offer);
        TextMeshProUGUI banner = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 64f);
        banner.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "banner", banner);

        Button primary = UIControls.ActionButton(panel, $"Take your nets trial · {NetsSession.TrialBalls} balls", screen.OnPrimary, true, 96f);
        primary.colors = UIStyle.MenuColors(true);
        TextMeshProUGUI label = UIControls.LabelOf(primary);
        label.enableAutoSizing = true;
        label.fontSizeMin = UIStyle.LabelSize;
        label.fontSizeMax = UIStyle.TitleSize - 4f;
        CareerDeckBuilder.Bind(so, "primary", primary);
        CareerDeckBuilder.Bind(so, "primaryLabel", label);
        TextMeshProUGUI locked = WorldPanelBuilder.Line(panel, "The trial sets your ratings before your first match.", UIStyle.HintSize + 2f,
                                                        FontStyles.Normal, UIStyle.TextMuted, 30f, TextAlignmentOptions.Center);
        CareerDeckBuilder.Bind(so, "locked", locked);
        UIControls.Divider(panel);
        UIControls.ActionButton(panel, "Main menu", screen.OnMainMenu, false);
    }
}
