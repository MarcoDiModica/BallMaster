using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float acceleration = 30f;
    public float deceleration = 20f;
    public float airControl = 0.5f;
    public float jumpForce = 6f;
    public float gravity = -15f;
    public float slowedSpeed = 3f;
    public float forwardBoost = 15f;

    [Header("Advanced Movement")]
    public float slideSpeed = 12f;
    public float slideDuration = 0.8f;
    public float slideHeight = 0.5f;
    public float slideCooldown = 1f;

    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float airDashCooldown = 1f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 0.2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    [Header("Camera Juice")]
    public float tiltAngle = 2f;
    public float tiltSpeed = 5f;
    public float bobFrequency = 10f;
    public float bobAmplitude = 0.1f;
    public float slideCameraDrop = 0.5f;

    [Header("References")]
    public Transform cameraTransform;
    public Transform ballEquipTransform;

    private CharacterController controller;
    private Vector3 velocity;

    //ground
    private float lastGroundedTime = 0f;
    private float lastJumpPressedTime = -1f;
    private bool isJumping = false;
    private bool isSprinting = false;

    //slide
    private bool isSliding = false;
    private float slideTimer = 0f;
    private Vector3 slideDirection;
    private float defaultHeight;
    private float defaultCenterY;
    private float lastSlideTime = -10f;

    //dash
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float lastDashTime = -10f;
    private bool hasAirDashed = false;

    //ball
    private Ball equippedBall = null;
    private bool isPaused = false;
    private float xRotation = 0f;
    private Vector2 currentInput;
    private float defaultYPos = 0;
    private float bobTimer = 0;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        defaultHeight = controller.height;
        defaultCenterY = controller.center.y;

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Start()
    {
        if (cameraTransform != null)
            defaultYPos = cameraTransform.localPosition.y;
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    public void Move(Vector2 inputDir)
    {
        currentInput = inputDir;
    }

    public void SetSprint(bool active)
    {
        isSprinting = active;
    }

    public void TrySlideOrDash()
    {
        if (isPaused)
            return;

        if (!controller.isGrounded)
        {
            if (!hasAirDashed && Time.time - lastDashTime > airDashCooldown)
            {
                StartDash();
            }
        }
        else
        {
            if (!isSliding && Time.time - lastSlideTime > slideCooldown)
            {
                StartSlide();
            }
        }
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        lastDashTime = Time.time;
        hasAirDashed = true;

        Vector3 dir = transform.right * currentInput.x + transform.forward * currentInput.y;
        if (dir.magnitude < 0.1f)
            dir = transform.forward;

        Vector3 dashVel = dir.normalized * dashForce;
        velocity.x = dashVel.x;
        velocity.z = dashVel.z;
        velocity.y = 0;
    }

    void StartSlide()
    {
        isSliding = true;
        slideTimer = slideDuration;
        lastSlideTime = Time.time;

        controller.height = slideHeight;
        controller.center = new Vector3(0, slideHeight / 2, 0);

        Vector3 dir = transform.right * currentInput.x + transform.forward * currentInput.y;
        if (dir.magnitude < 0.1f)
            dir = transform.forward;

        slideDirection = dir.normalized;

        Vector3 slideVel = slideDirection * slideSpeed;
        velocity.x = slideVel.x;
        velocity.z = slideVel.z;
    }

    void StopSlide()
    {
        isSliding = false;
        controller.height = defaultHeight;
        controller.center = new Vector3(0, defaultCenterY, 0);
    }

    public void Look(Vector2 delta)
    {
        if (isPaused)
            return;

        xRotation -= delta.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.Rotate(Vector3.up * delta.x);
    }

    public void Jump()
    {
        if (isPaused)
            return;
        lastJumpPressedTime = Time.time;

        if (isSliding)
            StopSlide();
    }

    public void TryThrow()
    {
        if (isPaused || equippedBall == null)
            return;
        ThrowBall();
    }

    void Update()
    {
        if (isPaused)
            return;

        bool isGrounded = controller.isGrounded;

        if (isGrounded)
        {
            hasAirDashed = false;
            lastGroundedTime = Time.time;
            isJumping = false;
        }

        //dash
        if (isDashing)
        {
            controller.Move(velocity * Time.deltaTime);
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0)
            {
                isDashing = false;
                velocity.x *= 0.5f;
                velocity.z *= 0.5f;
            }
            return;
        }

        //vertical
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        bool canJump = isGrounded || ((Time.time - lastGroundedTime) <= coyoteTime);
        bool jumpRequested = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (canJump && jumpRequested && !isJumping)
        {
            velocity.y = jumpForce;
            isJumping = true;
            lastJumpPressedTime = -1f;
            lastGroundedTime = -1f;

            //slide jump funciona como la ******
            if (isSliding)
            {
                float boost = forwardBoost;
                velocity.x += slideDirection.x * boost;
                velocity.z += slideDirection.z * boost;
                StopSlide();
            }
        }

        velocity.y += gravity * Time.deltaTime;

        //momentum
        if (isSliding)
        {
            float slideFriction = 2f;
            velocity.x = Mathf.MoveTowards(velocity.x, 0, slideFriction * Time.deltaTime);
            velocity.z = Mathf.MoveTowards(velocity.z, 0, slideFriction * Time.deltaTime);

            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0)
                StopSlide();
        }
        else
        {
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            if (equippedBall != null)
                targetSpeed = slowedSpeed;

            Vector3 targetDir =
                transform.right * currentInput.x + transform.forward * currentInput.y;
            Vector3 targetVel = targetDir * targetSpeed;

            Vector3 currentHorzVel = new Vector3(velocity.x, 0, velocity.z);

            float accelRate = (currentInput.magnitude > 0.01f) ? acceleration : deceleration;
            if (!isGrounded)
                accelRate *= airControl;

            currentHorzVel = Vector3.MoveTowards(
                currentHorzVel,
                targetVel,
                accelRate * Time.deltaTime
            );

            velocity.x = currentHorzVel.x;
            velocity.z = currentHorzVel.z;
        }

        controller.Move(velocity * Time.deltaTime);

        Vector3 horzVel = new Vector3(velocity.x, 0, velocity.z);
        HandleCameraJuice(currentInput.x, horzVel.magnitude);
    }

    void HandleCameraJuice(float inputX, float speedParam)
    {
        if (cameraTransform == null)
            return;

        //tilt
        float targetTilt = -inputX * tiltAngle;
        Quaternion currentRot = cameraTransform.localRotation;
        float newZ = Mathf.LerpAngle(
            currentRot.eulerAngles.z,
            targetTilt,
            Time.deltaTime * tiltSpeed
        );

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, newZ);

        //headbob & slide height
        float targetY = defaultYPos;

        if (isSliding)
        {
            targetY -= slideCameraDrop;
        }
        else if (speedParam > 0.1f && controller.isGrounded)
        {
            bobTimer += Time.deltaTime * bobFrequency * (isSprinting ? 1.5f : 1f);
            targetY += Mathf.Sin(bobTimer) * bobAmplitude;
        }
        else
        {
            bobTimer = 0;
        }

        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetY, Time.deltaTime * 10f);
        cameraTransform.localPosition = camPos;
    }

    void OnTriggerEnter(Collider other)
    {
        if (equippedBall != null)
            return;

        Ball ball = other.GetComponent<Ball>();
        if (ball != null)
        {
            string myId = GetComponent<NetworkObject>()?.objectId ?? "local";
            if (ball.CanBePickedUp(myId))
            {
                EquipBallWithSync(ball);
            }
        }
    }

    public void EquipBall(Ball ball)
    {
        equippedBall = ball;
        string myId = GetComponent<NetworkObject>()?.objectId ?? "local";

        if (ballEquipTransform != null)
        {
            ball.Equip(ballEquipTransform, myId);
        }
        else if (cameraTransform != null)
        {
            ball.Equip(cameraTransform, myId);
        }
    }

    public void EquipBallWithSync(Ball ball)
    {
        EquipBall(ball);

        string myId = GetComponent<NetworkObject>()?.objectId;
        string ballId = ball.GetComponent<NetworkObject>()?.objectId;

        if (
            playerManager != null
            && playerManager.NetworkManager != null
            && !string.IsNullOrEmpty(myId)
            && !string.IsNullOrEmpty(ballId)
        )
        {
            playerManager.NetworkManager.SendBallEquip(ballId, myId);
        }
    }

    void ThrowBall()
    {
        if (equippedBall == null)
            return;

        Vector3 shootDirection =
            cameraTransform != null ? cameraTransform.forward : transform.forward;

        string myId = GetComponent<NetworkObject>()?.objectId ?? "local";
        string ballId = equippedBall.GetComponent<NetworkObject>()?.objectId;
        Vector3 launchPos = equippedBall.transform.position;

        equippedBall.Unequip();

        equippedBall.Launch(shootDirection, myId);

        if (
            playerManager != null
            && playerManager.NetworkManager != null
            && !string.IsNullOrEmpty(ballId)
        )
        {
            playerManager.NetworkManager.SendBallLaunch(ballId, shootDirection, myId, launchPos);
        }

        equippedBall = null;
    }

    private PlayerManager playerManager;

    public void Initialize(PlayerManager manager)
    {
        this.playerManager = manager;
    }

    public void Respawn(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        velocity = Vector3.zero;

        if (equippedBall != null)
        {
            equippedBall.Unequip();
            equippedBall = null;
        }
    }

    public void RequestRespawn()
    {
        if (playerManager != null && playerManager.spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, playerManager.spawnPoints.Length);
            Vector3 spawnPos = playerManager.spawnPoints[randomIndex].position;
            Respawn(spawnPos);
        }
    }
}
