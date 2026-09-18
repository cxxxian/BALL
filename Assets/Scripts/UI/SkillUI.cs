using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内技能槽：底部协议芯片。视觉小、热区大；CD 用横向灯段。
/// 不改技能逻辑，只替换圆形按钮外观。
/// </summary>
public class SkillUI : MonoBehaviour
{
    private const int RailCount = 8;

    [Header("槽位索引（0/1=装备技能）")]
    public int slotIndex = 0;

    [Header("References（旧圆形，运行时隐藏）")]
    public Image cdRing;
    public Image iconDisc;

    public Graphic HighlightGraphic => _frame;

    private Image _frame;
    private Image _hit;
    private Text _label;
    private Text _state;
    private Image[] _rails;
    private Outline _frameEdge;

    private bool _subscribed;
    private bool _gameEventsSubscribed;
    private float _pulse;
    private bool _isEffectActive;

    private static readonly Color NearBlack = new Color(0.04f, 0.04f, 0.06f, 0.78f);
    private static readonly Color Cyan = new Color(0f, 0.91f, 1f, 1f);
    private static readonly Color Fuchsia = new Color(1f, 0.25f, 0.78f, 1f);
    private static readonly Color Ice = new Color(0.93f, 0.97f, 1f, 1f);
    private static readonly Color Muted = new Color(0.43f, 0.55f, 0.62f, 1f);
    private static readonly Color RailEmpty = new Color(0.12f, 0.16f, 0.2f, 0.55f);

    private static Sprite _white;

    private void Awake()
    {
        HideLegacyCircles();
        BuildChip();
        ActionHudLayout.Ensure();
    }

    private void Start()
    {
        ActionHudLayout.Ensure().Register(this);
    }

    private void HideLegacyCircles()
    {
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);

