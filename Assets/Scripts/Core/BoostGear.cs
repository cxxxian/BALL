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
    [Tooltip("速度提升百分比（0.5 = +50%）")]
    public float speedBoostPercent = 0.5f;
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
    private Color                 _savedTrailStart = Color.white;
    private Color                 _savedTrailEnd   = Color.clear;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr                = GetComponentInChildren<SpriteRenderer>();
        _mpb               = new MaterialPropertyBlock();
        _currentRotSpeed   = idleRotationSpeed;
    }

    private void Update()
    {
        transform.Rotate(Vector3.forward, _currentRotSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        // 如果已在加速中，重启计时
        if (_boostRoutine != null) StopCoroutine(_boostRoutine);
        _boostRoutine = StartCoroutine(BoostRoutine(ball));
    }

    private IEnumerator BoostRoutine(BallController ball)
    {
        float multiplier = 1f + speedBoostPercent;

        // ── 激活 ──────────────────────────────────────────
        _currentRotSpeed = activeRotationSpeed;
        SetGearColor(gearActiveColor);

        // 只在非加速状态时保存原始颜色：防止球再次触碰齿轮时把金色存为"原始色"
        var trail = ball.GetComponent<TrailRenderer>();
        if (!_isBoosting && trail != null)
        {
            _savedTrailStart = trail.startColor;
            _savedTrailEnd   = trail.endColor;
        }
        _isBoosting = true;

        if (trail != null)
        {
            trail.startColor = boostTrailColor;
            trail.endColor   = new Color(boostTrailColor.r, boostTrailColor.g, boostTrailColor.b, 0.05f);
        }

        // 立即提速：先设倍率，再推一把当前速度
        ball.SpeedMultiplier = multiplier;
        if (ball.Rb.velocity.sqrMagnitude > 0.01f)
            ball.Rb.velocity = ball.Rb.velocity.normalized * (ball.Rb.velocity.magnitude * multiplier);

        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(ball.transform.position, boostTrailColor, 0.9f);

        // ── 等待 duration 秒 ────────────────────────────────
        yield return new WaitForSeconds(duration);

        // ── 还原 ──────────────────────────────────────────
        _currentRotSpeed = idleRotationSpeed;
        SetGearColor(Color.white);
        _isBoosting = false;

        if (ball != null)
        {
            ball.SpeedMultiplier = 1f;
            if (trail != null)
            {
                trail.startColor = _savedTrailStart;
                trail.endColor   = _savedTrailEnd;
            }
        }

        _boostRoutine = null;
    }

    private void SetGearColor(Color c)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _sr.SetPropertyBlock(_mpb);
    }
}
