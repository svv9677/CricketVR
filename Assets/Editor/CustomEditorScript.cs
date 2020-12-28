//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEditor;

//[CustomEditor(typeof(Main))]
//[CanEditMultipleObjects]
//public class CustomEditorScript : Editor
//{
//    SerializedProperty zOffsetProperty;

//    public void OnEnable()
//    {
//        zOffsetProperty = serializedObject.FindProperty("zOffset");
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        zOffsetProperty.floatValue = EditorGUILayout.FloatField("z Offset", zOffsetProperty.floatValue);

//        DebugUIBuilder.instance.menuOffset.z = zOffsetProperty.floatValue;
//        DebugUIBuilder.instance.UpdatePosition();

//        serializedObject.ApplyModifiedProperties();
        
//    }
//}
