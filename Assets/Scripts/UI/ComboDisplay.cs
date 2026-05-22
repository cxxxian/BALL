using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ComboDisplay : MonoBehaviour
{
    [Header("References")]
    public Text comboText;   // 大数字：x3 x12
    public Text labelText;   // 小标签：COMBO

    [Header("Colors")]
    public Color normalColor = new Color(1f, 0.88f, 0.18f);
    public Color flashColor  = Color.white;
    public Color labelColor  = new Color(0.85f, 0.65f, 1f);

    private RectTransform _rt;
    private Coroutine     _punchCoroutine;
    private int           _threshold = 3;
    private bool          _subscribed = false;
    private bool          _visible = false;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rt.localScale = Vector3.one;
        SetVisible(false);
    }

    private void Update()
    {
        if (_subscribed) return;
        if (ComboSystem.Instance == null || GameManager.Instance == null) return;
        _threshold = GameManager.Instance.config != null
            ? GameManager.Instance.config.comboDisplayThreshold : 3;
        ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
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

    private void OnComboChanged(int combo)
    {
        if (combo < _threshold)
        {
            if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
            if (_visible) _punchCoroutine = StartCoroutine(FadeOut());
            return;
        }

        if (comboText != null) comboText.text = "x" + combo;
        SetVisible(true);

        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(Punch());
    }

    private IEnumerator Punch()
    {
        // 瞬间闪白
        if (comboText != null) comboText.color = flashColor;
        if (labelText != null) labelText.color = flashColor;

        // Scale punch：sin(p*π)*(1-p²)，永不 NaN
        float dur = 0.30f;
        float t   = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p      = Mathf.Clamp01(t / dur);
            float spring = Mathf.Sin(p * Mathf.PI) * (1f - p * p);
            _rt.localScale = Vector3.one * (1f + spring * 0.38f);

            float colorT = Mathf.Clamp01(p / 0.3f);
            if (comboText != null) comboText.color = Color.Lerp(flashColor, normalColor, colorT);
            if (labelText != null) labelText.color = Color.Lerp(flashColor, labelColor,  colorT);

            yield return null;
        }
        _rt.localScale = Vector3.one;
        if (comboText != null) comboText.color = normalColor;
        if (labelText != null) labelText.color = labelColor;
    }

    private IEnumerator FadeOut()
    {
        float dur        = 0.25f;
        float t          = 0f;
        Color numStart   = comboText != null ? comboText.color : normalColor;
        Color lblStart   = labelText != null ? labelText.color : labelColor;
        Vector3 scStart  = _rt.localScale;

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
