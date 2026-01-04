using DG.Tweening;
using UnityEngine;

public class BallAnimator : MonoBehaviour
{
    [Header("References")]
    public Transform visualChild;
    public Renderer ballRenderer;

    [Header("Velocity Stretch (Oliver y Benji Style)")]
    public float maxStretch = 1.5f;
    public float stretchSpeed = 15f;
    public float stretchSmoothing = 0.1f;

    [Header("Squash & Stretch")]
    public float squashAmount = 0.4f;
    public float squashDuration = 0.1f;
    public Ease squashEase = Ease.OutQuad;

    [Header("Hot State Pulse")]
    public float pulseScale = 1.15f;
    public float pulseDuration = 0.5f;
    public Color hotColor = new Color(1f, 0.3f, 0.1f);
    public float hotEmissionIntensity = 2f;

    [Header("Cold State")]
    public Color coldColor = Color.white;

    [Header("Launch Animation")]
    public float launchSqueezeScale = 0.7f;
    public float launchDuration = 0.08f;

    [Header("State Transition")]
    public float stateTransitionDuration = 0.3f;

    private Ball ball;
    private Rigidbody rb;
    private Vector3 originalScale;
    private Vector3 currentStretchScale;

    private Tween pulseTween;
    private Tween squashTween;
    private Tween colorTween;
    private Tween stateTween;

    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private bool isHot = false;

    void Awake()
    {
        ball = GetComponent<Ball>();
        rb = GetComponent<Rigidbody>();

        if (visualChild == null)
            visualChild = transform.GetChild(0);

        if (ballRenderer == null && visualChild != null)
            ballRenderer = visualChild.GetComponent<Renderer>();

        if (visualChild != null)
            originalScale = visualChild.localScale;

        currentStretchScale = Vector3.one;
        propertyBlock = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (visualChild == null || rb == null) return;

        HandleVelocityStretch();
    }

    private void HandleVelocityStretch()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed > 0.5f)
        {
            float stretchFactor = Mathf.Lerp(1f, maxStretch, speed / stretchSpeed);
            float compressFactor = 1f / Mathf.Sqrt(stretchFactor);

            Vector3 targetStretch = new Vector3(compressFactor, compressFactor, stretchFactor);
            currentStretchScale = Vector3.Lerp(currentStretchScale, targetStretch, Time.deltaTime / stretchSmoothing);
        }
        else
        {
            currentStretchScale = Vector3.Lerp(currentStretchScale, Vector3.one, Time.deltaTime / stretchSmoothing);
        }

        visualChild.localScale = Vector3.Scale(originalScale, currentStretchScale);
    }



    public void OnLaunch()
    {
        squashTween?.Kill();

        Vector3 squeezeScale = new Vector3(
            originalScale.x * (1f + (1f - launchSqueezeScale) * 0.5f),
            originalScale.y * (1f + (1f - launchSqueezeScale) * 0.5f),
            originalScale.z * launchSqueezeScale
        );

        visualChild.localScale = squeezeScale;
        squashTween = visualChild
            .DOScale(originalScale, launchDuration * 2f)
            .SetEase(Ease.OutElastic);

        OnStateChange(Ball.BallState.Hot);
    }

    public void OnBounce(Vector3 contactNormal)
    {
        squashTween?.Kill();

        Quaternion impactRotation = Quaternion.LookRotation(-contactNormal);
        Vector3 squashScale = new Vector3(
            originalScale.x * (1f + squashAmount * 0.5f),
            originalScale.y * (1f + squashAmount * 0.5f),
            originalScale.z * (1f - squashAmount)
        );

        Sequence bounceSequence = DOTween.Sequence();

        bounceSequence.Append(
            visualChild.DOScale(squashScale, squashDuration * 0.3f).SetEase(Ease.OutQuad)
        );

        bounceSequence.Append(
            visualChild.DOScale(originalScale, squashDuration * 0.7f).SetEase(Ease.OutElastic)
        );

        squashTween = bounceSequence;
    }

    public void OnStateChange(Ball.BallState newState)
    {
        bool wasHot = isHot;
        isHot = (newState == Ball.BallState.Hot);

        if (isHot == wasHot && pulseTween != null) return;

        pulseTween?.Kill();
        colorTween?.Kill();
        stateTween?.Kill();

        if (isHot)
        {
            StartHotState();
        }
        else
        {
            StartColdState();
        }
    }

    private void StartHotState()
    {
        if (ballRenderer == null) return;

        colorTween = DOTween.To(
            () => 0f,
            (t) => {
                ballRenderer.GetPropertyBlock(propertyBlock);
                Color currentColor = Color.Lerp(coldColor, hotColor, t);
                propertyBlock.SetColor(BaseColorId, currentColor);
                propertyBlock.SetColor(EmissionColorId, hotColor * hotEmissionIntensity * t);
                ballRenderer.SetPropertyBlock(propertyBlock);
            },
            1f,
            stateTransitionDuration
        ).SetEase(Ease.OutQuad);

        pulseTween = visualChild
            .DOScale(originalScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StartColdState()
    {
        stateTween = visualChild
            .DOScale(originalScale, stateTransitionDuration)
            .SetEase(Ease.OutQuad);

        if (ballRenderer == null) return;

        colorTween = DOTween.To(
            () => 1f,
            (t) => {
                ballRenderer.GetPropertyBlock(propertyBlock);
                Color currentColor = Color.Lerp(coldColor, hotColor, t);
                propertyBlock.SetColor(BaseColorId, currentColor);
                propertyBlock.SetColor(EmissionColorId, hotColor * hotEmissionIntensity * t);
                ballRenderer.SetPropertyBlock(propertyBlock);
            },
            0f,
            stateTransitionDuration
        ).SetEase(Ease.OutQuad);
    }

    public void OnEquip()
    {
        pulseTween?.Kill();
        squashTween?.Kill();

        currentStretchScale = Vector3.one;
        visualChild.localScale = originalScale;
    }

    void OnDisable()
    {
        pulseTween?.Kill();
        squashTween?.Kill();
        colorTween?.Kill();
        stateTween?.Kill();
    }
}
