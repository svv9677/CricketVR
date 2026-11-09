using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Quick Play screen for MenuPrefabBuilder: one tile per match length (MatchFormat.Order) in
/// the career slots' look (CareerMenuBuilder), each with its title and state, how the match stands
/// and its setup, and Continue / Restart / New Game wired with the row's index.
/// </summary>
public static class QuickPlayMenuBuilder
{
    private const float ContinueWidth = 150f, RestartWidth = 124f, NewWidth = 160f;

    public static MenuScreen Build(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "QuickPlayScreen", out GameObject go);
        QuickPlayScreen screen = go.AddComponent<QuickPlayScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "quickplay");
        MenuPrefabBuilder.Title(column, "Quick Play", "A match for each length. Leave any time: it's saved after every ball.");

        var so = new SerializedObject(screen);
        SerializedProperty slots = so.FindProperty("slots");
        slots.arraySize = MatchFormat.Order.Length;
        for (int i = 0; i < MatchFormat.Order.Length; i++)
            Tile(column, screen, i, slots.GetArrayElementAtIndex(i));
        so.ApplyModifiedProperties();

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        return screen;
    }

    private static void Tile(Transform column, QuickPlayScreen screen, int i, SerializedProperty slot)
    {
        Transform tile = WorldPanelBuilder.Column(column, true, 0f);
        tile.name = $"Slot {MatchFormat.Label(MatchFormat.Order[i])}";
        tile.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 12, 10, 10);
        Image well = tile.gameObject.AddComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;

        // The length and the buttons on one line; how the match stands, and its setup, under them
        // across the whole tile (beside three buttons the text had no room).
        Transform row = CareerMenuBuilder.Row(tile, UIStyle.Unit);
        TextMeshProUGUI title = WorldPanelBuilder.Line(row, MatchFormat.Label(MatchFormat.Order[i]), UIStyle.LabelSize + 2f,
                                                       FontStyles.Bold, UIStyle.Text, UIStyle.Control, TextAlignmentOptions.Left);
        LayoutElement titleLayout = title.GetComponent<LayoutElement>();
        titleLayout.preferredWidth = 0f;
        titleLayout.flexibleWidth = 1f;
        Button go = CareerMenuBuilder.IntButton(row, "Continue", screen.OnContinue, i, ContinueWidth);
        go.colors = UIStyle.MenuColors(true);
        Button again = CareerMenuBuilder.IntButton(row, "Restart", screen.OnRestart, i, RestartWidth);
        CareerMenuBuilder.IntButton(row, "New Game", screen.OnNew, i, NewWidth);
        TextMeshProUGUI detail = WorldPanelBuilder.Text(tile, "Set it up and play", UIStyle.HintSize, FontStyles.Normal,
                                                        UIStyle.TextMuted, 50f, TextAlignmentOptions.Left);
        go.gameObject.SetActive(false);
        again.gameObject.SetActive(false);

        slot.FindPropertyRelative("title").objectReferenceValue = title;
        slot.FindPropertyRelative("detail").objectReferenceValue = detail;
        slot.FindPropertyRelative("continueButton").objectReferenceValue = go.gameObject;
        slot.FindPropertyRelative("restartButton").objectReferenceValue = again.gameObject;
    }
}
