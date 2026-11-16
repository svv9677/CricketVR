using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the club ground in the match scene, switched off, and puts the club's replay screen guard
/// on the Replay Display, switched off. Until 2026-11-15 the game instantiated
/// Resources/Prefabs/ClubGround and added ClubReplayScreen in code when a club match loaded;
/// ClubGroundVenue now only switches them on.
///
/// The instance is a prefab instance of Assets/Resources/Prefabs/ClubGround.prefab at the prefab's
/// own transform (as Instantiate placed it), named ClubGround and saved inactive. Build the prefab
/// first (Tools > CricketVR > Build Club Ground). Run again: the instance is kept, only its active
/// state and the guard are put right.
/// </summary>
public static class ClubGroundPlacer
{
    [MenuItem("CricketVR/Scene Objects/Place Club Ground")]
    public static void Place()
    {
        Scene scene = SceneObjectAssets.Open(StadiumBuilderUtil.MatchScene);
        if (PlaceIn(scene) == null)
            return;
        SceneObjectAssets.Save(scene);
        Debug.Log("[ClubGroundPlacer] ClubGround (inactive) and the Replay Display's ClubReplayScreen (off) placed in " + StadiumBuilderUtil.MatchScene);
    }

    /// The club ground in `scene` (placed if not there, and left switched off), or null if the
    /// prefab is missing. Also puts ClubReplayScreen, switched off, on the Replay Display. Does not
    /// save the scene.
    public static ClubGround PlaceIn(Scene scene)
    {
        ClubGround club = FindClub(scene);
        if (club == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClubGroundBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[ClubGroundPlacer] " + ClubGroundBuilder.PrefabPath + " is missing: run Tools > CricketVR > Build Club Ground first.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = prefab.name;
            instance.transform.SetPositionAndRotation(prefab.transform.position, prefab.transform.rotation);
            instance.transform.localScale = prefab.transform.localScale;
            club = instance.GetComponent<ClubGround>();
        }
        club.gameObject.SetActive(false);
        GuardReplayScreen(scene);
        return club;
    }

    private static ClubGround FindClub(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.TryGetComponent(out ClubGround club))
                return club;
        return null;
    }

    private static void GuardReplayScreen(Scene scene)
    {
        Transform screen = StadiumBuilderUtil.Find(scene, "Main", "Displays/Replay Display");
        if (screen == null)
        {
            Debug.LogWarning("[ClubGroundPlacer] Main/Displays/Replay Display is not in the scene: no ClubReplayScreen placed.");
            return;
        }
        ClubReplayScreen guard = StadiumBuilderUtil.Ensure<ClubReplayScreen>(screen.gameObject);
        guard.enabled = false;
        EditorUtility.SetDirty(guard);
    }
}
