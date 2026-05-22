using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SlowMoFX : MonoBehaviour
{
    public static SlowMoFX Instance { get; private set; }

    [Header("Canvas Overlays")]
    public Image flashOverlay;    // 全屏瞬间亮闪
    public Image tintOverlay;     // 全屏暗蓝色调

    [Header("Post-Processing")]
    public Volume fxVolume;       // 专用 URP Global Volume（运行时开关）

    [Header("Transition Timing")]
    public float enterDuration = 0.10f;  // 进入时缓的实际秒数（快速冲击感）
    public float exitDuration  = 0.30f;  // 退出时缓的实际秒数（慢慢恢复）

    private ChromaticAberration _chroma;
    private Vignette            _vignette;
    private ColorAdjustments    _colorAdj;
    private Coroutine           _coroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (fxVolume != null && fxVolume.profile != null)
        {
            fxVolume.profile.TryGet(out _chroma);
            fxVolume.profile.TryGet(out _vignette);
            fxVolume.profile.TryGet(out _colorAdj);
        }

        SetOverlays(0f, Color.clear);
        SetPostFX(0f);
        if (fxVolume != null) fxVolume.enabled = false;
    }

    // ── 技能激活时调用 ─────────────────────────────────────────────────
    public void Activate(float targetTimeScale)
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(EnterRoutine(targetTimeScale));
    }

    // ── 技能发射/取消时调用 ────────────────────────────────────────────
    public void Deactivate()
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(ExitRoutine());
    }

    // ── 立即强制恢复（游戏重置时使用） ───────────────────────────────────
    public void ForceRestore()
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        SetPostFX(0f);
        SetOverlays(0f, Color.clear);
        if (fxVolume != null) fxVolume.enabled = false;
    }

    // ── 进入时缓动画 ──────────────────────────────────────────────────
    private IEnumerator EnterRoutine(float targetScale)
    {
        if (fxVolume != null) fxVolume.enabled = true;

        // ① 瞬间亮闪（使用非缩放时间，避免慢动作影响）
        if (flashOverlay != null)
        {
            flashOverlay.color = new Color(0.7f, 0.88f, 1f, 0.75f);
            float t = 0f;
            while (t < 0.08f)
            {
                t += Time.unscaledDeltaTime;
                flashOverlay.color = new Color(0.7f, 0.88f, 1f,
                    Mathf.Lerp(0.75f, 0f, t / 0.08f));
                yield return null;
            }
            flashOverlay.color = Color.clear;
        }

        // ② 时间倍率和视觉效果一起过渡（快速冲入）
        float startScale = Time.timeScale;
        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p      = Mathf.Clamp01(elapsed / enterDuration);
            float eased  = 1f - Mathf.Pow(1f - p, 3f);   // ease-out cubic：快速进入
            float curScale = Mathf.Lerp(startScale, targetScale, eased);
            Time.timeScale      = curScale;
            Time.fixedDeltaTime = 0.02f * curScale;
            SetPostFX(eased);
            SetOverlays(eased, new Color(0.02f, 0.04f, 0.18f, 0.52f * eased));
            yield return null;
        }

        Time.timeScale      = targetScale;
        Time.fixedDeltaTime = 0.02f * targetScale;
        SetPostFX(1f);
        SetOverlays(1f, new Color(0.02f, 0.04f, 0.18f, 0.52f));
    }

    // ── 退出时缓动画 ──────────────────────────────────────────────────
    private IEnumerator ExitRoutine()
    {
        float startScale = Time.timeScale;
        float elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p     = Mathf.Clamp01(elapsed / exitDuration);
            float eased = Mathf.Sqrt(p);                   // ease-in sqrt：开始快，收尾自然
            float curScale = Mathf.Lerp(startScale, 1f, eased);
            Time.timeScale      = curScale;
            Time.fixedDeltaTime = 0.02f * curScale;
            SetPostFX(1f - p);
            SetOverlays(1f - p, new Color(0.02f, 0.04f, 0.18f, 0.52f * (1f - p)));
            yield return null;
        }

        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        SetPostFX(0f);
        SetOverlays(0f, Color.clear);
        if (fxVolume != null) fxVolume.enabled = false;
    }

    // ── 设置 URP 后处理参数（t: 0=关闭, 1=最强） ─────────────────────
    private void SetPostFX(float t)
    {
        if (_chroma   != null) _chroma.intensity.Override(Mathf.Lerp(0f, 0.45f, t));
        if (_vignette != null) _vignette.intensity.Override(Mathf.Lerp(0f, 0.48f, t));
        if (_colorAdj != null) _colorAdj.saturation.Override(Mathf.Lerp(0f, -45f, t));
    }

    // ── 设置画布覆盖层透明度 ───────────────────────────────────────────
    private void SetOverlays(float t, Color tintColor)
    {
        if (tintOverlay != null) tintOverlay.color = tintColor;
    }
}
