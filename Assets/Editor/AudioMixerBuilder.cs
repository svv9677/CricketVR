using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Tools > CricketVR > Build Audio Mixer. Makes Assets/Resources/Audio/GameMixer.mixer: Master
/// with a child group per kind of sound (GameAudio.Channel), each group's volume exposed as
/// "&lt;Group&gt;Volume" so the settings sliders can drive it from 0 to 200% (+6 dB).
///
/// Unity has no public API for authoring a mixer, so this goes through the editor's own
/// AudioMixerController by reflection - the same calls the Audio Mixer window makes.
/// </summary>
public static class AudioMixerBuilder
{
    public const string MixerPath = "Assets/Resources/Audio/GameMixer.mixer";
    private const string BatPrefab = "Assets/Resources/Prefabs/Bat.prefab";

    /// The bat's sound source moves to a child, Impact Voice, that BatSounds puts at each contact
    /// point - the sound comes from where the ball met the blade, not the bat's pivot. The
    /// AudioSource and low-pass move with it, keeping their settings.
    [MenuItem("Tools/CricketVR/Add Bat Impact Voice")]
    public static void AddBatImpactVoice()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BatPrefab);
        try
        {
            var sounds = root.GetComponent<BatSounds>();
            var so = new SerializedObject(sounds);
            if (so.FindProperty("voice").objectReferenceValue != null)
                return;
            var voice = new GameObject("Impact Voice");
            voice.transform.SetParent(root.transform, false);
            var source = voice.AddComponent<AudioSource>();
            var tone = voice.AddComponent<AudioLowPassFilter>();
            AudioSource old = root.GetComponent<AudioSource>();
            AudioLowPassFilter oldTone = root.GetComponent<AudioLowPassFilter>();
            if (old != null)
            {
                EditorUtility.CopySerialized(old, source);
                if (oldTone != null)
                {
                    EditorUtility.CopySerialized(oldTone, tone);
                    UnityEngine.Object.DestroyImmediate(oldTone);
                }
                UnityEngine.Object.DestroyImmediate(old);
            }
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            so.FindProperty("voice").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BatPrefab);
            Debug.Log("[AudioMixerBuilder] Bat prefab: Impact Voice added.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/CricketVR/Build Audio Mixer")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
        {
            Debug.Log("[AudioMixerBuilder] " + MixerPath + " already exists; delete it to rebuild.");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio"))
            AssetDatabase.CreateFolder("Assets/Resources", "Audio");

        Assembly editor = typeof(Editor).Assembly;
        Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController");
        Type groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController");
        Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        Type parameterType = editor.GetType("UnityEditor.Audio.ExposedAudioParameter");
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        object mixer = controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", Any).GetValue(mixer);

        foreach (GameAudio.Channel channel in Enum.GetValues(typeof(GameAudio.Channel)))
        {
            object group = channel == GameAudio.Channel.Master
                ? master
                : controllerType.GetMethod("CreateNewGroup", Any).Invoke(mixer, new object[] { channel.ToString(), false });
            if (channel != GameAudio.Channel.Master)
            {
                controllerType.GetMethod("AddChildToParent", Any).Invoke(mixer, new[] { group, master });
            }
            object volume = groupType.GetMethod("GetGUIDForVolume", Any).Invoke(group, null);
            object path = Activator.CreateInstance(pathType, group, volume);
            controllerType.GetMethod("AddExposedParameter", Any).Invoke(mixer, new[] { path });

            // AddExposedParameter names it "MyExposedParam..."; rename the one just added.
            PropertyInfo exposed = controllerType.GetProperty("exposedParameters", Any);
            Array list = (Array)exposed.GetValue(mixer);
            for (int i = 0; i < list.Length; i++)
            {
                object p = list.GetValue(i);
                FieldInfo guid = parameterType.GetField("guid", Any);
                if (guid.GetValue(p).Equals(volume))
                {
                    parameterType.GetField("name", Any).SetValue(p, GameAudio.Parameter(channel));
                    list.SetValue(p, i);
                }
            }
            exposed.SetValue(mixer, list);
        }

        EditorUtility.SetDirty((UnityEngine.Object)mixer);
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioMixerBuilder] Built " + MixerPath);
    }
}
