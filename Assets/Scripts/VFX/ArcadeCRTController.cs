using System.Collections;
using UnityEngine;

/// <summary>
/// 两层 CRT：① 常驻细扫描线  ② Boss 每波首次派兵前的扫描线（扫完再出兵）
/// </summary>
public class ArcadeCRTController : MonoBehaviour
{
    public static ArcadeCRTController Instance { get; private set; }

    public static float EffectiveVignette { get; private set; }
    public static bool IsScanActive { get; private set; }

    [Header("Layer 1 — Always On CRT")]
    [Range(0f, 0.15f)] public float scanlineOpacity = 0.04f;
    [Range(0f, 0.25f)] public float vignetteStrength = 0.10f;
    public int scanlineCount = 520;
    [Range(0.02f, 0.2f)] public float scanlineWidth = 0.08f;
    public float vignettePower = 2.2f;
    public float vignetteRoundness = 3.5f;

    [Header("Layer 2 — Boss First Spawn Scan")]
    public float waveScanDuration = 1.6f;
    public float waveScanFadeOut = 0.3f;
    [Range(0f, 0.5f)] public float waveScanLineIntensity = 0.24f;
    [Range(8f, 48f)] public float waveScanWakePx = 28f;
    [Range(0f, 0.5f)] public float waveScanRevealDim = 0.35f;
    [Range(0f, 0.4f)] public float waveScanInteractBoost = 0.22f;
    public Color waveScanColor = new Color(0.65f, 0.92f, 1.15f, 1f);

    public struct RuntimeState
    {
        public float ScanlineOpacity;
        public float ScanlineCount;
        public float ScanlineWidth;
        public float EffectiveVignette;
        public float VignettePower;
        public float VignetteRoundness;
        public float EventMaster;
        public float EventHeadY;
        public float EventTime;
        public float EventLineIntensity;
        public float EventWakePx;
        public float EventRevealDim;
        public float EventInteractBoost;
        public Color EventColor;
    }

    private Coroutine _scanCoroutine;

    private static float _eventMaster;
    private static float _eventHeadY;
    private static float _eventTime;
    private static float _eventLineIntensity;
    private static float _eventWakePx;
    private static float _eventRevealDim;
    private static float _eventInteractBoost;
    private static Color _eventColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        UpdateEffectiveVignette();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        if (_scanCoroutine != null)
        {
            StopCoroutine(_scanCoroutine);
            _scanCoroutine = null;
        }

        IsScanActive = false;
        ResetEvent();
        TronArenaBackground.Instance?.ClearScanBand();
    }

    private void LateUpdate()
    {
        UpdateEffectiveVignette();
    }

    /// <summary>Boss 每波首次派兵前：扫描线扫完再出兵。</summary>
    public void TriggerWaveScan()
    {
        if (!isActiveAndEnabled) return;

        if (_scanCoroutine != null)
            StopCoroutine(_scanCoroutine);

        ApplyWaveScanParams();
        _scanCoroutine = StartCoroutine(WaveScanRoutine());
    }

    public IEnumerator WaitForScanComplete()
    {
        while (IsScanActive)
            yield return null;
    }

    public static bool TryGetRuntimeState(out RuntimeState state)
    {
        state = default;

        if (Instance == null || !Instance.isActiveAndEnabled)
            return false;

        state.ScanlineOpacity = Instance.scanlineOpacity;
        state.ScanlineCount = Instance.scanlineCount;
        state.ScanlineWidth = Instance.scanlineWidth;
        state.EffectiveVignette = EffectiveVignette;
        state.VignettePower = Instance.vignettePower;
        state.VignetteRoundness = Instance.vignetteRoundness;
        state.EventMaster = _eventMaster;
        state.EventHeadY = _eventHeadY;
        state.EventTime = _eventTime;
        state.EventLineIntensity = _eventLineIntensity;
        state.EventWakePx = _eventWakePx;
        state.EventRevealDim = _eventRevealDim;
        state.EventInteractBoost = _eventInteractBoost;
        state.EventColor = _eventColor;
        return true;
    }

    private void ApplyWaveScanParams()
    {
        _eventLineIntensity = waveScanLineIntensity;
        _eventWakePx = waveScanWakePx;
        _eventRevealDim = waveScanRevealDim;
        _eventInteractBoost = waveScanInteractBoost;
        _eventColor = waveScanColor;
    }

    private IEnumerator WaveScanRoutine()
    {
        var cam = Camera.main;
        IsScanActive = true;

        _eventMaster = 1f;
        _eventHeadY = 1.05f;
        _eventTime = 0f;

        const float startY = 1.05f;
        const float endY = -0.05f;
        float elapsed = 0f;

        while (elapsed < waveScanDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _eventTime = elapsed;
            float p = Mathf.Clamp01(elapsed / waveScanDuration);
            _eventHeadY = Mathf.Lerp(startY, endY, p);

            if (cam != null)
            {
                float worldY = cam.ViewportToWorldPoint(new Vector3(0.5f, _eventHeadY, cam.nearClipPlane)).y;
                TronArenaBackground.Instance?.SetScanBand(worldY, 1f);
            }

            yield return null;
        }

        TronArenaBackground.Instance?.ClearScanBand();

        _eventHeadY = endY;
        float fadeElapsed = 0f;
        while (fadeElapsed < waveScanFadeOut)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            _eventTime += Time.unscaledDeltaTime;
            _eventMaster = 1f - Mathf.Clamp01(fadeElapsed / waveScanFadeOut);
            yield return null;
        }

        ResetEvent();
        IsScanActive = false;
        _scanCoroutine = null;
    }

    private static void ResetEvent()
    {
        _eventMaster = 0f;
        _eventHeadY = 0f;
        _eventTime = 0f;
        _eventLineIntensity = 0f;
        _eventWakePx = 0f;
        _eventRevealDim = 0f;
        _eventInteractBoost = 0f;
    }

    private void UpdateEffectiveVignette()
    {
        float vig = vignetteStrength;

        if (SlowMoFX.Instance != null && SlowMoFX.Instance.fxVolume != null && SlowMoFX.Instance.fxVolume.enabled)
            vig *= 0.5f;

        EffectiveVignette = vig;
    }
}
