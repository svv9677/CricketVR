using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="BatGripPreview"/>: shows how close each handle is to the hand's grip
/// bone, and writes the tuned values back to the Bat prefab.
/// </summary>
[CustomEditor(typeof(BatGripPreview))]
public class BatGripPreviewEditor : Editor
{
    private const string BatPrefabPath = "Assets/Resources/Prefabs/Bat.prefab";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var preview = (BatGripPreview)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grip point to hand's grip bone", EditorStyles.boldLabel);
        Readout("Left", preview.HandleDistance(true));
        Readout("Right", preview.HandleDistance(false));
        EditorGUILayout.HelpBox(
            "Green sphere = the hand's grip bone. Magenta sphere = the chosen point on the bat's " +
            "handle. Blue line is the blade (-Z), white line the handle (+Z).\n\n" +
            "Press Solve to drop the magenta onto the green, then adjust by eye: slide " +
            "gripPointLocalZ for how high up the handle the hand sits, and nudge the euler for the " +
            "wrist angle.", MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("Solve positions (put grip point on the grip bone)"))
        {
            Undo.RecordObject(preview, "Solve bat grab offsets");
            if (preview.SolvePosition(true, out var l)) preview.leftGrabOffsetPosition = l;
            if (preview.SolvePosition(false, out var r)) preview.rightGrabOffsetPosition = r;
            preview.Apply();
            EditorUtility.SetDirty(preview);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Log offsets"))
            Debug.Log(Format(preview));

        if (GUILayout.Button("Write offsets to Bat prefab"))
            WriteToPrefab(preview);

        if (GUILayout.Button("Reload offsets from Bat prefab"))
        {
            var bat = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath).GetComponent<Bat>();
            Undo.RecordObject(preview, "Reload bat offsets");
            preview.leftGrabOffsetPosition = bat.leftGrabOffsetPosition;
            preview.leftGrabOffsetEuler = bat.leftGrabOffsetEuler;
            preview.rightGrabOffsetPosition = bat.rightGrabOffsetPosition;
            preview.rightGrabOffsetEuler = bat.rightGrabOffsetEuler;
            preview.Apply();
        }
    }

    private static void Readout(string label, float distance)
    {
        EditorGUILayout.LabelField(label, distance < 0f ? "-" : distance.ToString("F4") + " m");
    }

    private static string Format(BatGripPreview p)
    {
        return string.Format(
            "[BatGripPreview]\n  left.position  = ({0:F4}, {1:F4}, {2:F4})\n  left.euler     = ({3:F2}, {4:F2}, {5:F2})" +
            "\n  right.position = ({6:F4}, {7:F4}, {8:F4})\n  right.euler    = ({9:F2}, {10:F2}, {11:F2})",
            p.leftGrabOffsetPosition.x, p.leftGrabOffsetPosition.y, p.leftGrabOffsetPosition.z,
            p.leftGrabOffsetEuler.x, p.leftGrabOffsetEuler.y, p.leftGrabOffsetEuler.z,
            p.rightGrabOffsetPosition.x, p.rightGrabOffsetPosition.y, p.rightGrabOffsetPosition.z,
            p.rightGrabOffsetEuler.x, p.rightGrabOffsetEuler.y, p.rightGrabOffsetEuler.z);
    }

    /// <summary>
    /// Writes to the prefab, then warns about any scene that overrides the same fields - the
    /// CricketVR.unity Bat instance overrides several of them, so a prefab-only edit would appear
    /// to do nothing in game.
    /// </summary>
    private static void WriteToPrefab(BatGripPreview preview)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath);
        if (prefab == null) { Debug.LogError("[BatGripPreview] no Bat prefab at " + BatPrefabPath); return; }
        var bat = prefab.GetComponent<Bat>();

        Undo.RecordObject(bat, "Write bat grab offsets");
        bat.leftGrabOffsetPosition = preview.leftGrabOffsetPosition;
        bat.leftGrabOffsetEuler = preview.leftGrabOffsetEuler;
        bat.rightGrabOffsetPosition = preview.rightGrabOffsetPosition;
        bat.rightGrabOffsetEuler = preview.rightGrabOffsetEuler;
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();

        Debug.LogWarning("[BatGripPreview] written to " + BatPrefabPath + "\n" + Format(preview) +
            "\nNOTE: the Bat instance in CricketVR.unity overrides these fields, so open that scene, " +
            "select Main/Bat and either revert the overrides or paste the same values, or the game " +
            "will keep using the old ones.");
    }
}
