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
    private Vector3        _glowBaseScale;
    private Color          _glowBaseColor;
    private Color _baseColor;
    private bool _flashing = false;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;

        var glowT = transform.Find("Glow");
        if (glowT != null)
        {
            _glowSR        = glowT.GetComponent<SpriteRenderer>();
            _glowBaseScale = glowT.localScale;
            _glowBaseColor = _glowSR != null ? _glowSR.color : Color.white;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
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
        if (_sr != null) _sr.color = Color.white;

        // Glow 冲击扩张
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale * 2.0f;
            _glowSR.color = new Color(0.7f, 1f, 1f, 1f);
        }

        yield return new WaitForSeconds(flashDuration);

        if (_sr != null) _sr.color = _baseColor;
        if (_glowSR != null)
        {
            _glowSR.transform.localScale = _glowBaseScale;
            _glowSR.color = _glowBaseColor;
        }
        _flashing = false;
    }
}
