using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// A collider the builder places (world space): a BoxCollider on its own child object.
public struct ClubBox { public string name; public Vector3 centre; public float yawDeg; public Vector3 size; }
/// Where a spectator sits: the seat's surface under them, and the way they face.
public struct ClubSeat { public Vector3 pos; public Vector3 facing; }
/// Where a world-space text goes: its centre, the way its readable side faces, its box in metres.
public struct ClubLabel { public Vector3 pos; public Vector3 facing; public Vector2 rect; }

/// <summary>Everything the club ground is made of, one MeshData per material batch.</summary>
public sealed class ClubParts
{
    public readonly MeshData Outfield = new MeshData(), Surround = new MeshData(), Structures = new MeshData();
    public readonly MeshData Fence = new MeshData(), Trees = new MeshData(), Horizon = new MeshData(), Night = new MeshData();
    public readonly MeshData[] Spectators = { new MeshData { HasColors = true }, new MeshData { HasColors = true }, new MeshData { HasColors = true } };
    /// The frame behind the runtime black sightscreen, also merged into Structures (kept so tests
    /// can check its clearance from the replay camera).
    public readonly MeshData ScreenFrame = new MeshData();
    public readonly List<ClubBox> Colliders = new List<ClubBox>(), FenceColliders = new List<ClubBox>();
    public readonly List<ClubSeat> Seats = new List<ClubSeat>();
    public ClubLabel Figures, ClubName;
    public Vector3 FlagHoist, FlagFacing;
    public Vector3[] PylonHeads = new Vector3[0];
    public int TreeCount;
}

/// <summary>
/// The club ground's geometry from ClubGroundLayout's numbers, with no engine objects in it
/// (EditMode tests build it whole and check budgets and clearances).
/// </summary>
public static class ClubGeometry
{
    /// Must equal ClubGroundLayoutTests.TableSeed (the "seed" in Tools/clubground/layout_table.json):
    /// build_textures.py paints the trees' shadow pools from that table, so another seed plants the
    /// wood away from its shadows.
    public const int TreeSeed = 1, CrowdSeed = 1877;
    /// Mesh resolution of the ground: 128 round, rings every ~7.6 m on the outfield.
    const int GroundSegments = 128, OutfieldRings = 8, SurroundRings = 6, HorizonSegments = 96;
    /// club_horizon.png repeats four times round the ground (2048 px a quarter: ~23 px a degree).
    const int HorizonRepeats = 4;

    public static ClubParts Build(List<Vector2Int> people, bool surroundFlipV, StringBuilder log)
    {
        var p = new ClubParts();
        float g = ClubGroundLayout.OutfieldTop;
        Ground(p, g, surroundFlipV);
        var pav = ClubGroundLayout.Pavilion;
        ClubBuildings.Pavilion(p, SpotFrame(pav.pos, pav.yawDeg, g, true, "Pavilion", log), pav.footprint);
        var hut = ClubGroundLayout.ScoreHut;
        ClubBuildings.ScoreHut(p, SpotFrame(hut.pos, hut.yawDeg, g, true, "ScoreHut", log), hut.footprint);
        var rear = ClubGroundLayout.RearSightscreen;
        ClubFence.RearSightscreen(p.Structures, SpotFrame(rear.pos, rear.yawDeg, g, true, "RearSightscreen", log));
        ClubFence.ScreenFrame(p.ScreenFrame, g);
        p.Structures.Append(p.ScreenFrame);
        foreach (var b in ClubGroundLayout.Benches)
            ClubFence.Bench(p, SpotFrame(b.pos, b.yawDeg, g, true, "Bench", log), 2.4f, 3);
        var roller = ClubGroundLayout.Roller;
        ClubFence.Roller(p.Structures, SpotFrame(roller.pos, roller.yawDeg, g, false, "Roller", log));
        var flag = ClubGroundLayout.Flagpole;
        ClubFence.Flagpole(p, new Vector3(flag.pos.x, g, flag.pos.z), ClubGroundLayout.BatterEye);
        p.PylonHeads = ClubGroundLayout.GlowPoints();
        ClubFence.Pylons(p, p.PylonHeads, g);
        ClubFence.Fence(p, g, Mathf.Atan2(pav.pos.z, pav.pos.x));
        p.TreeCount = ClubTrees.Trees(p.Trees, g, TreeSeed);
        ClubTrees.Spectators(p, people, CrowdSeed);
        return p;
    }

