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
    private bool _passthrough = false; // 斩杀连锁期间：碰撞体关闭，球穿透飞过

    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _col = GetComponent<Collider2D>();
        if (_sr != null) _baseColor = _sr.color;

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

        Color dim = new Color(_baseColor.r * 0.15f, _baseColor.g * 0.15f, _baseColor.b * 0.15f, 1f);
        if (_sr != null) _sr.color = passthrough ? dim : (_disabled
            ? new Color(_baseColor.r * 0.25f, _baseColor.g * 0.25f, _baseColor.b * 0.25f, 1f)
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
        if (_sr != null) _sr.color = disabled ? new Color(_baseColor.r * 0.25f, _baseColor.g * 0.25f, _baseColor.b * 0.25f, 1f) : _baseColor;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (_disabled) return;
        if (!col.gameObject.CompareTag("Ball")) return;
        var rb = col.rigidbody;
        if (rb != null)
        {
            Vector2 dir = (col.transform.position - transform.position).normalized;
            rb.velocity = dir * pushForce;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        ComboSystem.Instance?.RegisterHit();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);

        // Tron 像素粒子爆发（取自身 Neon 颜色）
        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ImpactFX.Instance?.SpawnHit(hitPos, _baseColor, 1f);

        if (!_flashing) StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        _flashing = true;
        // HDR 超曝白：值 > 1 才能触发 URP Bloom，产生亮眼白色脉冲
        if (_sr != null) _sr.color = new Color(6f, 6f, 6f, 1f);

        // Glow 冲击扩张 + HDR 青色脉冲
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale * 2.4f;
            _glowSR.color = new Color(1.5f, 5f, 5f, 1f);
        }

        yield return new WaitForSeconds(flashDuration);

        if (_sr != null) _sr.color = _disabled
            ? new Color(_baseColor.r * 0.25f, _baseColor.g * 0.25f, _baseColor.b * 0.25f, 1f)
            : _baseColor;
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale;
            _glowSR.color = _glowBaseColor;
        }
        _flashing = false;
    }
}
