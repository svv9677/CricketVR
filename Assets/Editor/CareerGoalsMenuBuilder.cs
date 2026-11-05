using UnityEngine;

/// <summary>
/// The coach's goals screen, CareerGoalsScreen ("careerGoals", from the hub), built by
/// MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs). Phase 7 fills it in; for now the
/// title and Back.
/// </summary>
public static class CareerGoalsMenuBuilder
{
    public static MenuScreen BuildGoals(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerGoalsScreen", out GameObject go);
        CareerGoalsScreen screen = go.AddComponent<CareerGoalsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerGoals");
        MenuPrefabBuilder.Title(column, "Coach's goals", "This season's targets.");
        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        return screen;
    }
}
