using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ComboDisplay : MonoBehaviour
{
    [Header("References")]
    public Text comboText;
    public Text labelText;

    private RectTransform _rt;
    private Coroutine _punchCoroutine;
    private int _threshold = 3;
    private bool _subscribed;
    private bool _visible;

    private Color NormalFace => ProtocolUiStyle.IceFace;
    private Color HotFace => ProtocolUiStyle.AmberFace;
    private Color FlashFace => Color.white;
    private Color LabelFace => ProtocolUiStyle.CyanFace;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rt.localScale = Vector3.one;
        RemoveLegacyBorder();
        ApplyProtocolStyle();
        SetVisible(false);
    }

    private void ApplyProtocolStyle()
    {
        ProtocolUiStyle.ApplyDisplayFont(comboText, 42);
        ProtocolUiStyle.ApplyDisplayFont(labelText, 14);
        if (labelText != null)
            labelText.text = "COMBO";

        ProtocolUiStyle.ApplyValueFace(comboText, NormalFace, ProtocolUiStyle.CyanFace, 1.8f);
        ProtocolUiStyle.ApplyKeyLabel(labelText, 0.85f);
        CyberHudGlow.Ensure(comboText, CyberHudGlow.GlowStyle.WhiteScore);
        CyberHudGlow.Ensure(labelText, CyberHudGlow.GlowStyle.KeyLabel);
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
        SetComboText(combo);
        SetVisible(true);

        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(Punch(0.48f, 0.36f, hot: true));
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

        SetComboText(combo);
        SetVisible(true);

        if (_punchCoroutine != null) StopCoroutine(_punchCoroutine);
        _punchCoroutine = StartCoroutine(Punch(0.22f, 0.24f, hot: false));
    }

    private void SetComboText(int combo)
    {
        if (comboText != null)
            comboText.text = "x" + combo;
    }

    private static bool IsMilestoneCombo(int combo)
    {
        int shake = ComboSystem.GetShakeThreshold(ComboSystem.BaseShakeThreshold);
        int heavy = ComboSystem.GetShakeThreshold(ComboSystem.BaseHeavyShakeThreshold);
        if (combo == shake || combo == heavy) return true;
        return combo > heavy && (combo - heavy) % 5 == 0;
    }

    private IEnumerator Punch(float scaleAmp, float dur, bool hot)
    {
        Color rest = hot ? HotFace : NormalFace;
        if (hot)
            CyberHudGlow.Ensure(comboText, CyberHudGlow.GlowStyle.ComboAmber);
        else
            CyberHudGlow.Ensure(comboText, CyberHudGlow.GlowStyle.WhiteScore);

        if (comboText != null) comboText.color = FlashFace;
        if (labelText != null) labelText.color = FlashFace;

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float spring = Mathf.Sin(p * Mathf.PI) * (1f - p * p);
            _rt.localScale = Vector3.one * (1f + spring * scaleAmp);

            float colorT = Mathf.Clamp01(p / 0.35f);
            if (comboText != null) comboText.color = Color.Lerp(FlashFace, rest, colorT);
            if (labelText != null) labelText.color = Color.Lerp(FlashFace, LabelFace, colorT);

            yield return null;
        }
        _rt.localScale = Vector3.one;
        if (comboText != null) comboText.color = rest;
        if (labelText != null) labelText.color = LabelFace;
    }

    private IEnumerator FadeOut()
    {
        float dur = 0.22f;
        float t = 0f;
        Color numStart = comboText != null ? comboText.color : NormalFace;
        Color lblStart = labelText != null ? labelText.color : LabelFace;
        Vector3 scStart = _rt.localScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (comboText != null) comboText.color = Color.Lerp(numStart, new Color(numStart.r, numStart.g, numStart.b, 0f), p);
            if (labelText != null) labelText.color = Color.Lerp(lblStart, new Color(lblStart.r, lblStart.g, lblStart.b, 0f), p);
            _rt.localScale = Vector3.Lerp(scStart, Vector3.one * 0.82f, p);
            yield return null;
        }
        SetVisible(false);
        _rt.localScale = Vector3.one;
        if (comboText != null) comboText.color = NormalFace;
        if (labelText != null) labelText.color = LabelFace;
        CyberHudGlow.Ensure(comboText, CyberHudGlow.GlowStyle.WhiteScore);
    }
}
