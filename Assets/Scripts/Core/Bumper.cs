using System.Collections;
using UnityEngine;

public class Bumper : MonoBehaviour
{
    [Header("Settings")]
    public int scoreOnHit = 15;
    public float pushForce = 12f;

    [Header("Hit Juice")]
    [Tooltip("霓虹 HitFlash 峰值（喂 SpriteNeonHDR._HitFlash）")]
    public float flashPeak = 1f;
    [Tooltip("闪光衰减时长（秒）")]
    public float flashDuration = 0.14f;
    [Tooltip("撞击瞬间收缩比例")]
    [Range(0.6f, 1f)]
    public float squashScale = 0.82f;
    [Tooltip("回弹过冲比例")]
    [Range(1f, 1.35f)]
    public float popScale = 1.12f;
    [Tooltip("收缩耗时")]
    public float squashIn = 0.035f;
    [Tooltip("弹出过冲耗时")]
    public float popOut = 0.055f;
    [Tooltip("回落到原尺寸耗时")]
    public float settle = 0.09f;

    private SpriteRenderer _sr;
    private SpriteRenderer _glowSR;
    private Collider2D     _col;
    private Transform      _visual;
    private Vector3        _visualBaseScale = Vector3.one;
    private Vector3        _glowBaseScale;
    private Color          _glowBaseColor;
    private Color _baseColor;
    private bool _flashing   = false;
    private bool _disabled   = false;
    private bool _passthrough = false;

    private MaterialPropertyBlock _mpb;
    private float _flashValue;

    private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");

    // ── 碰撞冷却（防穿模抖动） ─────────────────────────────────────────
    private float _lastHitTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;

    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _col = GetComponent<Collider2D>();
        _mpb = new MaterialPropertyBlock();
        _visual = _sr != null ? _sr.transform : transform;
        _visualBaseScale = _visual.localScale;
        RefreshFromPalette();
        ApplyHitFlash(0f);

        // 弹开完全由脚本控速；材质弹力 >1 会在冷却帧/二次接触时偷偷加能量，诱发高频抖。
        if (_col != null)
        {
            var mat = new PhysicsMaterial2D("BumperScripted")
            {
                friction = 0f,
                bounciness = 0f
            };
            _col.sharedMaterial = mat;
        }

