using UnityEngine;
using UnityEngine.UI;

public class SkillUI : MonoBehaviour
{
    [Header("References")]
    public Image cdRing;      // 外圈：Radial360 Fill，冷却时从 0→1 填满
    public Image iconDisc;    // 内圆：就绪/冷却/激活三种颜色状态

    [Header("Colors")]
    public Color ringCDColor    = new Color(0.25f, 0.55f, 1f);
    public Color ringReadyColor = new Color(0.3f,  1f,   0.55f);
    public Color ringActiveColor = new Color(1f,   0.9f,  0.3f);
    public Color discNormalColor = new Color(0.18f, 0.18f, 0.28f);
    public Color discReadyColor  = new Color(0.1f,  0.6f,  0.3f);
    public Color discActiveColor = new Color(0.55f, 0.45f, 0.05f);

    private bool  _subscribed;
    private float _pulse;

    private void Update()
    {
        if (!_subscribed && SkillManager.Instance != null)
        {
            SkillManager.Instance.onCooldownChanged.AddListener(OnCooldownChanged);
            SkillManager.Instance.onActivated.AddListener(OnActivated);
            SkillManager.Instance.onFired.AddListener(OnFired);
            _subscribed = true;
            OnCooldownChanged(SkillManager.Instance.CooldownRatio);
        }

        if (SkillManager.Instance == null) return;

        // 就绪或激活时脉动发光
        if (SkillManager.Instance.IsActive)
        {
            _pulse += Time.unscaledDeltaTime * 5f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            if (cdRing   != null) cdRing.color   = Color.Lerp(ringActiveColor, Color.white, g * 0.6f);
            if (iconDisc != null) iconDisc.color = Color.Lerp(discActiveColor, Color.white, g * 0.4f);
        }
        else if (SkillManager.Instance.IsReady)
        {
            _pulse += Time.unscaledDeltaTime * 2f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            if (cdRing   != null) cdRing.color   = Color.Lerp(ringReadyColor, Color.white, g * 0.5f);
            if (iconDisc != null) iconDisc.color = Color.Lerp(discReadyColor, Color.white, g * 0.3f);
        }
    }

    private void OnCooldownChanged(float ratio)   // ratio: 1=满CD, 0=就绪
    {
        _pulse = 0f;
        if (cdRing != null)
        {
            cdRing.fillAmount = 1f - ratio;       // 0=刚用完, 1=冷却满=就绪
            cdRing.color      = ratio <= 0f ? ringReadyColor : ringCDColor;
        }
        if (iconDisc != null)
            iconDisc.color = ratio <= 0f ? discReadyColor : discNormalColor;
    }

    private void OnActivated()
    {
        _pulse = 0f;
        if (cdRing   != null) { cdRing.fillAmount = 1f; cdRing.color   = ringActiveColor; }
        if (iconDisc != null) iconDisc.color = discActiveColor;
    }

    private void OnFired(Vector2 _)
    {
        _pulse = 0f;
        if (cdRing   != null) { cdRing.fillAmount = 0f; cdRing.color   = ringCDColor; }
        if (iconDisc != null) iconDisc.color = discNormalColor;
    }

    public void OnButtonClicked()
    {
        SkillManager.Instance?.TryActivate();
    }
}
