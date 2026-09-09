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
    private Coroutine           _flashCoroutine;
    private Coroutine           _enemyTimestopCoroutine;
    private bool                _slowMoHeld;
    private bool                _enemyTimestopHeld;
    private int                 _combatHitStopDepth;
    private Coroutine           _combatHitStopPulseCo;
    private float               _combatHitStopEndUnscaled;

    /// <summary>斩杀瞄准时缓是否占用 timeScale（含进入/退出过渡）。</summary>
    public bool IsSkillSlowMoActive => _slowMoHeld || _coroutine != null;

    /// <summary>战斗顿帧是否占用 timeScale（含脉冲倒计时）。</summary>
    public bool IsCombatHitStopActive => _combatHitStopDepth > 0;

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

        ClearVisualOverlays();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ForceRestore);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(ForceRestore);
    }

    // ── 技能激活时调用 ─────────────────────────────────────────────────
    public void Activate(float targetTimeScale)
    {
        // 斩杀瞄准接管时间：丢弃战斗顿帧计数，避免结束后误写回
        if (_combatHitStopPulseCo != null)
        {
            StopCoroutine(_combatHitStopPulseCo);
            _combatHitStopPulseCo = null;
        }
        _combatHitStopDepth = 0;
        _combatHitStopEndUnscaled = 0f;
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(EnterRoutine(targetTimeScale));
    }

    // ── 技能发射/取消时调用 ────────────────────────────────────────────
    public void Deactivate()
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(ExitRoutine());
    }

    /// <summary>清除全屏闪/tint/后处理，不改动 timeScale（拉霸界面用）。</summary>
    public void ClearVisualOverlays()
    {
        if (_coroutine != null)
        {
            StopCoroutine(_coroutine);
            _coroutine = null;
        }

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        if (_enemyTimestopCoroutine != null)
        {
            StopCoroutine(_enemyTimestopCoroutine);
            _enemyTimestopCoroutine = null;
        }

        _slowMoHeld = false;
        _enemyTimestopHeld = false;
        _combatHitStopDepth = 0;
        if (_combatHitStopPulseCo != null)
        {
            StopCoroutine(_combatHitStopPulseCo);
            _combatHitStopPulseCo = null;
        }
        _combatHitStopEndUnscaled = 0f;

        if (flashOverlay != null) flashOverlay.color = Color.clear;
        SetPostFX(0f);
        SetOverlays(0f, Color.clear);

        if (_vignette != null)
        {
            _vignette.intensity.Override(0f);
            _vignette.color.Override(Color.black);
        }

        if (fxVolume != null) fxVolume.enabled = false;
    }

    /// <summary>取消斩击瞄准时即时恢复，跳过 Deactivate 退出动画（避免闪屏）。</summary>
    public void CancelSkillAim()
    {
        ClearVisualOverlays();
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    /// <summary>敌人时间减速：仅视觉 tint/vignette，不改 timeScale（与斩杀瞄准区分）。</summary>
    public void ActivateEnemyTimestopVisual()
    {
        if (_slowMoHeld) return;
        if (_enemyTimestopCoroutine != null) StopCoroutine(_enemyTimestopCoroutine);
        _enemyTimestopCoroutine = StartCoroutine(EnemyTimestopEnterRoutine());
    }

    public void DeactivateEnemyTimestopVisual()
    {
        if (!_enemyTimestopHeld && _enemyTimestopCoroutine == null) return;
        if (_enemyTimestopCoroutine != null) StopCoroutine(_enemyTimestopCoroutine);
        _enemyTimestopCoroutine = StartCoroutine(EnemyTimestopExitRoutine());
    }

    private IEnumerator EnemyTimestopEnterRoutine()
    {
        if (fxVolume != null) fxVolume.enabled = true;

        var tint = new Color(0.03f, 0.14f, 0.22f, 0.38f);
        float elapsed = 0f;
        const float dur = 0.12f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            SetOverlays(t, tint);
            if (_vignette != null)
            {
                _vignette.intensity.Override(Mathf.Lerp(0f, 0.22f, t));
                _vignette.color.Override(new Color(0.45f, 0.78f, 1f, 1f));
            }
            yield return null;
        }

        _enemyTimestopHeld = true;
        _enemyTimestopCoroutine = null;
    }

    private IEnumerator EnemyTimestopExitRoutine()
    {
        var tint = new Color(0.03f, 0.14f, 0.22f, 0.38f);
        float elapsed = 0f;
        const float dur = 0.25f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / dur);
            SetOverlays(t, new Color(tint.r, tint.g, tint.b, tint.a * t));
            if (_vignette != null) _vignette.intensity.Override(0.22f * t);
            yield return null;
        }

        _enemyTimestopHeld = false;
        ClearEnemyTimestopVisualOnly();
        _enemyTimestopCoroutine = null;
    }

    private void ClearEnemyTimestopVisualOnly()
    {
        if (_slowMoHeld) return;
        SetOverlays(0f, Color.clear);
        if (_vignette != null)
        {
            _vignette.intensity.Override(0f);
            _vignette.color.Override(Color.black);
        }
        if (fxVolume != null && !_slowMoHeld && _coroutine == null)
            fxVolume.enabled = false;
    }

    // ── 立即强制恢复（游戏重置时使用） ───────────────────────────────────
    public void ForceRestore()
    {
        ClearVisualOverlays();
        _combatHitStopDepth = 0;
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    /// <summary>
    /// 战斗顿帧（导弹弹刀等）。与 Slash 瞄准时缓互斥：瞄准中直接跳过，避免互相写坏 timeScale。
    /// 返回是否实际持有顿帧（调用方须成对 EndCombatHitStop）。
    /// </summary>
    public bool BeginCombatHitStop(float stopScale)
    {
        if (!CanStartCombatHitStop()) return false;

        stopScale = Mathf.Clamp(stopScale, 0.02f, 1f);
        if (_combatHitStopDepth == 0 || Time.timeScale > stopScale)
            Time.timeScale = stopScale;
        _combatHitStopDepth++;
        return true;
    }

    public void EndCombatHitStop()
    {
        if (_combatHitStopDepth <= 0) return;
        _combatHitStopDepth--;
        if (_combatHitStopDepth > 0) return;

        // Slash 已接管则交还，勿强行拉回 1
        if (IsSkillSlowMoActive) return;
        if (SkillManager.Instance != null && SkillManager.Instance.IsAiming) return;

        RestoreTimeScaleAfterCombatHitStop();
    }

    /// <summary>
    /// 自动结束的顿帧脉冲。重叠时取更深 scale、更晚结束时刻（不叠加 depth）。
    /// </summary>
    public void PulseCombatHitStop(float stopScale, float unscaledSeconds)
    {
        if (!CanStartCombatHitStop() && _combatHitStopDepth <= 0) return;

        stopScale = Mathf.Clamp(stopScale, 0.02f, 1f);
        unscaledSeconds = Mathf.Clamp(unscaledSeconds, 0.01f, 0.25f);
        float endAt = Time.unscaledTime + unscaledSeconds;

        if (_combatHitStopDepth > 0)
        {
            if (Time.timeScale > stopScale)
                Time.timeScale = stopScale;
            if (endAt > _combatHitStopEndUnscaled)
                _combatHitStopEndUnscaled = endAt;
            return;
        }

        if (!BeginCombatHitStop(stopScale)) return;
        _combatHitStopEndUnscaled = endAt;
        if (_combatHitStopPulseCo != null)
            StopCoroutine(_combatHitStopPulseCo);
        _combatHitStopPulseCo = StartCoroutine(CombatHitStopPulseRoutine());
    }

    private IEnumerator CombatHitStopPulseRoutine()
    {
        while (Time.unscaledTime < _combatHitStopEndUnscaled && _combatHitStopDepth > 0)
            yield return null;

        _combatHitStopPulseCo = null;
        if (_combatHitStopDepth > 0)
            EndCombatHitStop();
    }

    private bool CanStartCombatHitStop()
    {
        if (IsSkillSlowMoActive) return false;
        if (SkillManager.Instance != null && SkillManager.Instance.IsAiming) return false;
        if (TutorialTimeControl.Mode == TutorialTimeMode.HardPause) return false;
        if (TutorialTimeControl.Mode == TutorialTimeMode.SoftFreeze) return false;
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.BuffSelection)
            return false;
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsOpen)
            return false;
        return true;
    }

    private void RestoreTimeScaleAfterCombatHitStop()
    {
        // 教程 SlowMo：交还给校准态，避免强行拉回 1
        if (RunSession.IsTutorial && TutorialTimeControl.Mode == TutorialTimeMode.SlowMo)
        {
            Time.timeScale = TutorialTimeControl.SlowMoScale;
            Time.fixedDeltaTime = 0.02f * TutorialTimeControl.SlowMoScale;
            return;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    // ── 进入时缓动画 ──────────────────────────────────────────────────
    private IEnumerator EnterRoutine(float targetScale)
    {
        // 进入 Slash 时清掉战斗顿帧归属，避免结束后误恢复
        _combatHitStopDepth = 0;

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
        _slowMoHeld = true;
        _coroutine = null;
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

        ForceRestore();
        _coroutine = null;
    }

    /// <summary>短促全屏闪色（Boss 二阶段等警告用，不改动时间缩放）。</summary>
    public void PulseFlash(Color color, float peakAlpha = 0.55f, float duration = 0.14f)
    {
        if (flashOverlay == null) return;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(PulseFlashRoutine(color, peakAlpha, duration));
    }

    private IEnumerator PulseFlashRoutine(Color color, float peakAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float a = peakAlpha * (1f - p * p);
            flashOverlay.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        flashOverlay.color = Color.clear;
        _flashCoroutine = null;
    }

    /// <summary>
    /// Boss 击杀等外部事件：仅驱动 Vignette，不改动色差/去饱和。
    /// </summary>
    public void SetBossKillVignette(float intensity, Color color)
    {
        if (_vignette == null) return;

        if (fxVolume != null && intensity > 0f)
            fxVolume.enabled = true;

        _vignette.intensity.Override(Mathf.Clamp01(intensity));
        _vignette.color.Override(new Color(color.r, color.g, color.b, 1f));

        if (intensity <= 0f && !_slowMoHeld && _coroutine == null && fxVolume != null)
            fxVolume.enabled = false;
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
