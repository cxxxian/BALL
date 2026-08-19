using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 顶栏「协议灯轨」：竖柱 EQ 映射血量（条数可配，非一血一柱）。
/// 缓冲余晖 + Phase 变色 + 受击闪白。
/// </summary>
[DisallowMultipleComponent]
public class BossHealthBar : MonoBehaviour, IEnemyHealthBar
{
    [Header("UI")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(640f, 52f);
    [SerializeField] private Vector2 panelOffset = new Vector2(0f, -18f);
    [SerializeField] [Range(8, 48)] private int barCount = 24;
    [SerializeField] private float barMaxHeight = 36f;
    [SerializeField] private float barWidth = 8f;
    [SerializeField] private float barGap = 3f;
    [SerializeField] private float railThickness = 3f;

    [Header("Motion")]
    [SerializeField] private float bufferLerpSpeed = 1.2f;
    [SerializeField] private float bufferDelay = 0.45f;
    [SerializeField] private float eqPulseSpeed = 1.1f;
    [SerializeField] private float eqPulseAmount = 0.18f;
    [SerializeField] [Range(0f, 1.5f)] private float musicDrive = 0.85f;
    [SerializeField] private float musicFloor = 0.35f;
    [SerializeField] private float hitFlashDuration = 0.1f;

    private EnemyBase _enemy;
    private Boss _boss;
    private RectTransform _panel;
    private CanvasGroup _group;
    private Text _nameText;
    private Text _phaseText;
    private Image _rail;
    private Image[] _bufferBars;
    private Image[] _fillBars;
    private float[] _barProfile;
    private float[] _noiseSeed;

    private float _currentPct = 1f;
    private float _bufferPct = 1f;
    private float _delayTimer;
    private float _hitFlash;
    private Vector2 _basePos;
    private bool _wasPhase2;
    private string _cachedNameKey;
    private Coroutine _introRoutine;

    private static Sprite _whiteSprite;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _boss = GetComponent<Boss>();
        BuildUI();
        _introRoutine = StartCoroutine(IntroAnimation());
    }

    public void Bind(EnemyBase enemy)
    {
        _enemy = enemy;
        _boss = enemy as Boss;
        _cachedNameKey = null;
        RefreshLabels(force: true);
    }

    private void OnDestroy()
    {
        if (_introRoutine != null) StopCoroutine(_introRoutine);
        if (_panel != null) Destroy(_panel.gameObject);
    }

    private IEnumerator IntroAnimation()
    {
        if (_panel == null) yield break;

        _panel.localScale = new Vector3(0.15f, 1f, 1f);
        if (_group != null) _group.alpha = 0f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2.4f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            _panel.localScale = new Vector3(Mathf.Lerp(0.15f, 1f, ease), 1f, 1f);
            if (_group != null) _group.alpha = ease;
            yield return null;
        }

