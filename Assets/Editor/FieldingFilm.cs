using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Films fielders and the keeper taking the ball, for checking by eye: in Play mode, launch a ball
/// at a chosen fielder (or the keeper) from a chosen line, height and pace, record whether it is
/// taken, and save side-on frames in slow motion.
///
/// Driven from the Editor (e.g. the MCP execute_code tool) once Play mode is running:
///   FieldingFilm.Clear();
///   FieldingFilm.AtFielder(4, FieldingFilm.Kind.Ground, side: 1.5f);
///   FieldingFilm.AtKeeper(side: 0.9f, height: 0.6f);
///   FieldingFilm.Start("/path/to/frames");
///   ... FieldingFilm.Report  (one line per shot once Done)
/// Frames land in <dir>/<nn>_<name>/f<nnn>.png, filmed at FilmFps of game time with the game
/// slowed to TimeScale; each shot runs until the ball is taken, passes, or ShotTimeout.
/// </summary>
public static class FieldingFilm
{
    public enum Kind { Ground, Bounce, Catch, HighCatch, Roller }

    public class Shot
    {
        public string name;
        public Vector3 from, velocity, look;
        public bool delivery;
        public Vector3 cameraSide;
    }

    private const float TimeScale = 0.35f, FilmFps = 20f, ShotTimeout = 6f, AfterTake = 1.2f;
    private const int Width = 480, Height = 360;
    /// Where a hit leaves the bat: in front of the batter's stumps, a little above the ground.
    private static readonly Vector3 BatPoint = new Vector3(BallDelivery.BatsmanStumpsX - 1.2f, 0.7f, 0f);

    private static readonly List<Shot> shots = new List<Shot>();
    private static readonly List<string> report = new List<string>();
    private static int index;
    private static float shotStart, nextFrame, takenAt;
    private static int frame, phase;
    private static string outDir;
    private static Camera cam;
    private static RenderTexture rt;
    private static Texture2D grab;

    public static bool Done { get; private set; } = true;
    /// Also show the film camera in the Game view, on top of the game's own, to watch live.
    public static bool Watch;
    public static string Report => string.Join("\n", report);

    public static void Clear() { shots.Clear(); report.Clear(); }