    /// The outfield disc (club_outfield.png over 2 x OutfieldDisc), the surround ring sharing its
    /// edge vertices (club_surround.png over 2 x SurroundRadius) and the horizon band.
    static void Ground(ClubParts p, float g, bool flipV)
    {
        float od = ClubGroundLayout.OutfieldDisc, sr = ClubGroundLayout.SurroundRadius;
        ClubMeshKit.Annulus(p.Outfield, g, 0f, od, GroundSegments, OutfieldRings,
                            v => new Vector2(v.x / (2f * od) + 0.5f, v.z / (2f * od) + 0.5f));
        ClubMeshKit.Annulus(p.Surround, g, od, sr, GroundSegments, SurroundRings, v =>
        {
            float vv = v.z / (2f * sr) + 0.5f;
            return new Vector2(v.x / (2f * sr) + 0.5f, flipV ? 1f - vv : vv);
        });
        float hr = ClubGroundLayout.HorizonRadius;
        // HorizonHeight is the band's height above its bottom: club_horizon.png's v = 1 is painted at
        // HorizonBottom + HorizonHeight (+21 m), its ground line at v = 0.16.
        ClubMeshKit.Band(p.Horizon, hr, ClubGroundLayout.HorizonBottom, ClubGroundLayout.HorizonBottom + ClubGroundLayout.HorizonHeight, 0f, Mathf.PI * 2f,
                         HorizonSegments, Mathf.PI * 2f * hr / HorizonRepeats, true);
    }

    /// Where build_textures.py should have painted contact shadows: footprints over 4 square metres...
    public static List<Vector3> ShadedFootprints()
    {
        var spots = new List<Vector3>();
        foreach (var s in ClubGroundLayout.Footprints())
            if (s.footprint.x * s.footprint.y > 4f) spots.Add(s.pos);
        return spots;
    }

    /// ...and every tree the builder plants.
    public static List<Vector3> ShadedTrees()
    {
        var trees = new List<Vector3>();
        foreach (var t in ClubGroundLayout.Trees(TreeSeed)) trees.Add(t.pos);
        return trees;
    }

    /// A layout spot's frame on the ground. Spots that face the batter are checked: a yaw more than
    /// 20 degrees off the batter's eye means the layout and the builder disagree about the yaw
    /// convention, so the builder faces the eye itself and says so.
    public static Frame SpotFrame(Vector3 pos, float yawDeg, float g, bool facesBatter, string name, StringBuilder log)
    {
        if (facesBatter)
        {
            float want = Frame.YawToward(pos, ClubGroundLayout.BatterEye);
            if (Mathf.Abs(Mathf.DeltaAngle(yawDeg, want)) > 20f)
            {
                log?.AppendLine("WARNING " + name + ": yaw " + yawDeg.ToString("F1") + " does not face the batter (" +
                                want.ToString("F1") + "); using " + want.ToString("F1"));
                yawDeg = want;
            }
        }
        return Frame.Yaw(new Vector3(pos.x, g, pos.z), yawDeg);
    }
}

/// <summary>
/// Tools > CricketVR > Build Club Ground: builds Assets/Resources/Prefabs/ClubGround.prefab, the
/// village ground ClubGroundVenue dresses the match scene with (meshes in Assets/ClubGround/Meshes,
/// materials in Assets/ClubGround/Materials, textures and the ambience from Tools/clubground).
/// Run it after build_textures.py and build_sounds.py; rebuilding overwrites in place, keeping
/// every asset's GUID. With the match scene open it also checks the rope radius against the
/// scene's BoundaryCollider.
///
/// Budget (Quest): at most 15 draw calls and 35k triangles, checked and logged on every build.
/// Static geometry is merged per material: outfield, surround, structures, fence, trees, horizon,
/// three spectator groups, flag, night windows, three texts (the score hut's labels and figures,
/// the name board) = 14.
/// </summary>
public static class ClubGroundBuilder
{
    public const string PrefabPath = "Assets/Resources/Prefabs/ClubGround.prefab";
    public const int MaxDrawCalls = 15, MaxTriangles = 35000;
    /// Names GameObject.Find looks up elsewhere in the game; none may appear in the prefab.
    public static readonly string[] ForbiddenNames =
        { "Stadium", "Crowd", "SightScreen", "Pitch Lines", "Ball", "Floodlight Glows", "Main", "HUD" };
    /// Night dimming, as ClubGround.SetNight multiplies each renderer's _BaseColor.
    const float DimStructures = 0.45f, DimHorizon = 0.12f, DimSpectators = 0.6f;

