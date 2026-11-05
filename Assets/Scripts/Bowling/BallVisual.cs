using UnityEngine;

/// <summary>
/// What the player sees of the ball: a seamed ball, held the way the bowler grips it, spinning the
/// way it was bowled.
///
/// The ball's own mesh (Models/Ball.fbx) is 444,024 vertices with no UVs - so its seam texture
/// never showed (a plain white sphere) and it cost more to draw than most of the ground. This draws
/// a generated one instead (~3k vertices): a latitude/longitude sphere whose poles are the seam's
/// axis (local X), so the texture's seam band (ball-white.png, ~0.12..0.45 across) lies along the
/// equator, its stitching repeated round it. The physics ball is untouched (collider, rigidbody,
/// trail): only its MeshRenderer is switched off, and the visual is a child that is turned here.
///
///   * Held   - BowlerGrip sets the rotation every frame: the seam where the fingers put it.
///   * Flight - from the release (Spin) until it is hit or pitches after a shot: spinning about
///     the axis the bowler gave it - backspin on the seam for a seamer, side- and over-spin for a
///     spinner. Visual only: a spinning rigidbody would grip the pitch through PhysX friction and
///     move off it differently from the turn model BallDelivery aims with.
///   * Rolling - once it is on the ground after a shot, or in a fielder's hands: it follows the
///     rigidbody, so it rolls the way PhysX rolls it.
/// </summary>
public class BallVisual : MonoBehaviour
{
    public enum Mode { Physics, Held, Flight }

    private Transform visual;
    private Mode mode = Mode.Physics;
    private Vector3 spin;                 // rad/s, world
    private Quaternion offsetFromBody = Quaternion.identity;
    private Ball ball;

    private static Mesh mesh;
    /// The seam band's middle and how fast the texture runs across it per radian of latitude.
    private const float SeamU = 0.29f, UPerRadian = 1.0f, UMin = 0.03f, UMax = 0.56f;
    /// Times the photographed stretch of stitching repeats round the ball.
    private const float StitchRepeats = 5f;
    /// Spin lost per second in flight (it hardly slows in the second the ball is in the air).
    private const float SpinDecay = 0.15f;

    public Mode Current => mode;
    public Transform Visual => visual;

    private void Awake()
    {
        ball = GetComponent<Ball>();
        MeshRenderer own = GetComponent<MeshRenderer>();
        MeshFilter ownFilter = GetComponent<MeshFilter>();
        if (own == null || ownFilter == null)
        {
            enabled = false;
            return;
        }
        var go = new GameObject("Seamed Ball");
        visual = go.transform;
        visual.SetParent(transform, false);
        // Same size as the old mesh (its bounds), whatever the ball's scale.
        float radius = ownFilter.sharedMesh != null ? ownFilter.sharedMesh.bounds.extents.x : 0.0306f;
        go.AddComponent<MeshFilter>().sharedMesh = SeamedMesh();
        visual.localScale = Vector3.one * radius;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = own.sharedMaterials;
        Material mat = SeamedMaterial(own.sharedMaterial);
        if (mat != null)
            r.sharedMaterial = mat;
        r.shadowCastingMode = own.shadowCastingMode;
        r.receiveShadows = own.receiveShadows;
        r.lightProbeUsage = own.lightProbeUsage;
        r.gameObject.layer = gameObject.layer;
        own.enabled = false;
        Bat.ShotStruck += OnShot;
    }

    private void OnDestroy() => Bat.ShotStruck -= OnShot;

    /// The bowler has it: the seam where his fingers put it (world rotation; local X = seam axis).
    public void Hold(Quaternion rotation)
    {
        if (visual == null) return;
        mode = Mode.Held;
        visual.rotation = rotation;
    }

    /// Let go with this spin (rad/s, world).
    public void Release(Vector3 angularVelocity)
    {
        if (visual == null) return;
        mode = Mode.Flight;
        spin = angularVelocity;
    }

    private void OnShot(ShotInfo info)
    {
        // Off the bat it tumbles, the harder hit the faster.
        mode = Mode.Flight;
        spin = Random.onUnitSphere * Random.Range(40f, 110f);
    }

    private void LateUpdate()
    {
        if (visual == null) return;
        Main inst = Main.Instance;
        if (mode == Mode.Flight)
        {
            bool shot = inst != null && (inst.gameState == eGameState.InGame_BallHit || inst.gameState == eGameState.InGame_BallHitLoop);
            bool delivering = inst != null && (inst.gameState == eGameState.InGame_DeliverBall || inst.gameState == eGameState.InGame_DeliverBallLoop);
            Rigidbody rb = inst != null ? inst.theBallRigidBody : null;
            bool parked = rb != null && rb.isKinematic;
            if (parked || (!delivering && !shot) || (shot && ball != null && ball.bounced))
                ToPhysics();
            else
            {
                float w = spin.magnitude;
                if (w > 1e-3f)
                    visual.rotation = Quaternion.AngleAxis(w * Mathf.Rad2Deg * Time.deltaTime, spin / w) * visual.rotation;
                spin *= 1f - SpinDecay * Time.deltaTime;
            }
        }
        else if (mode == Mode.Held)
        {
            // Nobody set it this frame (the bowler let go some other way): follow the body again.
            if (inst == null || AnimatedBowler.Instance == null || !AnimatedBowler.Instance.CarryingBall)
                ToPhysics();
        }
        else
            visual.localRotation = offsetFromBody;
    }

