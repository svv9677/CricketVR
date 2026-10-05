using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything that moves in the big-screen replay: the players (bowler, fielders, keeper), the
/// ball, the bat, and the stumps and bails. Each has a stand-in (ReplayStandIns) under one
/// "Replay Stage" object, inactive unless a replay is on the screen.
///
/// The stage stays active: hidden (forceRenderingOff) and not skinned, a stand-in costs nothing,
/// and activating some 800 transforms when a replay began was a hitch of its own.
///
/// ShotRecorder captures the live actors into the recording's PoseTrack (Capture); CameraReplay
/// poses the stand-ins from it (Apply) and films them. Only the replay camera sees the stand-ins,
/// and only it is blind to the live actors, for the length of its render (SetReplayView) - so
/// the stand-ins stand exactly where the players stood, in the real stadium, with no copy of it.
///
/// One pose (PoseTrack) is laid out as: the `whole` objects posed in world space (each player's
/// root, each stump and bail), a position and a rotation each; then every player bone, a local
/// rotation each; then the bones that also move (hips, ragdoll bodies), a local position each.
/// </summary>
public sealed class ReplayActors
{
    private readonly List<Transform> wholeLive = new List<Transform>(), wholeStand = new List<Transform>();
    private readonly List<Transform> boneLive = new List<Transform>(), boneStand = new List<Transform>();
    private readonly List<Transform> pointLive = new List<Transform>(), pointStand = new List<Transform>();
    private readonly List<Renderer> live = new List<Renderer>(), stand = new List<Renderer>();
    private readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
    private bool[] liveWasOff;
    private GameObject stage;
    private Transform ballStand, batStand;
    private bool viewing;

    public int Players { get; private set; }
    public int Rotations => wholeLive.Count + boneLive.Count;
    public int Positions => wholeLive.Count + pointLive.Count;
    public int Renderers => stand.Count;
    /// The ball's stand-in (ReplayBallTrail sizes it for the camera).
    public Transform BallStandIn => ballStand;

    /// Builds the stand-ins for the scene's actors. Run once, at the start.
    public static ReplayActors Build(Main main)
    {
        var actors = new ReplayActors();
        actors.Create(main);
        return actors;
    }

