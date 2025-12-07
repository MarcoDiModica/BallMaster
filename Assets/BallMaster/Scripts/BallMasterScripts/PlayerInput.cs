using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerInput : MonoBehaviour
{
    private PlayerController playerController;

    [Header("Input Sensitivity")]
    public float mouseSensitivity = 2f;
    public float gamepadSensitivity = 100f;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        HandleMovement();
        HandleLook();
        HandleActions();
    }

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            float x =
                Keyboard.current.aKey.isPressed ? -1f
                : Keyboard.current.dKey.isPressed ? 1f
                : 0f;
            float y =
                Keyboard.current.sKey.isPressed ? -1f
                : Keyboard.current.wKey.isPressed ? 1f
                : 0f;
            input = new Vector2(x, y);
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            if (stick.magnitude > 0.1f)
            {
                input = stick;
            }
        }

        playerController.Move(input);
    }

    void HandleLook()
    {
        Vector2 delta = Vector2.zero;

        if (Mouse.current != null)
        {
            delta += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.rightStick.ReadValue();
            if (stick.magnitude > 0.1f)
            {
                delta += stick * gamepadSensitivity * Time.deltaTime;
            }
        }

        if (delta.sqrMagnitude > 0)
        {
            playerController.Look(delta);
        }
    }

    void HandleActions()
    {
        bool isJumpHeld =
            (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);

        playerController.SetJumpHeld(isJumpHeld);

        if (
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        )
        {
            playerController.Jump();
        }

        if (
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame)
        )
        {
            playerController.TryThrow();
        }

        bool isSprinting =
            (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        playerController.SetSprint(isSprinting);

        if (
            (
                Keyboard.current != null
                && (
                    Keyboard.current.leftCtrlKey.wasPressedThisFrame
                    || Keyboard.current.cKey.wasPressedThisFrame
                )
            ) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        )
        {
            playerController.TrySlideOrDash();
        }
    }
}
