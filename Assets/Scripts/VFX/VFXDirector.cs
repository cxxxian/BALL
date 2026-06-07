using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// VFX 总控：Boss 击杀时触发完整特效组合（顿帧 + 暗角 + 空间扭曲 + 定向色散）
/// </summary>
public class VFXDirector : MonoBehaviour
{
    public static VFXDirector Instance { get; private set; }
    public static bool IsChromaticAberrationActive { get; private set; }
    public static bool IsEffectPlaying => Instance != null && Instance._effectActive;

    [Header("Hit Stop Settings")]
    public float hitStopDuration = 0.1f;
    public float hitStopTimeScale = 0.02f;

    [Header("Vignette Settings")]
    public Color vignetteColor = new Color(0f, 0f, 0f, 0.8f);
    
    [Header("Space Distortion Settings")]
    public Material distortionMaterial;
    public float distortionMaxRadius = 3f;
    public AnimationCurve distortionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Post Effect Hold")]
    [Tooltip("Boss 击杀特效全部播完后，额外停留秒数，再允许 Buff 选择界面出现")]
    public float postEffectHoldDuration = 0.5f;

    [Header("Chromatic Aberration")]
    public float chromaticAberrationDuration = 0.5f;

    private Vignette _vignette;
    private bool _effectActive = false;
    private float _lastTriggerTime = -1f;
    private const float TRIGGER_COOLDOWN = 0.3f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        CacheVignette();
    }

    private void CacheVignette()
    {
        _vignette = null;

        if (SlowMoFX.Instance != null && SlowMoFX.Instance.fxVolume != null)
        {
            SlowMoFX.Instance.fxVolume.profile?.TryGet(out _vignette);
            return;
        }

        var volume = GameObject.Find("SlowMoFX_Volume")?.GetComponent<Volume>();
        volume?.profile?.TryGet(out _vignette);
    }

    /// <summary>
    /// 触发 Boss 击杀完整特效（仅应由 EnemyBase 在 Boss 球击杀时调用）
    /// </summary>
    public void TriggerBossKillEffect(Vector3 worldPosition)
    {
        if (_effectActive || Time.unscaledTime - _lastTriggerTime < TRIGGER_COOLDOWN) return;
        _lastTriggerTime = Time.unscaledTime;

        StartCoroutine(BossKillEffectRoutine(worldPosition));
    }

    /// <summary>
    /// 等待当前 Boss 击杀特效序列结束（WaveManager 在弹出 Buff 界面前调用）
    /// </summary>
    public IEnumerator WaitForEffectComplete()
    {
        while (_effectActive)
            yield return null;
    }

    private IEnumerator BossKillEffectRoutine(Vector3 worldPosition)
    {
        _effectActive = true;
        IsChromaticAberrationActive = false;

        // T=0.00s: 顿帧 + 暗角 + 生成扭曲球
        Time.timeScale = hitStopTimeScale;
        SetVignetteIntensity(vignetteColor.a);

        GameObject distortionObj = null;
        if (distortionMaterial != null)
        {
            distortionObj = CreateDistortionSphere(worldPosition);
        }

        // T=0.05s: 扭曲球膨胀
        float elapsed = 0f;
        float expandDuration = hitStopDuration * 0.5f;
        while (elapsed < expandDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / expandDuration;
            if (distortionObj != null)
            {
                float radius = Mathf.Lerp(0f, distortionMaxRadius, distortionCurve.Evaluate(t));
                distortionObj.transform.localScale = Vector3.one * radius;
            }
            yield return null;
        }

        // T=0.10s: 恢复时间 + 清除暗角
        yield return new WaitForSecondsRealtime(hitStopDuration * 0.5f);

        Time.timeScale = 1.0f;
        SetVignetteIntensity(0f);

        // 扭曲球快速炸开消失
        if (distortionObj != null)
        {
            float fadeTime = 0.1f;
            float fadeElapsed = 0f;
            Vector3 startScale = distortionObj.transform.localScale;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = fadeElapsed / fadeTime;
                distortionObj.transform.localScale = startScale * (1f + t * 2f);
                yield return null;
            }
            Destroy(distortionObj);
        }

        // T=0.10s+: 定向色散（仅 Boss 击杀窗口）
        yield return ChromaticAberrationDurationRoutine(chromaticAberrationDuration);

        // 额外停留，突出 Boss 击杀反馈
        if (postEffectHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(postEffectHoldDuration);

        _effectActive = false;
    }

    private GameObject CreateDistortionSphere(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "SpaceDistortion";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.zero;
        
        Destroy(sphere.GetComponent<Collider>());
        
        var renderer = sphere.GetComponent<Renderer>();
        if (distortionMaterial != null)
            renderer.material = distortionMaterial;
        renderer.sortingOrder = 10;
        
        return sphere;
    }

    private void SetVignetteIntensity(float intensity)
    {
        if (_vignette == null)
            CacheVignette();

        if (SlowMoFX.Instance != null)
        {
            SlowMoFX.Instance.SetBossKillVignette(intensity, vignetteColor);
            return;
        }

        if (_vignette == null) return;

        _vignette.intensity.Override(Mathf.Clamp01(intensity));
        _vignette.color.Override(new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 1f));
    }

    private IEnumerator ChromaticAberrationDurationRoutine(float duration)
    {
        IsChromaticAberrationActive = true;
        yield return new WaitForSecondsRealtime(duration);
        IsChromaticAberrationActive = false;
    }

    private void OnDestroy()
    {
        IsChromaticAberrationActive = false;
        SetVignetteIntensity(0f);

        if (Time.timeScale != 1.0f)
            Time.timeScale = 1.0f;
    }
}
