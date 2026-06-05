using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossHealthBar : MonoBehaviour, IEnemyHealthBar
{
    [Header("UI")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(800f, 60f); // 60-70% width
    [SerializeField] private Vector2 panelOffset = new Vector2(0f, -30f); // 置顶

    [Header("Style")]
    [SerializeField] private Color fillColor = new Color(1f, 0f, 0.5f, 1f); // Magenta
    [SerializeField] private Color bufferColor = new Color(1f, 0.9f, 0.2f, 1f); // Yellow buffer
    [SerializeField] private Color frameColor = new Color(0.1f, 0.05f, 0.15f, 0.9f); // Dark metal
    [SerializeField] private Color glowColor = new Color(1f, 0f, 0.5f, 0.3f); // Glow border
    [SerializeField] private Color textColor = new Color(0.95f, 0.9f, 1f, 0.95f);

    private EnemyBase _enemy;
    private Boss _boss; // 尝试获取Boss脚本
    private RectTransform _panel;
    private RectTransform _fillRect;
    private RectTransform _bufferRect;
    private Text _titleText;
    private Text _stageText;
    private Image _avatarImage;
    private CanvasGroup _group;
    
    private float _currentPct = 1f;
    private float _bufferPct = 1f;
    private float _delayTimer;
    private Coroutine _flashRoutine;
    private Vector2 _basePos;

    private static Sprite _whiteSprite;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _boss = GetComponent<Boss>();
        BuildUI();
        StartCoroutine(IntroAnimation());
    }

    public void Bind(EnemyBase enemy)
    {
        _enemy = enemy;
    }

    private IEnumerator IntroAnimation()
    {
        if (_panel != null)
        {
            _panel.localScale = new Vector3(0f, 1f, 1f);
            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                // 缓动从中间展开
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                _panel.localScale = new Vector3(ease, 1f, 1f);
                yield return null;
            }
            _panel.localScale = Vector3.one;
        }
    }

    private void LateUpdate()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            if (_group != null) _group.alpha = 0f;
            return;
        }

        // Calculate target percentage
        float targetPct = _enemy.maxHits <= 0 ? 0f : Mathf.Clamp01(1f - (float)_enemy.CurrentHits / _enemy.maxHits);
        _currentPct = targetPct;

        if (_fillRect != null) 
            _fillRect.anchorMax = new Vector2(_currentPct, 1f);

        // 缓冲扣血槽延迟0.5秒后平滑缩减
        if (_delayTimer > 0f) 
        {
            _delayTimer -= Time.deltaTime;
        }
        else 
        {
            _bufferPct = Mathf.MoveTowards(_bufferPct, _currentPct, Time.deltaTime * 0.4f);
        }

        if (_bufferRect != null) 
            _bufferRect.anchorMax = new Vector2(_bufferPct, 1f);

        if (_group != null) _group.alpha = 1f;
        
        string title = "WARNING: CYBER-TITAN";
        if (_boss != null && _boss.definition != null)
            title = "WARNING: " + _boss.definition.bossName.ToUpper();
        if (_titleText != null) _titleText.text = title;

        if (_stageText != null)
        {
            int stages = 1;
            int currentStage = 1;
            // 简单模拟阶段
            if (_boss != null)
            {
                stages = 2; // BossDef 里有 Phase 2
                currentStage = _enemy.CurrentHits >= _enemy.maxHits / 2 ? 2 : 1;
                // 从后往前数阶段
                int displayStage = stages - currentStage + 1;
                _stageText.text = $"[x{displayStage}]";
            }
            else
            {
                _stageText.text = "[x1]";
            }
        }
    }

    public void OnEnemyHit()
    {
        _delayTimer = 0.5f; // 0.5s延迟
        _bufferPct = Mathf.Max(_bufferPct, _currentPct); // 重置 buffer
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
        Shake();
    }

    public void OnEnemyDeath()
    {
        if (_group != null) _group.alpha = 0f;
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
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            targetCanvas = canvas;
        }

        var panelObj = new GameObject("BossUI_Panel");
        panelObj.transform.SetParent(canvas.transform, false);
        _panel = panelObj.AddComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f, 1f);
        _panel.anchorMax = new Vector2(0.5f, 1f);
        _panel.pivot = new Vector2(0.5f, 1f); // 锚点顶部居中
        _panel.sizeDelta = panelSize;
        _panel.anchoredPosition = panelOffset;
        _basePos = panelOffset;

        _group = panelObj.AddComponent<CanvasGroup>();
        _group.alpha = 1f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // Glow Border
        var glow = CreateImage("Glow", panelObj.transform, glowColor);
        Stretch(glow.rectTransform);
        glow.rectTransform.sizeDelta = new Vector2(10f, 10f); // 比边框稍微大一点

        // Metal Frame
        var frame = CreateImage("Frame", panelObj.transform, frameColor);
        Stretch(frame.rectTransform);

        // Avatar Box (Left)
        var avatarBg = CreateImage("AvatarBg", panelObj.transform, new Color(0.15f, 0.05f, 0.2f, 1f));
        avatarBg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        avatarBg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        avatarBg.rectTransform.pivot = new Vector2(0f, 0.5f);
        avatarBg.rectTransform.sizeDelta = new Vector2(80f, 80f);
        avatarBg.rectTransform.anchoredPosition = new Vector2(-40f, 0f);
        // Rotation for diamond shape
        avatarBg.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

        var avatarMaskObj = new GameObject("AvatarMask");
        avatarMaskObj.transform.SetParent(avatarBg.transform, false);
        var avatarMaskImg = avatarMaskObj.AddComponent<Image>();
        avatarMaskImg.sprite = GetWhiteSprite();
        avatarMaskImg.color = Color.white;
        var mask = avatarMaskObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        var maskRt = avatarMaskObj.GetComponent<RectTransform>();
        Stretch(maskRt);
        maskRt.sizeDelta = new Vector2(-6f, -6f);

        var avatarImgObj = new GameObject("AvatarImg");
        avatarImgObj.transform.SetParent(avatarMaskObj.transform, false);
        _avatarImage = avatarImgObj.AddComponent<Image>();
        _avatarImage.color = Color.white;
        if (_boss != null && _boss.definition != null && _boss.definition.sprite != null)
            _avatarImage.sprite = _boss.definition.sprite;
        var avatarRt = avatarImgObj.GetComponent<RectTransform>();
        Stretch(avatarRt);
        avatarRt.localEulerAngles = new Vector3(0f, 0f, -45f); // 抵消父节点的旋转
        avatarRt.sizeDelta = new Vector2(30f, 30f);

        // Title
        var title = CreateText("Title", panelObj.transform, 20, TextAnchor.MiddleLeft, textColor);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(60f, -5f);
        title.rectTransform.sizeDelta = new Vector2(500f, 30f);
        title.fontStyle = FontStyle.Bold;
        _titleText = title;

        // Stage Indicator [x3]
        var stage = CreateText("Stage", panelObj.transform, 18, TextAnchor.MiddleRight, textColor);
        stage.rectTransform.anchorMin = new Vector2(1f, 0f);
        stage.rectTransform.anchorMax = new Vector2(1f, 0f);
        stage.rectTransform.pivot = new Vector2(1f, 0f);
        stage.rectTransform.anchoredPosition = new Vector2(-10f, 5f);
        stage.rectTransform.sizeDelta = new Vector2(100f, 30f);
        stage.fontStyle = FontStyle.Bold;
        _stageText = stage;

        // Bar Background
        var barBg = CreateImage("BarBg", panelObj.transform, new Color(0.05f, 0.05f, 0.07f, 0.85f));
        barBg.rectTransform.anchorMin = new Vector2(0f, 0f);
        barBg.rectTransform.anchorMax = new Vector2(1f, 0f);
        barBg.rectTransform.pivot = new Vector2(0.5f, 0f);
        barBg.rectTransform.sizeDelta = new Vector2(-120f, 24f); // 留出空间
        barBg.rectTransform.anchoredPosition = new Vector2(30f, 12f);

        // Buffer Bar
        var buffer = CreateImage("Buffer", barBg.transform, bufferColor);
        _bufferRect = buffer.rectTransform;
        Stretch(_bufferRect);
        _bufferRect.anchorMax = Vector2.one;
        _bufferRect.pivot = new Vector2(0f, 0.5f);

        // Fill Bar
        var fill = CreateImage("Fill", barBg.transform, fillColor);
        _fillRect = fill.rectTransform;
        Stretch(_fillRect);
        _fillRect.anchorMax = Vector2.one;
        _fillRect.pivot = new Vector2(0f, 0.5f);
    }

    private void Shake()
    {
        if (_panel == null) return;
        _panel.anchoredPosition = _basePos + Random.insideUnitCircle * 5.0f; // UI震动
        CancelInvoke(nameof(ResetShake));
        Invoke(nameof(ResetShake), 0.1f);
    }

    private void ResetShake()
    {
        if (_panel != null) _panel.anchoredPosition = _basePos;
    }

    private IEnumerator FlashRoutine()
    {
        if (_fillRect != null)
        {
            var img = _fillRect.GetComponent<Image>();
            if (img != null) img.color = Color.white;
        }
        yield return new WaitForSeconds(0.08f);
        if (_fillRect != null)
        {
            var img = _fillRect.GetComponent<Image>();
            if (img != null) img.color = fillColor;
        }
        _flashRoutine = null;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var img = obj.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.type = Image.Type.Sliced;
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
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }
}
