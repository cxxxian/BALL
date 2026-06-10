using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ComboDisplay : MonoBehaviour
{
    [Header("References")]
    public Text comboText;
    public Text labelText;

    [Header("Colors")]
    public Color normalColor = new Color(1f, 0.88f, 0.18f);
    public Color flashColor  = Color.white;
    public Color labelColor  = new Color(0.85f, 0.65f, 1f);

    private RectTransform _rt;
    private Coroutine _punchCoroutine;
    private int _threshold = 3;
    private bool _subscribed;
    private bool _visible;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rt.localScale = Vector3.one;
        RemoveLegacyBorder();
        SetVisible(false);
    }

    private void RemoveLegacyBorder()
    {
        var legacy = transform.Find("MilestoneBorder");
        if (legacy != null)
            Destroy(legacy.gameObject);
    }

    private void Update()
    {
        if (_subscribed) return;
        if (ComboSystem.Instance == null || GameManager.Instance == null) return;
        int baseThreshold = GameManager.Instance.config != null
            ? GameManager.Instance.config.comboDisplayThreshold : 3;
        _threshold = ComboSystem.GetEffectiveThreshold(baseThreshold);
        ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
        ComboSystem.Instance.onComboMilestone.AddListener(OnComboMilestone);
        GameManager.Instance.onGameStart.AddListener(OnGameStart);
        _subscribed = true;
    }

    private void SetVisible(bool show)
    {
        _visible = show;
        if (comboText != null) comboText.enabled = show;
        if (labelText != null) labelText.enabled = show;
    }

    private void OnGameStart()
    {
        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _rt.localScale = Vector3.one;
        SetVisible(false);
    }

    private void OnComboMilestone(int combo)
    {
        if (combo < _threshold) return;
        if (comboText != null) comboText.text = "x" + combo;
        SetVisible(true);

        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(Punch(0.55f, 0.42f));
    }

    private void OnComboChanged(int combo)
    {
        if (GameManager.Instance != null)
        {
            int baseThreshold = GameManager.Instance.config != null
                ? GameManager.Instance.config.comboDisplayThreshold : 3;
            _threshold = ComboSystem.GetEffectiveThreshold(baseThreshold);
        }

        if (combo < _threshold)
        {
            if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
            if (_visible) _punchCoroutine = StartCoroutine(FadeOut());
            return;
        }

        if (IsMilestoneCombo(combo)) return;

        if (comboText != null) comboText.text = "x" + combo;
        SetVisible(true);

        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(Punch(0.22f, 0.28f));
    }

    private static bool IsMilestoneCombo(int combo)
    {
        int shake = ComboSystem.GetEffectiveThreshold(ComboSystem.BaseShakeThreshold);
        int heavy = ComboSystem.GetEffectiveThreshold(ComboSystem.BaseHeavyShakeThreshold);
        if (combo == shake || combo == heavy) return true;
        return combo > heavy && (combo - heavy) % 5 == 0;
    }

    private IEnumerator Punch(float scaleAmp, float dur)
    {
        if (comboText != null) comboText.color = flashColor;
        if (labelText != null) labelText.color = flashColor;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float spring = Mathf.Sin(p * Mathf.PI) * (1f - p * p);
            _rt.localScale = Vector3.one * (1f + spring * scaleAmp);

            float colorT = Mathf.Clamp01(p / 0.3f);
            if (comboText != null) comboText.color = Color.Lerp(flashColor, normalColor, colorT);
            if (labelText != null) labelText.color = Color.Lerp(flashColor, labelColor, colorT);

            yield return null;
        }
        _rt.localScale = Vector3.one;
        if (comboText != null) comboText.color = normalColor;
        if (labelText != null) labelText.color = labelColor;
    }

    private IEnumerator FadeOut()
    {
        float dur = 0.25f;
        float t = 0f;
        Color numStart = comboText != null ? comboText.color : normalColor;
        Color lblStart = labelText != null ? labelText.color : labelColor;
        Vector3 scStart = _rt.localScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (comboText != null) comboText.color = Color.Lerp(numStart, new Color(numStart.r, numStart.g, numStart.b, 0f), p);
            if (labelText != null) labelText.color = Color.Lerp(lblStart, new Color(lblStart.r, lblStart.g, lblStart.b, 0f), p);
            _rt.localScale = Vector3.Lerp(scStart, Vector3.one * 0.75f, p);
            yield return null;
        }
        SetVisible(false);
        _rt.localScale = Vector3.one;
        if (comboText != null) comboText.color = normalColor;
        if (labelText != null) labelText.color = labelColor;
    }
}
