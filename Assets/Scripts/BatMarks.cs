using UnityEngine;

/// <summary>
/// The ball's marks on the bat's face (2026-11-09: "an imprint on the bat after I hit it ... not
/// exactly a circle because it'll be moving; it'll sort of scrape against the bat surface ... if
/// it's a white ball, then it should be a white imprint. If it's a red ball, then it should be a
/// red imprint ... in the beginning, it's clear ... the imprints keep stacking up ... subtly
/// towards the end of the match ... you should subtly be able to tell where you're hitting the
/// ball the most").
///
/// Each hit leaves one faint print where the ball met the face, in the ball's leather (red, pink
/// or white): bigger and a touch stronger the harder it was struck (the ball squashes more), and
/// drawn out along the way it slid across the face - round for a ball met square on, a smear for
/// a glance. One alone is barely there (Strongest 0.11); a hundred in the middle make a patch.
///
/// All the prints are one mesh on the face (one draw call), a few tenths of a millimetre proud of
/// it, made new with the bat each match. The face is the bat's local +Y at BatGeometry.faceY;
/// a ball met on the back leaves nothing (the back is curved, and you do not look at it).
/// </summary>
[RequireComponent(typeof(BatGeometry))]
public class BatMarks : MonoBehaviour
{
    /// Prints kept: past this the oldest is written over (a Test's worth of hits is far more).
    public const int MaxMarks = 400;
    /// A print's width (m) for the gentlest and the firmest contact, and the impact speed (m/s) that
    /// makes the firmest.
    public const float SmallestPrint = 0.026f, BiggestPrint = 0.042f, FirmImpact = 28f;
    /// One print's opacity, gentlest to firmest.
    public const float Faintest = 0.04f, Strongest = 0.11f;
    /// The most a print is drawn out along its slide (length / width).
    public const float LongestSmear = 2.4f;
    private const float Proud = 0.0004f;
    private const int TextureSize = 64;

    private BatGeometry geometry;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color32[] colours;
    private int next, count;
    private Matrix4x4 lastWorld;
    private float lastTime = -1f;

    public int Count => count;

    private void Start()
    {
        geometry = GetComponent<BatGeometry>();
        Build();
        lastWorld = transform.localToWorldMatrix;
        lastTime = Time.time;
        Bat.ShotStruck += OnShot;
    }

    private void OnDestroy()
    {
        Bat.ShotStruck -= OnShot;
        if (mesh != null)
            Destroy(mesh);
    }

    /// The bat's frame as it was a moment ago: how fast the face was moving where the ball met it.
    private void LateUpdate()
    {
        lastWorld = transform.localToWorldMatrix;
        lastTime = Time.time;
    }

    private void OnShot(ShotInfo shot)
    {
        if (geometry == null || !geometry.IsFitted)
            return;
        Vector3 local = transform.InverseTransformPoint(shot.contactPoint);
        if (local.y < 0.5f * (geometry.faceY + geometry.backY))
            return;                                     // on the back
        float dt = Mathf.Max(1e-3f, Time.time - lastTime);
        Vector3 faceWas = lastWorld.MultiplyPoint3x4(local);
        Vector3 batPoint = (shot.contactPoint - faceWas) / dt;
        Vector3 slide = transform.InverseTransformDirection(shot.incoming - batPoint);
        Add(new Vector2(local.x, local.z), new Vector2(slide.x, slide.z), Mathf.Abs(slide.y), shot.impactSpeed, LeatherNow());
    }

