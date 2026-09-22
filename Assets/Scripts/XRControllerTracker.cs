using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class XRControllerTracker : MonoBehaviour
{
    public bool isLeftHand;

    private readonly List<InputDevice> _devices = new List<InputDevice>();

    void Update()
    {
        var chars = (isLeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
                    | InputDeviceCharacteristics.Controller;
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(chars, _devices);
        if (_devices.Count > 0)
        {
            if (_devices[0].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                transform.localPosition = pos;
            if (_devices[0].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                transform.localRotation = rot;
        }
    }
}
