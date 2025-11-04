using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 5f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 2f;
    public float networkSendRate = 0.05f;
    public float groundCheckDistance = 0.2f; 
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;
    public Transform cameraTransform;
    
    private CharacterController controller;
    private NetworkObject networkObject;
    private Vector3 velocity;
    private bool isLocalPlayer = false;
    private float xRotation = 0f;
    private float nextSendTime = 0f;
    private float lastGroundedTime = 0f; 
    private float lastJumpPressedTime = -1f; 
    private bool isJumping = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        networkObject = GetComponent<NetworkObject>();
        
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }
    }

    public void SetAsLocalPlayer()
    {
        isLocalPlayer = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(true);
        
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.transform != cameraTransform)
                cam.gameObject.SetActive(false);
        }
    }

    public void SetAsRemotePlayer()
    {
        isLocalPlayer = false;
        
        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(false);
    }

    public bool IsLocalPlayer()
    {
        return isLocalPlayer;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        float mouseX = Mouse.current.delta.ReadValue().x * mouseSensitivity;
        float mouseY = Mouse.current.delta.ReadValue().y * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        float h = Keyboard.current.aKey.isPressed ? -1f : Keyboard.current.dKey.isPressed ? 1f : 0f;
        float v = Keyboard.current.wKey.isPressed ? 1f : Keyboard.current.sKey.isPressed ? -1f : 0f;
        
        bool isGrounded = controller.isGrounded;
        
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            isJumping = false;
        }
        
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            lastJumpPressedTime = Time.time;
        }
        
        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * speed * Time.deltaTime);

        bool canJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool jumpRequested = (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        if (canJump && jumpRequested && !isJumping)
        {
            velocity.y = jumpForce;
            isJumping = true;
            lastJumpPressedTime = -1f;
        }
        
        velocity.y += gravity * Time.deltaTime;
        
        controller.Move(velocity * Time.deltaTime);

        if (Time.time >= nextSendTime && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendPlayerTransform(
                networkObject.objectId,
                transform.position,
                transform.rotation
            );
            nextSendTime = Time.time + networkSendRate;
        }
    }
}