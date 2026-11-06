using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career hub as floating panels (CareerDeckBuilder; CareerHubScreen's fields):
///
///   Left    YOUR PLAYER - the kit bar and the name, "#18 · India · Right-hand bat", the level and
///           season, the overall and the form as two big tiles, and the trial / left-out banner.
///   Centre  NEXT MATCH - who against (big), the round and format, where you bat, a scout watching;
///           the one big button (the nets trial, Play match, Sim, the next season); and whether to
///           watch your partner's innings.
///   Right   CLUB NEWS - an offer waiting, unread messages and the newest titles, the coach's goals;
///           Main menu.
///   Top     the tabs: Home lit; Training, Season, Ratings, Inbox (with the unread count), Goals (with
///           the goals reached), Locker.
/// </summary>
public static class CareerDeckHub
{
    public static MenuScreen Build(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "CareerHubScreen");
        CareerHubScreen screen = go.AddComponent<CareerHubScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerDeckScreen.Hub);
        var so = new SerializedObject(screen);

        Player(CareerDeckBuilder.Panel(go, "Player", CareerDeckBuilder.Slot.Left), so);
        NextMatch(CareerDeckBuilder.Panel(go, "Next Match", CareerDeckBuilder.Slot.Centre), screen, so);
        News(CareerDeckBuilder.Panel(go, "News", CareerDeckBuilder.Slot.Right), screen, so);

        Dictionary<string, Button> tabs = CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.Hub);
        CareerDeckBuilder.Bind(so, "nets", tabs[CareerDeckScreen.Training]);
        CareerDeckBuilder.Bind(so, "inboxLabel", UIControls.LabelOf(tabs[CareerDeckScreen.InboxTab]));
        CareerDeckBuilder.Bind(so, "goals", tabs[CareerDeckScreen.GoalsTab]);
        CareerDeckBuilder.Bind(so, "goalsLabel", UIControls.LabelOf(tabs[CareerDeckScreen.GoalsTab]));
        CareerDeckBuilder.Bind(so, "locker", tabs[CareerDeckScreen.LockerTab]);
        so.ApplyModifiedProperties();
        return screen;
    }

    private static void Player(Transform panel, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Your player");
        Transform who = CareerMenuBuilder.Row(panel, UIStyle.Gap);
        CareerDeckBuilder.Bind(so, "kitBar", CareerMenuBuilder.Swatch(who, 12f, 64f, 2f));
        TextMeshProUGUI name = WorldPanelBuilder.Line(who, "Player", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 64f,
                                                      TextAlignmentOptions.Left);
        name.GetComponent<LayoutElement>().flexibleWidth = 1f;
        name.enableAutoSizing = true;
        name.fontSizeMin = UIStyle.LabelSize;
        name.fontSizeMax = UIStyle.TitleSize;
        CareerDeckBuilder.Bind(so, "playerName", name);
        CareerDeckBuilder.Bind(so, "playerDetail", CareerDeckBuilder.Body(panel, "", UIStyle.TextMuted));
        Transform level = CareerMenuBuilder.Row(panel, UIStyle.Gap);
        TextMeshProUGUI stage = WorldPanelBuilder.Line(level, "Club cricket", UIStyle.LabelSize, FontStyles.Bold, UIStyle.AccentText, 36f,
                                                       TextAlignmentOptions.Left);
        stage.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CareerDeckBuilder.Bind(so, "stage", stage);
        CareerDeckBuilder.Bind(so, "season", WorldPanelBuilder.Line(level, "Season 1", UIStyle.LabelSize, FontStyles.Normal, UIStyle.TextMuted,
                                                                    36f, TextAlignmentOptions.Right, 160f));
        UIControls.Divider(panel);
        Transform tiles = WorldPanelBuilder.Column(panel, false, UIStyle.Gap);
        CareerDeckBuilder.Bind(so, "overallChip", CareerDeckBuilder.StatTile(tiles, "OVERALL", out TextMeshProUGUI grade));
        CareerDeckBuilder.Bind(so, "overallGrade", grade);
        CareerDeckBuilder.Bind(so, "formChip", CareerDeckBuilder.StatTile(tiles, "FORM", out _));
        TextMeshProUGUI banner = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 64f);
        banner.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "banner", banner);
    }

    private static void NextMatch(Transform panel, CareerHubScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Next match");
        TextMeshProUGUI title = WorldPanelBuilder.Line(panel, "", UIStyle.TitleSize + 4f, FontStyles.Bold, UIStyle.Text, 64f,
                                                       TextAlignmentOptions.Left);
        title.enableAutoSizing = true;
        title.fontSizeMin = UIStyle.LabelSize;
        title.fontSizeMax = UIStyle.TitleSize + 4f;
        CareerDeckBuilder.Bind(so, "nextTitle", title);
        CareerDeckBuilder.Bind(so, "nextDetail", CareerDeckBuilder.Body(panel, "", UIStyle.TextMuted));
        CareerDeckBuilder.Bind(so, "nextSlot", CareerDeckBuilder.Body(panel, "", UIStyle.AccentText, 36f, 0f, FontStyles.Bold));
        TextMeshProUGUI scout = CareerDeckBuilder.Body(panel, "", UIStyle.MenuPrimary, 36f, 0f, FontStyles.Bold);
        scout.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "nextScout", scout);

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
        Transform row = CareerMenuBuilder.Row(panel, UIStyle.Gap);
        WorldPanelBuilder.Line(row, "Partner's innings", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.TextMuted, UIStyle.Control,
                               TextAlignmentOptions.Left, 240f);
        SegmentedControl watch = UIControls.Segmented(row, CareerHubScreen.WatchOptions, screen.OnWatchPartner);
        watch.GetComponent<LayoutElement>().flexibleWidth = 1f;
        CareerDeckBuilder.Bind(so, "watchPartner", watch);
    }

    private static void News(Transform panel, CareerHubScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Club news");
        TextMeshProUGUI offer = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 64f, 0f, FontStyles.Bold);
        offer.gameObject.SetActive(false);
        CareerDeckBuilder.Bind(so, "offerLine", offer);
        TextMeshProUGUI news = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 200f);
        news.alignment = TextAlignmentOptions.TopLeft;
        CareerDeckBuilder.Bind(so, "news", news);
        UIControls.ActionButton(panel, "Open inbox", screen.OnInbox, false);
        UIControls.Divider(panel);
        UIControls.ActionButton(panel, "Main menu", screen.OnMainMenu, false);
    }
}