    /// A ball hit at fielder `number` (1-9, as named AFielder_0n), `side` metres off his line
    /// (positive to his right as he faces the bat).
    public static void AtFielder(int number, Kind kind, float side = 0f, float pace = 20f)
    {
        Vector3 start = Constants.fieldingPositions[number - 1];
        start.y = BallFlight.GroundY(start);
        Vector3 toBat = Flat(BatPoint - start).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toBat);
        Vector3 target = start + right * side;
        Vector3 from = BatPoint;
        Vector3 v;
        switch (kind)
        {
            case Kind.Ground: from.y = 0.1f; v = Flat(target - from).normalized * pace; break;
            case Kind.Bounce: v = Solve(from, target + toBat * 8f, 0.05f, pace); break;
            case Kind.Catch: v = Solve(from, target, 1.3f, pace); break;
            case Kind.HighCatch: v = Solve(from, target, 2.0f, pace * 0.7f); break;
            default:
                // Rolls to a stop about a metre in front of him.
                from.y = 0.1f;
                float run = Flat(target - from).magnitude - 1f;
                v = Flat(target - from).normalized * Mathf.Sqrt(2f * BallFlight.RollingDecel * run);
                break;
        }
        shots.Add(new Shot
        {
            name = $"F{number}_{kind}_{side:+0.0;-0.0;0}",
            from = from, velocity = v, look = start + Vector3.up * 0.9f,
            cameraSide = right,
        });
    }

    /// A ball hit straight back at the bowler where he stands, `side` metres off his line.
    public static void AtBowler(Kind kind, float side = 0f, float pace = 18f)
    {
        Transform b = AnimatedBowler.Instance.transform;
        Vector3 start = b.position;
        start.y = BallFlight.GroundY(start);
        Vector3 toBat = Flat(BatPoint - start).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toBat);
        Vector3 target = start + right * side;
        Vector3 from = BatPoint;
        Vector3 v = kind == Kind.Ground ? Flat(target - from).normalized * pace
                  : kind == Kind.Catch ? Solve(from, target, 1.3f, pace)
                  : Solve(from, target + toBat * 4f, 0.05f, pace);
        if (kind == Kind.Ground) from.y = 0.1f;
        shots.Add(new Shot
        {
            name = $"Bowler_{kind}_{side:+0.0;-0.0;0}",
            from = from, velocity = v, look = start + Vector3.up * 0.9f,
            cameraSide = right,
        });
    }

    /// A ball past the bat to the keeper, arriving `side` metres to his right (as he faces the
    /// bowler) at `height`, at `pace` m/s.
    /// The ball starts well up the pitch (x 2, ~0.45 s from him at 30 m/s: what a keeper standing
    /// back gets once it has pitched) on a line parallel to the pitch, so it misses the stumps; one
    /// straight at them (|side| under 0.3) starts past the stumps instead.
    public static void AtKeeper(float side, float height, float pace = 30f)
    {
        Transform keeper = Main.Instance.theKeeper.transform;
        Vector3 k = keeper.position;
        Vector3 facing = Flat(keeper.forward).normalized;             // up the pitch
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        Vector3 target = k + facing * 0.5f + right * side;
        target.y = BallFlight.GroundY(k) + height;
        Vector3 from = Mathf.Abs(side) < 0.3f ? new Vector3(11.0f, height + 0.2f, 0f)
                                              : new Vector3(2f, height + 0.4f, target.z);
        float t = Flat(target - from).magnitude / pace;
        Vector3 v = Flat(target - from) / t;
        v.y = (target.y - from.y + 0.5f * -Physics.gravity.y * t * t) / t;
        shots.Add(new Shot
        {
            name = $"K_{side:+0.0;-0.0;0}_{height:0.0}",
            from = from, velocity = v, look = k + Vector3.up * 0.7f, delivery = true,
            cameraSide = -facing * 0.35f + right,
        });
    }

    /// Launch speed from `from` so the ball comes down through `height` above the ground at
    /// `target`, `pace` m/s along the ground: vertical speed by bisection on the real flight model.
    private static Vector3 Solve(Vector3 from, Vector3 target, float height, float pace)
    {
        Vector3 flat = Flat(target - from);
        float distance = flat.magnitude;
        Vector3 dir = flat / distance;
        var path = new List<BallFlight.Sample>(1024);
        float lo = -2f, hi = 30f;
        for (int i = 0; i < 40; i++)
        {
            float vy = 0.5f * (lo + hi);
            BallFlight.Simulate(from, dir * pace + Vector3.up * vy, BallFlight.DeliveryEffects.None, 8f, path,
                s => Flat(s.position - from).magnitude >= distance, 0.01f);
            float y = path.Count > 0 ? path[path.Count - 1].position.y - BallFlight.GroundY(target) : 0f;
            bool reached = path.Count > 0 && Flat(path[path.Count - 1].position - from).magnitude >= distance - 0.05f;
            if (!reached || y < height) lo = vy; else hi = vy;
        }
        return dir * pace + Vector3.up * hi;
    }

    public static void Start(string directory)
    {
        outDir = directory;
        Directory.CreateDirectory(outDir);
        index = -1;
        Done = false;
        report.Clear();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Next();
    }

    private static void Next()
    {
        index++;
        if (index >= shots.Count)
        {
            Finish();
            return;
        }
        phase = 0;   // waiting for a quiet field
        ResetPlay();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        if (cam != null) Object.DestroyImmediate(cam.gameObject);
        if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
        Done = true;
    }

    /// Everyone back to their marks, the keeper out of any take, the ball free.
    private static void ResetPlay()
    {
        Main inst = Main.Instance;
        Time.timeScale = 1f;
        // Main's own dead-ball timers from the last shot (WaitAndSetGameState) would otherwise fire
        // into the next one: a take near the end of a shot left one pending, it reset the game
        // mid-roller, and the fielder stood on his mark beside a dead ball (looked like a stall).
        inst.StopAllCoroutines();
        inst.gameState = eGameState.InGame_ResetToReady;
        // Park the ball, still and without physics, where the next shot starts. Teleported there
        // from the keeper's gloves at the moment of launch, it registered in the missed-ball trigger
        // behind him on its way out, and the delivery ended before it began.
        Rigidbody rb = inst.theBallRigidBody;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        Collider bc = inst.theBall.GetComponent<Collider>();
        if (bc != null) bc.enabled = false;
        inst.theBall.transform.SetParent(null);
        rb.position = shots[index].from;
        inst.theBall.transform.position = shots[index].from;
        Physics.SyncTransforms();
        // A fielder only lets go of the ball on a new delivery (SelectDelivery), which this skips:
        // left holding, he would pin every later ball to his hands.
        foreach (AnimatedFielder f in Object.FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
        {
            typeof(AnimatedFielder).GetField("holdingBall", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(f, false);
            f.ReturnToStart();
        }
        BowlerFielding bowlerFielding = Object.FindFirstObjectByType<BowlerFielding>();
        if (bowlerFielding != null)
            bowlerFielding.StopFielding();
        KeeperCatcher keeper = inst.theKeeper.GetComponentInChildren<KeeperCatcher>();
        if (keeper != null)
        {
            typeof(KeeperCatcher).GetField("holding", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(keeper, false);
            typeof(KeeperCatcher).GetMethod("ResetStance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(keeper, null);
        }
        shotStart = Time.time;
    }

    private static void Tick()
    {
        if (!Application.isPlaying)
        {
            Finish();
            return;
        }
        Main inst = Main.Instance;
        if (phase == 0)
        {
            // Launch once Main is idle in Ready (its reset timer would otherwise fire mid-shot) and
            // the field has had a moment at their marks.
            if (Time.time - shotStart < 0.8f || inst.gameState != eGameState.InGame_Ready)
                return;
            Launch(shots[index]);
            phase = 1;
            return;
        }

        Track(shots[index]);
        if (Time.time >= nextFrame)
        {
            Film();
            nextFrame += 1f / FilmFps;
        }
        eGameState s = inst.gameState;
        bool taken = s == eGameState.InGame_BallFielded || s == eGameState.InGame_BallFielded_Loop ||
                     (shots[index].delivery && (s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop) && inst.theBallRigidBody.isKinematic);
        if (taken && takenAt < 0f)
            takenAt = Time.time;
        float elapsed = Time.time - shotStart;
        bool over = (takenAt >= 0f && Time.time - takenAt > AfterTake) || elapsed > ShotTimeout ||
                    s == eGameState.InGame_BallPastBoundary || s == eGameState.InGame_BallPastBoundaryLoop ||
                    (shots[index].delivery && !taken && (s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop) && elapsed > 1.5f);
        if (!over)
            return;
        report.Add($"{shots[index].name}: {(takenAt >= 0f ? $"TAKEN at {takenAt - shotStart:F2}s" : "NOT TAKEN")} " +
                   $"closest to hands {closest:F2} m at {closestAt:F2}s ({closestWho}, reach {closestWeight:F2}, {closestNote}); " +
                   $"worst arm clip {worstClip * 100f:F1} cm ({worstClipWhere}) at {worstClipAt:F2}s");
        Next();
    }

    private static void Launch(Shot shot)
    {
        Main inst = Main.Instance;
        Rigidbody rb = inst.theBallRigidBody;
        inst.theBall.transform.SetParent(null);
        Collider c = inst.theBall.GetComponent<Collider>();
        if (c != null) c.enabled = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.position = shot.from;
        inst.theBall.transform.position = shot.from;
        rb.linearVelocity = shot.velocity;
        rb.angularVelocity = Vector3.zero;
        inst.theBallScript.fresh = false;
        inst.theBallScript.bounced = shot.delivery;
        inst.theBatScript.hasHitBall = true;   // the idle bat in the Editor must not meet it
        inst.gameState = shot.delivery ? eGameState.InGame_DeliverBallLoop : eGameState.InGame_BallHit;

        Time.timeScale = TimeScale;
        shotStart = Time.time;
        takenAt = -1f;
        frame = 0;
        nextFrame = Time.time;
        PlaceCamera(shot);
        cameraFocus = shot.look;
        subjectFacing = shot.delivery ? Flat(inst.theKeeper.transform.forward).normalized
                                      : Flat(BatPoint - shot.look).normalized;
        closest = float.MaxValue;
        closestAt = closestWeight = 0f;
        closestWho = "nobody";
        closestNote = "";
        worstClip = 0f;
        worstClipWhere = "none";
        worstClipAt = 0f;
        Directory.CreateDirectory(ShotDir());
    }

    // Closest the ball came to the hands of whoever was going for it, and his state then.
    private static float closest, closestAt, closestWeight;
    private static string closestWho, closestNote;
    private static Vector3 cameraFocus;

    /// Follow the man going for it (the keeper on a keeper shot; otherwise the fielder with a
    /// target or the ball, nearest the ball), and note how close it came to his hands.
    private static void Track(Shot shot)
    {
        Main inst = Main.Instance;
        Vector3 ball = inst.theBall.transform.position;
        HumanoidReach reach = null;
        string who = null, note = "";
        if (shot.delivery)
        {
            reach = inst.theKeeper.GetComponentInChildren<HumanoidReach>();
            who = "keeper";
        }
        else
        {
            float best = float.MaxValue;
            foreach (AnimatedFielder f in Object.FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
            {
                if (!f.HasTarget && !f.HoldingBall)
                    continue;
                float d = (f.transform.position - ball).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    reach = f.GetComponentInChildren<HumanoidReach>();
                    who = f.name;
                    Vector3 v = Flat(inst.theBallRigidBody.linearVelocity);
                    float offLine = v.sqrMagnitude > 0.25f ? Mathf.Abs(Vector3.Cross(v.normalized, Flat(f.transform.position - ball)).y) : -1f;
                    note = $"speed {f.CurrentSpeed:F1}, {Flat(f.Target - f.transform.position).magnitude:F2} m off his mark, {offLine:F2} m off the line";
                }
            }
        }
        if (reach == null || !reach.Ready)
            return;
        float gap = Vector3.Distance(ball, reach.HoldPoint);
        Animator anim = reach.GetComponent<Animator>();
        float clip = ArmPenetration(anim, out string clipWhere);
        // The IK goals as set, against the torso, and how far each wrist is from its goal: a goal
        // outside with the wrist inside means the solver is not landing it.
        Transform hipsT = anim.GetBoneTransform(HumanBodyBones.Hips), neckT = anim.GetBoneTransform(HumanBodyBones.Neck);
        Vector3 goalL = reach.GoalLeft, goalR = reach.GoalRight;
        float rawDepthL = 0.12f - DistanceToSegment(reach.RawGoalLeft, hipsT.position, neckT.position);
        float goalDepthL = 0.12f - DistanceToSegment(goalL, hipsT.position, neckT.position);
        float goalDepthR = 0.12f - DistanceToSegment(goalR, hipsT.position, neckT.position);
        float missL = Vector3.Distance(goalL, anim.GetBoneTransform(HumanBodyBones.LeftHand).position);
        float missR = Vector3.Distance(goalR, anim.GetBoneTransform(HumanBodyBones.RightHand).position);
        float guardErr = Mathf.Max(Vector3.Distance(reach.GuardHips, hipsT.position), Vector3.Distance(reach.GuardNeck, neckT.position));
        clipWhere += $" | raw L {rawDepthL:F3} goal L {goalDepthL:F3} R {goalDepthR:F3} wrist off goal L {missL:F3} R {missR:F3} guard spine off {guardErr:F3}";
        if (clip > worstClip)
        {
            worstClip = clip;
            worstClipWhere = clipWhere;
            worstClipAt = Time.time - shotStart;
        }
        // Every frame of the approach, for reading why a ball got past.
        File.AppendAllText(Path.Combine(ShotDir(), "trace.csv"),
            $"{Time.time - shotStart:F3},{gap:F3},{reach.HoldPoint.y:F3},{ball.y:F3},{reach.weight:F2},{reach.SmoothedWeight:F2},{clip:F3},{clipWhere},{note}\n");
        if (gap < closest && !inst.theBallRigidBody.isKinematic)
        {
            closest = gap;
            closestAt = Time.time - shotStart;
            closestWho = who;
            closestWeight = reach.SmoothedWeight;
            closestNote = note;
        }
        Vector3 focus = reach.transform.position + Vector3.up * 0.8f;
        cameraFocus = Vector3.Lerp(cameraFocus, focus, 0.2f);
        // Which way he faces, for placing the cameras round him: the keeper's anchor (his body
        // rolls in a dive), a fielder's own body.
        Vector3 facing = Flat(shot.delivery ? inst.theKeeper.transform.forward : reach.transform.forward);
        if (facing.sqrMagnitude > 1e-4f)
            subjectFacing = Vector3.Slerp(subjectFacing, facing.normalized, 0.15f).normalized;
        PoseCamera(90f);
    }

    /// Cameras round the man, by angle from his front (0 = in front looking at him, 90 = his
    /// right side, 180 = behind), and the folder each one's frames go in.
    private static readonly (float angle, string name)[] Angles =
    {
        (90f, "side"), (0f, "front"), (45f, "front34"), (180f, "back"),
    };
    private const float CameraDistance = 3.0f;
    private static Vector3 subjectFacing = Vector3.forward;

    private static void PoseCamera(float angle)
    {
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * subjectFacing;
        cam.transform.position = cameraFocus + dir * CameraDistance + Vector3.up * 0.35f;
        cam.transform.LookAt(cameraFocus);
    }

    // ---- Arms through the body --------------------------------------------------------------------
    // Body parts as capsules (bone to bone, radius metres) and points along each arm. An arm point
    // inside a capsule is an arm through the body; the depth is how far in. The torso radius is
    // its depth, not its width: an arm hanging at the side sits ~0.2 m off the spine and is clear.
    private static readonly (HumanBodyBones a, HumanBodyBones b, float r, string name)[] BodyParts =
    {
        (HumanBodyBones.Hips, HumanBodyBones.Neck, 0.12f, "torso"),
        (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.075f, "L thigh"),
        (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.075f, "R thigh"),
        (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.055f, "L shin"),
        (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.055f, "R shin"),
        (HumanBodyBones.Head, HumanBodyBones.Head, 0.11f, "head"),
    };

    /// Deepest any arm point goes into the body this frame, and where.
    public static float ArmPenetration(Animator a, out string where)
    {
        where = "";
        float worst = 0f;
        foreach (bool left in new[] { true, false })
        {
            Transform upper = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (upper == null || lower == null || hand == null)
                continue;
            var points = new (Vector3 p, string n)[]
            {
                (Vector3.Lerp(upper.position, lower.position, 0.6f), "upper arm"),
                (lower.position, "elbow"),
                (Vector3.Lerp(lower.position, hand.position, 0.5f), "forearm"),
                (hand.position, "wrist"),
            };
            foreach (var part in BodyParts)
            {
                Transform ta = a.GetBoneTransform(part.a), tb = a.GetBoneTransform(part.b);
                if (ta == null || tb == null)
                    continue;
                foreach (var (p, n) in points)
                {
                    float depth = part.r - DistanceToSegment(p, ta.position, tb.position);
                    if (depth > worst)
                    {
                        worst = depth;
                        where = $"{(left ? "L" : "R")} {n} in {part.name}";
                    }
                }
            }
        }
        return worst;
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    private static float worstClip;
    private static string worstClipWhere;
    private static float worstClipAt;

    private static string ShotDir() => Path.Combine(outDir, $"{index:00}_{shots[index].name}");

    private static void PlaceCamera(Shot shot)
    {
        if (cam == null)
        {
            cam = new GameObject("FieldingFilm camera").AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 45f;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            rt = new RenderTexture(Width, Height, 24);
            grab = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        }
        cam.enabled = Watch;
        cam.depth = 100f;
        Vector3 side = Flat(shot.cameraSide).normalized;
        cam.transform.position = shot.look + side * 7f + Vector3.up * 0.6f;
        cam.transform.LookAt(shot.look);
    }

    private static void Film()
    {
        foreach (var (angle, angleName) in Angles)
        {
            PoseCamera(angle);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            grab.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            grab.Apply();
            RenderTexture.active = null;
            string dir = Path.Combine(ShotDir(), angleName);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"f{frame:000}.png"), grab.EncodeToPNG());
        }
        cam.targetTexture = null;   // back to the Game view when watching
        PoseCamera(90f);
        frame++;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