        var glowT = transform.Find("Glow");
        if (glowT != null)
        {
            _glowSR        = glowT.GetComponent<SpriteRenderer>();
            _glowBaseScale = glowT.localScale;
            _glowBaseColor = _glowSR != null ? _glowSR.color : Color.white;
        }
    }

    // 斩杀连锁期间调用：碰撞体关闭，弹珠完全穿透，同时视觉暗化提示
    public void SetPassthrough(bool passthrough)
    {
        _passthrough = passthrough;
        if (_col != null) _col.enabled = !passthrough;

        if (_sr != null)
            _sr.color = passthrough
                ? (UsesTableArt ? new Color(0.2f, 0.2f, 0.2f, 1f) : NeonPalette.Dim(_baseColor, 0.15f))
                : ResolveDisplayColor();
        if (_glowSR != null) _glowSR.color = passthrough
            ? new Color(_glowBaseColor.r, _glowBaseColor.g, _glowBaseColor.b, 0.08f)
            : _glowBaseColor;

        if (passthrough)
        {
            _flashValue = 0f;
            ApplyHitFlash(0f);
            if (_visual != null) _visual.localScale = _visualBaseScale;
        }
    }

    public void SetDisabled(bool disabled)
    {
        _disabled = disabled;
        if (disabled)
        {
            StopAllCoroutines();
            _flashing = false;
            _flashValue = 0f;
            ApplyHitFlash(0f);
            if (_visual != null) _visual.localScale = _visualBaseScale;
            if (_glowSR != null)
            {
                _glowSR.transform.localScale = _glowBaseScale;
                _glowSR.color = _glowBaseColor;
            }
        }
        if (_sr != null) _sr.color = ResolveDisplayColor();
    }

    public void RefreshFromPalette()
    {
        _baseColor = NeonColors.Active.GetBase(NeonRole.Bumper);
        if (_sr != null && !_flashing && !_passthrough)
            _sr.color = ResolveDisplayColor();
    }

    private bool UsesTableArt =>
        _sr != null && _sr.sprite != null && _sr.sprite.name == "bumper_round";

    private Color ResolveDisplayColor()
    {
        if (UsesTableArt)
        {
            // 贴图自带配色；禁用时压暗，正常保持白 tint 喂 SpriteNeonHDR
            return _disabled ? new Color(0.28f, 0.28f, 0.28f, 1f) : Color.white;
        }
        return _disabled ? NeonPalette.Dim(_baseColor, 0.25f) : _baseColor;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_disabled) return;
        if (!col.gameObject.CompareTag("Ball")) return;

        // ── 碰撞冷却检测：防止穿模抖动 ─────────────────────────────────────
        float currentTime = Time.time;
        if (currentTime - _lastHitTime < COLLISION_COOLDOWN) return;
        _lastHitTime = currentTime;

        AudioManager.Instance?.PlayBounce();

        var rb = col.rigidbody;
        if (rb != null)
        {
            Vector2 dir = (col.transform.position - transform.position).normalized;
            float force = pushForce;
            if (ProtocolFieldDirector.Instance != null)
                force *= ProtocolFieldDirector.Instance.OverloadBumperForceMult;
            rb.velocity = dir * force;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ComboSystem.Instance?.RegisterAirtimeHit(hitPos);
        float speed = rb != null ? rb.velocity.magnitude : 8f;
        JuiceRouter.Play(JuiceRouter.Tier.Hit, hitPos, _baseColor, speed, applyHitStop: false);

        OnBallHit?.Invoke();

        if (!_flashing) StartCoroutine(HitJuice());
        else
        {
            // 连击时再顶一波闪光，不打断收缩动画
            _flashValue = Mathf.Max(_flashValue, flashPeak);
            ApplyHitFlash(_flashValue);
        }
    }

    /// <summary>任意 Bumper 被弹珠击中时广播（教学用）。</summary>
    public static event System.Action OnBallHit;

    private IEnumerator HitJuice()
    {
        _flashing = true;
        var palette = NeonColors.Active;

        _flashValue = flashPeak;
        ApplyHitFlash(_flashValue);

        if (_sr != null && !UsesTableArt)
            _sr.color = palette.GetFlash(NeonRole.Bumper);

        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale * 2.4f;
            _glowSR.color = palette.GetBumperFlashGlow();
        }

        // 收缩 → 过冲弹出 → 回落；闪光在弹出阶段保持高亮，回落时衰减
        yield return AnimateScale(_visualBaseScale * squashScale, squashIn, holdFlash: true);
        yield return AnimateScale(_visualBaseScale * popScale, popOut, holdFlash: true);
        yield return AnimateScale(_visualBaseScale, settle, holdFlash: false);

        float t = 0f;
        float startFlash = _flashValue;
        float trail = Mathf.Max(0.04f, flashDuration * 0.45f);
        while (t < trail)
        {
            t += Time.deltaTime;
            _flashValue = Mathf.Lerp(startFlash, 0f, t / trail);
            ApplyHitFlash(_flashValue);
            yield return null;
        }

        _flashValue = 0f;
        ApplyHitFlash(0f);
        if (_visual != null) _visual.localScale = _visualBaseScale;

        if (_sr != null) _sr.color = ResolveDisplayColor();
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale;
            _glowSR.color = _glowBaseColor;
        }
        _flashing = false;
    }

    private IEnumerator AnimateScale(Vector3 target, float duration, bool holdFlash)
    {
        if (_visual == null || duration <= 0.0001f)
        {
            if (_visual != null) _visual.localScale = target;
            yield break;
        }

        Vector3 from = _visual.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            u = u * u * (3f - 2f * u);
            _visual.localScale = Vector3.LerpUnclamped(from, target, u);

            if (holdFlash)
                _flashValue = Mathf.Max(_flashValue, flashPeak * 0.85f);
            else
                _flashValue = Mathf.Max(0f, _flashValue - Time.deltaTime / Mathf.Max(flashDuration, 0.01f));
            ApplyHitFlash(_flashValue);
            yield return null;
        }
        _visual.localScale = target;
    }

    private void ApplyHitFlash(float value)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HitFlashID, Mathf.Clamp01(value));
        _sr.SetPropertyBlock(_mpb);
    }
}
