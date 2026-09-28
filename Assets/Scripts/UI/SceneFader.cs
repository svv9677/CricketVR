using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fades to black, loads a scene, fades back in. A hard cut between VR scenes is genuinely
/// unpleasant in a headset, and the black also covers the frame or two where XRRigSetup is still
/// looking for a floor-relative tracking origin and the camera sits at the fallback eye height.
///
/// Placed in each scene by the scene builders, on a screen-space overlay canvas that sorts above
/// everything. The fade uses unscaled time, so it still runs while a match is paused.
/// </summary>
public class SceneFader : MonoBehaviour
{
    private const float FadeSeconds = 0.35f;

    [SerializeField] private Image black;

    private static SceneFader instance;

    private void Awake()
    {
        instance = this;
        if (black != null)
        {
            black.color = new Color(0f, 0f, 0f, 1f);
            black.raycastTarget = false;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Start()
    {
        StartCoroutine(Fade(1f, 0f));
    }

    /// Load `sceneName` behind a fade. Safe to call with no fader in the scene: it falls back to
    /// a plain load rather than failing to change scene at all.
    public static void Load(string sceneName)
    {
        // Pause may have frozen time. Restore it first: a paused game that changed scene would
        // otherwise arrive frozen, and WaitForSeconds anywhere would never complete.
        Time.timeScale = 1f;
        if (instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }
        instance.StartCoroutine(instance.LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        yield return Fade(0f, 1f);
        yield return SceneManager.LoadSceneAsync(sceneName);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (black == null)
            yield break;
        black.raycastTarget = to > from;   // swallow clicks while covering the view
        for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(from, to, t / FadeSeconds);
            black.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        black.color = new Color(0f, 0f, 0f, to);
        black.raycastTarget = to > 0.99f;
    }
}
