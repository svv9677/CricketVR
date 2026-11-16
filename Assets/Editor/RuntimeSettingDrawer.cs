using UnityEditor;
using UnityEngine;

/// <summary>
/// Greys out a [RuntimeSetting] field outside Play and says where its value comes from, so nobody
/// edits a value that Play replaces. In Play it is editable as normal.
/// </summary>
[CustomPropertyDrawer(typeof(RuntimeSettingAttribute))]
public class RuntimeSettingDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        EditorGUI.GetPropertyHeight(property, label, true);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (RuntimeSettingAttribute)attribute;
        string from = $"Set in Play from {attr.source}";
        label.tooltip = string.IsNullOrEmpty(label.tooltip) ? from : $"{label.tooltip}\n{from}";
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (!Application.isPlaying)
                label.text += " (Play)";
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}
