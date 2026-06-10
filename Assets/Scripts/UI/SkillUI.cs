using UnityEngine;
using UnityEngine.UI;

public class SkillUI : MonoBehaviour
{
    [Header("槽位索引（0=斩击, 1=护盾）")]
    public int slotIndex = 0;

    [Header("References")]
    public Image cdRing;
    public Image iconDisc;

    [Header("Colors")]
    public Color ringCDColor;
    public Color ringReadyColor;
    public Color ringActiveColor = new Color(1f,    0.9f,  0.3f);
    public Color discNormalColor = new Color(0.18f, 0.18f, 0.28f);
    public Color discReadyColor  = new Color(0.1f,  0.6f,  0.3f);
    public Color discActiveColor = new Color(0.55f, 0.45f, 0.05f);

    private bool  _subscribed;
    private bool  _gameEventsSubscribed;
    private float _pulse;
    private bool  _isEffectActive;

    private void Awake()
    {
        ringCDColor    = NeonUiColors.BumperCyanUiDim();
        ringReadyColor = NeonUiColors.BumperCyanUi();
        if (slotIndex == 1)
            ringActiveColor = NeonUiColors.BumperCyanUi(1.25f);
    }

    private void Update()
    {
        if (!_subscribed && SkillManager.Instance != null)
        {
            SkillManager.Instance.onSlotCooldownChanged.AddListener(OnSlotCooldownChanged);
            SkillManager.Instance.onSlotActivated.AddListener(OnSlotActivated);
            SkillManager.Instance.onFired.AddListener(OnFired);
            _subscribed = true;

            if (slotIndex < SkillManager.Instance.slots.Length)
                OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
        }

        if (GameManager.Instance != null && !_gameEventsSubscribed)
        {
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
            GameManager.Instance.onGameOver.AddListener(OnBallLost);
            _gameEventsSubscribed = true;
        }

        if (SkillManager.Instance == null) return;

        if (slotIndex == 1)
        {
            bool shieldNow = BlockShield.Instance != null && BlockShield.Instance.IsActive;
            if (shieldNow != _isEffectActive)
            {
                _isEffectActive = shieldNow;
                if (!_isEffectActive)
                {
                    _pulse = 0f;
                    if (slotIndex < SkillManager.Instance.slots.Length)
                        OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
                }
            }
        }

        if (_isEffectActive)
        {
            _pulse += Time.unscaledDeltaTime * 5f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            if (cdRing   != null) cdRing.color   = Color.Lerp(ringActiveColor, Color.white, g * 0.6f);
            if (iconDisc != null) iconDisc.color = Color.Lerp(discActiveColor, Color.white, g * 0.4f);
        }
        else if (IsSlotReady())
        {
            _pulse += Time.unscaledDeltaTime * 2f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            if (cdRing   != null) cdRing.color   = Color.Lerp(ringReadyColor, NeonUiColors.ScoreUi(), g * 0.35f);
            if (iconDisc != null) iconDisc.color = Color.Lerp(discReadyColor, Color.white, g * 0.3f);
        }
    }

    private bool IsSlotReady()
    {
        if (SkillManager.Instance == null) return false;
        if (slotIndex >= SkillManager.Instance.slots.Length) return false;
        return SkillManager.Instance.slots[slotIndex].IsReady;
    }

    private void OnSlotCooldownChanged(int idx, float ratio)
    {
        if (idx != slotIndex) return;
        _pulse = 0f;
        if (cdRing != null)
        {
            cdRing.fillAmount = 1f - ratio;
            cdRing.color      = ratio <= 0f ? ringReadyColor : ringCDColor;
        }
        if (iconDisc != null)
            iconDisc.color = ratio <= 0f ? discReadyColor : discNormalColor;
    }

    private void OnSlotActivated(int idx)
    {
        if (idx != slotIndex) return;
        _pulse          = 0f;
        _isEffectActive = true;
        if (cdRing   != null) { cdRing.fillAmount = 1f; cdRing.color = ringActiveColor; }
        if (iconDisc != null) iconDisc.color = discActiveColor;
    }

    private void OnFired(Vector2 _)
    {
        if (slotIndex != 0) return;
        ResetSlashVisual();
    }

    private void OnBallLost()
    {
        if (slotIndex != 0) return;
        ResetSlashVisual();
    }

    private void ResetSlashVisual()
    {
        _isEffectActive = false;
        _pulse          = 0f;
        if (SkillManager.Instance == null || slotIndex >= SkillManager.Instance.slots.Length) return;
        OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
    }

    public void OnButtonClicked()
    {
        SkillManager.Instance?.TryActivate(slotIndex);
    }
}