    /// A print at `at` (the face's local x, z), the ball sliding `along` across it while closing at
    /// `closing` (m/s, local), struck at `impact` m/s, in `leather`.
    public void Add(Vector2 at, Vector2 along, float closing, float impact, Color leather)
    {
        if (mesh == null)
            return;
        float firm = Mathf.Clamp01(impact / FirmImpact);
        Vector3 scale = transform.lossyScale;
        float size = Mathf.Lerp(SmallestPrint, BiggestPrint, Mathf.Sqrt(firm));
        float smear = SmearOf(along.magnitude, closing);
        Vector2 dir = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.up;
        Vector2 side = new Vector2(-dir.y, dir.x);
        // Half extents in local units (the bat may be scaled unevenly).
        Vector2 halfLong = new Vector2(dir.x / scale.x, dir.y / scale.z) * (0.5f * size * smear);
        Vector2 halfWide = new Vector2(side.x / scale.x, side.y / scale.z) * (0.5f * size);
        // On the face: a print hanging off an edge is pulled in until it is all on the wood.
        float edge = geometry.halfWidth - Mathf.Abs(halfLong.x) - Mathf.Abs(halfWide.x);
        at.x = Mathf.Clamp(at.x, -Mathf.Max(0f, edge), Mathf.Max(0f, edge));
        float y = geometry.faceY + Proud / Mathf.Max(1e-4f, scale.y);
        int v = next * 4;
        vertices[v + 0] = Corner(at - halfLong - halfWide, y);
        vertices[v + 1] = Corner(at - halfLong + halfWide, y);
        vertices[v + 2] = Corner(at + halfLong + halfWide, y);
        vertices[v + 3] = Corner(at + halfLong - halfWide, y);
        Color c = leather;
        c.a = Mathf.Lerp(Faintest, Strongest, firm);
        Color32 c32 = c;
        for (int i = 0; i < 4; i++)
            colours[v + i] = c32;
        next = (next + 1) % MaxMarks;
        count = Mathf.Min(MaxMarks, count + 1);
        mesh.vertices = vertices;
        mesh.colors32 = colours;
        mesh.RecalculateBounds();
    }

    /// How drawn out a print is: square on, round; the more it slid for how hard it came in, the
    /// longer the smear.
    public static float SmearOf(float sliding, float closing)
    {
        float glance = sliding / Mathf.Max(0.5f, closing);
        return Mathf.Lerp(1f, LongestSmear, Mathf.Clamp01(glance / 1.5f));
    }

    private static Vector3 Corner(Vector2 p, float y) => new Vector3(p.x, y, p.y);

    /// The ball in play's leather: a Test's red (pink under lights), else white.
    public static Color LeatherNow()
    {
        if (!TestLook.Applies)
            return new Color(0.96f, 0.95f, 0.92f);
        return TestLook.PinkBall ? TestLook.PinkLeather : TestLook.RedLeather;
    }

    private void Build()
    {
        vertices = new Vector3[MaxMarks * 4];
        colours = new Color32[MaxMarks * 4];
        var uv = new Vector2[MaxMarks * 4];
        var tris = new int[MaxMarks * 6];
        for (int m = 0; m < MaxMarks; m++)
        {
            int v = m * 4, t = m * 6;
            uv[v] = new Vector2(0f, 0f); uv[v + 1] = new Vector2(1f, 0f); uv[v + 2] = new Vector2(1f, 1f); uv[v + 3] = new Vector2(0f, 1f);
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;      // facing +Y, the face's way
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }
        mesh = new Mesh { name = "Bat Marks" };
        mesh.MarkDynamic();
        var normals = new Vector3[MaxMarks * 4];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.colors32 = colours;
        mesh.triangles = tris;
        var go = new GameObject("Ball Marks") { layer = gameObject.layer };
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = MakeMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    /// CricketVR/BatMark (Always Included in the graphics settings): the print texture tinted and
    /// faded per print by its vertex colour, lit like the face. URP's own unlit and particle
    /// shaders drew every print solid white (measured in play: the vertex colours ignored).
    private static Material MakeMaterial()
    {
        var mat = new Material(Shader.Find("CricketVR/BatMark")) { name = "Bat Marks" };
        mat.SetTexture("_BaseMap", PrintTexture());
        return mat;
    }

    /// One print: a soft oval, densest just inside its rim where the leather pressed hardest, a
    /// little mottled (the grain of the leather and the seam), fading out at the edge.
    /// `readable`: kept on the CPU too (tests); the game's copy lives on the GPU only.
    public static Texture2D PrintTexture(bool readable = false)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "Ball Print", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[TextureSize * TextureSize];
        var rng = new System.Random(7);
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (x + 0.5f) / TextureSize * 2f - 1f, w = (y + 0.5f) / TextureSize * 2f - 1f;
                float r = Mathf.Sqrt(u * u + w * w);
                float body = Mathf.Clamp01((1f - r) / 0.35f);                   // fades over the outer third
                float rim = Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.16f, 2f)) * 0.35f;
                float seam = Mathf.Exp(-Mathf.Pow(u * 0.9f + w * 0.44f, 2f) / 0.004f) * 0.3f;
                float grain = 0.75f + 0.25f * (float)rng.NextDouble();
                float a = Mathf.Clamp01((0.65f * body * body + rim + seam * body) * grain);
                px[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, !readable);
        return tex;
    }
}
