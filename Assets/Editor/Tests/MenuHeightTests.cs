using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The phase 7 screens fit the card (integration note 3), measured on the built MenuRoot prefab
/// (Tools > CricketVR > Build Menu Prefabs) with no scene: every screen is a column under the one
/// card (MenuPrefabBuilder: WorldPanelBuilder.MakeCard + FitContent on the panel), so a screen's
/// height is the card's preferred height with that screen the only one on. There is no scroll
/// view, so a screen taller than its budget runs off the card.
///   * Records no taller than the drills screen, whose shape it copies;
///   * the career result with its scout, best and goal lines all showing no taller than Match
///     Setup (measured here, not the plan's unverified 854);
///   * Goals within 640 px, Locker within 600 px;
///   * the hub (Back | Goals | Locker on Back's old row) no taller than Match Setup - the hub at
///     f7106ca, before phase 7, cannot be measured from a test, so Match Setup stands in;
///   * no row on those screens wider than the card's inside (760 - 2 x 32 = 696 px).
/// </summary>
public class MenuHeightTests
{
    private const string MenuRootPrefab = "Assets/Resources/Prefabs/UI/MenuRoot.prefab";
    private const float GoalsBudget = 640f, LockerBudget = 600f, RowWidth = 696f;
    /// Layout rounding: half a pixel either way is not a screen off the card.
    private const float Slack = 0.5f;

    private const string Setup = "setup", Hub = "careerHub", Result = "careerResult", Drills = "careerDrills",
                         Records = "careerRecords", Goals = "careerGoals", Locker = "careerLocker";

