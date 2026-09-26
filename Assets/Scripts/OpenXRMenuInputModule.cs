using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Drives the world-space menus (settings, between-balls, grip calibration) with the controller
/// laser, through Unity's OpenXR-compatible UI input stack.
///
/// Lives on the scene's EventSystem, with everything it needs placed and assigned in the editor by
/// Tools > CricketVR > Build UI Prefabs - the input module, the aim transform, the laser, and a
/// saved input-actions asset whose pointer bindings already point at the XR controllers. Nothing
/// is created at runtime. (It used to be set up by the Oculus DebugUIBuilder, which spawned an
/// EventSystem, cloned the default actions and added raycasters when the game started.)
///
/// Panels register themselves when first shown; the pointer and the UI input run only while one
/// of them is open.
/// </summary>
public class OpenXRMenuInputModule : MonoBehaviour
{
    [SerializeField] private InputSystemUIInputModule inputModule;
    [SerializeField] private InputActionAsset menuActions;
    [SerializeField] private LaserPointer laser;
    [SerializeField] private LineRenderer beam;
    [SerializeField] private Transform aim;

    private XRController activeHand;
    private readonly List<GameObject> panels = new List<GameObject>();

    public static OpenXRMenuInputModule Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        inputModule.actionsAsset = menuActions;
        inputModule.enabled = false;
        // This driver draws the beam itself, after the EventSystem has raycast this frame.
        laser.enabled = false;
        laser.gameObject.SetActive(false);
    }

    /// Let the laser drive this panel. Its canvas already carries the raycasters (prefab).
    public static void RegisterPanel(GameObject panel)
    {
        if (Instance == null || panel == null || Instance.panels.Contains(panel))
            return;
        Instance.panels.Add(panel);
        foreach (var canvas in panel.GetComponentsInChildren<Canvas>(true))
            canvas.worldCamera = Camera.main;
    }

    private bool AnyPanelOpen()
    {
        foreach (GameObject p in panels)
            if (p != null && p.activeInHierarchy)
                return true;
        return false;
    }

    private static bool IsTracked(XRController hand)
    {
        return hand != null && hand.added && hand.TryGetChildControl<ButtonControl>("pointer/isTracked")?.isPressed == true;
    }

    private void Update()
    {
        if (inputModule.xrTrackingOrigin == null && Camera.main != null)
            inputModule.xrTrackingOrigin = Camera.main.transform.parent;
        bool open = AnyPanelOpen() && Application.isFocused;
        // Disabling flushes pressed/drag/hover state when the menus close or focus is lost.
        inputModule.enabled = open;
        if (laser.gameObject.activeSelf != open)
            laser.gameObject.SetActive(open);
    }

    private void LateUpdate()
    {
        var right = XRController.rightHand;
        var left = XRController.leftHand;
        if (!IsTracked(activeHand)) activeHand = IsTracked(right) ? right : left;
        if (IsTracked(right) && right.TryGetChildControl<ButtonControl>("triggerPressed")?.wasPressedThisFrame == true) activeHand = right;
        if (IsTracked(left) && left.TryGetChildControl<ButtonControl>("triggerPressed")?.wasPressedThisFrame == true) activeHand = left;
        bool visible = inputModule.enabled && laser.gameObject.activeInHierarchy
            && inputModule.xrTrackingOrigin != null && IsTracked(activeHand);
        if (!visible)
        {
            beam.enabled = false;
            if (laser.cursorVisual != null) laser.cursorVisual.SetActive(false);
            return;
        }
        var origin = inputModule.xrTrackingOrigin;
        aim.SetPositionAndRotation(
            origin.TransformPoint(activeHand.GetChildControl<Vector3Control>("pointerPosition").ReadValue()),
            origin.rotation * activeHand.GetChildControl<QuaternionControl>("pointerRotation").ReadValue());
        var hit = inputModule.GetLastRaycastResult(activeHand.deviceId);
        bool hasHit = hit.gameObject != null;
        Vector3 end = hasHit ? hit.worldPosition : aim.position + aim.forward * laser.maxLength;
        // An aiming ray must be visible even before it intersects a menu.
        beam.enabled = true;
        beam.SetPosition(0, aim.position);
        beam.SetPosition(1, end);
        if (laser.cursorVisual != null)
        {
            laser.cursorVisual.SetActive(hasHit);
            if (hasHit) laser.cursorVisual.transform.position = end;
        }
    }

    private void OnDisable()
    {
        if (inputModule != null) inputModule.enabled = false;
    }
}