        if (cdRing != null) cdRing.enabled = false;
        if (iconDisc != null) iconDisc.enabled = false;
    }

    private void BuildChip()
    {
        var root = GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(118f, 88f);

        var hitGo = new GameObject("HitArea");
        hitGo.transform.SetParent(transform, false);
        var hitRt = hitGo.AddComponent<RectTransform>();
        Stretch(hitRt);
        _hit = hitGo.AddComponent<Image>();
        _hit.sprite = White();
        _hit.color = new Color(1f, 1f, 1f, 0.01f);
        _hit.raycastTarget = true;

        var button = GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = _hit;
            button.transition = Selectable.Transition.None;
        }

        var chip = new GameObject("Chip");
        chip.transform.SetParent(transform, false);
        var chipRt = chip.AddComponent<RectTransform>();
        chipRt.anchorMin = chipRt.anchorMax = new Vector2(0.5f, 0.5f);
        chipRt.pivot = new Vector2(0.5f, 0.5f);
        chipRt.sizeDelta = new Vector2(100f, 64f);

        _frame = CreateImage(chip.transform, "Frame", NearBlack, false);
        Stretch(_frame.rectTransform);
        _frameEdge = _frame.gameObject.AddComponent<Outline>();
        _frameEdge.effectDistance = new Vector2(1.1f, -1.1f);
        _frameEdge.useGraphicAlpha = true;

        _label = CreateLabel(chip.transform, "Label", slotIndex == 0 ? "SLOT 0" : "SLOT 1", 13, Ice,
            new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.92f), TextAnchor.MiddleLeft);
        _state = CreateLabel(chip.transform, "State", "", 11, Cyan,
            new Vector2(0.42f, 0.42f), new Vector2(0.94f, 0.92f), TextAnchor.MiddleRight);

        var rail = new GameObject("Rail");
        rail.transform.SetParent(chip.transform, false);
        var railRt = rail.AddComponent<RectTransform>();
        railRt.anchorMin = new Vector2(0.08f, 0.12f);
        railRt.anchorMax = new Vector2(0.92f, 0.36f);
        railRt.offsetMin = railRt.offsetMax = Vector2.zero;

        _rails = new Image[RailCount];
        for (int i = 0; i < RailCount; i++)
        {
            float x0 = i / (float)RailCount;
            float x1 = (i + 0.72f) / RailCount;
            var seg = CreateImage(rail.transform, "Seg" + i, RailEmpty, false);
            var rt = seg.rectTransform;
            rt.anchorMin = new Vector2(x0, 0f);
            rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _rails[i] = seg;
        }

        ApplyVisual(1f, false);
    }

    private void Update()
    {
        if (!_subscribed && SkillManager.Instance != null)
        {
            SkillManager.Instance.onSlotCooldownChanged.AddListener(OnSlotCooldownChanged);
            SkillManager.Instance.onSlotActivated.AddListener(OnSlotActivated);
            SkillManager.Instance.onAimingEnded.AddListener(OnAimingEnded);
            _subscribed = true;

            if (SkillManager.Instance.slots != null && slotIndex < SkillManager.Instance.slots.Length)
                OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
        }

        if (GameManager.Instance != null && !_gameEventsSubscribed)
        {
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
            GameManager.Instance.onGameOver.AddListener(OnBallLost);
            _gameEventsSubscribed = true;
        }

        TrackPersistentEffects();

        if (_isEffectActive)
        {
            _pulse += Time.unscaledDeltaTime * 5f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            ApplyVisual(0f, true, g);
        }
        else if (IsSlotReady())
        {
            _pulse += Time.unscaledDeltaTime * 2.2f;
            float g = (Mathf.Sin(_pulse) + 1f) * 0.5f;
            ApplyVisual(0f, false, g);
        }
    }

    private void TrackPersistentEffects()
    {
        if (SkillManager.Instance == null || SkillManager.Instance.slots == null) return;
        if (slotIndex < 0 || slotIndex >= SkillManager.Instance.slots.Length) return;

        var def = SkillManager.Instance.slots[slotIndex].definition;
        if (def == null) return;

        bool? now = def.implementationType switch
        {
            ActiveSkillType.BlockShield => BlockShield.Instance != null && BlockShield.Instance.IsActive,
            ActiveSkillType.TimestopAura => TimestopAura.Instance != null && TimestopAura.Instance.IsActive,
            ActiveSkillType.ExecuteChain => SkillManager.Instance.IsExecuteArmed,
            ActiveSkillType.SplitProtocol => SplitProtocol.Instance != null && SplitProtocol.Instance.IsActive,
            ActiveSkillType.ProtocolRedirect => SkillManager.Instance.IsAiming,
            _ => null
        };

        if (now == null || now == _isEffectActive) return;
        _isEffectActive = now.Value;
        if (!_isEffectActive)
            OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
    }

    private bool IsSlotReady()
    {
        if (SkillManager.Instance?.slots == null) return false;
        if (slotIndex < 0 || slotIndex >= SkillManager.Instance.slots.Length) return false;
        return SkillManager.Instance.slots[slotIndex].IsReady;
    }

    private void OnSlotCooldownChanged(int idx, float ratio)
    {
        if (idx != slotIndex || _isEffectActive) return;
        _pulse = 0f;
        ApplyVisual(ratio, false);
    }

    private void ApplyVisual(float cooldownRatio, bool active, float pulse01 = 0f)
    {
        bool ready = !active && cooldownRatio <= 0.001f;
        float charge = active || ready ? 1f : 1f - Mathf.Clamp01(cooldownRatio);
        int lit = Mathf.Clamp(Mathf.CeilToInt(charge * RailCount), 0, RailCount);
        if (!active && !ready && charge <= 0.001f) lit = 0;

        Color edge = active ? Fuchsia : (ready ? Cyan : Muted);
        float bright = active ? 1f + pulse01 * 0.25f : (ready ? 0.85f + pulse01 * 0.2f : 0.55f);
        if (_frame != null)
            _frame.color = new Color(NearBlack.r, NearBlack.g, NearBlack.b, active || ready ? 0.86f : 0.62f);
        if (_frameEdge != null)
            _frameEdge.effectColor = new Color(edge.r, edge.g, edge.b, bright);

        if (_label != null)
            _label.color = ready || active ? Ice : Muted;
        if (_state != null)
        {
            _state.text = active ? "AIM" : (ready ? "RDY" : "");
            _state.color = active ? Fuchsia : Cyan;
        }

        if (_rails == null) return;
        for (int i = 0; i < _rails.Length; i++)
        {
            if (_rails[i] == null) continue;
            if (i < lit)
            {
                var c = active ? Fuchsia : Cyan;
                _rails[i].color = new Color(c.r * bright, c.g * bright, c.b * bright, 1f);
            }
            else
                _rails[i].color = RailEmpty;
        }
    }

    private void OnSlotActivated(int idx)
    {
        if (idx != slotIndex) return;
        _pulse = 0f;
        _isEffectActive = true;
        ApplyVisual(0f, true);
    }

    private void OnAimingEnded(int idx)
    {
        if (idx != slotIndex) return;
        ResetAimVisual();
    }

    private void OnBallLost() => ResetAimVisual();

    private void ResetAimVisual()
    {
        _isEffectActive = false;
        _pulse = 0f;
        if (SkillManager.Instance?.slots == null || slotIndex < 0 || slotIndex >= SkillManager.Instance.slots.Length)
            return;
        OnSlotCooldownChanged(slotIndex, SkillManager.Instance.slots[slotIndex].CooldownRatio);
    }

    public void OnButtonClicked()
    {
        SkillManager.Instance?.TryActivate(slotIndex);
    }

    private static Image CreateImage(Transform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = White();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static Text CreateLabel(Transform parent, string name, string content, int size, Color color,
        Vector2 anchorMin, Vector2 anchorMax, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.AddComponent<Text>();
        text.text = content;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        ProtocolUiStyle.ApplyDisplayFont(text, size);
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Sprite White()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _white;
    }
}
