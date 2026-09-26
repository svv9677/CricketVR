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

    void Update()
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
