using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Puts the left-armer's mirror on the bowling controller (AnimatedBowlerArm): a bool parameter,
/// and every state of every layer mirrored by it - the humanoid's own mirror, left for right.
/// Safe to run again.
/// </summary>
public static class BowlerMirrorSetup
{
    public const string ControllerPath = "Assets/Resources/Animations/Bowling/Steyn.controller";

    [MenuItem("CricketVR/Bowling/Add Left-Arm Mirror To Bowling Controller")]
    public static void Apply()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError("[BowlerMirrorSetup] no controller at " + ControllerPath);
            return;
        }
        bool has = false;
        foreach (AnimatorControllerParameter p in ctrl.parameters)
            has |= p.name == AnimatedBowler.MirrorParam;
        if (!has)
            ctrl.AddParameter(AnimatedBowler.MirrorParam, AnimatorControllerParameterType.Bool);
        int states = 0;
        foreach (AnimatorControllerLayer layer in ctrl.layers)
            states += Mirror(layer.stateMachine);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BowlerMirrorSetup] {states} states mirrored by {AnimatedBowler.MirrorParam}");
    }

    private static int Mirror(AnimatorStateMachine sm)
    {
        int n = 0;
        foreach (ChildAnimatorState c in sm.states)
        {
            c.state.mirrorParameterActive = true;
            c.state.mirrorParameter = AnimatedBowler.MirrorParam;
            EditorUtility.SetDirty(c.state);
            n++;
        }
        foreach (ChildAnimatorStateMachine child in sm.stateMachines)
            n += Mirror(child.stateMachine);
        return n;
    }
}
