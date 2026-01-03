using DG.Tweening;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Walk/Run Bounce")]
    public float walkBounceHeight = 0.03f;
    public float walkBounceFrequency = 8f;
    public float runBounceHeight = 0.06f;
    public float runBounceFrequency = 12f;

    [Header("Jump Stretch")]
    public float jumpStretchY = 1.15f;
    public float jumpSquashXZ = 0.9f;
    public float jumpAnimDuration = 0.12f;

    [Header("Land Squash")]
    public float landSquashY = 0.8f;
    public float landStretchXZ = 1.15f;
    public float landAnimDuration = 0.15f;

    [Header("Wall Run Tilt")]
    public float wallRunTiltAngle = 15f;
    public float tiltDuration = 0.2f;

    [Header("References")]
    public Transform visualTransform;
    public bool isLocalPlayer = false;

    private Vector3 originalScale;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private float bounceTimer = 0f;
    private Tweener currentScaleTween;
    private Tweener currentTiltTween;
    private bool wasGrounded = true;
    private bool wasWallRunning = false;

    void Awake()
    {
        if (visualTransform == null)
            visualTransform = transform;

        originalScale = visualTransform.localScale;
        originalLocalPosition = visualTransform.localPosition;
        originalLocalRotation = visualTransform.localRotation;
    }

    public void UpdateAnimations(bool isGrounded, bool isWallRunning, bool isWallLeft, float horizontalSpeed, bool isSprinting)
    {
        if (isLocalPlayer)
            return;

        if (!wasGrounded && isGrounded)
        {
            PlayLandSquash();
        }
        else if (wasGrounded && !isGrounded && !isWallRunning)
        {
            PlayJumpStretch();
        }

        if (!wasWallRunning && isWallRunning)
        {
            PlayWallRunTilt(isWallLeft);
        }
        else if (wasWallRunning && !isWallRunning)
        {
            ResetTilt();
        }

        if (isGrounded && horizontalSpeed > 0.5f)
        {
            PlayMovementBounce(isSprinting, horizontalSpeed);
        }
        else if (!isWallRunning)
        {
            ResetBounce();
        }

        wasGrounded = isGrounded;
        wasWallRunning = isWallRunning;
    }

    private void PlayMovementBounce(bool isSprinting, float speed)
    {
        float height = isSprinting ? runBounceHeight : walkBounceHeight;
        float freq = isSprinting ? runBounceFrequency : walkBounceFrequency;

        bounceTimer += Time.deltaTime * freq;
        float bounceOffset = Mathf.Sin(bounceTimer) * height;

        Vector3 pos = originalLocalPosition;
        pos.y += bounceOffset;
        visualTransform.localPosition = pos;
    }

    private void ResetBounce()
    {
        bounceTimer = 0f;
        visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, originalLocalPosition, Time.deltaTime * 10f);
    }

    public void PlayJumpStretch()
    {
        if (isLocalPlayer) return;

        currentScaleTween?.Kill();

        Vector3 stretchScale = new Vector3(
            originalScale.x * jumpSquashXZ,
            originalScale.y * jumpStretchY,
            originalScale.z * jumpSquashXZ
        );

        currentScaleTween = visualTransform.DOScale(stretchScale, jumpAnimDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                currentScaleTween = visualTransform.DOScale(originalScale, jumpAnimDuration * 0.5f)
                    .SetEase(Ease.InQuad);
            });
    }

    public void PlayLandSquash()
    {
        if (isLocalPlayer) return;

        currentScaleTween?.Kill();

        Vector3 squashScale = new Vector3(
            originalScale.x * landStretchXZ,
            originalScale.y * landSquashY,
            originalScale.z * landStretchXZ
        );

        currentScaleTween = visualTransform.DOScale(squashScale, landAnimDuration * 0.4f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                currentScaleTween = visualTransform.DOScale(originalScale, landAnimDuration * 0.6f)
                    .SetEase(Ease.OutBounce);
            });
    }

    public void PlayWallRunTilt(bool isWallLeft)
    {
        if (isLocalPlayer) return;

        currentTiltTween?.Kill();

        float targetAngle = isWallLeft ? -wallRunTiltAngle : wallRunTiltAngle;
        Vector3 targetEuler = originalLocalRotation.eulerAngles;
        targetEuler.z = targetAngle;

        currentTiltTween = visualTransform.DOLocalRotate(targetEuler, tiltDuration).SetEase(Ease.OutQuad);
    }

    public void ResetTilt()
    {
        if (isLocalPlayer) return;

        currentTiltTween?.Kill();
        currentTiltTween = visualTransform.DOLocalRotateQuaternion(originalLocalRotation, tiltDuration).SetEase(Ease.OutQuad);
    }

    void OnDestroy()
    {
        currentScaleTween?.Kill();
        currentTiltTween?.Kill();
    }
}
