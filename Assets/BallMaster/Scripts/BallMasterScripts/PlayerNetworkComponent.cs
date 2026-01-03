using System;
using UnityEngine;

public class PlayerNetworkComponent : MonoBehaviour
{
    [Header("Network Settings")]
    public float interpolationSpeed = 15f;
    private bool isLocalPlayer = false;

    public event Action OnTransformModified;

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float lastSendTime;
    private const float SEND_INTERVAL = 0.033f;

    private NetworkObject networkObject;
    private PlayerController playerController;
    private NetworkManager cachedNetworkManager;
    private PlayerManager playerManager;
    private PlayerAnimator playerAnimator;
    private bool useSnapshotInterpolation = false;

    private Vector3 lastPosition;
    private bool lastGroundedState = true;
    private float groundCheckDistance = 0.3f;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        playerController = GetComponent<PlayerController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        cachedNetworkManager = FindFirstObjectByType<NetworkManager>();
        playerManager = FindFirstObjectByType<PlayerManager>();

        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    void OnEnable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated += UpdateNetworkState;
        }
    }

    void OnDisable()
    {
        if (networkObject != null)
        {
            networkObject.OnStateUpdated -= UpdateNetworkState;
        }
    }

    public void Initialize(bool local)
    {
        isLocalPlayer = local;
        useSnapshotInterpolation =
            !local && cachedNetworkManager != null && !cachedNetworkManager.isHost;

        if (!isLocalPlayer)
        {
            if (playerController != null)
            {
                playerController.enabled = false;

                if (playerController.cameraTransform != null)
                {
                    Camera cam = playerController.cameraTransform.GetComponent<Camera>();
                    if (cam != null)
                        cam.enabled = false;

                    var listeners =
                        playerController.cameraTransform.GetComponentsInChildren<AudioListener>();
                    foreach (var listener in listeners)
                        listener.enabled = false;
                }
            }

            PlayerInput input = GetComponent<PlayerInput>();
            if (input != null)
                input.enabled = false;

            if (playerAnimator != null)
                playerAnimator.isLocalPlayer = false;

            if (playerController != null && playerController.wallStaminaCanvasGroup != null)
                playerController.wallStaminaCanvasGroup.gameObject.SetActive(false);
        }
        else
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            if (playerController != null)
            {
                playerController.enabled = true;

                if (playerController.cameraTransform != null)
                {
                    playerController.cameraTransform.gameObject.SetActive(true);

                    var listeners =
                        playerController.cameraTransform.GetComponentsInChildren<AudioListener>();
                    foreach (var listener in listeners)
                        listener.enabled = true;

                    foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    {
                        if (cam.transform != playerController.cameraTransform)
                        {
                            cam.enabled = false;
                            var l = cam.GetComponent<AudioListener>();
                            if (l != null)
                                l.enabled = false;
                        }
                    }
                }
            }

            if (playerAnimator != null)
                playerAnimator.isLocalPlayer = true;
        }
    }

    public bool IsLocalPlayer => isLocalPlayer;

    private void UpdateNetworkState(Vector3 pos, Quaternion rot)
    {
        if (isLocalPlayer)
            return;

        targetPosition = pos;
        targetRotation = rot;
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            if (transform.hasChanged)
            {
                if (networkObject != null)
                {
                    if (cachedNetworkManager != null && cachedNetworkManager.isHost)
                    {
                        networkObject.MarkDirty();
                    }
                    else if (
                        cachedNetworkManager != null
                        && cachedNetworkManager.isConnected
                        && Time.time - lastSendTime >= SEND_INTERVAL
                    )
                    {
                        cachedNetworkManager.SendMyPlayerTransform(
                            networkObject.objectId,
                            transform.position,
                            transform.rotation
                        );
                        lastSendTime = Time.time;
                    }
                }

                OnTransformModified?.Invoke();
                transform.hasChanged = false;
            }
            return;
        }

        if (useSnapshotInterpolation && networkObject != null)
        {
            var (pos, rot, valid) = networkObject.InterpolateState();
            if (valid)
            {
                transform.position = pos;
                transform.rotation = rot;
            }
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                interpolationSpeed * Time.deltaTime
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                interpolationSpeed * Time.deltaTime
            );
        }

        UpdateRemoteAnimations();
    }

    private void UpdateRemoteAnimations()
    {
        if (playerAnimator == null)
            return;

        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        bool isSprinting = horizontalSpeed > 6f;

        bool isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, groundCheckDistance + 0.1f);

        bool isWallRunning = false;
        bool isWallLeft = false;
        if (Mathf.Abs(velocity.y) < 0.5f && !isGrounded && horizontalSpeed > 3f)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, -transform.right, out hit, 0.8f))
            {
                isWallRunning = true;
                isWallLeft = true;
            }
            else if (Physics.Raycast(transform.position, transform.right, out hit, 0.8f))
            {
                isWallRunning = true;
                isWallLeft = false;
            }
        }

        playerAnimator.UpdateAnimations(isGrounded, isWallRunning, isWallLeft, horizontalSpeed, isSprinting);

        lastPosition = transform.position;
        lastGroundedState = isGrounded;
    }
}
