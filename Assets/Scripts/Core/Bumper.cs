using System.Collections;
using UnityEngine;

public class Bumper : MonoBehaviour
{
    [Header("Settings")]
    public int scoreOnHit = 100;
    public float pushForce = 12f;
    public float flashDuration = 0.12f;

    private SpriteRenderer _sr;
    private SpriteRenderer _glowSR;
    private Collider2D     _col;
    private Vector3        _glowBaseScale;
    private Color          _glowBaseColor;
    private Color _baseColor;
    private bool _flashing   = false;
    private bool _disabled   = false;
    private bool _passthrough = false;

    // ── 碰撞冷却（防穿模抖动） ─────────────────────────────────────────
    private float _lastHitTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;

    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _col = GetComponent<Collider2D>();
        RefreshFromPalette();

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

        Color dim = NeonPalette.Dim(_baseColor, 0.15f);
        if (_sr != null) _sr.color = passthrough ? dim : (_disabled
            ? NeonPalette.Dim(_baseColor, 0.25f)
            : _baseColor);
        if (_glowSR != null) _glowSR.color = passthrough
            ? new Color(_glowBaseColor.r, _glowBaseColor.g, _glowBaseColor.b, 0.08f)
            : _glowBaseColor;
    }

    public void SetDisabled(bool disabled)
    {
        _disabled = disabled;
        if (disabled)
        {
            StopAllCoroutines(); // 彻底停止闪烁协程，防止它在 yield 结束后覆盖颜色
            _flashing = false;
            if (_glowSR != null)
            {
                _glowSR.transform.localScale = _glowBaseScale;
                _glowSR.color = _glowBaseColor;
            }
        }
        if (_sr != null) _sr.color = disabled ? NeonPalette.Dim(_baseColor, 0.25f) : _baseColor;
    }

    public void RefreshFromPalette()
    {
        _baseColor = NeonColors.Active.GetBase(NeonRole.Bumper);
        if (_sr != null && !_flashing && !_passthrough)
            _sr.color = _disabled ? NeonPalette.Dim(_baseColor, 0.25f) : _baseColor;
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
            rb.velocity = dir * pushForce;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ComboSystem.Instance?.RegisterAirtimeHit(hitPos);
        JuiceRouter.Play(JuiceRouter.Tier.Hit, hitPos, _baseColor);

        if (!_flashing) StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        _flashing = true;
        var palette = NeonColors.Active;
        if (_sr != null) _sr.color = palette.GetFlash(NeonRole.Bumper);

        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale * 2.4f;
            _glowSR.color = palette.GetBumperFlashGlow();
        }

        yield return new WaitForSeconds(flashDuration);

        if (_sr != null) _sr.color = _disabled
            ? NeonPalette.Dim(_baseColor, 0.25f)
            : _baseColor;
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale;
            _glowSR.color = _glowBaseColor;
        }
        _flashing = false;
    }
}
