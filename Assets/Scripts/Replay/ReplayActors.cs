using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    private Transform headStand;        // the batter's helmet, posed from the recorded head
    private bool viewing;
    /// From the headset's eye to the middle of the head: back and a little up.
    private static readonly Vector3 EyeToHeadCentre = new Vector3(0f, 0.02f, -0.08f);

    public int Players { get; private set; }
    public int Rotations => wholeLive.Count + boneLive.Count;
    public int Positions => wholeLive.Count + pointLive.Count;
    public int Renderers => stand.Count;

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
        headStand = AddBatterHelmet();
        Players = players.Count;
    }

    /// A helmet for the batter's head in the replay: the keeper's own helmet mesh and materials,
    /// turned to face the way the head does and centred on it. Not in the live game: the batter
    /// plays without one, and it is shown only to the replay camera (as every stand-in is).
    private Transform AddBatterHelmet()
    {
        MeshFilter source = null;
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (filter.transform.parent != null && filter.transform.parent.name == "Helmet" && filter.sharedMesh != null &&
                !filter.transform.IsChildOf(stage.transform))     // the live one, not a stand-in's copy of it
            {
                source = filter;
                break;
            }
        var liveRenderer = source != null ? source.GetComponent<MeshRenderer>() : null;
        if (liveRenderer == null)
            return null;
        Transform helmet = source.transform;
        // Which way the helmet faces, from the keeper facing the batter; and where its middle is.
        Animator animator = helmet.GetComponentInParent<Animator>();
        Vector3 facing = animator != null ? Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up) : Vector3.right;
        Quaternion toFace = Quaternion.Inverse(Quaternion.LookRotation(facing.sqrMagnitude > 1e-4f ? facing : Vector3.right, Vector3.up));
        Quaternion relative = toFace * helmet.rotation;
        Vector3 centre = helmet.InverseTransformPoint(liveRenderer.bounds.center);

        var parent = new GameObject("Batter Helmet").transform;
        parent.SetParent(stage.transform, false);
        var piece = new GameObject("Helmet").transform;
        piece.SetParent(parent, false);
        piece.localRotation = relative;
        piece.localScale = helmet.lossyScale;
        piece.localPosition = -(relative * Vector3.Scale(centre, helmet.lossyScale));
        piece.gameObject.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var copy = piece.gameObject.AddComponent<MeshRenderer>();
        copy.sharedMaterials = liveRenderer.sharedMaterials;
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.forceRenderingOff = true;
        stand.Add(copy);        // shown and hidden with the other stand-ins (SetReplayView)

        // A head in it: the helmet alone is a dome and a grille floating apart.
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(head.GetComponent<Collider>());
        head.name = "Head";
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, -0.035f, 0.005f);
        head.transform.localScale = new Vector3(0.165f, 0.21f, 0.19f);
        var skin = head.GetComponent<MeshRenderer>();
        var skinMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Batter Head" };
        skinMaterial.SetColor("_BaseColor", new Color(0.5f, 0.34f, 0.25f));
        skin.sharedMaterial = skinMaterial;
        skin.shadowCastingMode = ShadowCastingMode.Off;
        skin.forceRenderingOff = true;
        stand.Add(skin);
        return parent;
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
        if (headStand != null && shot.Head(time, out Vector3 head, out Quaternion look))
            headStand.SetPositionAndRotation(head + look * EyeToHeadCentre, look);

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