    /// The prefab opened for the test, its screens by name; unloaded whatever happens.
    private static void WithMenu(Action<Dictionary<string, MenuScreen>> test)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MenuRootPrefab);
        try
        {
            Assert.IsNotNull(root, "MenuRoot.prefab loads (Tools > CricketVR > Build Menu Prefabs)");
            var screens = new Dictionary<string, MenuScreen>();
            foreach (MenuScreen m in root.GetComponentsInChildren<MenuScreen>(true))
                if (!string.IsNullOrEmpty(m.ScreenName))
                    screens[m.ScreenName] = m;
            foreach (string name in new[] { Setup, Hub, Result, Drills, Records, Goals, Locker })
                Assert.IsTrue(screens.ContainsKey(name), $"the prefab has the {name} screen");
            test(screens);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// The card a screen is a column of (the panel MenuPrefabBuilder made a card).
    private static RectTransform Card(MenuScreen screen)
    {
        var card = screen.transform.parent as RectTransform;
        Assert.IsNotNull(card, $"{screen.ScreenName} sits under the card");
        Assert.IsNotNull(card.GetComponent<VerticalLayoutGroup>(), "the card lays its screens out");
        Assert.IsNotNull(card.GetComponent<ContentSizeFitter>(), "and fits its height to them");
        return card;
    }

    /// The card's preferred height with only `screen` on (after `prepare` sets its lines), laid out
    /// at once. The screen is left on, laid out, for a width check.
    private static float Height(MenuScreen screen, Action<MenuScreen> prepare = null)
    {
        RectTransform card = Card(screen);
        foreach (Transform child in card)
            child.gameObject.SetActive(child == screen.transform);
        prepare?.Invoke(screen);
        LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        float h = LayoutUtility.GetPreferredHeight(card);
        Assert.Greater(h, 0f, $"{screen.ScreenName} measures");
        TestContext.WriteLine($"{screen.ScreenName}: {h:0.#} px");
        return h;
    }

    /// The object a screen's serialized field points at (a TextMeshProUGUI, a GameObject ...).
    private static GameObject Field(MenuScreen screen, string field)
    {
        SerializedProperty prop = new SerializedObject(screen).FindProperty(field);
        Assert.IsNotNull(prop, $"{screen.ScreenName}.{field} exists");
        UnityEngine.Object o = prop.objectReferenceValue;
        Assert.IsNotNull(o, $"{screen.ScreenName}.{field} is wired");
        return o is GameObject go ? go : ((Component)o).gameObject;
    }

    /// Every entry of a screen's serialized array, switched on or off.
    private static void SetAll(MenuScreen screen, string array, bool on)
    {
        SerializedProperty prop = new SerializedObject(screen).FindProperty(array);
        Assert.IsNotNull(prop, $"{screen.ScreenName}.{array} exists");
        Assert.Greater(prop.arraySize, 0, $"{screen.ScreenName}.{array} has entries");
        for (int i = 0; i < prop.arraySize; i++)
        {
            UnityEngine.Object o = prop.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.IsNotNull(o, $"{screen.ScreenName}.{array}[{i}] is wired");
            (o is GameObject go ? go : ((Component)o).gameObject).SetActive(on);
        }
    }

    /// The result with every line it can show: the scout's report, a record broken, a goal reached.
    private static void AllResultLines(MenuScreen screen)
    {
        foreach (string field in new[] { "scout", "best", "goal" })
        {
            GameObject line = Field(screen, field);
            line.SetActive(true);
            TextMeshProUGUI text = line.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = "A line long enough to fill its width on the card, as the longest the screen writes.";
        }
    }

    /// No row on the screen (laid out by Height) wider than the card's inside: the screen's own
    /// column, each line in it, and every horizontal row anywhere under it.
    private static void RowsFit(MenuScreen screen)
    {
        var rows = new List<RectTransform> { (RectTransform)screen.transform };
        foreach (Transform child in screen.transform)
            rows.Add((RectTransform)child);
        foreach (HorizontalLayoutGroup row in screen.GetComponentsInChildren<HorizontalLayoutGroup>(false))
            rows.Add((RectTransform)row.transform);
        foreach (RectTransform r in rows)
        {
            if (!r.gameObject.activeInHierarchy)
                continue;
            Assert.LessOrEqual(r.rect.width, RowWidth + Slack, $"{screen.ScreenName}: {Path(r, screen.transform)} is {r.rect.width:0.#} px wide");
        }
    }

    private static string Path(Transform t, Transform top)
    {
        string path = t.name;
        for (Transform p = t; p != top && p.parent != null;)
        {
            p = p.parent;
            path = p.name + "/" + path;
        }
        return path;
    }

    // ---- Heights ----------------------------------------------------------------------------------

    [Test]
    public void Records_IsNoTallerThanTheDrillsScreen()
    {
        WithMenu(s =>
        {
            float drills = Height(s[Drills]);
            float records = Height(s[Records]);
            Assert.LessOrEqual(records, drills + Slack, $"records {records:0.#} px, drills {drills:0.#} px");
        });
    }

    [Test]
    public void TheResult_WithScoutBestAndGoalLines_IsNoTallerThanMatchSetup()
    {
        WithMenu(s =>
        {
            float setup = Height(s[Setup]);
            float result = Height(s[Result], AllResultLines);
            Assert.LessOrEqual(result, setup + Slack, $"result {result:0.#} px, match setup {setup:0.#} px");
        });
    }

    [Test]
    public void Goals_FitsIn640_WithItsRows_OrItsEmptyLine()
    {
        WithMenu(s =>
        {
            float rows = Height(s[Goals], g => { SetAll(g, "rows", true); Field(g, "empty").SetActive(false); });
            Assert.LessOrEqual(rows, GoalsBudget + Slack, $"goals with every row {rows:0.#} px");
            float empty = Height(s[Goals], g => { SetAll(g, "rows", false); Field(g, "empty").SetActive(true); });
            Assert.LessOrEqual(empty, GoalsBudget + Slack, $"goals with the empty line {empty:0.#} px");
        });
    }

    [Test]
    public void Locker_FitsIn600()
    {
        WithMenu(s =>
        {
            float locker = Height(s[Locker]);
            Assert.LessOrEqual(locker, LockerBudget + Slack, $"locker {locker:0.#} px");
        });
    }

    [Test]
    public void TheHub_IsNoTallerThanMatchSetup()
    {
        // Stands in for "the hub's height is unchanged from f7106ca", which a test cannot measure.
        WithMenu(s =>
        {
            float setup = Height(s[Setup]);
            float hub = Height(s[Hub]);
            Assert.LessOrEqual(hub, setup + Slack, $"hub {hub:0.#} px, match setup {setup:0.#} px");
        });
    }

    // ---- Widths -----------------------------------------------------------------------------------

    [Test]
    public void NoRowOnThePhase7Screens_IsWiderThanTheCard()
    {
        WithMenu(s =>
        {
            Height(s[Records]); RowsFit(s[Records]);
            Height(s[Result], AllResultLines); RowsFit(s[Result]);
            Height(s[Goals], g => SetAll(g, "rows", true)); RowsFit(s[Goals]);
            Height(s[Locker]); RowsFit(s[Locker]);
            Height(s[Hub]); RowsFit(s[Hub]);
        });
    }
}
