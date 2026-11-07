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
/// (the umpires keep theirs). Built with the match scene; the fielders are dressed as they appear.
/// </summary>
public class TestLook : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// The colours: whites (a cream, not paper white), a red ball's leather, a pink ball's, the
    /// stitching, and the sightscreen.
    public static readonly Color Whites = new Color(0.93f, 0.92f, 0.86f);
    public static readonly Color RedLeather = new Color(0.55f, 0.05f, 0.06f);
    public static readonly Color PinkLeather = new Color(0.95f, 0.36f, 0.6f);
    public static readonly Color Stitching = new Color(0.95f, 0.93f, 0.86f);
    public static readonly Color Screen = new Color(0.96f, 0.96f, 0.95f);
    private const float DressForSeconds = 10f;

    private readonly HashSet<Renderer> dressed = new HashSet<Renderer>();
    private Material whites;
    private float until;

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
        if (scene.name != MatchScene || CareerMatchSession.Active || !MatchFormat.IsTest || FindFirstObjectByType<TestLook>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Test Look", typeof(TestLook)), scene);
    }

    /// A day-night Test (night lights): the pink ball.
    public static bool PinkBall => MatchSetup.TimeOfDay == eTimeOfDay.Night;

    private void Start()
    {
        until = Time.time + DressForSeconds;
        SightScreen();
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
        if (whites == null)
        {
            whites = new Material(skin.sharedMaterial) { name = "Test Whites" };
            whites.mainTexture = null;       // one flat colour: the atlas would show through
            if (whites.HasProperty("_BaseColor")) whites.SetColor("_BaseColor", Whites);
            if (whites.HasProperty("_Color")) whites.SetColor("_Color", Whites);
        }
        var mats = skin.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
            mats[i] = whites;
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
            if (dressed.Contains(r) || r is TrailRenderer || r.sharedMaterial == null)
                continue;
            dressed.Add(r);
            Material m = new Material(r.sharedMaterial) { name = PinkBall ? "Pink Ball" : "Red Ball" };
            Texture2D leather = Recolour(m.mainTexture, PinkBall ? PinkLeather : RedLeather);
            if (leather != null)
            {
                m.mainTexture = leather;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", leather);
            }
            else if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", PinkBall ? PinkLeather : RedLeather);
            r.sharedMaterial = m;
        }
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

    private static void SightScreen()
    {
        GameObject screen = GameObject.Find("SightScreen");
        Renderer r = screen != null ? screen.GetComponent<Renderer>() : null;
        if (r == null)
            return;
        var m = new Material(r.sharedMaterial) { name = "Test Sightscreen" };
        m.color = Screen;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Screen);
        r.sharedMaterial = m;
    }
}
