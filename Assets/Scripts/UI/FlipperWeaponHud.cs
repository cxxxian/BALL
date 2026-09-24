using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挡板武器分段条（左下）：HDR 面色提亮，外描边仅作细边（避免 UI Outline 假 Bloom）。
/// </summary>
public class FlipperWeaponHud : MonoBehaviour
{
    private const int SegmentCount = 20;

    // 面色 >1 供后期 Bloom 拾取；外发光用极细、低透明度描边
    private static readonly Color ChargeFace = new Color(1.08f, 1.28f, 1.45f, 1f);
    private static readonly Color ChargeEdge = new Color(0.35f, 0.95f, 1.15f, 0.22f);
    private static readonly Color EmptyFace = new Color(0.14f, 0.18f, 0.24f, 0.5f);
    private static readonly Color EmptyEdge = new Color(0.1f, 0.28f, 0.38f, 0.18f);
    private static readonly Color ReadyFace = new Color(1.35f, 0.52f, 1.18f, 1f);
    private static readonly Color ReadyEdge = new Color(1.1f, 0.35f, 0.95f, 0.28f);

    private struct SegmentVisual
    {
        public Image Image;
        public Outline Outline;
    }

    private SegmentVisual[] _segments;
    private Text _labelText;
    private Text _readyText;
    private RectTransform _root;
    private CanvasGroup _group;
    private Coroutine _readyPulse;
    private bool _isReady;
    private float _energyRatio;

    private static Sprite _whiteSprite;

    private void Awake()
    {
        BuildUi();
        ApplySegmentVisuals(0, false, 0f);
        SetReadyVisible(false);
    }

    private void Start()
    {
        if (FlipperWeaponController.Instance != null)
            Bind(FlipperWeaponController.Instance);
        else
            StartCoroutine(WaitAndBind());
    }

    private IEnumerator WaitAndBind()
    {
        while (FlipperWeaponController.Instance == null)
            yield return null;
        Bind(FlipperWeaponController.Instance);
    }

    private void Bind(FlipperWeaponController ctrl)
    {
        ctrl.onEnergyChanged.AddListener(OnEnergyChanged);
        ctrl.onWeaponReady.AddListener(OnReady);
        ctrl.onWeaponFired.AddListener(OnFired);
        ctrl.onWeaponEquipped.AddListener(UpdateWeaponLabel);
        UpdateWeaponLabel(ctrl.EquippedWeapon);
        OnEnergyChanged(ctrl.EnergyRatio);
    }

