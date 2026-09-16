using System.Collections;
using UnityEngine;

/// <summary>
/// 加速齿轮：球经过时速度提升 speedBoostPercent（默认50%），持续 duration 秒。
/// 加速期间拖尾变金色；还原时同步复原。
/// 齿轮自身持续缓慢旋转，激活时高速旋转一下。
/// 需要此 GameObject 有 Collider2D（设为 IsTrigger=true）。
/// </summary>
public class BoostGear : MonoBehaviour
{
    [Header("Boost Settings")]
    [Tooltip("速度提升百分比（0.28 = +28%；再受 ballHardMaxSpeed 硬顶）")]
    public float speedBoostPercent = 0.28f;
    [Tooltip("加速持续秒数")]
    public float duration = 2.0f;

    [Header("Rotation")]
    public float idleRotationSpeed   = 90f;   // 闲置旋转速度 (度/秒)
    public float activeRotationSpeed = 540f;  // 激活时飞速旋转

    [Header("Visual")]
    public Color boostTrailColor  = new Color(1f, 0.85f, 0f, 1f); // 亮金色
    public Color gearActiveColor  = new Color(2f, 1.7f, 0f, 1f);  // HDR 高亮黄

    // ──────────────────────────────────────────────────────
    private SpriteRenderer        _sr;
    private MaterialPropertyBlock _mpb;
    private float                 _currentRotSpeed;
    private Coroutine             _boostRoutine;
    private bool                  _isBoosting;
    private float                 _flashValue;
    private float                 _flashHold;

    private static readonly int HitFlashID   = Shader.PropertyToID("_HitFlash");
    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");

    private static readonly Color GearFlashColor = new Color(2.8f, 2.05f, 0.4f, 1f);

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr = ResolveVisualRenderer();
        _mpb             = new MaterialPropertyBlock();
        _currentRotSpeed = idleRotationSpeed;
        ApplyHitFlash(0f);
    }

    private SpriteRenderer ResolveVisualRenderer()
    {
        var visualT = transform.Find("Visual");
        if (visualT != null)
        {
            var vsr = visualT.GetComponent<SpriteRenderer>();
            if (vsr != null) return vsr;
        }

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null && srs[i].enabled && srs[i].sprite != null)
                return srs[i];
        }
        return GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Update()
    {
        transform.Rotate(Vector3.forward, _currentRotSpeed * Time.deltaTime);

        if (_flashHold > 0f)
        {
            _flashHold -= Time.deltaTime;
            _flashValue = Mathf.Max(_flashValue, 1f);
        }
        else if (_flashValue > 0f)
            _flashValue = Mathf.Max(0f, _flashValue - Time.deltaTime / 0.16f);

        ApplyHitFlash(_flashValue);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        ComboSystem.Instance?.RegisterAirtimeHit(transform.position);

        // 如果已在加速中，重启计时
        if (_boostRoutine != null) StopCoroutine(_boostRoutine);
        _boostRoutine = StartCoroutine(BoostRoutine(ball));
    }

    private IEnumerator BoostRoutine(BallController ball)
    {
        float multiplier = 1f + speedBoostPercent;

        // ── 激活 ──────────────────────────────────────────
        _currentRotSpeed = activeRotationSpeed;
        _flashValue = 1f;
        _flashHold = 0.12f;
        ApplyHitFlash(_flashValue);

        // 只在非加速状态时保存原始颜色：防止球再次触碰齿轮时把金色存为"原始色"
        var trail = ball.GetComponent<TrailRenderer>();
        _isBoosting = true;
        ball.SetOverrideTrailColor(boostTrailColor, new Color(boostTrailColor.r, boostTrailColor.g, boostTrailColor.b, 0.05f));

        // 立即提速：先设倍率，再推一把，并钳到有效硬顶
        ball.SpeedMultiplier = multiplier;
        if (ball.Rb != null && ball.Rb.velocity.sqrMagnitude > 0.01f)
        {
            float boosted = ball.Rb.velocity.magnitude * multiplier;
            float cap = ball.EffectiveMaxSpeed;
            ball.Rb.velocity = ball.Rb.velocity.normalized * Mathf.Min(boosted, cap);
        }

        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(ball.transform.position, boostTrailColor, 0.9f);

        // ── 等待 duration 秒 ────────────────────────────────
        yield return new WaitForSeconds(duration);

        // ── 还原 ──────────────────────────────────────────
        _currentRotSpeed = idleRotationSpeed;
        _isBoosting = false;
        _flashHold = 0f;

        if (ball != null)
        {
            if (Mathf.Approximately(ball.SpeedMultiplier, multiplier))
            {
                ball.SpeedMultiplier = 1f;
                ball.ResetTrailColor();
                if (ball.Rb != null && ball.Rb.velocity.magnitude > ball.EffectiveMaxSpeed)
                    ball.Rb.velocity = ball.Rb.velocity.normalized * ball.EffectiveMaxSpeed;
            }
        }

        _boostRoutine = null;
    }

    private void ApplyHitFlash(float value)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HitFlashID, Mathf.Clamp01(value));
        _mpb.SetColor(FlashColorID, GearFlashColor);
        _sr.SetPropertyBlock(_mpb);
    }
}
