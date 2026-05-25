using System.Collections;
using UnityEngine;

/// <summary>
/// 能量炮台：球碰到后短暂吸附，然后发射 bulletCount 颗自导弹，再弹出球。
/// 5 秒冷却期间炮台变暗，充能完毕时脉冲动画。
/// 需要此 GameObject 有 Collider2D（设为 IsTrigger=true）。
/// </summary>
public class EnergyCannon : MonoBehaviour
{
    [Header("Cannon Settings")]
    public int   bulletCount     = 3;
    public float cooldown        = 5f;
    public float captureHold     = 0.15f;   // 吸附停顿秒数
    [Tooltip("弹出球的速度（吸附后给球的初速）")]
    public float releaseSpeed    = 12f;
    [Tooltip("弹出方向（世界空间，不设置则每次随机斜角弹出）")]
    public Vector2 releaseDir    = Vector2.zero; // zero = 运行时随机左/右斜角

    [Header("Bullet Settings")]
    public float bulletSpeed     = 13f;
    public float bulletTurnRate  = 240f;
    public float bulletLifetime  = 4f;

    [Header("Visual")]
    public Color activeColor   = new Color(2f, 0.8f, 0f, 1f);  // HDR 橙，激活闪光
    public Color cooldownColor = new Color(0.3f, 0.2f, 0f, 1f); // 冷却暗色
    public Color readyColor    = new Color(1.2f, 0.6f, 0f, 1f); // 待机橙
    public float pulsePeriod   = 0.5f;   // 充能完毕后的脉冲周期

    // ──────────────────────────────────────────────────────
    private bool                  _onCooldown;
    private float                 _cooldownTimer;
    private SpriteRenderer        _sr;
    private MaterialPropertyBlock _mpb;
    private float                 _pulseTimer;
    private bool                  _pulsing;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        SetColor(readyColor);
        _pulsing = true;
    }

    private void Update()
    {
        if (_onCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _onCooldown = false;
                _pulsing    = true;
                StartCoroutine(ReadyPulse());
            }
        }

        if (_pulsing && !_onCooldown)
        {
            _pulseTimer += Time.deltaTime;
            float t = (Mathf.Sin(_pulseTimer * Mathf.PI * 2f / pulsePeriod) + 1f) * 0.5f;
            SetColor(Color.Lerp(readyColor * 0.6f, readyColor, t));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_onCooldown) return;
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        StartCoroutine(FireRoutine(ball));
    }

    private IEnumerator FireRoutine(BallController ball)
    {
        _onCooldown    = true;
        _cooldownTimer = cooldown;
        _pulsing       = false;

        // ── 吸附：停球，移到炮台中心 ─────────────────────
        var rb = ball.Rb;
        rb.velocity    = Vector2.zero;
        rb.isKinematic = true;
        ball.transform.position = transform.position;

        SetColor(activeColor);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);

        yield return new WaitForSeconds(captureHold);

        // ── 发射追踪弹 ───────────────────────────────────
        for (int i = 0; i < bulletCount; i++)
        {
            SpawnBullet();
            yield return new WaitForSeconds(0.08f); // 三颗之间微小间隔，视觉更好看
        }

        // ── 弹出球：随机左/右斜角，避开正轴线 Bumper ──────────
        rb.isKinematic = false;
        Vector2 dir = releaseDir.sqrMagnitude > 0.01f
            ? releaseDir.normalized
            : (Random.value > 0.5f ? new Vector2(0.6f, -1f).normalized : new Vector2(-0.6f, -1f).normalized);
        rb.velocity = dir * releaseSpeed;

        AudioManager.Instance?.PlayBounce();
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(transform.position, activeColor, 1.4f);

        // ── 进入冷却显示 ──────────────────────────────────
        SetColor(cooldownColor);
    }

    private void SpawnBullet()
    {
        // ── 彻底修复 ──：直接创建干净的 2D GameObject，绝不使用 3D Sphere 以防 MeshFilter 冲突崩溃
        var go = new GameObject("HomingBullet");
        go.transform.position   = transform.position;
        go.transform.localScale = Vector3.one * 0.45f; // 可见尺寸

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = CreateCircleSprite(32, activeColor);
        sr.material     = new Material(Shader.Find("Sprites/Default")); // Unlit：保证亮度不受场景光照影响
        sr.color        = activeColor;
        sr.sortingOrder = 6;

        var col2d       = go.AddComponent<CircleCollider2D>();
        col2d.radius    = 0.11f;
        col2d.isTrigger = true;

        var bullet             = go.AddComponent<HomingBullet>();
        bullet.speed           = bulletSpeed;
        bullet.turnRate        = bulletTurnRate;
        bullet.lifetime        = bulletLifetime;
        bullet.bulletColor     = activeColor;
    }

    private IEnumerator ReadyPulse()
    {
        // 充能完毕：一次明亮脉冲提示
        SetColor(activeColor * 1.5f);
        yield return new WaitForSeconds(0.15f);
        SetColor(readyColor);
    }

    private void SetColor(Color c)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _sr.SetPropertyBlock(_mpb);
    }

    // 运行时生成小圆形 Sprite，避免依赖 Asset
    private static Sprite CreateCircleSprite(int size, Color color)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        float r    = half - 1f;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float dx   = (i % size) - half + 0.5f;
            float dy   = (i / size) - half + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            pixels[i]  = dist <= r ? color : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.3f);
    }
}
