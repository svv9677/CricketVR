using System.Collections;
using UnityEngine;

/// <summary>
/// Opens the menu as soon as Splash loads. In the match scenes MenuRoot is opened with B; in
/// Splash the menu IS the scene, so something has to open it, and a small component keeps that
/// out of MenuRoot itself.
///
/// It waits for the rig first. XRRigSetup only lifts the camera to eye height in its Update - by
/// floor-relative tracking on a headset, or by the fallback height without one - so a menu opened
/// in Start is placed against a camera still sitting at y = 0, and ends up on the floor about a
/// metre and a half below the player's eyeline. That is exactly what happened the first time.
/// </summary>
[RequireComponent(typeof(MenuRoot))]
public class SplashMenuOpener : MonoBehaviour
{
    /// Give up waiting for a real floor origin and use whatever height the camera has. In the
    /// editor with no headset there is never a floor origin, and one frame is enough for
    /// XRRigSetup to have applied its fallback.
    private const float MaxWaitSeconds = 0.5f;

    private IEnumerator Start()
    {
        var rig = FindFirstObjectByType<XRRigSetup>();
        float waited = 0f;

        // At least one frame, so XRRigSetup.Update has run at all.
        yield return null;

        while (rig != null && !rig.HasFloorOrigin && waited < MaxWaitSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        GetComponent<MenuRoot>().Open();
    }
}
