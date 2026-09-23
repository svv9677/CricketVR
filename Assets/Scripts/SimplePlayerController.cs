using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SimplePlayerController : MonoBehaviour
{
    private CharacterController _cc;
    private float _verticalVelocity;
    private float _cameraPitch;
    private Transform _cameraTransform;
    private readonly List<UnityEngine.XR.InputDevice> _xrDevices = new List<UnityEngine.XR.InputDevice>();

    void Start()
    {
        _cc = GetComponent<CharacterController>();
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        Vector3 moveInput = Vector3.zero;
        float yawInput = 0f;
        float pitchInput = 0f;

        // --- Keyboard (editor) ---
        if (Keyboard.current != null)
        {
            if (Keyboard.current[Key.W].isPressed) moveInput.z += 1f;
            if (Keyboard.current[Key.S].isPressed) moveInput.z -= 1f;
            if (Keyboard.current[Key.A].isPressed) moveInput.x -= 1f;
            if (Keyboard.current[Key.D].isPressed) moveInput.x += 1f;
            if (Keyboard.current[Key.LeftArrow].isPressed) yawInput -= 1f;
            if (Keyboard.current[Key.RightArrow].isPressed) yawInput += 1f;
            if (Keyboard.current[Key.UpArrow].isPressed) pitchInput -= 1f;
            if (Keyboard.current[Key.DownArrow].isPressed) pitchInput += 1f;
        }

        // Mouse look (right-click held)
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yawInput += delta.x * 0.15f;
            pitchInput += delta.y * 0.15f;
        }

        // --- XR thumbsticks (device) ---
        // Right controller stick → movement
        Vector2 rightStick = GetThumbstick(leftHand: false);
        moveInput.x += rightStick.x;
        moveInput.z += rightStick.y;

        // Left controller stick → yaw rotation
        Vector2 leftStick = GetThumbstick(leftHand: true);
        yawInput += leftStick.x;

        // --- Apply yaw rotation ---
        if (Mathf.Abs(yawInput) > 0.01f)
            transform.Rotate(Vector3.up, yawInput * 60f * Time.deltaTime, Space.World);

        // --- Apply camera pitch (editor only — on device, head tracking overrides) ---
        if (_cameraTransform != null && Mathf.Abs(pitchInput) > 0.01f)
        {
            _cameraPitch = Mathf.Clamp(_cameraPitch + pitchInput * 45f * Time.deltaTime, -80f, 80f);
            Vector3 euler = _cameraTransform.localEulerAngles;
            euler.x = _cameraPitch;
            _cameraTransform.localEulerAngles = euler;
        }

        // --- Apply movement with gravity ---
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.z;
        if (move.sqrMagnitude > 1f) move.Normalize();
        move *= 3f;

        if (_cc != null)
        {
            if (_cc.isGrounded)
                _verticalVelocity = -0.5f;
            else
                _verticalVelocity += Physics.gravity.y * Time.deltaTime;

            move.y = _verticalVelocity;
            _cc.Move(move * Time.deltaTime);
        }
        else
        {
            transform.position += move * Time.deltaTime;
        }
    }

    private Vector2 GetThumbstick(bool leftHand)
    {
        var chars = (leftHand ? UnityEngine.XR.InputDeviceCharacteristics.Left : UnityEngine.XR.InputDeviceCharacteristics.Right)
                    | UnityEngine.XR.InputDeviceCharacteristics.Controller;
        _xrDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(chars, _xrDevices);
        for (int i = 0; i < _xrDevices.Count; i++)
        {
            if (_xrDevices[i].TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 val))
            {
                if (val.magnitude < 0.15f) return Vector2.zero;
                return val;
            }
        }
        return Vector2.zero;
    }
}