    private void Create(Main main)
    {
        stage = new GameObject("Replay Stage");
        var players = new List<Animator>();
        foreach (Animator animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            if (animator.isHuman && animator.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                players.Add(animator);

        // Actors never copy each other: the ball may be in a hand, a player under a player.
        var skip = new HashSet<Transform>();
        if (main.theBall != null) skip.Add(main.theBall.transform);
        if (main.theBat != null) skip.Add(main.theBat.transform);
        foreach (Animator player in players)
            skip.Add(player.transform);

        foreach (Animator player in players)
            AddPlayer(player, skip);
        foreach (Transform prop in FindProps(skip))
            AddWhole(prop, skip);
        ballStand = main.theBall != null ? AddFree(main.theBall.transform, skip) : null;
        batStand = main.theBat != null ? AddFree(main.theBat.transform, skip) : null;
        Players = players.Count;
    }

    private void AddPlayer(Animator player, HashSet<Transform> skip)
    {
        Transform root = player.transform;
        var map = new Dictionary<Transform, Transform>();
        Transform copy = ReplayStandIns.CopyTree(root, stage.transform, skip, map);
        wholeLive.Add(root);
        wholeStand.Add(copy);

        var seen = new HashSet<Transform>();
        foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            foreach (Transform bone in skin.bones)
            {
                if (bone == null || bone == root || !map.ContainsKey(bone) || !seen.Add(bone))
                    continue;
                boneLive.Add(bone);
                boneStand.Add(map[bone]);
                // The hips carry the body; a ragdoll body can stretch from its joint.
                if (bone == player.GetBoneTransform(HumanBodyBones.Hips) || bone.GetComponent<Rigidbody>() != null)
                {
                    pointLive.Add(bone);
                    pointStand.Add(map[bone]);
                }
            }
        }
        AddRenderers(root, map);
    }

    /// Something posed whole, in world space, from the pose track: a stump, a bail.
    private void AddWhole(Transform source, HashSet<Transform> skip)
    {
        var map = new Dictionary<Transform, Transform>();
        Transform copy = ReplayStandIns.CopyTree(source, stage.transform, skip, map);
        wholeLive.Add(source);
        wholeStand.Add(copy);
        AddRenderers(source, map);
    }

    /// Something posed from the recording's own ball-and-bat track rather than the pose track.
    private Transform AddFree(Transform source, HashSet<Transform> skip)
    {
        var map = new Dictionary<Transform, Transform>();
        Transform copy = ReplayStandIns.CopyTree(source, stage.transform, skip, map);
        AddRenderers(source, map);
        return copy;
    }

    private void AddRenderers(Transform source, Dictionary<Transform, Transform> map)
    {
        int from = stand.Count;
        ReplayStandIns.CopyRenderers(source, map, live, stand);
        for (int i = from; i < stand.Count; i++)
            if (stand[i] is SkinnedMeshRenderer skin)
                skins.Add(skin);
    }

    /// Loose things the ball can knock about: a body with its own mesh (the stumps and bails),
    /// not part of a player, the ball or the bat.
    private static List<Transform> FindProps(HashSet<Transform> actors)
    {
        var props = new List<Transform>();
        foreach (Rigidbody body in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (body.GetComponent<MeshRenderer>() == null)
                continue;
            bool inActor = false;
            for (Transform t = body.transform; t != null && !inActor; t = t.parent)
                inActor = actors.Contains(t);
            if (!inActor)
                props.Add(body.transform);
        }
        return props;
    }

    /// Hide these from the replay camera as well (the screen itself, the hologram, the hands).
    public void HideAlso(IEnumerable<Renderer> renderers)
    {
        foreach (Renderer r in renderers)
            if (r != null && !live.Contains(r))
                live.Add(r);
    }

    /// And these canvases (the menus), for good: a canvas is not a renderer, so it moves to `layer`,
    /// which the replay camera does not draw and the eyes' camera does. Once, not per replay frame:
    /// a canvas takes its layer when its geometry is built, and swapping it each frame rebuilt it.
    public static void MoveCanvases(IEnumerable<Canvas> canvases, int layer)
    {
        foreach (Canvas canvas in canvases)
            if (canvas != null)
                canvas.gameObject.layer = layer;
    }

    // ---- Recording -----------------------------------------------------------------------------

    /// The live actors' pose now, into `slot` of `poses`.
    public void Capture(PoseTrack poses, int slot)
    {
        int whole = wholeLive.Count;
        int r = slot * poses.Rotations, p = slot * poses.Positions;
        Quaternion[] q = poses.rotations;
        Vector3[] v = poses.positions;
        for (int i = 0; i < whole; i++)
            wholeLive[i].GetPositionAndRotation(out v[p + i], out q[r + i]);
        r += whole;
        p += whole;
        for (int i = 0; i < boneLive.Count; i++)
            q[r + i] = boneLive[i].localRotation;
        for (int i = 0; i < pointLive.Count; i++)
            v[p + i] = pointLive[i].localPosition;
    }

    /// False when an actor has been destroyed since the stand-ins were built.
    public bool StillLive()
    {
        foreach (Transform t in wholeLive) if (t == null) return false;
        foreach (Transform t in boneLive) if (t == null) return false;
        return true;
    }

    // ---- Playback ------------------------------------------------------------------------------

    /// Every stand-in as it was `time` seconds into `shot`.
    public void Apply(ShotRecording shot, float time)
    {
        shot.Evaluate(time, out Vector3 ball, out Vector3 batPosition, out Quaternion batRotation);
        if (ballStand != null) ballStand.localPosition = ball;
        if (batStand != null) batStand.SetLocalPositionAndRotation(batPosition, batRotation);

        PoseTrack poses = shot.Poses;
        if (poses == null || poses.Rotations != Rotations || !poses.Locate(shot.SinceBegin(time), out int a, out int b, out float f))
            return;
        int whole = wholeStand.Count;
        int ra = a * poses.Rotations, rb = b * poses.Rotations, pa = a * poses.Positions, pb = b * poses.Positions;
        Quaternion[] q = poses.rotations;
        Vector3[] v = poses.positions;
        for (int i = 0; i < whole; i++)
            wholeStand[i].SetLocalPositionAndRotation(Vector3.LerpUnclamped(v[pa + i], v[pb + i], f),
                                                      Quaternion.Slerp(q[ra + i], q[rb + i], f));
        for (int i = 0; i < boneStand.Count; i++)
            boneStand[i].localRotation = Quaternion.Slerp(q[ra + whole + i], q[rb + whole + i], f);
        for (int i = 0; i < pointStand.Count; i++)
            pointStand[i].localPosition = Vector3.LerpUnclamped(v[pa + whole + i], v[pb + whole + i], f);
    }

    /// Skin the stand-ins this frame. Only for frames the replay camera films: a stand-in is never
    /// visible to the eyes' camera, so without this it would not be skinned in time to be filmed.
    public void SkinThisFrame(bool on)
    {
        foreach (SkinnedMeshRenderer skin in skins)
            skin.updateWhenOffscreen = on;
    }

    /// For the replay camera's render only: the stand-ins in, the live actors (and HideAlso) out.
    /// Put back afterwards exactly as they were.
    public void SetReplayView(bool on)
    {
        if (on == viewing)
            return;
        viewing = on;
        if (liveWasOff == null || liveWasOff.Length != live.Count)
            liveWasOff = new bool[live.Count];
        for (int i = 0; i < live.Count; i++)
        {
            Renderer r = live[i];
            if (r == null)
                continue;
            if (on)
            {
                liveWasOff[i] = r.forceRenderingOff;
                r.forceRenderingOff = true;
            }
            else
            {
                r.forceRenderingOff = liveWasOff[i];
            }
        }
        foreach (Renderer r in stand)
            r.forceRenderingOff = !on;
    }
}