    private void ToPhysics()
    {
        mode = Mode.Physics;
        offsetFromBody = Quaternion.Inverse(transform.rotation) * visual.rotation;
    }

    private static Material material;
    /// The photographed seam band's edges across the texture; outside them, plain leather.
    private const float BandFrom = 0.105f, BandTo = 0.47f, Feather = 0.035f;

    /// The ball's material with a texture made from its own: the photo's seam band kept, the rest
    /// of it (shading and creases from the photograph, which sampled at a fixed column made stripes
    /// radiating from the poles) replaced with even leather of the same colour.
    private static Material SeamedMaterial(Material source)
    {
        if (material != null) return material;
        Texture src = source != null ? source.mainTexture : null;
        if (src == null) return null;
        int w = src.width, h = src.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Seamed Ball", wrapMode = TextureWrapMode.Repeat };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels();
        // The leather: the brightest half of the pixels just outside the band, averaged.
        var samples = new System.Collections.Generic.List<Color>();
        for (int y = 0; y < h; y++)
            for (int x = Mathf.RoundToInt((BandTo + 0.01f) * w); x < Mathf.RoundToInt((BandTo + 0.07f) * w); x++)
                samples.Add(px[y * w + x]);
        samples.Sort((a, b) => b.grayscale.CompareTo(a.grayscale));
        Color leather = Color.black;
        int n = Mathf.Max(1, samples.Count / 2);
        for (int i = 0; i < n; i++) leather += samples[i];
        leather /= n;
        leather.a = 1f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float inBand = Mathf.Min(Mathf.InverseLerp(BandFrom - Feather, BandFrom, u), 1f - Mathf.InverseLerp(BandTo, BandTo + Feather, u));
                px[y * w + x] = Color.Lerp(leather, px[y * w + x], Mathf.Clamp01(inBand));
            }
        tex.SetPixels(px);
        tex.Apply(true, true);
        material = new Material(source) { name = "Seamed Ball" };
        material.mainTexture = tex;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
        return material;
    }

    /// A unit sphere, poles on local X (the seam's axis), latitude rings packed tight at the seam.
    public static Mesh SeamedMesh()
    {
        if (mesh != null) return mesh;
        const int HalfRings = 24, Segments = 64;
        int rings = 2 * HalfRings + 1;
        var verts = new Vector3[rings * (Segments + 1)];
        var uvs = new Vector2[verts.Length];
        int k = 0;
        for (int i = 0; i < rings; i++)
        {
            // Latitude from the seam, -pi/2..pi/2, finer near 0 where the stitching is.
            float s = (i - HalfRings) / (float)HalfRings;
            float lat = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 1.7f) * Mathf.PI * 0.5f;
            float u = Mathf.Clamp(SeamU + UPerRadian * lat, UMin, UMax);
            for (int j = 0; j <= Segments; j++, k++)
            {
                float lon = j / (float)Segments * Mathf.PI * 2f;
                verts[k] = new Vector3(Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Cos(lat) * Mathf.Sin(lon));
                uvs[k] = new Vector2(u, j / (float)Segments * StitchRepeats);
            }
        }
        var tris = new int[(rings - 1) * Segments * 6];
        int t = 0;
        for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < Segments; j++)
            {
                int a = i * (Segments + 1) + j, b = a + Segments + 1;
                tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                tris[t++] = a + 1; tris[t++] = b + 1; tris[t++] = b;
            }
        mesh = new Mesh { name = "Seamed Ball" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.normals = System.Array.ConvertAll(verts, v => v.normalized);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        // Check winding against the normals on a triangle at the seam (the ones at the poles are
        // degenerate): flip all if it faces inward.
        int m = HalfRings * Segments * 6;
        Vector3 n = Vector3.Cross(verts[tris[m + 1]] - verts[tris[m]], verts[tris[m + 2]] - verts[tris[m]]);
        if (Vector3.Dot(n, verts[tris[m]] + verts[tris[m + 1]] + verts[tris[m + 2]]) < 0f)
        {
            for (int q = 0; q < tris.Length; q += 3) { int tmp = tris[q + 1]; tris[q + 1] = tris[q + 2]; tris[q + 2] = tmp; }
            mesh.triangles = tris;
        }
        return mesh;
    }
}
