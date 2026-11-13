using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Bowling actions captured from video (Tools/mocap) arrive as FBX files in <see cref="Folder"/>,
/// each with a <c>&lt;name&gt;.mocap.json</c> beside it (written by Tools/mocap/install_clip.py).
/// After import each becomes a humanoid clip (the bowler's own skeleton), with root motion set up
/// like the hand-made actions (Steyn Cleaned.anim: rotation and height baked into the pose, ground
/// travel as root motion) and the ReleaseBall event on the release frame. Then it is wired into the
/// bowling controller (<see cref="MocapBowlerSetup"/>) as its own state, alongside the existing
/// actions and reached only through its own Action value, so nothing plays it unless asked.
/// </summary>
public class MocapBowlerImport : AssetPostprocessor
{
    public const string Folder = "Assets/Resources/Animations/Bowling/Mocap/";

    [System.Serializable]
    public class Sidecar
    {
        public int frames;
        public float fps;
        public int release_frame;
        public int action = MocapDelivery.Action;
        public float state_speed = 0.8f;
    }

    public static Sidecar ReadSidecar(string fbxPath)
    {
        string json = Path.ChangeExtension(fbxPath, ".mocap.json");
        return File.Exists(json) ? JsonUtility.FromJson<Sidecar>(File.ReadAllText(json)) : null;
    }

    /// After every script reload too (a fresh checkout, or this file changed): settings that are
    /// already right cost nothing, so this only does work when something is out of date.
    [InitializeOnLoadMethod]
    private static void ConfigureAll()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
                Configure(AssetDatabase.GUIDToAssetPath(guid));
            MocapBowlerSetup.Apply();
        };
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool any = false;
        foreach (string p in imported)
            if (p.StartsWith(Folder) && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                string path = p;
                // After the import, on the main editor process: settings changed during an import
                // run in a worker (measured: loading Bowler.fbx's avatar there returned null).
                EditorApplication.delayCall += () => Configure(path);
                any = true;
            }
        if (any)
            EditorApplication.delayCall += MocapBowlerSetup.Apply;
    }

    /// <summary>The importer's settings for a captured delivery; reimports only if they changed
    /// (the settings live in the .meta, so a second pass finds them already right).
    ///
    /// The avatar is made from the clip's own skeleton, not copied from Bowler.fbx: Blender's
    /// export puts an "Armature" node above mixamorig5:Hips, so a copied avatar fails ("Copied
    /// Avatar Rig Configuration mis-match. Transform hierarchy does not match", 2026-11-13). The
    /// skeleton and its T-pose are Bowler.fbx's own (Tools/mocap round trip: rest bones within
    /// 1e-6 m), so the humanoid retarget onto the bowler is exact either way.</summary>
    public static void Configure(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            return;
        if (importer.animationType != ModelImporterAnimationType.Human ||
            importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();          // the takes below exist only once it imports as humanoid
        }
        ModelImporterClipAnimation clip = Clip(importer, path);
        ModelImporterClipAnimation[] now = importer.clipAnimations;
        if (clip == null)
        {
            Debug.LogError($"[MocapBowlerImport] {path}: no animation take in the file");
            return;
        }
        if (now == null || now.Length != 1 || !Same(now[0], clip))
        {
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            Debug.Log($"[MocapBowlerImport] {path}: humanoid clip {clip.name}, ReleaseBall at " +
                      $"{(clip.events.Length > 0 ? clip.events[0].time : -1f):F3} of the clip");
        }
    }

    private static ModelImporterClipAnimation Clip(ModelImporter importer, string path)
    {
        ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
        if (takes == null || takes.Length == 0)
            return null;
        Sidecar side = ReadSidecar(path);
        ModelImporterClipAnimation clip = takes[0];
        clip.name = Path.GetFileNameWithoutExtension(path);
        clip.loopTime = false;
        // As Steyn Cleaned.anim: turn and height in the pose, ground travel as root motion.
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = false;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        if (side != null && side.frames > 1)
            clip.events = new[]
            {
                new AnimationEvent
                {
                    functionName = nameof(AnimatedBowler.ReleaseBall),
                    time = Mathf.Clamp01(side.release_frame / (float)(side.frames - 1)),
                }
            };
        else
            Debug.LogError($"[MocapBowlerImport] {path}: no .mocap.json beside it, so no ReleaseBall event - " +
                           "the game would wait for a ball that never leaves his hand");
        return clip;
    }

    private static bool Same(ModelImporterClipAnimation a, ModelImporterClipAnimation b) =>
        a.name == b.name && a.loopTime == b.loopTime && a.lockRootRotation == b.lockRootRotation &&
        a.lockRootHeightY == b.lockRootHeightY && a.lockRootPositionXZ == b.lockRootPositionXZ &&
        a.keepOriginalOrientation == b.keepOriginalOrientation && a.keepOriginalPositionY == b.keepOriginalPositionY &&
        a.keepOriginalPositionXZ == b.keepOriginalPositionXZ && a.events.Length == b.events.Length &&
        (a.events.Length == 0 || (a.events[0].functionName == b.events[0].functionName &&
                                  Mathf.Abs(a.events[0].time - b.events[0].time) < 1e-4f));
}