    private void OnEnergyChanged(float ratio)
    {
        _energyRatio = Mathf.Clamp01(ratio);

        // A new run resets the controller's energy without firing OnFired.
        // Clear the HUD's cached ready state when that reset reaches zero.
        if (_energyRatio <= 0f && _isReady)
        {
            _isReady = false;
            if (_readyPulse != null)
            {
                StopCoroutine(_readyPulse);
                _readyPulse = null;
            }
            SetReadyVisible(false);
        }

        if (!_isReady)
            ApplySegmentVisuals(Mathf.FloorToInt(_energyRatio * SegmentCount), false, 0f);

        if (_group != null)
            _group.alpha = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(_energyRatio * 1.35f));
    }

    private void OnReady()
    {
        _isReady = true;
        SetReadyVisible(true);
        if (_readyPulse != null) StopCoroutine(_readyPulse);
        _readyPulse = StartCoroutine(PulseReady());
    }

    private void OnFired()
    {
        _isReady = false;
        _energyRatio = 0f;
        if (_readyPulse != null) StopCoroutine(_readyPulse);
        SetReadyVisible(false);
        ApplySegmentVisuals(0, false, 0f);
        if (_group != null) _group.alpha = 0.45f;
    }

    private void UpdateWeaponLabel(FlipperWeaponDefinition weapon)
    {
        if (_labelText == null) return;
        _labelText.text = GetWeaponHudLabel(weapon);
    }

    private static string GetWeaponHudLabel(FlipperWeaponDefinition weapon)
    {
        if (weapon == null) return "挡板武器";
        return weapon.weaponId switch
        {
            "cannon" => "重炮",
            "bomb"   => "炸弹",
            "laser"  => "激光",
            _        => string.IsNullOrEmpty(weapon.displayName) ? "挡板武器" : weapon.displayName
        };
    }

    private void SetReadyVisible(bool show)
    {
        if (_readyText == null) return;
        _readyText.enabled = show;
        if (!show) return;

        _readyText.color = ReadyFace;
        var outline = _readyText.GetComponent<Outline>();
        if (outline == null) outline = _readyText.gameObject.AddComponent<Outline>();
        outline.effectColor = ReadyEdge;
        outline.effectDistance = new Vector2(0.55f, -0.55f);
        outline.useGraphicAlpha = true;
    }

    private void ApplySegmentVisuals(int litCount, bool ready, float pulse01)
    {
        if (_segments == null) return;
        litCount = Mathf.Clamp(litCount, 0, SegmentCount);

        for (int i = 0; i < SegmentCount; i++)
        {
            bool lit = i < litCount;
            var seg = _segments[i];
            if (seg.Image == null) continue;

            Color face;
            Color edge;
            float spread = 0.38f;

            if (!lit)
            {
                face = EmptyFace;
                edge = EmptyEdge;
            }
            else if (ready)
            {
                // 仅亮度闪烁，不放大、不加粗描边
                float bright = 1f + pulse01 * 0.35f;
                face = new Color(ReadyFace.r * bright, ReadyFace.g * bright, ReadyFace.b * bright, 1f);
                edge = ReadyEdge;
            }
            else
            {
                face = ChargeFace;
                edge = ChargeEdge;
            }

            seg.Image.color = face;
            if (seg.Outline != null)
            {
                seg.Outline.effectColor = edge;
                seg.Outline.effectDistance = new Vector2(spread, -spread);
            }
        }
    }

    private IEnumerator PulseReady()
    {
        while (true)
        {
            float g = (Mathf.Sin(Time.unscaledTime * 6.5f) + 1f) * 0.5f;
            ApplySegmentVisuals(SegmentCount, true, g);

            if (_readyText != null)
            {
                float bright = 1f + g * 0.28f;
                _readyText.color = new Color(ReadyFace.r * bright, ReadyFace.g * bright, ReadyFace.b * bright, 1f);
            }

            yield return null;
        }
    }

    private void BuildUi()
    {
        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        var panel = new GameObject("FlipperWeaponPanel");
        panel.transform.SetParent(canvas.transform, false);
        _root = panel.AddComponent<RectTransform>();
        _root.anchorMin = new Vector2(0f, 0f);
        _root.anchorMax = new Vector2(0f, 0f);
        _root.pivot = new Vector2(0f, 0f);
        _root.anchoredPosition = new Vector2(18f, 22f);
        _root.sizeDelta = new Vector2(172f, 42f);

        _group = panel.AddComponent<CanvasGroup>();
        _group.alpha = 0.45f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        _labelText = CreateText(panel.transform, "WeaponLabel", "挡板武器", 14,
            new Vector2(0f, 0.56f), new Vector2(0.52f, 1f), Vector2.zero, Vector2.zero,
            TextAnchor.MiddleLeft, ProtocolUiStyle.IceFace);
        ProtocolUiStyle.ApplyHudTag(_labelText);

        _readyText = CreateText(panel.transform, "ReadyStatus", "武器就绪", 14,
            new Vector2(0.48f, 0.56f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
            TextAnchor.MiddleRight, ReadyFace);
        _readyText.enabled = false;

        var stripGo = new GameObject("SegmentStrip");
        stripGo.transform.SetParent(panel.transform, false);
        var stripRt = stripGo.AddComponent<RectTransform>();
        stripRt.anchorMin = new Vector2(0f, 0.04f);
        stripRt.anchorMax = new Vector2(1f, 0.5f);
        stripRt.offsetMin = Vector2.zero;
        stripRt.offsetMax = Vector2.zero;

        BuildSegments(stripRt);
    }

    private void BuildSegments(RectTransform strip)
    {
        const float segW = 5.5f;
        const float segH = 11f;
        const float gap = 3f;
        float totalW = SegmentCount * segW + (SegmentCount - 1) * gap;
        float startX = -totalW * 0.5f + segW * 0.5f;

        _segments = new SegmentVisual[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            float x = startX + i * (segW + gap);

            var segGo = new GameObject($"Seg_{i:00}");
            segGo.transform.SetParent(strip, false);
            var rt = segGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(segW, segH);

            var img = segGo.AddComponent<Image>();
            img.sprite = GetWhiteSprite();
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            img.color = EmptyFace;

            var outline = segGo.AddComponent<Outline>();
            outline.effectColor = EmptyEdge;
            outline.effectDistance = new Vector2(0.38f, -0.38f);
            outline.useGraphicAlpha = true;

            _segments[i] = new SegmentVisual { Image = img, Outline = outline };
        }
    }

    private static Text CreateText(Transform parent, string name, string content, int size,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        TextAnchor align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var text = go.AddComponent<Text>();
        text.text = content;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        ProtocolUiStyle.ApplyDisplayFont(text, size);
        return text;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }
}
