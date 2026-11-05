using UnityEngine;

/// <summary>
/// The career records screen, CareerRecordsScreen ("careerRecords", from Ratings), built by
/// MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs). Phase 7 fills it in; for now the
/// title and Back.
/// </summary>
public static class CareerRecordsMenuBuilder
{
    public static MenuScreen BuildRecords(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerRecordsScreen", out GameObject go);
        CareerRecordsScreen screen = go.AddComponent<CareerRecordsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerRecords");
        MenuPrefabBuilder.Title(column, "Records", "Your career bests.");
        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        return screen;
    }
}
