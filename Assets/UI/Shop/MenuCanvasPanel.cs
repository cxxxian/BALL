using DG.Tweening;
using UnityEngine;

/// <summary>Visibility only: authored RectTransforms are never repositioned at runtime.</summary>
public abstract class MenuCanvasPanel : MonoBehaviour
{
    [SerializeField] protected Canvas canvas;
    [SerializeField] protected CanvasGroup visibility;
    private Tween fade;
    public bool Visible => canvas != null && canvas.enabled;
    protected virtual void Awake() => Hide();
    public virtual void Show()
    {
        RefreshPresentation();
        canvas.enabled = true;
        visibility.interactable = visibility.blocksRaycasts = true;
        fade?.Kill(); visibility.alpha = 0;
        fade = visibility.DOFade(1, 0.18f).SetUpdate(true);
    }
    public void Hide()
    {
        fade?.Kill();
        if (canvas != null) canvas.enabled = false;
        if (visibility != null) visibility.interactable = visibility.blocksRaycasts = false;
    }
    public abstract void RefreshPresentation();
    protected virtual void OnDestroy() => fade?.Kill();
}
