using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The locker screen, CareerLockerScreen ("careerLocker", from the hub), built by MenuPrefabBuilder
/// (Tools > CricketVR > Build Menu Prefabs): the title, three ‹ [swatch] name › steppers (Bat,
/// Gloves, Helmet trim; CareerMenuBuilder.StepperRow, as Create Look's), a two-line status, Back.
/// About 590 px tall, inside the 600 px budget.
/// </summary>
public static class CareerLockerMenuBuilder
{
    public static MenuScreen BuildLocker(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerLockerScreen", out GameObject go);
        CareerLockerScreen screen = go.AddComponent<CareerLockerScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerLocker");
        MenuPrefabBuilder.Title(column, "Locker", "Cosmetic only. Nothing here changes how you bat.");

        // In eCosmeticSlot's order: CareerLockerScreen reads names[] and swatches[] by slot.
        TextMeshProUGUI bat = CareerMenuBuilder.StepperRow(column, "Bat", screen.OnBat, true, out Image batSwatch);
        TextMeshProUGUI gloves = CareerMenuBuilder.StepperRow(column, "Gloves", screen.OnGloves, true, out Image glovesSwatch);
        TextMeshProUGUI trim = CareerMenuBuilder.StepperRow(column, "Helmet trim", screen.OnTrim, true, out Image trimSwatch);
        TextMeshProUGUI status = WorldPanelBuilder.Text(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 52f,
                                                        TextAlignmentOptions.Center);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        var so = new SerializedObject(screen);
        Fill(so.FindProperty("names"), bat, gloves, trim);
        Fill(so.FindProperty("swatches"), batSwatch, glovesSwatch, trimSwatch);
        so.FindProperty("status").objectReferenceValue = status;
        so.ApplyModifiedProperties();
        return screen;
    }

    private static void Fill(SerializedProperty array, params Object[] items)
    {
        array.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
