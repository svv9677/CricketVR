using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MigrateOVRToXR
{
    [MenuItem("Tools/Migrate OVR to Unity XR")]
    public static void Migrate()
    {
        var playerControllerGO = GameObject.Find("OVRPlayerController");
        if (playerControllerGO == null)
        {
            Debug.LogError("OVRPlayerController not found in scene");
            return;
        }

        var cameraRigGO = playerControllerGO.transform.Find("OVRCameraRig");
        if (cameraRigGO == null)
        {
            Debug.LogError("OVRCameraRig not found under OVRPlayerController");
            return;
        }

        Vector3 playerPos = playerControllerGO.transform.position;
        Quaternion playerRot = playerControllerGO.transform.rotation;

        // Find the center eye camera
        Camera centerCam = null;
        var trackingSpace = cameraRigGO.Find("TrackingSpace");
        if (trackingSpace != null)
        {
            var centerEye = trackingSpace.Find("CenterEyeAnchor");
            if (centerEye != null)
                centerCam = centerEye.GetComponent<Camera>();
        }
        if (centerCam == null)
            centerCam = cameraRigGO.GetComponentInChildren<Camera>();
        if (centerCam == null)
        {
            Debug.LogError("No camera found in OVRCameraRig hierarchy");
            return;
        }

        Debug.Log($"Found camera: {centerCam.gameObject.name} at {centerCam.transform.position}");

        Undo.RegisterFullObjectHierarchyUndo(playerControllerGO, "Migrate OVR to XR");

        // 1. Add TrackedPoseDriver to the camera for head tracking
        var tpd = centerCam.gameObject.GetComponent<TrackedPoseDriver>();
        if (tpd == null)
            tpd = Undo.AddComponent<TrackedPoseDriver>(centerCam.gameObject);
        var posAction = new UnityEngine.InputSystem.InputAction("Position", binding: "<XRHMD>/centerEyePosition");
        tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(posAction);
        var rotAction = new UnityEngine.InputSystem.InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation");
        tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(rotAction);

        // 2. Remove OVR components from OVRCameraRig
        RemoveComponent<OVRHeadsetEmulator>(cameraRigGO.gameObject);
        RemoveComponent<OVRManager>(cameraRigGO.gameObject);
        RemoveComponent<OVRCameraRig>(cameraRigGO.gameObject);

        // 3. Remove OVRPlayerController component (keep CharacterController for potential locomotion)
        RemoveComponent<OVRPlayerController>(playerControllerGO);

        // 4. Replace OVRRaycaster with GraphicRaycaster on any canvases
        var ovrRaycasters = playerControllerGO.GetComponentsInChildren<OVRRaycaster>(true);
        foreach (var raycaster in ovrRaycasters)
        {
            var go = raycaster.gameObject;
            Undo.DestroyObjectImmediate(raycaster);
            if (go.GetComponent<GraphicRaycaster>() == null)
                Undo.AddComponent<GraphicRaycaster>(go);
        }

        // 5. Remove OVRInputModule if present, ensure StandaloneInputModule exists
        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        foreach (var es in eventSystems)
        {
            var ovrInput = es.GetComponent<OVRInputModule>();
            if (ovrInput != null)
                Undo.DestroyObjectImmediate(ovrInput);
            if (es.GetComponent<StandaloneInputModule>() == null)
                Undo.AddComponent<StandaloneInputModule>(es.gameObject);
        }

        // 6. Rename for clarity
        playerControllerGO.name = "XRPlayerController";
        cameraRigGO.gameObject.name = "XRCameraRig";

        EditorUtility.SetDirty(playerControllerGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"Migration complete. Player at {playerPos}, rotation {playerRot.eulerAngles}. " +
                  $"TrackedPoseDriver added to {centerCam.gameObject.name}. OVR components removed.");
    }

    static void RemoveComponent<T>(GameObject go) where T : Component
    {
        var comp = go.GetComponent<T>();
        if (comp != null)
        {
            Debug.Log($"Removing {typeof(T).Name} from {go.name}");
            Undo.DestroyObjectImmediate(comp);
        }
    }
}
