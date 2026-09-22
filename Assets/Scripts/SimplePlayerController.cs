using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerController : MonoBehaviour
{
    private CharacterController _cc;

    void Start()
    {
        _cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        Vector3 move = Vector3.zero;
        if (Keyboard.current[Key.W].isPressed) move += transform.forward;
        if (Keyboard.current[Key.S].isPressed) move -= transform.forward;
        if (Keyboard.current[Key.A].isPressed) move -= transform.right;
        if (Keyboard.current[Key.D].isPressed) move += transform.right;
        if (Keyboard.current[Key.Q].isPressed) move += Vector3.up;
        if (Keyboard.current[Key.E].isPressed) move -= Vector3.up;

        float speed = 3f;
        if (move.sqrMagnitude < 0.001f) return;
        move = move.normalized * speed * Time.deltaTime;

        if (_cc != null)
            _cc.Move(move);
        else
            transform.position += move;

        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            transform.Rotate(Vector3.up, delta.x * 0.2f, Space.World);
        }
    }
}
