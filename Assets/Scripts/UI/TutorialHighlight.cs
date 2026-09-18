using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>教程控件高亮（uGUI）；不负责逻辑。</summary>
public class TutorialHighlight : MonoBehaviour
{
    public static TutorialHighlight Instance { get; private set; }

    private Outline _outline;
    private Coroutine _pulseCo;
    private Graphic _targetGraphic;

    public static TutorialHighlight Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TutorialHighlight");
        return go.AddComponent<TutorialHighlight>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Clear()
    {
        // Unity 销毁后引用是“假 null”，?. 拦不住；先用重载 == 再碰协程。
        TutorialHighlight self = this;
        if (self == null) return;

        if (_pulseCo != null)
        {
            StopCoroutine(_pulseCo);
            _pulseCo = null;
        }

        if (_outline != null)
        {
            Destroy(_outline);
            _outline = null;
        }

        _targetGraphic = null;
    }

    public void PulseSkillSlot(int slotIndex, float duration = 0.22f)
    {
        var skill = FindSkillUi(slotIndex);
        if (skill == null) return;
        PulseGraphic(skill.HighlightGraphic != null ? skill.HighlightGraphic : skill.GetComponent<Graphic>(), duration);
    }

    public void PulseCombo(float duration = 0.22f)
    {
        var combo = Object.FindAnyObjectByType<ComboDisplay>();
        if (combo == null || combo.comboText == null) return;
        PulseGraphic(combo.comboText, duration);
    }

    public void PulseFlipperWeaponHud(float duration = 0.22f)
    {
        var hud = Object.FindAnyObjectByType<FlipperWeaponHud>();
        if (hud == null) return;
        var g = hud.GetComponentInChildren<Text>();
        if (g != null) PulseGraphic(g, duration);
    }

    private static SkillUI FindSkillUi(int slotIndex)
    {
        var all = Object.FindObjectsByType<SkillUI>(FindObjectsSortMode.None);
        foreach (var s in all)
            if (s != null && s.slotIndex == slotIndex)
                return s;
        return null;
    }

    private void PulseGraphic(Graphic graphic, float duration)
    {
        Clear();
        if (graphic == null) return;

        _targetGraphic = graphic;
        _outline = graphic.gameObject.GetComponent<Outline>();
        if (_outline == null)
            _outline = graphic.gameObject.AddComponent<Outline>();

        _outline.effectColor = new Color(0f, 0.91f, 1f, 0.95f);
        _outline.effectDistance = new Vector2(3f, 3f);
        _outline.useGraphicAlpha = true;
        _pulseCo = StartCoroutine(PulseOutline(duration));
    }

    private IEnumerator PulseOutline(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (_outline != null)
            {
                float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 28f));
                _outline.effectColor = new Color(0f, 0.91f, 1f, a);
            }

            yield return null;
        }

        Clear();
    }
}
