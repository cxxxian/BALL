using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 击杀得分弹出：世界坐标出现 +N，短暂停留后吸入右上 Score。
/// Combo 热时琥珀，否则青白。
/// </summary>
[DisallowMultipleComponent]
public class ScorePopUI : MonoBehaviour
{
    public static ScorePopUI Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] RectTransform scoreTarget;
    [SerializeField] Font font;
    [SerializeField] Canvas rootCanvas;

    [Header("Motion")]
    [SerializeField] float holdSeconds = 0.32f;
    [SerializeField] float flySeconds = 0.5f;
    [SerializeField] float risePixels = 48f;
    [SerializeField] float startScale = 1.18f;
    [SerializeField] float endScale = 0.5f;
    [SerializeField] int poolSize = 12;
    [SerializeField] int fontSize = 28;

    private readonly Queue<Text> _pool = new Queue<Text>(16);
    private RectTransform _rt;
    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _rt = transform as RectTransform;
        SetupFullScreenLayer();
        ResolveRefs();
        EnsurePool();
    }

    private void Start()
    {
        ResolveRefs();
        ApplyFontToPool();
        transform.SetAsLastSibling();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void SetupFullScreenLayer()
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
        _rt.localScale = Vector3.one;
        _rt.localPosition = Vector3.zero;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        var nested = GetComponent<Canvas>();
        if (nested != null && nested != rootCanvas)
            Destroy(nested);
        var ray = GetComponent<GraphicRaycaster>();
        if (ray != null)
            Destroy(ray);

        transform.SetAsLastSibling();
    }

    private void ResolveRefs()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        var hud = HUDController.Instance;
        if (hud != null && scoreTarget == null && hud.scoreText != null)
            scoreTarget = hud.scoreText.rectTransform;

        font = ProtocolUiStyle.ResolvePopFont();
        if (font == null && hud != null && hud.scoreText != null)
            font = hud.scoreText.font;
    }

    public static void Spawn(Vector2 worldPos, int points)
    {
        if (points <= 0) return;
        EnsureInstance();
        if (Instance == null)
        {
            Debug.LogWarning("[ScorePopUI] Spawn failed: no instance (HUD/Canvas missing).");
            return;
        }
        Instance.SpawnInternal(worldPos, points);
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var hud = HUDController.Instance;
        if (hud == null) return;
        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("ScorePopUI", typeof(RectTransform), typeof(ScorePopUI));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
    }

    private void SpawnInternal(Vector2 worldPos, int points)
    {
        ResolveRefs();
        EnsurePool();
        if (_rt == null) return;

        transform.SetAsLastSibling();

        Text label = _pool.Count > 0 ? _pool.Dequeue() : CreateLabel();
        if (label == null) return;

        if (font != null)
            label.font = font;
        if (label.font == null)
        {
            Debug.LogWarning("[ScorePopUI] Missing font — pop skipped.");
            _pool.Enqueue(label);
            return;
        }

        bool comboHot = IsComboHot();
        Color face = comboHot ? ProtocolUiStyle.AmberFace : ProtocolUiStyle.IceFace;
        Color glow = comboHot ? ProtocolUiStyle.AmberFace : ProtocolUiStyle.CyanFace;

        label.text = ProtocolUiStyle.FormatScorePop(points, font);
        label.fontSize = fontSize;
        label.color = face;
        ProtocolUiStyle.ApplyNeonOutline(label, glow, spread: 1.6f, glowAlpha: 0.7f);
        label.gameObject.SetActive(true);
        label.transform.SetAsLastSibling();

        Vector2 startLocal = WorldToLocal(worldPos);
        var rt = label.rectTransform;
        rt.anchoredPosition = startLocal;
        rt.localScale = Vector3.one * startScale;
        rt.localRotation = Quaternion.identity;

        StartCoroutine(FlyRoutine(label, startLocal, face));
    }

    private IEnumerator FlyRoutine(Text label, Vector2 startLocal, Color face)
    {
        var rt = label.rectTransform;
        float t = 0f;

        while (t < holdSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / holdSeconds);
            float ease = 1f - (1f - u) * (1f - u);
            rt.anchoredPosition = startLocal + Vector2.up * (risePixels * ease);
            rt.localScale = Vector3.one * Mathf.Lerp(startScale, 1.02f, ease);
            label.color = face;
            yield return null;
        }

        Vector2 from = rt.anchoredPosition;
        Vector2 to = GetScoreTargetLocal();
        t = 0f;
        while (t < flySeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / flySeconds);
            float ease = u * u;
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
            rt.localScale = Vector3.one * Mathf.Lerp(1.02f, endScale, ease);
            Color c = face;
            c.a = Mathf.Lerp(1f, 0.2f, ease);
            label.color = c;
            yield return null;
        }

        if (HUDController.Instance != null)
            HUDController.Instance.PunchScore();

        Recycle(label);
    }

    private void Recycle(Text label)
    {
        if (label == null) return;
        label.gameObject.SetActive(false);
        _pool.Enqueue(label);
    }

    private Vector2 GetScoreTargetLocal()
    {
        if (scoreTarget == null)
            return new Vector2(_rt.rect.width * 0.35f, _rt.rect.height * 0.4f);

        Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, scoreTarget.position);
        return ScreenToLocal(screen);
    }

    private Vector2 WorldToLocal(Vector2 worldPos)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null)
            _cam = Object.FindObjectOfType<Camera>();

        Vector3 screen;
        if (_cam != null)
        {
            screen = _cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0f));
            if (screen.z < 0f)
                screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.55f, 0f);
        }
        else
        {
            screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.55f, 0f);
        }
        return ScreenToLocal(screen);
    }

    private Vector2 ScreenToLocal(Vector3 screen)
    {
        Camera eventCam = null;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = rootCanvas.worldCamera != null ? rootCanvas.worldCamera : _cam;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, eventCam, out Vector2 local))
            local = Vector2.zero;
        return local;
    }

    private static bool IsComboHot()
    {
        if (ComboSystem.Instance == null) return false;
        int threshold = ComboSystem.MinComboThreshold;
        if (GameManager.Instance != null && GameManager.Instance.config != null)
            threshold = ComboSystem.GetEffectiveThreshold(GameManager.Instance.config.comboDisplayThreshold);
        return ComboSystem.Instance.CurrentCombo >= threshold;
    }

    private void EnsurePool()
    {
        while (_pool.Count < poolSize)
            _pool.Enqueue(CreateLabel());
    }

    private void ApplyFontToPool()
    {
        if (font == null) return;
        foreach (var t in _pool)
        {
            if (t != null) t.font = font;
        }
    }

    private Text CreateLabel()
    {
        var go = new GameObject("ScorePop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_rt != null ? _rt : transform, false);
        rt.sizeDelta = new Vector2(200f, 52f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = false;

        ProtocolUiStyle.ApplyNeonOutline(text, ProtocolUiStyle.CyanFace, 1.6f, 0.7f);

        go.SetActive(false);
        return text;
    }
}
