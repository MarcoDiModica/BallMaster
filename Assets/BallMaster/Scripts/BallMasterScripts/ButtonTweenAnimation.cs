using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class ButtonTweenAnimation
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
{
    [Header("Scale Settings")]
    public float hoverScale = 1.1f;
    public float pressedScale = 0.95f;
    public float animationDuration = 0.15f;
    public Ease scaleEase = Ease.OutBack;

    private Vector3 originalScale;
    private Tween currentTween;
    private bool isSelected = false;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    void OnEnable()
    {
        transform.localScale = originalScale;
    }

    void OnDisable()
    {
        currentTween?.Kill();
        transform.localScale = originalScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateScale(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isSelected)
        {
            AnimateScale(1f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateScale(pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateScale(hoverScale);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        AnimateScale(hoverScale);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        AnimateScale(1f);
    }

    private void AnimateScale(float targetScale)
    {
        currentTween?.Kill();
        currentTween = transform
            .DOScale(originalScale * targetScale, animationDuration)
            .SetEase(scaleEase)
            .SetUpdate(true);
    }
}
