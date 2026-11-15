using System.Linq;
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
///
/// None of this runs by itself: only from CricketVR > Bowling > Set Up Mocap Deliveries, after a
/// clip is exported or re-exported (2026-11-15: no automatic setup; what is set in the editor is
/// what plays).
/// </summary>
public static class MocapBowlerImport
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
        // The handover from Run (normalised Run time it leaves at, where in this clip it enters,
        // seconds of blend): Run -> Steyn's 0.537 and 0.3 s unless measured for the clip
        // (Tools/mocap: the run phase that matches the clip's first frames).
        public float run_exit = 0.537f;
        public float clip_offset = 0f;
        public float blend = 0.3f;
        // And out of it: the state it hands over to (Idle, or Follow Through when the clip ends
        // early in the follow-through and he should run on), when, how long, and where in it.
        public string exit_to = "Idle";
        public float exit_time = 0.9f;
        public float exit_blend = 0.25f;
        public float exit_offset = 0f;
        // Optionally another clip's run-in and load-up first (e.g. Steyn Cleaned's gather), handing
        // over to this clip at loadup_exit (normalised, in the load-up clip) into clip_offset.
        public string loadup_clip = "";
        public float loadup_speed = 4.725f;      // Steyn's state speed (4.5) x BowlerSnap's base (1.05)
        public float loadup_exit = 0.465f;
        public float loadup_blend = 0.2f;
    }

    public static Sidecar ReadSidecar(string fbxPath)
    {
        string json = Path.ChangeExtension(fbxPath, ".mocap.json");
        return File.Exists(json) ? JsonUtility.FromJson<Sidecar>(File.ReadAllText(json)) : null;
    }

    /// Each clip's import settings (only those that are out of date, so a second run changes nothing),
    /// then any missing controller states. Run it by hand after exporting or re-exporting a clip.
    [MenuItem("CricketVR/Bowling/Set Up Mocap Deliveries")]
    public static void SetUpAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))
            return;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
            Configure(AssetDatabase.GUIDToAssetPath(guid));
        MocapBowlerSetup.Apply();
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
        // Keyframe reduction re-fits the curves within an error budget: on a captured action that has
        // been smoothed frame by frame it removes 38% of the keys (MocapAnderson, measured) and puts
        // back small wobbles. Every key is kept.
        if (importer.animationCompression != ModelImporterAnimationCompression.Off)
        {
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
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
/// from Run when Action is the clip's action value (as Steyn is reached with Action 2), on to its
/// exit_to state at its end. Mirrored for left-armers like every other state.
///
/// Only adds what is missing. A state or transition that is already there is left exactly as it is,
/// so timings tuned in the Animator window stay (2026-11-15: they used to be put back from the
/// sidecar on every script reload, entering Play included). The sidecar's timings are only the
/// starting values for a new clip. Runs only from the menu (MocapBowlerImport.SetUpAll).
/// </summary>
public static class MocapBowlerSetup
{
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
        int changed = EnsureRunPace(ctrl, run) ? 1 : 0, ready = 0;
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
            WarnIfKeepsBall(sm, clip, side);
        }
        changed += Prune(sm, run);
        MocapDelivery.Reload();            // a new or re-exported clip's sidecar (marks, priority) is read again
        if (changed > 0)
        {
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
        }
        Debug.Log($"[MocapBowlerSetup] {ready} mocap deliveries; {changed} added or changed in {BowlerMirrorSetup.ControllerPath}");
    }

    /// The Run state's speed multiplied by a RunPace parameter (default 1: unchanged), so a captured
    /// delivery can have the run-up come in at its own speed (MocapDelivery.SetRunPace).
    private static bool EnsureRunPace(AnimatorController ctrl, AnimatorState run)
    {
        bool changed = false;
        if (!ctrl.parameters.Any(p => p.name == MocapDelivery.RunPaceParam))
        {
            ctrl.AddParameter(new AnimatorControllerParameter
            {
                name = MocapDelivery.RunPaceParam, type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });
            changed = true;
        }
        if (!run.speedParameterActive || run.speedParameter != MocapDelivery.RunPaceParam)
        {
            run.speedParameterActive = true;
            run.speedParameter = MocapDelivery.RunPaceParam;
            EditorUtility.SetDirty(run);
            changed = true;
        }
        return changed;
    }

    /// The states of a mocap clip that has been deleted (its FBX gone, so the state has no motion):
    /// reached from Run with Action 5 or more (the game's own actions are 0-4), with its load-up.
    private static int Prune(AnimatorStateMachine sm, AnimatorState run)
    {
        int removed = 0;
        foreach (AnimatorStateTransition t in run.transitions.ToArray())
        {
            AnimatorState dest = t.destinationState;
            bool mocap = t.conditions.Any(c => c.parameter == "Action" && c.mode == AnimatorConditionMode.Equals && c.threshold >= 5f);
            if (dest == null || !mocap)
                continue;
            string name = dest.name.EndsWith("LoadUp") ? dest.name.Substring(0, dest.name.Length - "LoadUp".Length) : dest.name;
            AnimatorState main = Find(sm, name), lu = Find(sm, name + "LoadUp");
            if (main != null && main.motion != null)
                continue;
            run.RemoveTransition(t);
            if (lu != null) sm.RemoveState(lu);
            if (main != null) sm.RemoveState(main);
            Debug.Log($"[MocapBowlerSetup] {name}: its clip is gone; state removed from the bowling controller");
            removed++;
        }
        return removed;
    }

    /// The state a clip hands over to at its end (Sidecar exit_to), Idle if there is no such state.
    private static AnimatorState ExitState(AnimatorStateMachine sm, AnimatorState idle, MocapBowlerImport.Sidecar side) =>
        (string.IsNullOrEmpty(side.exit_to) ? null : Find(sm, side.exit_to)) ?? idle;

    private static AnimatorState Find(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState c in sm.states)
            if (c.state.name == name)
                return c.state;
        return null;
    }

    // Into it from Run and out of it, per clip (Sidecar). With a load-up clip the chain is
    // Run -> <clip>LoadUp (another action's run-in and load-up) -> <clip> -> exit_to; without one,
    // Run -> <clip> -> exit_to. Each piece is made only if it is missing.
    private static bool Wire(AnimatorStateMachine sm, AnimatorState run, AnimatorState idle, AnimationClip clip,
                             MocapBowlerImport.Sidecar side)
    {
        AnimationClip luClip = string.IsNullOrEmpty(side.loadup_clip) ? null
            : AssetDatabase.LoadAssetAtPath<AnimationClip>(side.loadup_clip);
        if (!string.IsNullOrEmpty(side.loadup_clip) && luClip == null)
            Debug.LogError($"[MocapBowlerSetup] {clip.name}: no load-up clip at {side.loadup_clip}; wired without one");
        bool changed = false;
        AnimatorState st = Find(sm, clip.name);
        if (st == null)
        {
            st = sm.AddState(clip.name, new Vector3(650, 260 + 60 * sm.states.Length, 0));
            // Real time at a pace ball's Time (~1.24); Time still scales it with the ball's speed.
            Configure(st, clip, side.state_speed);
            changed = true;
        }
        else if (st.motion == null)
        {
            st.motion = clip;              // its clip was re-imported: the rest of the state is left as it is
            changed = true;
        }
        AnimatorState lu = Find(sm, clip.name + "LoadUp");
        if (luClip != null && lu == null)
        {
            lu = sm.AddState(clip.name + "LoadUp", new Vector3(450, 260 + 60 * sm.states.Length, 0));
            Configure(lu, luClip, side.loadup_speed);
            changed = true;
        }
        AnimatorState entry = lu ?? st;
        if (!run.transitions.Any(t => t.destinationState == entry))
        {
            AnimatorStateTransition into = run.AddTransition(entry);
            Set(into, side.run_exit, side.blend, lu != null ? 0f : side.clip_offset);
            into.AddCondition(AnimatorConditionMode.Equals, side.action, "Action");
            into.AddCondition(AnimatorConditionMode.Equals, 0, "Run Repeat");
            changed = true;
        }
        if (lu != null && lu.transitions.Length == 0)
        {
            Set(lu.AddTransition(st), side.loadup_exit, side.loadup_blend, side.clip_offset);
            EditorUtility.SetDirty(lu);
            changed = true;
        }
        if (st.transitions.Length == 0)
        {
            Set(st.AddTransition(ExitState(sm, idle, side)), side.exit_time, side.exit_blend, side.exit_offset);
            changed = true;
        }
        if (changed)
            EditorUtility.SetDirty(st);
        return changed;
    }

    /// A blend out that starts before the release never reaches the ReleaseBall event: he keeps the ball.
    private static void WarnIfKeepsBall(AnimatorStateMachine sm, AnimationClip clip, MocapBowlerImport.Sidecar side)
    {
        AnimatorState st = Find(sm, clip.name);
        if (st == null || side.frames < 2)
            return;
        float release = side.release_frame / (float)(side.frames - 1);
        foreach (AnimatorStateTransition t in st.transitions)
            if (t.hasExitTime && t.exitTime <= release)
                Debug.LogWarning($"[MocapBowlerSetup] {clip.name}: it blends out at exit time {t.exitTime:F3}, before the " +
                                 $"release at {release:F3}, so he will keep the ball");
    }

    private static void Configure(AnimatorState st, AnimationClip clip, float speed)
    {
        st.motion = clip;
        st.speed = speed;
        st.speedParameterActive = true;
        st.speedParameter = "Time";
        st.mirrorParameterActive = true;
        st.mirrorParameter = AnimatedBowler.MirrorParam;
        st.iKOnFeet = true;                // as Steyn, Bumrah, Lyon: the pinned feet stay pinned
    }

    private static void Set(AnimatorStateTransition t, float exit, float duration, float offset)
    {
        t.hasExitTime = true;
        t.exitTime = exit;
        t.duration = duration;
        t.offset = offset;
    }
}
