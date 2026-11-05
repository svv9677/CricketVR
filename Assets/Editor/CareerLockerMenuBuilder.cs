using UnityEngine;

/// <summary>
/// The locker screen, CareerLockerScreen ("careerLocker", from the hub), built by MenuPrefabBuilder
/// (Tools > CricketVR > Build Menu Prefabs). Phase 7 fills it in; for now the title and Back.
/// </summary>
public static class CareerLockerMenuBuilder
{
    public static MenuScreen BuildLocker(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerLockerScreen", out GameObject go);
        CareerLockerScreen screen = go.AddComponent<CareerLockerScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerLocker");
        MenuPrefabBuilder.Title(column, "Locker", "Cosmetic only. Nothing here changes how you bat.");
        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        return screen;
    }
}
