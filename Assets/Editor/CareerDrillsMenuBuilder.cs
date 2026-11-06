using UnityEngine;

/// <summary>
/// The career nets screen, CareerDrillsScreen ("careerDrills", the Training tab), built by
/// MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs). Since 2026-11-06 it is floating
/// panels - the coach, the drill tiles, the chosen drill - built by CareerDeckTraining; this entry
/// point stays so MenuPrefabBuilder's list of screens is unchanged.
/// </summary>
public static class CareerDrillsMenuBuilder
{
    public static MenuScreen BuildDrills(Transform parent) => CareerDeckTraining.Build(parent);
}