/// <summary>
/// Each mocap clip in <see cref="MocapBowlerImport.Folder"/> as a state of the bowling controller:
/// from Run when Action is the clip's action value (as Steyn is reached with Action 2), back to Idle
/// at its end. Mirrored for left-armers like every other state. Safe to run again: an existing
/// state of the same name is updated, not duplicated.
/// </summary>
public static class MocapBowlerSetup
{
    [MenuItem("CricketVR/Bowling/Add Mocap Deliveries To Bowling Controller")]
    public static void Apply()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BowlerMirrorSetup.ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError("[MocapBowlerSetup] no controller at " + BowlerMirrorSetup.ControllerPath);
            return;
        }
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState run = Find(sm, "Run"), idle = Find(sm, "Idle");
        if (run == null || idle == null)
        {
            Debug.LogError("[MocapBowlerSetup] the bowling controller has no Run or Idle state");
            return;
        }
        int changed = 0, ready = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MocapBowlerImport.Folder.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    clip = c;
            MocapBowlerImport.Sidecar side = MocapBowlerImport.ReadSidecar(path);
            if (clip == null || side == null)
                continue;
            ready++;
            if (Wire(sm, run, idle, clip, side))
                changed++;
        }
        // Only when something changed: this runs after every script reload, and rewriting an
        // unchanged controller would churn its file ids in git.
        if (changed > 0)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MocapBowlerSetup] {changed} of {ready} mocap deliveries wired into {BowlerMirrorSetup.ControllerPath}");
        }
    }

    private static AnimatorState Find(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState c in sm.states)
            if (c.state.name == name)
                return c.state;
        return null;
    }

    // As Run -> Steyn; out to Idle near the end.
    private const float IntoExit = 0.537f, IntoDuration = 0.3f, OutExit = 0.9f, OutDuration = 0.25f;

    /// The clip's state and its two transitions, as they should be. False if they already were.
    private static bool Wire(AnimatorStateMachine sm, AnimatorState run, AnimatorState idle, AnimationClip clip,
                             MocapBowlerImport.Sidecar side)
    {
        AnimatorState st = Find(sm, clip.name);
        if (st != null && Correct(st, run, idle, clip, side))
            return false;
        st ??= sm.AddState(clip.name, new Vector3(650, 260 + 60 * sm.states.Length, 0));
        st.motion = clip;
        // Real time at a pace ball's Time (~1.24); Time still scales it with the ball's speed.
        st.speed = side.state_speed;
        st.speedParameterActive = true;
        st.speedParameter = "Time";
        st.mirrorParameterActive = true;
        st.mirrorParameter = AnimatedBowler.MirrorParam;
        st.iKOnFeet = true;                // as Steyn, Bumrah, Lyon: the pinned feet stay pinned

        foreach (AnimatorStateTransition t in run.transitions)
            if (t.destinationState == st)
                run.RemoveTransition(t);
        AnimatorStateTransition into = run.AddTransition(st);
        into.hasExitTime = true;
        into.exitTime = IntoExit;
        into.duration = IntoDuration;
        into.AddCondition(AnimatorConditionMode.Equals, side.action, "Action");
        into.AddCondition(AnimatorConditionMode.Equals, 0, "Run Repeat");

        foreach (AnimatorStateTransition t in st.transitions)
            st.RemoveTransition(t);
        AnimatorStateTransition outOf = st.AddTransition(idle);
        outOf.hasExitTime = true;
        outOf.exitTime = OutExit;
        outOf.duration = OutDuration;
        EditorUtility.SetDirty(st);
        return true;
    }

    private static bool Correct(AnimatorState st, AnimatorState run, AnimatorState idle, AnimationClip clip,
                                MocapBowlerImport.Sidecar side)
    {
        if (st.motion != clip || !Mathf.Approximately(st.speed, side.state_speed) || !st.speedParameterActive ||
            st.speedParameter != "Time" || !st.mirrorParameterActive || st.mirrorParameter != AnimatedBowler.MirrorParam ||
            !st.iKOnFeet)
            return false;
        int into = 0;
        foreach (AnimatorStateTransition t in run.transitions)
        {
            if (t.destinationState != st)
                continue;
            into++;
            if (!t.hasExitTime || !Mathf.Approximately(t.exitTime, IntoExit) || !Mathf.Approximately(t.duration, IntoDuration) ||
                t.conditions.Length != 2 ||
                t.conditions[0].parameter != "Action" || !Mathf.Approximately(t.conditions[0].threshold, side.action) ||
                t.conditions[1].parameter != "Run Repeat" || !Mathf.Approximately(t.conditions[1].threshold, 0f))
                return false;
        }
        if (into != 1 || st.transitions.Length != 1)
            return false;
        AnimatorStateTransition o = st.transitions[0];
        return o.destinationState == idle && o.hasExitTime && Mathf.Approximately(o.exitTime, OutExit) &&
               Mathf.Approximately(o.duration, OutDuration);
    }
}
