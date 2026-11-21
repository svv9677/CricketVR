using UnityEditor;
using UnityEngine;

/// <summary>
/// CricketVR > Scene Objects > Set Up Crowd Audio. Sets the AudioSource on the Crowd object (in
/// Assets/Prefabs/Scene/Crowd.prefab and on its instance in CricketVR.unity) to what CrowdController
/// used to overwrite on it as the scene woke (until 2026-11-15): no play on awake, 2D (the crowd
/// surrounds the player), the cheer clip, looping, at the ambience volume. The clip and volume are
/// the CrowdController's own `cheerClip` and `ambienceVolume`, so change them there and run this
/// again. CrowdController still starts the loop and routes it to the Crowd channel.
/// </summary>
public static class CrowdAudioBuilder
{
    private const string CrowdPrefab = "Assets/Prefabs/Scene/Crowd.prefab";
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";

    [MenuItem("CricketVR/Scene Objects/Set Up Crowd Audio")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[CrowdAudioBuilder] Leave Play mode first.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(CrowdPrefab);
        try
        {
            if (Apply(root.GetComponentInChildren<CrowdController>(true)))
                PrefabUtility.SaveAsPrefabAsset(root, CrowdPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        var scene = SceneObjectAssets.Open(MatchScene);
        CrowdController inScene = Object.FindFirstObjectByType<CrowdController>(FindObjectsInactive.Include);
        if (Apply(inScene))
            SceneObjectAssets.Save(scene);
        Debug.Log("[CrowdAudioBuilder] Crowd audio set in " + CrowdPrefab + " and " + MatchScene);
    }

    /// Sets the AudioSource beside `crowd`. False if there is none to set.
    private static bool Apply(CrowdController crowd)
    {
        AudioSource source = crowd != null ? crowd.GetComponent<AudioSource>() : null;
        if (source == null)
        {
            Debug.LogError("[CrowdAudioBuilder] No CrowdController with an AudioSource found.");
            return false;
        }
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.clip = crowd.cheerClip;
        source.loop = true;
        source.volume = crowd.ambienceVolume;
        PlayerSetupBuilder.Touch(source);
        return true;
    }
}
