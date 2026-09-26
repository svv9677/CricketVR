using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

/// <summary>Wires the settings menu to Unity's OpenXR-compatible UI input stack.</summary>
public class OpenXRMenuInputModule : MonoBehaviour
{
    private InputSystemUIInputModule inputModule;
    private InputActionAsset menuActions;
    private GameObject menu;
    private LaserPointer laser;
    private LineRenderer beam;
    private Transform aim;
    private XRController activeHand;
    // Other world-space panels (e.g. the between-balls menu) that the same pointer drives.
    private readonly System.Collections.Generic.List<GameObject> extraMenus = new System.Collections.Generic.List<GameObject>();

    public static OpenXRMenuInputModule Instance { get; private set; }

    /// Let the laser pointer drive another world-space panel as well as the settings menu.
    public static void RegisterPanel(GameObject panelRoot)
    {
        if (Instance == null || panelRoot == null || Instance.extraMenus.Contains(panelRoot))
            return;
        Instance.extraMenus.Add(panelRoot);
        Instance.SetUpCanvases(panelRoot);
    }

    private bool ExtraMenuVisible()
    {
        foreach (GameObject m in extraMenus)
            if (m != null && m.activeInHierarchy)
                return true;
        return false;
    }

    private void SetUpCanvases(GameObject root)
    {
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            canvas.worldCamera = Camera.main;
            foreach (var old in canvas.GetComponents<OVRRaycaster>()) old.enabled = false;
            GraphicRaycaster mouse = null;
            foreach (var candidate in canvas.GetComponents<GraphicRaycaster>())
                if (candidate.GetType() == typeof(GraphicRaycaster)) { mouse = candidate; break; }
            if (mouse == null) mouse = canvas.gameObject.AddComponent<GraphicRaycaster>();
            mouse.enabled = true;
            var tracked = canvas.GetComponent<TrackedDeviceRaycaster>();
            if (tracked == null) tracked = canvas.gameObject.AddComponent<TrackedDeviceRaycaster>();
            tracked.maxDistance = laser != null ? laser.maxLength : 10f;
            // Menus should remain usable in front of the bat/body colliders.
            tracked.checkFor3DOcclusion = false;
            tracked.checkFor2DOcclusion = false;
        }
    }

    public static void Configure(GameObject menuRoot, GameObject helpers, LaserPointer pointer)
    {
        var helperSystem = helpers != null ? helpers.GetComponentInChildren<EventSystem>(true) : null;
        EventSystem system = null;
        foreach (var candidate in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            if (candidate != helperSystem && candidate.isActiveAndEnabled) { system = candidate; break; }
        if (system == null) system = helperSystem;
        if (system == null)
            system = new GameObject("Menu EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        if (helperSystem != null && helperSystem != system) helperSystem.enabled = false;
        if (helpers != null)
            foreach (var old in helpers.GetComponentsInChildren<BaseInputModule>(true)) old.enabled = false;
        foreach (var old in system.GetComponents<BaseInputModule>()) old.enabled = false;

        var driver = system.GetComponent<OpenXRMenuInputModule>();
        if (driver == null) driver = system.gameObject.AddComponent<OpenXRMenuInputModule>();
        driver.menu = menuRoot;
        driver.laser = pointer;
        driver.beam = pointer.GetComponent<LineRenderer>();
        driver.beam.sharedMaterial = Resources.Load<Material>("Materials/XRPointer");
        driver.beam.useWorldSpace = true;
        driver.beam.widthMultiplier = 0.004f;
        driver.beam.numCapVertices = 4;
        driver.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        driver.beam.receiveShadows = false;
        if (pointer.cursorVisual != null)
        {
            var renderer = pointer.cursorVisual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = driver.beam.sharedMaterial;
        }
        driver.aim = new GameObject("OpenXR Menu Aim").transform;
        driver.aim.SetParent(system.transform, false);
        driver.inputModule = system.GetComponent<InputSystemUIInputModule>();
        if (driver.inputModule == null) driver.inputModule = system.gameObject.AddComponent<InputSystemUIInputModule>();
        driver.inputModule.enabled = false;
        driver.inputModule.AssignDefaultActions();
        // Clone the defaults so the aim bindings never modify another UI module's actions.
        driver.menuActions = Instantiate(driver.inputModule.actionsAsset);
        driver.inputModule.actionsAsset = driver.menuActions;
        SetAimBinding(driver.inputModule.trackedDevicePosition.action, "<XRController>/pointerPosition");
        SetAimBinding(driver.inputModule.trackedDeviceOrientation.action, "<XRController>/pointerRotation");
        driver.inputModule.xrTrackingOrigin = Camera.main != null ? Camera.main.transform.parent : null;

        driver.SetUpCanvases(menuRoot);
        Instance = driver;
        // This driver updates the visual after EventSystem processes this frame's raycasts.
        pointer.enabled = false;
    }

    private static void SetAimBinding(InputAction action, string path)
    {
        for (int i = 0; i < action.bindings.Count; i++) action.ApplyBindingOverride(i, path);
    }

    private static bool IsTracked(XRController hand)
    {
        return hand != null && hand.added && hand.TryGetChildControl<ButtonControl>("pointer/isTracked")?.isPressed == true;
    }

    private void Update()
    {
        if (inputModule == null) return;
        if (inputModule.xrTrackingOrigin == null && Camera.main != null)
            inputModule.xrTrackingOrigin = Camera.main.transform.parent;
        bool settings = menu != null && menu.activeInHierarchy;
        bool extra = ExtraMenuVisible();
        bool visible = (settings || extra) && Application.isFocused;
        // Disable flushes pressed/drag/hover state when the menu closes or focus is lost.
        // Keep the module enabled without an HMD for mouse interaction in the Editor.
        inputModule.enabled = visible;
        // The laser object is switched with the settings menu; a between-balls panel needs it too.
        if (laser != null && !settings)
            laser.gameObject.SetActive(extra);
    }

    private void LateUpdate()
    {
        if (laser == null || inputModule == null) return;
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
        // An aiming ray must be visible even before it intersects the menu.
        beam.enabled = true;
        beam.SetPosition(0, aim.position);
        beam.SetPosition(1, end);
        if (laser.cursorVisual != null)
        {
            laser.cursorVisual.SetActive(hasHit);
            if (hasHit) laser.cursorVisual.transform.position = end;
        }
    }

    private void OnDestroy()
    {
        if (inputModule != null) inputModule.enabled = false;
        if (menuActions != null) Destroy(menuActions);
        if (aim != null) Destroy(aim.gameObject);
    }
}
