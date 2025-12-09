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

    [Header("Slide/Dash")]
    public float slideSpeed = 12f;
    public float slideDuration = 0.8f;
    public float slideHeight = 0.5f;
    public float slideCooldown = 1f;

    public float dashForce = 20f;
    public float dashDuration = 0.2f;
    public float airDashCooldown = 1f;
    public float lowJumpMultiplier = 2.5f;

    [Header("Momentum Settings")]
    public float slideJumpBoost = 15f;
    public float momentumDrag = 10f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 0.2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    [Header("Wall Run Settings")]
    public float wallRunSpeed = 10f;
    public float wallDetectionDistance = 0.8f;
    public float minWallRunHeight = 1.5f;
    public float minWallRunSpeed = 3f;
    public LayerMask wallRunLayers = ~0;

    [Header("Wall Jump Settings")]
    public float wallJumpUpForce = 8f;
    public float wallJumpSideForce = 6f;
    public float wallCoyoteTime = 0.15f;
    public float wallKickWindow = 0.2f;

    [Header("Wall Run Camera")]
    public float wallRunCameraTilt = 12f;

    [Header("Slope Settings")]
    public float slopeStickForce = 10f;
    public float slopeSlideMultiplier = 1.5f;
    public float maxSlopeAngle = 45f;

    [Header("Camera Juice")]
    public float tiltAngle = 2f;
    public float tiltSpeed = 5f;
    public float bobFrequency = 10f;
    public float bobAmplitude = 0.1f;
    public float slideCameraDrop = 0.5f;
    public float baseFov = 60f;
    public float sprintFov = 75f;
    public float dashFov = 85f;
    public float fovSpeed = 5f;

    [Header("References")]
    public Transform cameraTransform;
    private Camera playerCamera;
    public Transform ballEquipTransform;

    private CharacterController controller;
    private Vector3 velocity;

    private float lastGroundedTime = 0f;
    private float lastJumpPressedTime = -1f;
    private bool isJumping = false;
    private bool isSprinting = false;

    private bool isSliding = false;
    private float slideTimer = 0f;
    private Vector3 slideDirection;
    private float defaultHeight;
    private float defaultCenterY;
    private float lastSlideTime = -10f;

    private bool isDashing = false;
    private float dashTimer = 0f;
    private float lastDashTime = -10f;
    private bool hasAirDashed = false;

    private Ball equippedBall = null;
    private bool isPaused = false;
    private float xRotation = 0f;
    private Vector2 currentInput;
    private float defaultYPos = 0;
    private float bobTimer = 0;

    private Vector3 groundNormal = Vector3.up;
    private bool isOnSlope = false;
    private float currentSlopeAngle = 0f;

    private bool isWallRunning = false;
    private bool isWallLeft = false;
    private bool isWallRight = false;
    private Vector3 wallNormal = Vector3.zero;
    private float lastWallTime = -10f;
    private float wallContactTime = -10f;
    private bool hasWallJumped = false;

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
        {
            defaultYPos = cameraTransform.localPosition.y;
            playerCamera = cameraTransform.GetComponent<Camera>();
            if (playerCamera != null)
                baseFov = playerCamera.fieldOfView;
        }
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

    public void TryDrop()
    {
        if (isPaused || equippedBall == null)
            return;
        DropBall();
    }

    void Update()
    {
        if (isPaused)
            return;

        bool isGrounded = controller.isGrounded;
        DetectSlope();

        if (isGrounded)
        {
            hasAirDashed = false;
            hasWallJumped = false;
            lastGroundedTime = Time.time;
            isJumping = false;

            if (isWallRunning)
                StopWallRun();
        }

        HandleWallRun();

        if (HandleDash())
            return;

        if (!isWallRunning)
        {
            HandleGravity(isGrounded);
        }

        HandleJumping(isGrounded);

        if (!isWallRunning)
        {
            HandleMovementPhysics(isGrounded);
        }

        controller.Move(velocity * Time.deltaTime);

        Vector3 horzVel = new Vector3(velocity.x, 0, velocity.z);
        HandleCameraJuice(currentInput.x, horzVel.magnitude);
    }

    private void DetectSlope()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out hit, controller.height / 2f + 0.5f))
        {
            groundNormal = hit.normal;
            currentSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            isOnSlope = currentSlopeAngle > 0.1f && currentSlopeAngle <= maxSlopeAngle;
        }
        else
        {
            groundNormal = Vector3.up;
            currentSlopeAngle = 0f;
            isOnSlope = false;
        }
    }

    private void DetectWalls()
    {
        if (controller.isGrounded)
        {
            isWallLeft = false;
            isWallRight = false;
            return;
        }

        if (Physics.Raycast(transform.position, Vector3.down, minWallRunHeight))
        {
            isWallLeft = false;
            isWallRight = false;
            return;
        }

        RaycastHit leftHit,
            rightHit;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        isWallLeft = Physics.Raycast(
            origin,
            -transform.right,
            out leftHit,
            wallDetectionDistance,
            wallRunLayers
        );
        isWallRight = Physics.Raycast(
            origin,
            transform.right,
            out rightHit,
            wallDetectionDistance,
            wallRunLayers
        );

        if (isWallLeft)
            wallNormal = leftHit.normal;
        else if (isWallRight)
            wallNormal = rightHit.normal;
    }

    private void HandleWallRun()
    {
        DetectWalls();

        bool wallDetected = isWallLeft || isWallRight;
        bool hasSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude > minWallRunSpeed;
        bool movingForward = currentInput.y > 0.1f;
        bool isFalling = velocity.y < 0;

        if (
            !isWallRunning
            && wallDetected
            && hasSpeed
            && movingForward
            && !hasWallJumped
            && isFalling
        )
        {
            StartWallRun();
        }

        if (isWallRunning)
        {
            UpdateWallRun();
        }

        if (wallDetected && !isWallRunning)
        {
            wallContactTime = Time.time;
        }
    }

    private void StartWallRun()
    {
        isWallRunning = true;
        velocity.y = 0;
    }

    private void UpdateWallRun()
    {
        lastWallTime = Time.time;

        bool wallLost = !isWallLeft && !isWallRight;
        bool grounded = controller.isGrounded;

        if (wallLost || grounded)
        {
            StopWallRun();
            return;
        }

        Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up);

        if (Vector3.Dot(wallForward, transform.forward) < 0)
            wallForward = -wallForward;

        velocity = wallForward * wallRunSpeed;
        velocity.y = 0;
    }

    private void StopWallRun()
    {
        isWallRunning = false;
        lastWallTime = Time.time;
    }

    private bool HandleDash()
    {
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
            return true;
        }
        return false;
    }

    private void HandleGravity(bool isGrounded)
    {
        if (isGrounded && velocity.y < 0)
        {
            if (isOnSlope && !isJumping)
            {
                Vector3 moveDir = new Vector3(velocity.x, 0, velocity.z).normalized;
                float slopeInfluence = Vector3.Dot(
                    moveDir,
                    Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized
                );

                if (slopeInfluence > 0.1f)
                {
                    velocity.y = -slopeStickForce * (1f + currentSlopeAngle / maxSlopeAngle);
                }
                else
                {
                    velocity.y = -2f;
                }
            }
            else
            {
                velocity.y = -2f;
            }
        }
    }

    private void HandleJumping(bool isGrounded)
    {
        bool jumpRequested = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (jumpRequested && !isGrounded)
        {
            if (isWallRunning)
            {
                PerformWallJump();
                return;
            }

            bool inWallCoyote = (Time.time - lastWallTime) <= wallCoyoteTime;
            if (inWallCoyote && !hasWallJumped)
            {
                PerformWallJump();
                return;
            }

            bool wallDetected = isWallLeft || isWallRight;
            bool inWallKickWindow = (Time.time - wallContactTime) <= wallKickWindow;
            if ((wallDetected || inWallKickWindow) && !hasWallJumped)
            {
                PerformWallJump();
                return;
            }
        }

        bool canJump = isGrounded || ((Time.time - lastGroundedTime) <= coyoteTime);

        if (canJump && jumpRequested && !isJumping)
        {
            velocity.y = jumpForce;
            isJumping = true;
            lastJumpPressedTime = -1f;
            lastGroundedTime = -1f;

            if (isSliding)
            {
                Vector3 boostVel = slideDirection * slideJumpBoost;
                velocity.x = boostVel.x;
                velocity.z = boostVel.z;
                StopSlide();
            }
        }

        if (isJumping && velocity.y > 0 && !isJumpHeld)
        {
            velocity.y += gravity * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        if (!isWallRunning)
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    private void PerformWallJump()
    {
        StopWallRun();

        Vector3 currentHorizontal = new Vector3(velocity.x, 0, velocity.z);
        Vector3 wallPush = wallNormal * wallJumpSideForce;

        velocity.x = currentHorizontal.x + wallPush.x;
        velocity.z = currentHorizontal.z + wallPush.z;
        velocity.y = wallJumpUpForce;

        hasWallJumped = true;
        lastJumpPressedTime = -1f;
        isJumping = true;
    }

    private void HandleMovementPhysics(bool isGrounded)
    {
        if (isSliding)
        {
            if (isOnSlope && isGrounded)
            {
                Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                float slopeAlignment = Vector3.Dot(slideDirection, slopeDir);

                if (slopeAlignment > 0.1f)
                {
                    float slopeBoost = slopeSlideMultiplier * (currentSlopeAngle / maxSlopeAngle);
                    velocity.x += slopeDir.x * slopeBoost * Time.deltaTime * slideSpeed;
                    velocity.z += slopeDir.z * slopeBoost * Time.deltaTime * slideSpeed;

                    Vector3 horzVel = new Vector3(velocity.x, 0, velocity.z);
                    if (horzVel.magnitude > slideSpeed * 2f)
                    {
                        horzVel = horzVel.normalized * slideSpeed * 2f;
                        velocity.x = horzVel.x;
                        velocity.z = horzVel.z;
                    }
                }
                else if (slopeAlignment < -0.1f)
                {
                    float slopeFriction = 8f * (currentSlopeAngle / maxSlopeAngle);
                    velocity.x = Mathf.MoveTowards(velocity.x, 0, slopeFriction * Time.deltaTime);
                    velocity.z = Mathf.MoveTowards(velocity.z, 0, slopeFriction * Time.deltaTime);
                }
                else
                {
                    float slideFriction = 2f;
                    velocity.x = Mathf.MoveTowards(velocity.x, 0, slideFriction * Time.deltaTime);
                    velocity.z = Mathf.MoveTowards(velocity.z, 0, slideFriction * Time.deltaTime);
                }
            }
            else
            {
                float slideFriction = 2f;
                velocity.x = Mathf.MoveTowards(velocity.x, 0, slideFriction * Time.deltaTime);
                velocity.z = Mathf.MoveTowards(velocity.z, 0, slideFriction * Time.deltaTime);
            }

            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0)
                StopSlide();
        }
        else
        {
            bool canSprint = isSprinting && equippedBall == null;
            float targetSpeed = canSprint ? sprintSpeed : walkSpeed;
            if (equippedBall != null)
                targetSpeed = slowedSpeed;

            Vector3 targetDir =
                transform.right * currentInput.x + transform.forward * currentInput.y;

            if (targetDir.sqrMagnitude > 1f)
                targetDir.Normalize();

            Vector3 targetVel = targetDir * targetSpeed;
            Vector3 currentHorzVel = new Vector3(velocity.x, 0, velocity.z);

            float currentSpeed = currentHorzVel.magnitude;

            if (currentSpeed > targetSpeed && targetSpeed > 0.1f)
            {
                float speedDrop = momentumDrag * Time.deltaTime;
                float newSpeed = Mathf.Max(targetSpeed, currentSpeed - speedDrop);

                if (currentInput.magnitude > 0.01f)
                {
                    Vector3 blendedDir = Vector3.RotateTowards(
                        currentHorzVel.normalized,
                        targetDir.normalized,
                        10f * Time.deltaTime,
                        0f
                    );
                    currentHorzVel = blendedDir * newSpeed;
                }
                else
                {
                    currentHorzVel = currentHorzVel.normalized * newSpeed;
                }
            }
            else
            {
                float accelRate = (currentInput.magnitude > 0.01f) ? acceleration : deceleration;
                if (!isGrounded)
                    accelRate *= airControl;

                currentHorzVel = Vector3.MoveTowards(
                    currentHorzVel,
                    targetVel,
                    accelRate * Time.deltaTime
                );
            }

            velocity.x = currentHorzVel.x;
            velocity.z = currentHorzVel.z;
        }
    }

    void HandleCameraJuice(float inputX, float speedParam)
    {
        if (cameraTransform == null)
            return;

        float targetTilt = -inputX * tiltAngle;

        if (isWallRunning)
        {
            targetTilt = isWallLeft ? -wallRunCameraTilt : wallRunCameraTilt;
        }

        Quaternion currentRot = cameraTransform.localRotation;
        float newZ = Mathf.LerpAngle(
            currentRot.eulerAngles.z,
            targetTilt,
            Time.deltaTime * tiltSpeed
        );

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, newZ);

        float targetY = defaultYPos;

        if (isSliding)
        {
            targetY -= slideCameraDrop;
        }
        else if (speedParam > 0.1f && controller.isGrounded)
        {
            bool actualSprinting =
                isSprinting && equippedBall == null && speedParam > walkSpeed + 1f;
            bobTimer += Time.deltaTime * bobFrequency * (actualSprinting ? 1.5f : 1f);
            targetY += Mathf.Sin(bobTimer) * bobAmplitude;
        }
        else
        {
            bobTimer = 0;
        }

        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetY, Time.deltaTime * 10f);
        cameraTransform.localPosition = camPos;

        if (playerCamera != null)
        {
            float targetFov = baseFov;
            if (isDashing)
                targetFov = dashFov;
            else if (isSprinting && equippedBall == null && speedParam > walkSpeed + 1f)
                targetFov = sprintFov;
            else if (isSliding)
                targetFov = sprintFov;

            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFov,
                Time.deltaTime * fovSpeed
            );
        }
    }

    private bool isJumpHeld = false;

    public void SetJumpHeld(bool held)
    {
        isJumpHeld = held;
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

        Vector3 shootDirection = transform.forward;
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(50f);
            }

            shootDirection = (targetPoint - equippedBall.transform.position).normalized;
        }
        else if (cameraTransform != null)
        {
            shootDirection = cameraTransform.forward;
        }

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

    void DropBall()
    {
        if (equippedBall == null)
            return;

        Vector3 dropDirection = (transform.forward + Vector3.up * 0.2f).normalized * 5f;

        string myId = GetComponent<NetworkObject>()?.objectId ?? "local";
        string ballId = equippedBall.GetComponent<NetworkObject>()?.objectId;
        Vector3 launchPos = equippedBall.transform.position;

        equippedBall.Unequip();

        equippedBall.Drop(dropDirection, myId);

        if (
            playerManager != null
            && playerManager.NetworkManager != null
            && !string.IsNullOrEmpty(ballId)
        )
        {
            playerManager.NetworkManager.SendBallDrop(ballId, dropDirection, myId, launchPos);
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