    struct Mats { public Material outfield, surround, structures, fence, trees, horizon, flag, night, spectators; }

    [MenuItem("Tools/CricketVR/Build Club Ground")]
    public static void Build()
    {
        List<string> missing = ClubTextures.Missing();
        if (missing.Count > 0)
        {
            Debug.LogError("[ClubGround] missing inputs (run Tools/clubground/build_textures.py and build_sounds.py):\n  " +
                           string.Join("\n  ", missing));
            return;
        }
        var log = new StringBuilder();
        int errors = CheckRope(log);
        ClubTextures.ConfigureImporters();
        ClubTextures.EnsureFolder(ClubTextures.MeshDir);
        ClubTextures.EnsureFolder(ClubTextures.MaterialDir);
        ClubTextures.EnsureFolder("Assets/Resources/Prefabs");
        ClubParts parts = ClubGeometry.Build(People(log), SurroundFlipped(log), log);
        GameObject root = ClubPrefab.Assemble(parts, MakeMaterials(), log, ref errors);
        try
        {
            errors += ClubPrefab.Validate(root, log);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            log.AppendLine(saved ? "saved " + PrefabPath : "ERROR: could not save " + PrefabPath);
            if (!saved) errors++;
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        if (errors > 0) Debug.LogError("[ClubGround] " + errors + " problem(s)\n" + log);
        else Debug.Log("[ClubGround]\n" + log);
    }

    /// The rope is the scene's BoundaryCollider (a 56.64 m capsule, cached by BoundaryCollider and
    /// the fielders in Awake); the fence and rope are drawn from ClubGroundLayout.RopeRadius.
    static int CheckRope(StringBuilder log)
    {
        GameObject bc = GameObject.Find("Main/Colliders/BoundaryCollider");
        var cap = bc != null ? bc.GetComponent<CapsuleCollider>() : null;
        if (cap == null) { log.AppendLine("rope check skipped (match scene not open)"); return 0; }
        Vector3 s = cap.transform.lossyScale;
        float r = cap.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        bool ok = Mathf.Abs(r - ClubGroundLayout.RopeRadius) < 0.01f;
        log.AppendLine((ok ? "rope " : "ERROR: rope ") + r.ToString("F3") + " m in the scene, layout " + ClubGroundLayout.RopeRadius);
        return ok ? 0 : 1;
    }

    /// The crowd atlas's occupied cells (an empty one would put an invisible spectator on a bench).
    static List<Vector2Int> People(StringBuilder log)
    {
        Color32[] px = ClubTextures.ReadPixels(ClubTextures.CrowdAtlas, out int w, out int h);
        List<Vector2Int> people = px != null ? ClubAtlas.OccupiedPeople(px, w, h) : null;
        if (people == null || people.Count < 20)
        {
            log.AppendLine("WARNING crowd atlas unreadable or nearly empty; using every cell");
            return ClubAtlas.AllPeople();
        }
        log.AppendLine("crowd atlas: " + people.Count + " of " + ClubAtlas.PeopleColumns * ClubAtlas.PeopleRows + " cells occupied");
        return people;
    }

    /// Whether club_surround.png was painted with world +z at the bottom of the image rather than
    /// the top: its contact-shadow pools must sit under the layout's footprints and under the
    /// trees this builder plants (ClubGeometry.TreeSeed). The pavilion and the hut are nearly
    /// mirror images across the pitch's line, so the trees are what tell the two readings apart.
    static bool SurroundFlipped(StringBuilder log)
    {
        Color32[] px = ClubTextures.ReadPixels(ClubTextures.Surround, out int w, out int h);
        List<Vector3> feet = ClubGeometry.ShadedFootprints(), trees = ClubGeometry.ShadedTrees();
        if (px == null) { log.AppendLine("WARNING surround shade check skipped"); return false; }
        bool flip = ClubTextures.SurroundLooksFlipped(px, w, h, feet, trees);
        if (flip) log.AppendLine("WARNING surround painted with +z at the bottom; flipping its V");
        float atFeet = feet.Count > 0 ? ClubTextures.FootprintShade(px, w, h, feet, flip) : 1f;
        float atTrees = trees.Count > 0 ? ClubTextures.FootprintShade(px, w, h, trees, flip) : 1f;
        log.AppendLine("surround shade (1 = no pool): footprints " + atFeet.ToString("F2") + ", trees " + atTrees.ToString("F2"));
        if (atFeet >= 0.95f) log.AppendLine("WARNING no contact shadows under the footprints in club_surround.png");
        if (atTrees >= 0.97f) log.AppendLine("WARNING no contact shadows under the trees planted with seed " + ClubGeometry.TreeSeed);
        return flip;
    }

    static Mats MakeMaterials()
    {
        Texture2D T(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        string dir = ClubTextures.MaterialDir + "/";
        Texture2D atlas = T(ClubTextures.Atlas);
        var crowd = AssetDatabase.LoadAssetAtPath<Material>(ClubTextures.CrowdMaterial);
        return new Mats
        {
            outfield = ClubTextures.SaveMaterial(ClubTextures.OutfieldMaterial(T(ClubTextures.Outfield), T(ClubTextures.GrassDetail),
                                                 ClubGroundLayout.OutfieldDisc), dir + "ClubOutfield.mat"),
            surround = ClubTextures.SaveMaterial(ClubTextures.SimpleLit("ClubSurround", T(ClubTextures.Surround), false, false), dir + "ClubSurround.mat"),
            structures = ClubTextures.SaveMaterial(ClubTextures.SimpleLit("ClubStructures", atlas, false, false), dir + "ClubStructures.mat"),
            fence = ClubTextures.SaveMaterial(ClubTextures.SimpleLit("ClubFence", T(ClubTextures.Picket), true, true), dir + "ClubFence.mat"),
            trees = ClubTextures.SaveMaterial(ClubTextures.SimpleLit("ClubTrees", atlas, true, true), dir + "ClubTrees.mat"),
            horizon = ClubTextures.SaveMaterial(ClubTextures.Unlit("ClubHorizon", T(ClubTextures.Horizon), Color.white, true), dir + "ClubHorizon.mat"),
            flag = ClubTextures.SaveMaterial(ClubTextures.SimpleLit("ClubFlag", T(ClubTextures.Flag), false, true), dir + "ClubFlag.mat"),
            night = ClubTextures.SaveMaterial(ClubTextures.Unlit("ClubNightWindows", atlas, new Color(1f, 0.85f, 0.58f, 1f), false), dir + "ClubNightWindows.mat"),
            spectators = ClubTextures.SaveMaterial(ClubTextures.Spectators(crowd), dir + "ClubSpectators.mat"),
        };
    }

    /// <summary>The prefab's GameObjects, components and ClubGround wiring.</summary>
    static class ClubPrefab
    {
        /// What Assemble collects on the way: the renderers SetNight dims and by how much.
        sealed class Built
        {
            public Transform root; public int layer;
            public readonly List<Object> dim = new List<Object>(), groups = new List<Object>();
            public readonly List<float> factor = new List<float>();
            public GameObject Dim(GameObject go, float k) { dim.Add(go.GetComponent<Renderer>()); factor.Add(k); return go; }
            public GameObject Part(string name, MeshData mesh, Material mat, bool receive = false) =>
                ClubPrefab.Part(name, root, ClubTextures.SaveMesh(mesh, ClubTextures.MeshDir + "/" + name.Replace(" ", "") + ".asset"), mat, receive, layer);
        }

        public static GameObject Assemble(ClubParts p, Mats m, StringBuilder log, ref int errors)
        {
            int layer = LayerMask.NameToLayer("Stadium");
            if (layer < 0) { log.AppendLine("WARNING no Stadium layer; using Default"); layer = 0; }
            var root = new GameObject("ClubGround") { layer = layer };
            var b = new Built { root = root.transform, layer = layer };
            BoxCollider ground = Outfield(p, m, b);
            b.Dim(b.Part("Club Surround", p.Surround, m.surround), DimStructures);
            GameObject structures = b.Dim(b.Part("Club Structures", p.Structures, m.structures), DimStructures);
            GameObject fence = b.Dim(b.Part("Club Fence", p.Fence, m.fence), DimStructures);
            b.Dim(b.Part("Club Trees", p.Trees, m.trees), DimStructures);
            b.Dim(b.Part("Club Horizon", p.Horizon, m.horizon), DimHorizon);
            for (int i = 0; i < p.Spectators.Length; i++)
                b.groups.Add(b.Dim(b.Part("Club Spectators " + i, p.Spectators[i], m.spectators), DimSpectators));
            foreach (ClubBox c in p.Colliders) Collider(c, structures.transform, layer);
            foreach (ClubBox c in p.FenceColliders) Collider(c, fence.transform, layer);
            b.Dim(Flag(p, root.transform, m.flag, layer), DimStructures);
            GameObject night = b.Part("Club Night", p.Night, m.night);
            night.SetActive(false);
            TMP_FontAsset font = Font();
            TextMeshPro figures = Scoreboard(p.Figures, root.transform, font, layer, log);
            var board = figures.gameObject.AddComponent<ClubScoreboard>();
            errors += Wire(board, log, ("figures", (Object)figures));
            TextMeshPro name = Label("Club Name Board", root.transform, p.ClubName, "Cricket Club", 6f, TextAlignmentOptions.Center, new Color(0.97f, 0.95f, 0.85f), font, layer);
            name.fontStyle = FontStyles.Bold;
            var heads = new List<Object>();
            for (int i = 0; i < p.PylonHeads.Length; i++)
            {
                var head = new GameObject("Club Pylon Head " + i) { layer = layer };
                head.transform.SetParent(root.transform, false);
                head.transform.position = p.PylonHeads[i];
                heads.Add(head.transform);
            }
            var club = root.AddComponent<ClubGround>();
            errors += Wire(club, log, ("outfield", ground), ("ambience", AssetDatabase.LoadAssetAtPath<AudioClip>(ClubTextures.Ambience)),
                           ("scoreboard", board), ("clubName", name), ("nightOnly", night));
            errors += WireArray(club, "spectatorGroups", b.groups, log) + WireArray(club, "pylonHeads", heads, log) +
                      WireArray(club, "dimAtNight", b.dim, log) + WireFloats(club, "nightFactor", b.factor, log);
            log.AppendLine("trees " + p.TreeCount + ", seats " + p.Seats.Count + ", colliders " + p.Colliders.Count + " + fence " + p.FenceColliders.Count);
            return root;
        }

        /// The outfield takes over from the stadium's Ground00 when the stadium is switched off:
        /// tagged Ground (Ball, MatchSounds and PitchLines look for it), the Outfield physic material,
        /// its renderer on the collider's object (PitchLines needs both), a 320 m slab whose top is
        /// BallFlight.OutfieldY so the ball's physics and its predicted flight agree.
        static BoxCollider Outfield(ClubParts p, Mats m, Built b)
        {
            GameObject outfield = b.Part("Club Outfield", p.Outfield, m.outfield, true);
            outfield.tag = "Ground";
            var ground = outfield.AddComponent<BoxCollider>();
            ground.size = new Vector3(320f, 1f, 320f);
            ground.center = new Vector3(0f, ClubGroundLayout.OutfieldTop - 0.5f, 0f);
            ground.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ClubTextures.OutfieldPhysics);
            return ground;
        }

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material mat, bool receiveShadows, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            Quiet(r, receiveShadows);
            return go;
        }

        /// Realtime-lit and unbaked: no shadows cast (the sun's 25 m shadow distance never reaches
        /// the boundary), none received but on the outfield, no probes (the scene has none).
        static void Quiet(Renderer r, bool receiveShadows)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        }

