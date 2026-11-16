using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A Test looks like a Test (2026-11-06: "for a test match, everyone wears white, and the back
/// screen is actually white as well... and the ball is red"). Limited-overs cricket uses a white
/// ball, coloured kit and a black sightscreen so the white ball stands out; Test cricket the
/// reverse: a red ball, whites, and a white sightscreen behind the bowler for the red ball to show
/// against. Under lights (a day-night Test) the red ball is hard to see and the ball is pink.
///
/// Quick play's Test only (MatchFormat): the ball's leather red (or pink) with its stitching left
/// light, the sightscreen white, and every fielder - the bowler and keeper too - in cream whites
/// (the umpire in a white shirt: Umpire.Dress). The component sits under the match scene's Systems,
/// switched off, and is switched on for a Test; the fielders are dressed as they appear. The replay
/// re-copies all of it when it starts (ReplayActors.RefreshLooks).
///
/// The materials and textures it swaps in are saved assets (TestLookAssets, made by CricketVR >
/// Scene Objects > Build Test Look; until 2026-11-15 they were made in code at the start of each
/// Test). Still made at runtime: the ball's leather, recoloured from the ball's texture, which
/// BallVisual itself generates at runtime (Ball, BallMaterial).
/// </summary>
public class TestLook : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// The colours: whites (a cream, not paper white), a red ball's leather, a pink ball's, the
    /// stitching, and the sightscreen.
    public static readonly Color Whites = new Color(0.93f, 0.92f, 0.86f);
    /// Brighter than real leather, and lit from within too (BallGlow): a dark red was lost against
    /// the crowd (2026-11-07: "the red ball is not that visible... it blends in with the crowd").
    public static readonly Color RedLeather = new Color(0.78f, 0.05f, 0.05f);
    public static readonly Color PinkLeather = new Color(0.95f, 0.36f, 0.6f);
    public static readonly Color Stitching = new Color(0.95f, 0.93f, 0.86f);
    /// The sightscreen: off-white, not paper white (2026-11-06: "seems a little too white").
    public static readonly Color Screen = new Color(0.76f, 0.76f, 0.73f);
    /// The ball's trail in a Test: a bright red, nearly opaque at the ball and blended over what is
    /// behind it, not added to it - added, a red over a bright crowd barely showed (2026-11-07:
    /// "make it brighter and not so transparent").
    public static readonly Color Trail = new Color(1f, 0.12f, 0.1f, 0.9f);
    /// How much the ball lights itself: its own texture, this bright, on top of the light it gets.
    public const float BallGlow = 0.45f;
    /// The umpire's shirt in a Test: white, as theirs is (the black was only there to hide nothing
    /// white behind the white ball). Built with the umpire (Umpire.Dress).
    public static readonly Color UmpireShirt = new Color(0.95f, 0.95f, 0.93f);

    /// Whether this match is dressed as a Test: quick play's Test only.
    public static bool Applies => !CareerMatchSession.Active && MatchFormat.IsTest;
    private const float DressForSeconds = 10f;

    private readonly HashSet<Renderer> dressed = new HashSet<Renderer>();
    private TestLookAssets assets;
    private Dictionary<Material, Material> trailSwaps;
    private float until;

    /// The Test Look under the match scene's Systems is saved switched off (a component, not the
    /// object) and is switched on here when the match is a Test: not for limited overs, so Start
    /// never runs then, exactly as when it was not created.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != MatchScene || !Applies)
            return;
        TestLook look = FindFirstObjectByType<TestLook>(FindObjectsInactive.Include);
        if (look != null)
            look.enabled = true;
        else
            Debug.LogWarning("[TestLook] The match scene has no Test Look (run CricketVR > Scene Objects > Build Test Look): the Test is not dressed.");
    }

    /// A day-night Test (night lights): the pink ball.
    public static bool PinkBall => MatchSetup.TimeOfDay == eTimeOfDay.Night;

    private void Start()
    {
        assets = Resources.Load<TestLookAssets>(TestLookAssets.ResourcePath);
        if (assets == null)
        {
            Debug.LogWarning($"[TestLook] Resources/{TestLookAssets.ResourcePath} is missing (run CricketVR > Scene Objects > Build Test Look): the Test is not dressed.");
            enabled = false;
            return;
        }
        trailSwaps = TestLookAssets.Lookup(PinkBall ? assets.pinkTrail : assets.redTrail);
        until = Time.time + DressForSeconds;
        SightScreen();
        NoRedBehind();
    }

    /// Fielders come and go (the bowler's look copy, the keeper's): dressed as they appear.
    private void LateUpdate()
    {
        if (Time.time > until)
            return;
        Ball();
        foreach (SkinnedMeshRenderer skin in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            if (!dressed.Contains(skin) && IsKit(skin))
                Dress(skin);
    }

    /// A fielder's shirt, trousers or shoes - not an umpire's, nor the partner batter's.
    private static bool IsKit(SkinnedMeshRenderer skin)
    {
        string n = skin.name;
        if (!n.Contains("Hoody") && !n.Contains("Pants") && !n.Contains("Sneakers"))
            return false;
        return skin.GetComponentInParent<Umpire>() == null && skin.GetComponentInParent<PartnerBatter>() == null;
    }

    private void Dress(SkinnedMeshRenderer skin)
    {
        dressed.Add(skin);
        if (assets.whites == null)
            return;
        var mats = skin.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
            mats[i] = assets.whites;
        skin.sharedMaterials = mats;
    }

    /// The ball's leather red (pink under lights), its stitching left light: the white ball's own
    /// texture, its bright leather taken to the colour and its darker seam lines to the thread. Every
    /// renderer on the ball - the one drawn is BallVisual's child, made from the ball's own, so it is
    /// looked for each frame while the scene starts.
    private void Ball()
    {
        Main main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        GameObject ball = main != null ? main.theBall : null;
        if (ball == null)
            return;
        foreach (Renderer r in ball.GetComponentsInChildren<Renderer>(true))
        {
            if (dressed.Contains(r) || r.sharedMaterial == null)
                continue;
            dressed.Add(r);
            if (r is TrailRenderer trail)
                DressTrail(trail);
            else
                r.sharedMaterial = BallMaterial(r.sharedMaterial);
        }
    }

    /// The trail follows the ball: red (pink under lights), fading to nothing at its tail, alpha
    /// blended (the white ball's is additive, URP Particles _Blend 2) so it shows over the crowd.
    private void DressTrail(TrailRenderer ballTrail)
    {
        Color c = PinkBall ? PinkLeather : Trail;
        ballTrail.startColor = new Color(c.r, c.g, c.b, Trail.a);
        ballTrail.endColor = new Color(c.r, c.g, c.b, 0f);
        if (trailSwaps.TryGetValue(ballTrail.sharedMaterial, out Material test))
            ballTrail.sharedMaterial = test;
        else
            Debug.LogWarning("[TestLook] The ball's trail material has no Test version (run CricketVR > Scene Objects > Build Test Look): it keeps the white ball's.");
    }

    /// The white ball's material as the red (pink) ball: its texture recoloured, and lit from
    /// within by that texture (URP Lit _EMISSION, the variant the display boards already use).
    private static Material BallMaterial(Material white)
    {
        Color leatherColour = PinkBall ? PinkLeather : RedLeather;
        Material m = new Material(white) { name = PinkBall ? "Pink Ball" : "Red Ball" };
        Texture2D leather = Recolour(m.mainTexture, leatherColour);
        if (leather != null)
        {
            m.mainTexture = leather;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", leather);
        }
        else if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", leatherColour);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            if (leather != null && m.HasProperty("_EmissionMap"))
                m.SetTexture("_EmissionMap", leather);
            m.SetColor("_EmissionColor", (leather != null ? Color.white : leatherColour) * BallGlow);
        }
        return m;
    }

    /// The white ball's texture, recoloured: bright (leather) to `leather`, dark (seam) to the thread.
    public static Texture2D Recolour(Texture source, Color leather)
    {
        if (source == null)
            return null;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) { name = "Ball Leather" };
        tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels();
        for (int i = 0; i < px.Length; i++)
            px[i] = Leather(px[i], leather);
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    /// One pixel: the white ball's leather (bright) to the colour, its seam (darker) to the thread,
    /// keeping the shading between.
    public static Color Leather(Color white, Color leather)
    {
        float lum = 0.299f * white.r + 0.587f * white.g + 0.114f * white.b;
        float seam = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.85f, lum));
        Color c = Color.Lerp(leather * Mathf.Clamp01(lum / 0.9f), Stitching * lum, seam);
        c.a = 1f;
        return c;
    }

    private void SightScreen()
    {
        GameObject screen = GameObject.Find("SightScreen");
        Renderer r = screen != null ? screen.GetComponent<Renderer>() : null;
        if (r != null && assets.sightScreen != null)
            r.sharedMaterial = assets.sightScreen;
    }

    // ---- Nothing red behind the bowler -----------------------------------------------------------

    /// The roots whose textures are looked over: the stadium, its crowd, the club ground.
    private static readonly string[] Backdrop = { "Stadium", "Crowd" };

    /// A red ball against red hoardings or red shirts in the stand is lost at release (2026-11-06:
    /// "ensure there's nothing red so that it doesn't distract the view when the bowler is
    /// bowling"): every red in the ground's textures turned blue for a Test. The grass is left
    /// alone; so are browns and oranges (the seats, the crowd's cut-out colour).
    private void NoRedBehind()
    {
        Dictionary<Material, Material> swaps = TestLookAssets.Lookup(assets.backdrop);
        Dictionary<Texture, Texture> textures = TestLookAssets.Lookup(assets.backdropTextures);
        var roots = new List<GameObject>();
        foreach (string name in Backdrop)
            if (GameObject.Find(name) is GameObject g) roots.Add(g);
        if (FindFirstObjectByType<ClubGround>() is ClubGround club)
            roots.Add(club.gameObject);
        foreach (GameObject root in roots)
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                SwapBackdrop(r, swaps, textures);
    }

    /// A renderer's materials for their Test versions. The club ground's night copies (made as it
    /// goes dark, named "... (night)") have no saved Test version, and are the ground's own
    /// instances, so their texture is swapped in place.
    private static void SwapBackdrop(Renderer r, Dictionary<Material, Material> swaps, Dictionary<Texture, Texture> textures)
    {
        Material[] mats = r.sharedMaterials;
        bool changed = false;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null)
                continue;
            if (swaps.TryGetValue(m, out Material test))
            {
                mats[i] = test;
                changed = true;
            }
            else if (m.name.EndsWith(NightCopySuffix) && m.mainTexture != null && textures.TryGetValue(m.mainTexture, out Texture blue))
            {
                m.mainTexture = blue;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", blue);
            }
        }
        if (changed)
            r.sharedMaterials = mats;
    }

    /// How ClubGround names the copy of a material it makes for the night.
    public const string NightCopySuffix = " (night)";

    /// The crowd's atlas for a Test, baked beside its keyed one (CrowdKeyBaker); null if not there.
    public static Texture2D CrowdForTest(Texture keyed) =>
        Resources.Load<Texture2D>("Textures/" + keyed.name.Replace("_Keyed", "_KeyedTest"));

    /// `source` with its reds turned blue; null when it has no red worth changing.
    public static Texture2D Derred(Texture source)
    {
        Texture2D tex = Readable(source);
        Color32[] px = tex.GetPixels32();
        int reds = 0;
        for (int i = 0; i < px.Length; i++)
        {
            Color32 c = px[i];
            Color32 b = RedToBlue(c);
            if (b.r != c.r || b.g != c.g || b.b != c.b)
            {
                px[i] = b;
                reds++;
            }
        }
        if (reds < px.Length / 500)     // a fleck here and there is not worth a texture
        {
            if (Application.isPlaying) Object.Destroy(tex); else Object.DestroyImmediate(tex);
            return null;
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    /// One pixel: a red (hue within about 18 degrees of red, saturated, not dark) to the same
    /// shade of blue; anything else as it is.
    public static Color32 RedToBlue(Color32 c)
    {
        Color.RGBToHSV(c, out float h, out float sat, out float v);
        bool red = (h < 0.04f || h > 0.95f) && sat > 0.4f && v > 0.2f;
        if (!red)
            return c;
        Color blue = Color.HSVToRGB(0.6f, sat, v);
        return new Color32((byte)(blue.r * 255f), (byte)(blue.g * 255f), (byte)(blue.b * 255f), c.a);
    }

    private static Texture2D Readable(Texture source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture was = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) { name = source.name + " (Test)" };
        tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }
}
