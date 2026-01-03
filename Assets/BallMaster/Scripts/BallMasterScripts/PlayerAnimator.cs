using DG.Tweening;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Movement Detection")]
    public Transform rootTransform;
    public LayerMask groundLayers = ~0;
    public float groundCheckDistance = 0.4f;

    [Header("Movement Lean")]
    public float leanAngle = 6f;
    public float leanSmoothing = 0.15f;

    [Header("Walk/Run Squash")]
    public float squashAmount = 0.03f;
    public float squashFrequency = 8f;
    public float squashSmoothing = 0.1f;

    [Header("Wall Run Tilt")]
    public float wallTiltAngle = 12f;
    public float wallTiltSmoothing = 0.15f;
    public float wallCheckDistance = 0.7f;

    private Vector3 lastRootPosition;
    private Vector3 originalScale;
    private Quaternion originalLocalRot;

    private float squashPhase;
    private Vector3 currentScale;
    private Vector3 targetScale;
    private Quaternion currentLeanRot;
    private Quaternion targetLeanRot;

    void Start()
    {
        if (rootTransform == null)
            rootTransform = transform.parent;
        if (rootTransform == null)
            rootTransform = transform;

        lastRootPosition = rootTransform.position;
        originalScale = transform.localScale;
        originalLocalRot = transform.localRotation;

        currentScale = originalScale;
        targetScale = originalScale;
        currentLeanRot = originalLocalRot;
        targetLeanRot = originalLocalRot;
    }

    void LateUpdate()
    {
        if (rootTransform == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0) return;

        Vector3 velocity = (rootTransform.position - lastRootPosition) / dt;
        lastRootPosition = rootTransform.position;

        float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        bool isGrounded = CheckGrounded();

        HandleMovementSquash(isGrounded, horizontalSpeed);
        HandleMovementLean(velocity, isGrounded);
        HandleWallTilt(isGrounded);

        ApplyTransforms(dt);
    }

    private bool CheckGrounded()
    {
        Vector3 origin = rootTransform.position + Vector3.up * 0.15f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance + 0.15f, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private void HandleMovementSquash(bool isGrounded, float speed)
    {
        if (!isGrounded || speed < 0.5f)
        {
            targetScale = originalScale;
            squashPhase = 0;
            return;
        }

        squashPhase += Time.deltaTime * squashFrequency;
        float sin = Mathf.Sin(squashPhase * Mathf.PI * 2f);

        float scaleY = originalScale.y * (1f - sin * squashAmount);
        float scaleXZ = originalScale.x * (1f + sin * squashAmount * 0.5f);
        targetScale = new Vector3(scaleXZ, scaleY, scaleXZ);
    }

    private void HandleMovementLean(Vector3 velocity, bool isGrounded)
    {
        if (!isGrounded || velocity.sqrMagnitude < 0.25f)
        {
            targetLeanRot = originalLocalRot;
            return;
        }

        Vector3 localVel = rootTransform.InverseTransformDirection(velocity);
        float normalizedZ = Mathf.Clamp(localVel.z / 10f, -1f, 1f);
        float normalizedX = Mathf.Clamp(localVel.x / 10f, -1f, 1f);

        float forwardLean = normalizedZ * leanAngle;
        float sideLean = normalizedX * leanAngle * 0.5f;

        targetLeanRot = originalLocalRot * Quaternion.Euler(forwardLean, 0, -sideLean);
    }

    private void HandleWallTilt(bool isGrounded)
    {
        if (isGrounded) return;

        Vector3 origin = rootTransform.position + Vector3.up * 0.5f;
        bool wallLeft = Physics.Raycast(origin, -rootTransform.right, wallCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);
        bool wallRight = Physics.Raycast(origin, rootTransform.right, wallCheckDistance, groundLayers, QueryTriggerInteraction.Ignore);

        if (wallLeft)
        {
            targetLeanRot = originalLocalRot * Quaternion.Euler(0, 0, -wallTiltAngle);
        }
        else if (wallRight)
        {
            targetLeanRot = originalLocalRot * Quaternion.Euler(0, 0, wallTiltAngle);
        }
    }

    private void ApplyTransforms(float dt)
    {
        currentScale = Vector3.Lerp(currentScale, targetScale, dt / squashSmoothing);
        transform.localScale = currentScale;

        currentLeanRot = Quaternion.Slerp(currentLeanRot, targetLeanRot, dt / leanSmoothing);
        transform.localRotation = currentLeanRot;
    }
}
