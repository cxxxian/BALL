using System.Collections;
using UnityEngine;

/// <summary>
/// 弹簧板（Kickback）：放置在挡板两侧的侧道。
/// 球滑入时自动弹出，方向可配置（默认斜上方）。
/// 每波次限用 1 次，新波次开始时自动充能。
/// 需要此 GameObject 有 Collider2D（设为 IsTrigger=true）。
/// </summary>
public class SpringBoard : MonoBehaviour
{
    [Header("Kick Settings")]
    [Tooltip("弹出方向（世界空间，会自动归一化）")]
    public Vector2 launchDirection = new Vector2(0.6f, 1f);
    [Tooltip("弹出速度（绝对值，不受入射速度影响，保证救球有效）")]
    public float launchSpeed = 16f;

    [Header("Per-Wave Charge")]
    [Tooltip("每波刷新后的可用次数")]
    public int chargesPerWave = 1;

    [Header("Visual")]
    public Color chargedColor   = new Color(0f, 2f, 0.6f, 1f);   // 充能：亮绿 HDR
    public Color depletedColor  = new Color(0.1f, 0.25f, 0.15f, 1f); // 耗尽：暗绿
    public float squeezeAmount  = 0.55f;    // 弹射时挤压缩放比例
    public float restoreSpeed   = 10f;

    // ──────────────────────────────────────────────────────
    private int              _charges;
    private SpriteRenderer   _sr;
    private MaterialPropertyBlock _mpb;
    private Vector3          _originalScale;
    private bool             _squeezing;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr            = GetComponentInChildren<SpriteRenderer>();
        _mpb           = new MaterialPropertyBlock();
        _originalScale = transform.localScale;
    }

    private void Start()
    {
        _charges = chargesPerWave;
        RefreshVisual();

        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.AddListener(OnWaveStart);
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.RemoveListener(OnWaveStart);
    }

    private void OnWaveStart(int _)
    {
        _charges = chargesPerWave;
        RefreshVisual();
    }

    // ──────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        if (_charges > 0)
        {
            _charges--;
            Kick(ball);
        }
        // 耗尽时：不干预物理，球正常弹开或落底
    }

    private void Kick(BallController ball)
    {
        ball.Rb.velocity = launchDirection.normalized * launchSpeed;

        // 特效
        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);

        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(ball.transform.position, chargedColor, 1.3f);

        RefreshVisual();
        StartCoroutine(SqueezeAnim());
    }

    // ── 视觉 ──────────────────────────────────────────────
    private void RefreshVisual()
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", _charges > 0 ? chargedColor : depletedColor);
        _sr.SetPropertyBlock(_mpb);
    }

    private IEnumerator SqueezeAnim()
    {
        _squeezing = true;
        // 沿弹射方向横向挤压
        Vector3 squeezed = new Vector3(
            _originalScale.x * (1f + (1f - squeezeAmount) * Mathf.Abs(launchDirection.normalized.y)),
            _originalScale.y * squeezeAmount,
            _originalScale.z);
        transform.localScale = squeezed;
        _squeezing = false;
        yield break;
    }

    private void Update()
    {
        if (_squeezing) return;
        if (transform.localScale != _originalScale)
            transform.localScale = Vector3.Lerp(transform.localScale, _originalScale, Time.deltaTime * restoreSpeed);
    }
}
