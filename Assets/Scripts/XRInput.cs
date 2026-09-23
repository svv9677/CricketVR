using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public enum XRButton { A, B, X, Y }

public static class XRInput
{
    private static readonly Dictionary<XRButton, bool> _prev = new Dictionary<XRButton, bool>();
    private static readonly List<InputDevice> _devices = new List<InputDevice>();

    public static bool Get(XRButton button)
    {
        GetMapping(button, out var chars, out var usage);
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(chars, _devices);
        for (int i = 0; i < _devices.Count; i++)
        {
            if (_devices[i].TryGetFeatureValue(usage, out bool val) && val)
                return true;
        }
        return false;
    }

    public static bool GetDown(XRButton button)
    {
        bool current = Get(button);
        _prev.TryGetValue(button, out bool prev);
        return current && !prev;
    }

    public static void LateUpdate()
    {
        _prev[XRButton.A] = Get(XRButton.A);
        _prev[XRButton.B] = Get(XRButton.B);
        _prev[XRButton.X] = Get(XRButton.X);
        _prev[XRButton.Y] = Get(XRButton.Y);
    }

    /// <summary>Thumbstick axes for one hand, with a small dead zone. Zero when absent.</summary>
    public static Vector2 GetThumbstick(bool leftHand, float deadZone = 0.15f)
    {
        var chars = (leftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
                    | InputDeviceCharacteristics.Controller;
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(chars, _devices);
        for (int i = 0; i < _devices.Count; i++)
        {
            if (_devices[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 val))
                return val.magnitude < deadZone ? Vector2.zero : val;
        }
        return Vector2.zero;
    }

    public static void SendHaptics(bool leftHand, float amplitude, float duration)
    {
        var chars = (leftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right)
                    | InputDeviceCharacteristics.Controller;
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(chars, _devices);
        for (int i = 0; i < _devices.Count; i++)
        {
            if (_devices[i].TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            {
                _devices[i].SendHapticImpulse(0, amplitude, duration);
                return;
            }
        }
    }

    private static void GetMapping(XRButton button, out InputDeviceCharacteristics chars, out InputFeatureUsage<bool> usage)
    {
        switch (button)
        {
            case XRButton.A:
                chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
                usage = CommonUsages.primaryButton;
                return;
            case XRButton.B:
                chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
                usage = CommonUsages.secondaryButton;
                return;
            case XRButton.X:
                chars = InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
                usage = CommonUsages.primaryButton;
                return;
            case XRButton.Y:
                chars = InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
                usage = CommonUsages.secondaryButton;
                return;
            default:
                chars = 0;
                usage = default;
                return;
        }
    }
}
