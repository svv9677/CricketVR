using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class XRControllerTracker : MonoBehaviour
{
    public bool isLeftHand;

    private readonly List<InputDevice> _devices = new List<InputDevice>();

    [Tooltip("The hand model under this controller - a prefab instance placed in the scene " +
             "(Tools > CricketVR > Build UI Prefabs), not spawned at runtime.")]
    [SerializeField] private XRHandVisual visual;

    // Read in Update for the game, and again just before rendering (as the camera's
    // TrackedPoseDriver does for the head) so the hands are drawn from as fresh a pose as the view.
    private void OnEnable() => Application.onBeforeRender += Refresh;
    private void OnDisable() => Application.onBeforeRender -= Refresh;

    void Update() => Refresh();

    public void Refresh()
    {
        var chars = (isLeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
                    | InputDeviceCharacteristics.Controller;
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(chars, _devices);
        bool tracked = false;
        if (_devices.Count > 0)
        {
            tracked = _devices[0].TryGetFeatureValue(CommonUsages.isTracked, out bool value) && value;
            if (_devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                transform.localPosition = pos;
            if (_devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                transform.localRotation = rot;
        }
        if (visual != null) visual.SetTracked(tracked);
    }
}
