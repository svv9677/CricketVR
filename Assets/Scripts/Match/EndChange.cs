using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The ends change every over (2026-11-06: "we're only batting from one side... every over we'll
/// have to switch"). The game is played from one fixed pitch - the batter's stumps at +x, the
/// bowler's at -x, the fielders, the ball and the rig all in that frame - so instead of moving all of
/// that, the ground turns round it: at the end of every over, behind a short blink, the stadium (or
/// the club ground), its crowd, the sun or the lights' glows go half a turn about the middle of
/// the pitch, and the player is batting at the other end. The bowl is round and centred on the
/// pitch (measured 2026-11-06: ground disc centred at the origin, stands within 1.2 m of it), so
/// the boundary does not move.
///
/// Left where they are: the sightscreen (one is always behind the bowler), the giant screens and
/// their frame (the scorecard you read stays in front of you), the boundary colliders.
///
/// Odd overs bowled: the far end. Match scene only (not the nets). Built per scene load.
/// Editor/EndChangeUnbatch keeps the stadium out of static batching so it can turn in a build.
/// </summary>
public class EndChange : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// The stadium's screen and its frame: stay with the screens.
    private static readonly string[] StayStill = { "NMD_stD_display_0", "NMD_stD_metal_0" };

    /// Batting at the other end now (the ground turned half round).
    public static bool AtOtherEnd { get; private set; }

    private readonly ViewBlack black = new ViewBlack();
    private bool blinking;

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
        if (scene.name != MatchScene || FindFirstObjectByType<EndChange>() != null)
            return;
        AtOtherEnd = false;
        SceneManager.MoveGameObjectToScene(new GameObject("End Change", typeof(EndChange)), scene);
    }

    /// Which end after `oversBowled` completed overs: the first from this end, the next from the other.
    public static bool OtherEndAfter(int oversBowled) => oversBowled % 2 == 1;

    private void Update()
    {
        Main main = Main.Instance;
        if (blinking || main == null || main.theHUD == null || main.gameState != eGameState.InGame_Ready ||
            MatchFlow.Over || NonStrikerView.Blinking)
            return;
        if (OtherEndAfter(main.theHUD.Overs) != AtOtherEnd)
            StartCoroutine(Blink());
    }

    private void OnDestroy() => black.Dispose();

    private IEnumerator Blink()
    {
        blinking = true;
        bool visible = black.Ensure();
        float start = Time.unscaledTime;
        bool turned = false;
        while (true)
        {
            float t = Time.unscaledTime - start;
            if (!turned && t >= NonStrikerView.FadeSeconds)
            {
                turned = true;
                start = Time.unscaledTime - NonStrikerView.FadeSeconds;
                t = NonStrikerView.FadeSeconds;
                black.Set(1f, visible);
                Turn();
                AtOtherEnd = !AtOtherEnd;
            }
            black.Set(NonStrikerView.BlinkAlpha(t), visible);
            if (t >= NonStrikerView.BlinkSeconds)
                break;
            yield return null;
        }
        blinking = false;
    }

    /// Half a turn of the ground about the middle of the pitch (the origin).
    private static void Turn()
    {
        var turned = new List<Transform>();
        GameObject stadium = GameObject.Find("Stadium");
        if (stadium != null)
        {
            TurnHalf(stadium.transform, turned);
            foreach (Transform t in stadium.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(StayStill, t.name) >= 0)
                    t.RotateAround(Vector3.zero, Vector3.up, -180f);
        }
        if (GameObject.Find("Crowd") is GameObject crowd) TurnHalf(crowd.transform, turned);
        if (FindFirstObjectByType<ClubGround>() is ClubGround club) TurnHalf(club.transform, turned);
        if (GameObject.Find("Floodlight Glows") is GameObject glows && glows.transform.parent == null)
            TurnHalf(glows.transform, turned);
        // The sun (or the floodlights' key) comes from the other side too, as it would.
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && !Under(l.transform, turned))
                l.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * l.transform.rotation;
    }

    private static void TurnHalf(Transform t, List<Transform> turned)
    {
        t.RotateAround(Vector3.zero, Vector3.up, 180f);
        turned.Add(t);
    }

    private static bool Under(Transform t, List<Transform> roots)
    {
        foreach (Transform r in roots)
            if (t.IsChildOf(r))
                return true;
        return false;
    }
}