        static void Collider(ClubBox b, Transform parent, int layer)
        {
            var go = new GameObject(b.name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(b.centre, Quaternion.Euler(0f, b.yawDeg, 0f));
            go.AddComponent<BoxCollider>().size = b.size;
        }

        /// The flag: FlagWave builds and waves its own mesh in Awake; it hangs from the hoist with
        /// its face (local -Z, the side a quad shows) toward the batter, flying along local +X.
        static GameObject Flag(ClubParts p, Transform parent, Material mat, int layer)
        {
            var go = new GameObject("Club Flag") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(p.FlagHoist, Quaternion.LookRotation(-p.FlagFacing, Vector3.up));
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            Quiet(r, false);
            go.AddComponent<FlagWave>();
            return go;
        }

        /// The score hut's two texts over the same box on the plate: the fixed row labels
        /// (ClubScoreboardFigures.Labels) and the figures ClubScoreboard writes, which Compose puts
        /// at FiguresAt across. One fixed size for both, the largest that fits the six rows, so the
        /// rows line up and auto-sizing never shrinks one text and not the other.
        static TextMeshPro Scoreboard(ClubLabel at, Transform parent, TMP_FontAsset font, int layer, StringBuilder log)
        {
            var white = new Color(0.96f, 0.96f, 0.92f);
            int rows = ClubBuildings.ScoreboardRows;
            float spacing = ClubBuildings.ScoreboardLineHeight;
            float size = font == null ? ClubBuildings.RowsFontSize(at.rect.y, rows, spacing) :
                ClubBuildings.RowsFontSize(at.rect.y, rows, spacing, font.faceInfo.ascentLine, font.faceInfo.descentLine,
                                          font.faceInfo.lineHeight, font.faceInfo.pointSize, font.faceInfo.scale);
            log.AppendLine("scoreboard font size " + size.ToString("0.00"));
            TextMeshPro labels = Label("Club Scoreboard Labels", parent, at, ClubScoreboardFigures.Labels, size, TextAlignmentOptions.Left, white, font, layer);
            TextMeshPro figures = Label("Club Scoreboard", parent, at, ClubScoreboardFigures.Compose(default), size, TextAlignmentOptions.Left, white, font, layer);
            labels.enableAutoSizing = figures.enableAutoSizing = false;
            return figures;
        }

        static TMP_FontAsset Font()
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            return f != null ? f : TMP_Settings.defaultFontAsset;
        }

