using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 底部 Action HUD：左武器条 + 右双技能芯片，并让出全面屏安全区。
/// 只排版，不改技能/武器逻辑。
/// </summary>
public class ActionHudLayout : MonoBehaviour
{
    public static ActionHudLayout Instance { get; private set; }

    private readonly List<SkillUI> _slots = new List<SkillUI>(2);
    private RectTransform _bar;
    private RectTransform _weaponSlot;
    private RectTransform _skillRow;
    private Canvas _canvas;

    public static ActionHudLayout Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ActionHudLayout");
        return go.AddComponent<ActionHudLayout>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _canvas = FindHudCanvas();
        if (_canvas == null) return;

        var barGo = new GameObject("ActionHudBar");
        barGo.transform.SetParent(_canvas.transform, false);
        _bar = barGo.AddComponent<RectTransform>();
        _bar.anchorMin = new Vector2(0f, 0f);
        _bar.anchorMax = new Vector2(1f, 0f);
        _bar.pivot = new Vector2(0.5f, 0f);
        _bar.sizeDelta = new Vector2(0f, 96f);

        var line = new GameObject("Hairline");
        line.transform.SetParent(_bar, false);
        var lineRt = line.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.04f, 1f);
        lineRt.anchorMax = new Vector2(0.96f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.sizeDelta = new Vector2(0f, 1f);
        var lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(0f, 0.91f, 1f, 0.28f);
        lineImg.raycastTarget = false;

        var weaponGo = new GameObject("WeaponAnchor");
        weaponGo.transform.SetParent(_bar, false);
        _weaponSlot = weaponGo.AddComponent<RectTransform>();
        _weaponSlot.anchorMin = new Vector2(0f, 0f);
        _weaponSlot.anchorMax = new Vector2(0.42f, 1f);
        _weaponSlot.offsetMin = new Vector2(16f, 8f);
        _weaponSlot.offsetMax = new Vector2(-8f, -10f);

        var skillGo = new GameObject("SkillRow");
        skillGo.transform.SetParent(_bar, false);
        _skillRow = skillGo.AddComponent<RectTransform>();
        // 略靠右、与左武器条呼应，但不顶到场墙（介于居中与贴右之间）
        _skillRow.anchorMin = new Vector2(0.50f, 0f);
        _skillRow.anchorMax = new Vector2(0.96f, 1f);
        _skillRow.offsetMin = new Vector2(4f, 4f);
        _skillRow.offsetMax = new Vector2(-8f, -6f);

        var layout = skillGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        HideLegacyFlipperKeyHint();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        ApplySafeArea();
    }

    public void Register(SkillUI slot)
    {
        if (slot == null || _slots.Contains(slot)) return;
        _slots.Add(slot);
        _slots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        Apply();
    }

    private void Start()
    {
        Apply();
    }

    /// <summary>移除底部 Z/X 挡板键位条（手游不需要，且与 Action HUD 抢位）。</summary>
    private static void HideLegacyFlipperKeyHint()
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (t.name == "ControlHint")
            {
                t.gameObject.SetActive(false);
                continue;
            }

            var text = t.GetComponent<Text>();
            if (text == null || string.IsNullOrEmpty(text.text)) continue;
            if (text.text.IndexOf("LEFT", System.StringComparison.OrdinalIgnoreCase) >= 0
                && text.text.IndexOf("RIGHT", System.StringComparison.OrdinalIgnoreCase) >= 0
                && text.text.IndexOf('Z') >= 0)
                t.gameObject.SetActive(false);
        }
    }

    private void Apply()
    {
        if (_bar == null) return;

        var weapon = GameObject.Find("FlipperWeaponPanel");
        if (weapon != null)
        {
            var rt = weapon.GetComponent<RectTransform>();
            rt.SetParent(_weaponSlot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            var rt = _slots[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.SetParent(_skillRow, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(118f, 88f);
            var le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 118f;
            le.preferredHeight = 88f;
        }

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (_bar == null || _canvas == null) return;
        var canvasRt = _canvas.GetComponent<RectTransform>();
        if (canvasRt == null) return;

        float canvasH = canvasRt.rect.height;
        if (canvasH < 1f || Screen.height < 1) return;

        float bottom = Screen.safeArea.yMin / Screen.height * canvasH;
        bottom = Mathf.Max(8f, bottom);
        _bar.anchoredPosition = new Vector2(0f, bottom);
    }

    private static Canvas FindHudCanvas()
    {
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (var c in canvases)
        {
            if (c == null || !c.isRootCanvas) continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == "Canvas")
                return c;
            if (fallback == null && c.renderMode == RenderMode.ScreenSpaceOverlay)
                fallback = c;
        }
        return fallback;
    }
}
