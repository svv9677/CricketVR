using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// The headset's camera feed behind the scene (Quest passthrough), for moments the player needs to
/// see their own room and hands - grip calibration.
///
/// Meta's OpenXR passthrough (com.unity.xr.meta-openxr, the "Meta Quest: Session" and "Meta Quest:
/// Camera (Passthrough)" features, Android only) runs while an ARSession and an ARCameraManager on
/// the rendering camera are enabled, and shows wherever the camera clears to alpha 0. Both are
/// made here on Begin and destroyed on End, so neither scene carries them and the game itself never
/// pays for them. Only the layers in `visibleLayers` are drawn meanwhile: the stadium goes, the room
/// shows through.
///
/// In the Editor (no passthrough) the camera clears to black instead, so the effect is still visible.
/// </summary>
public static class Passthrough
{
    public static bool Active { get; private set; }

    private static Camera camera;
    private static GameObject sessionObject;
    private static ARCameraManager cameraManager;
    private static CameraClearFlags savedClear;
    private static Color savedBackground;
    private static int savedMask;

    public static void Begin(Camera target, int visibleLayers)
    {
        if (Active || target == null)
            return;
        Active = true;
        camera = target;
        savedClear = camera.clearFlags;
        savedBackground = camera.backgroundColor;
        savedMask = camera.cullingMask;

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = visibleLayers;

        sessionObject = new GameObject("Passthrough AR Session");
        sessionObject.AddComponent<ARSession>();
        cameraManager = camera.gameObject.AddComponent<ARCameraManager>();
    }

    public static void End()
    {
        if (!Active)
            return;
        Active = false;
        if (cameraManager != null)
            Object.Destroy(cameraManager);
        if (sessionObject != null)
            Object.Destroy(sessionObject);
        cameraManager = null;
        sessionObject = null;
        if (camera != null)
        {
            camera.clearFlags = savedClear;
            camera.backgroundColor = savedBackground;
            camera.cullingMask = savedMask;
        }
        camera = null;
    }
}
