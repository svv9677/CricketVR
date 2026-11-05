using UnityEngine;

/// <summary>
/// The replay screen at the club ground, where there is no big screen: put on the Replay Display by
/// ClubGroundVenue. At home the screen would hang in the open sky 32 m up, so it is not drawn
/// there; the replay is brought in front of the player instead (CameraReplay.SetScreenInFront, once,
/// as the player's own toggle would - the toggle shows it, and he can still turn it off, in which
/// case the replay plays unseen at home).
///
/// Hidden while within 20 m of home, not only exactly at home: the screen takes 0.8 s to fly down
/// from 32 m, and a screen sailing across the sky is as odd as one hanging in it. forceRenderingOff,
/// not enabled = false: the renderer's bounds stay valid for CameraReplay's ScreenInView, and
/// nothing else turns it back on.
/// </summary>
[DisallowMultipleComponent]
public class ClubReplayScreen : MonoBehaviour
{
    private const float HomeRadius = 20f;

    private Vector3 home;
    private Renderer[] renderers;
    private bool broughtInFront;

    private void Start()
    {
        home = transform.position;
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void LateUpdate()
    {
        bool atHome = (transform.position - home).sqrMagnitude < HomeRadius * HomeRadius;
        foreach (Renderer r in renderers)
            if (r != null)
                r.forceRenderingOff = atHome;
        if (!broughtInFront && CameraReplay.Instance != null)
        {
            broughtInFront = true;
            CameraReplay.Instance.SetScreenInFront(true);
        }
    }
}