        _panel.localScale = Vector3.one;
        if (_group != null) _group.alpha = 1f;
        _introRoutine = null;
    }

    private void LateUpdate()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            if (_group != null) _group.alpha = 0f;
            return;
        }

        float targetPct = _enemy.maxHits <= 0
            ? 0f
            : Mathf.Clamp01(1f - (float)_enemy.CurrentHits / _enemy.maxHits);
        _currentPct = targetPct;

        if (_delayTimer > 0f)
            _delayTimer -= Time.unscaledDeltaTime;
        else
            _bufferPct = Mathf.MoveTowards(_bufferPct, _currentPct, Time.unscaledDeltaTime * bufferLerpSpeed);

        if (_hitFlash > 0f)
            _hitFlash = Mathf.Max(0f, _hitFlash - Time.unscaledDeltaTime);

        bool phase2 = IsPhase2();
        if (phase2 != _wasPhase2)
        {
            _wasPhase2 = phase2;
            RefreshLabels(force: true);
        }

        UpdateBars(phase2);
        RefreshLabels(force: false);

        if (_group != null) _group.alpha = 1f;
    }

    public void OnEnemyHit()
    {
        _delayTimer = bufferDelay;
        _bufferPct = Mathf.Max(_bufferPct, _currentPct);
        _hitFlash = hitFlashDuration;
        Shake();
    }

    public void OnEnemyDeath()
    {
        if (_group != null) _group.alpha = 0f;
    }

    private bool IsPhase2()
    {
        if (_enemy == null || _enemy.maxHits <= 0) return false;
        return _enemy.CurrentHits >= _enemy.maxHits / 2;
    }

    private void RefreshLabels(bool force = true)
    {
        string name = "CYBER-TITAN";
        if (_boss != null && _boss.definition != null && !string.IsNullOrEmpty(_boss.definition.bossName))
            name = _boss.definition.bossName.ToUpperInvariant();

        bool p2 = IsPhase2();
        string key = name + (p2 ? "|2" : "|1");
        if (!force && key == _cachedNameKey) return;
        _cachedNameKey = key;

        if (_nameText != null) _nameText.text = name;

        if (_phaseText != null)
        {
            _phaseText.text = p2 ? "PHASE 02" : "PHASE 01";
            _phaseText.color = p2 ? NeonUiColors.DangerUi(1.05f) : NeonUiColors.MenuCyanUi(0.95f);
        }

        if (_rail != null)
            _rail.color = p2
                ? new Color(1f, 0.35f, 0.12f, 0.95f)
                : new Color(1f, 0.2f, 0.55f, 0.9f);
    }

    private void UpdateBars(bool phase2)
    {
        if (_fillBars == null || _bufferBars == null) return;

        // P1: 粉 fill + 琥珀 buffer；P2: 血红 fill + 柠檬黄 buffer（避免同色相糊成一片）
        Color fillLive = phase2
            ? new Color(1f, 0.16f, 0.1f, 1f)
            : new Color(1f, 0.12f, 0.55f, 1f);
        Color fillFlash = Color.white;
        Color bufferCol = phase2
            ? new Color(1f, 0.9f, 0.22f, 0.52f)
            : new Color(1f, 0.62f, 0.18f, 0.4f);

        float flashT = hitFlashDuration > 0f ? Mathf.Clamp01(_hitFlash / hitFlashDuration) : 0f;
        float time = Time.unscaledTime * eqPulseSpeed;

        for (int i = 0; i < barCount; i++)
        {
            float fillH = SampleBarHeight(i, _currentPct, time);
            float bufH = SampleBarHeight(i, _bufferPct, time * 0.85f + 0.17f);
            bufH = Mathf.Max(bufH, fillH);

            SetBarHeight(_bufferBars[i], bufH);
            if (_bufferBars[i] != null)
                _bufferBars[i].color = bufferCol;

            SetBarHeight(_fillBars[i], fillH);
            if (_fillBars[i] != null)
            {
                Color c = Color.Lerp(fillLive, fillFlash, flashT * flashT);
                // 已灭的柱保持极低透明度轮廓
                if (fillH < 0.04f)
                    c.a = 0.12f;
                _fillBars[i].color = c;
            }
        }
    }

    /// <summary>
    /// 将 [0,1] 血量映射到第 i 根柱高度。
    /// 点亮范围由血量决定；柱内起伏由 BGM 频谱驱动（无音频时回退 Perlin）。
    /// </summary>
    private float SampleBarHeight(int index, float pct, float time)
    {
        float n = Mathf.Max(1, barCount);
        float lit = pct * n;
        float segment = lit - index;
        float baseLit = Mathf.Clamp01(segment);

        float profile = _barProfile != null && index < _barProfile.Length
            ? _barProfile[index]
            : 0.7f;

        float music = 0f;
        if (AudioManager.Instance != null)
            music = AudioManager.Instance.GetBand(index, barCount);

        float seed = _noiseSeed != null && index < _noiseSeed.Length ? _noiseSeed[index] : index * 0.31f;
        float fallback = Mathf.PerlinNoise(seed, time);
        float eqSrc = music > 0.001f ? music : fallback;

        // 血量控「这根还亮不亮」；音乐控「亮着的柱怎么跳」
        float eq = musicFloor + (1f - musicFloor) * Mathf.Clamp01(eqSrc * musicDrive);
        eq = Mathf.Lerp(1f - eqPulseAmount + eqPulseAmount * fallback, eq, music > 0.001f ? 1f : 0.35f);

        float urgency = 1f + (1f - pct) * 0.35f;
        float h = baseLit * profile * eq * urgency;
        return Mathf.Clamp01(h);
    }

    private static void SetBarHeight(Image img, float normalized)
    {
        if (img == null) return;
        var rt = img.rectTransform;
        Vector3 s = rt.localScale;
        s.y = Mathf.Clamp01(normalized);
        rt.localScale = s;
    }

    private void Shake()
    {
        if (_panel == null) return;
        _panel.anchoredPosition = _basePos + Random.insideUnitCircle * 4f;
        CancelInvoke(nameof(ResetShake));
        Invoke(nameof(ResetShake), 0.08f);
    }

    private void ResetShake()
    {
        if (_panel != null) _panel.anchoredPosition = _basePos;
    }

    private void BuildUI()
    {
        Canvas canvas = targetCanvas;
        if (canvas == null)
        {
            var canvasObj = new GameObject("BossUI_Canvas");
            canvasObj.transform.SetParent(transform.root, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            targetCanvas = canvas;
        }

        // 清理旧版厚面板（热重载 / 重复挂载）
        var legacy = canvas.transform.Find("BossUI_Panel");
        if (legacy != null) Destroy(legacy.gameObject);

        var panelObj = new GameObject("BossUI_Panel");
        panelObj.transform.SetParent(canvas.transform, false);
        _panel = panelObj.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 1f);
        _panel.anchorMax = new Vector2(0.5f, 1f);
        _panel.pivot = new Vector2(0.5f, 1f);
        _panel.sizeDelta = panelSize;
        _panel.anchoredPosition = panelOffset;
        _basePos = panelOffset;

        _group = panelObj.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // 顶栏微暗底，不再用厚金属框
        var plate = CreateImage("Plate", panelObj.transform, new Color(0.02f, 0.04f, 0.08f, 0.55f));
        Stretch(plate.rectTransform);

        // 名称（左）— 去掉 WARNING 喊话
        _nameText = CreateText("Name", panelObj.transform, 15, TextAnchor.MiddleLeft, NeonUiColors.MenuCyanUi(0.95f));
        _nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
        _nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _nameText.rectTransform.pivot = new Vector2(0f, 1f);
        _nameText.rectTransform.anchoredPosition = new Vector2(10f, -2f);
        _nameText.rectTransform.sizeDelta = new Vector2(-120f, 18f);
        _nameText.fontStyle = FontStyle.Bold;
        _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 阶段（右）
        _phaseText = CreateText("Phase", panelObj.transform, 13, TextAnchor.MiddleRight, NeonUiColors.MenuCyanUi(0.9f));
        _phaseText.rectTransform.anchorMin = new Vector2(1f, 1f);
        _phaseText.rectTransform.anchorMax = new Vector2(1f, 1f);
        _phaseText.rectTransform.pivot = new Vector2(1f, 1f);
        _phaseText.rectTransform.anchoredPosition = new Vector2(-10f, -3f);
        _phaseText.rectTransform.sizeDelta = new Vector2(110f, 16f);
        _phaseText.fontStyle = FontStyle.Bold;

        // 灯轨容器
        var stripObj = new GameObject("EqStrip");
        stripObj.transform.SetParent(panelObj.transform, false);
        var strip = stripObj.AddComponent<RectTransform>();
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(1f, 1f);
        strip.offsetMin = new Vector2(12f, 4f);
        strip.offsetMax = new Vector2(-12f, -20f);

        // 顶光轨（柱子悬挂线）
        _rail = CreateImage("Rail", strip, new Color(1f, 0.2f, 0.55f, 0.9f));
        _rail.rectTransform.anchorMin = new Vector2(0f, 1f);
        _rail.rectTransform.anchorMax = new Vector2(1f, 1f);
        _rail.rectTransform.pivot = new Vector2(0.5f, 1f);
        _rail.rectTransform.anchoredPosition = Vector2.zero;
        _rail.rectTransform.sizeDelta = new Vector2(0f, railThickness);

        BuildBarArrays(strip);
        RefreshLabels(force: true);
    }

    private void BuildBarArrays(RectTransform strip)
    {
        barCount = Mathf.Clamp(barCount, 8, 48);
        _bufferBars = new Image[barCount];
        _fillBars = new Image[barCount];
        _barProfile = new float[barCount];
        _noiseSeed = new float[barCount];

        float totalW = barCount * barWidth + (barCount - 1) * barGap;
        float startX = -totalW * 0.5f + barWidth * 0.5f;

        for (int i = 0; i < barCount; i++)
        {
            // 中间偏高、两端略矮的轮廓，满血时也不像砖墙
            float t = (i + 0.5f) / barCount;
            _barProfile[i] = 0.42f + 0.58f * Mathf.Sin(t * Mathf.PI);
            _noiseSeed[i] = i * 0.37f + 1.7f;

            float x = startX + i * (barWidth + barGap);

            var buf = CreateBar("Buf_" + i, strip, x, new Color(1f, 0.62f, 0.18f, 0.4f));
            _bufferBars[i] = buf;

            var fill = CreateBar("Fill_" + i, strip, x, new Color(1f, 0.12f, 0.55f, 1f));
            _fillBars[i] = fill;
        }
    }

    private Image CreateBar(string name, RectTransform parent, float x, Color color)
    {
        var img = CreateImage(name, parent, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f); // 从顶光轨向下生长
        rt.anchoredPosition = new Vector2(x, -railThickness);
        rt.sizeDelta = new Vector2(barWidth, barMaxHeight);
        rt.localScale = new Vector3(1f, 1f, 1f);
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        return img;
    }

    private static Text CreateText(string name, Transform parent, int size, TextAnchor align, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.text = string.Empty;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