        /// World-space text, readable from the side `at.facing` points to (TMP reads from its -Z),
        /// auto-sized to its box from `maxSize` (10 = 1 m em) down to a third of it.
        static TextMeshPro Label(string name, Transform parent, ClubLabel at, string text, float maxSize,
                                 TextAlignmentOptions align, Color colour, TMP_FontAsset font, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            go.transform.SetPositionAndRotation(at.pos, Quaternion.LookRotation(-at.facing, Vector3.up));
            if (font != null) t.font = font;
            t.rectTransform.sizeDelta = at.rect;
            t.text = text;
            t.richText = true;
            t.enableAutoSizing = true;
            t.fontSizeMax = maxSize;
            t.fontSizeMin = maxSize / 3f;
            t.fontSize = maxSize;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = colour;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) Quiet(r, false);
            return t;
        }

        // ---- Serialized wiring (the field names are ClubGround's and ClubScoreboard's) -----------

        static int Wire(Object target, StringBuilder log, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            int errors = 0;
            foreach (var (field, value) in fields)
            {
                SerializedProperty prop = Find(so, field, log);
                if (prop == null) { errors++; continue; }
                if (value == null) { log.AppendLine("ERROR: nothing to put in " + target.GetType().Name + "." + field); errors++; continue; }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return errors;
        }

        static int WireArray(Object target, string field, List<Object> values, StringBuilder log)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = Find(so, field, log);
            if (prop == null || !prop.isArray) return 1;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return 0;
        }

        static int WireFloats(Object target, string field, List<float> values, StringBuilder log)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = Find(so, field, log);
            if (prop == null || !prop.isArray) return 1;
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) prop.GetArrayElementAtIndex(i).floatValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return 0;
        }

        static SerializedProperty Find(SerializedObject so, string field, StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) log.AppendLine("ERROR: " + so.targetObject.GetType().Name + " has no serialized field '" + field + "'");
            return prop;
        }

        // ---- Checks -------------------------------------------------------------------------------

        /// Names, then the budget: every renderer's draw calls (one per submesh; TMP one each) and
        /// triangles (TMP two per character, FlagWave's 12 x 8 grid at most 192).
        public static int Validate(GameObject root, StringBuilder log)
        {
            int errors = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(ForbiddenNames, t.name) >= 0 || !t.name.StartsWith("Club"))
                {
                    log.AppendLine("ERROR: object named '" + t.name + "'"); errors++;
                }
            }
            int draws = 0, tris = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                var tmp = r.GetComponent<TMP_Text>();
                var mf = r.GetComponent<MeshFilter>();
                if (tmp != null) { draws++; tris += 2 * Mathf.Max(16, tmp.text.Length); }
                else if (r.GetComponent<FlagWave>() != null) { draws++; tris += 192; }
                else if (mf != null && mf.sharedMesh != null)
                {
                    draws += mf.sharedMesh.subMeshCount;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
                }
            }
            bool ok = draws <= MaxDrawCalls && tris <= MaxTriangles;
            log.AppendLine((ok ? "budget " : "ERROR: over budget ") + draws + " draw calls (max " + MaxDrawCalls + "), " +
                           tris + " triangles (max " + MaxTriangles + ")");
            return errors + (ok ? 0 : 1);
        }
    }
}
