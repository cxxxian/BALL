using System.Collections;
using UnityEngine;

public class Slingshot : MonoBehaviour
{
    [Header("Settings")]
    public int scoreOnHit = 50;
    public float kickForce = 10f;
    public float flashDuration = 0.1f;

    private SpriteRenderer _sr;
    private Color _baseColor;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;
        var rb = col.rigidbody;
        if (rb != null)
        {
            // 反弹并加速朝远离弹弓的方向
            Vector2 normal = col.contacts[0].normal;
            Vector2 reflected = Vector2.Reflect(rb.velocity, normal).normalized;
            rb.velocity = reflected * Mathf.Max(rb.velocity.magnitude, kickForce);
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        ComboSystem.Instance?.RegisterHit();
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ImpactFX.Instance?.SpawnHit(hitPos, _baseColor, 1.2f);

        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        if (_sr != null) _sr.color = Color.yellow;
        yield return new WaitForSeconds(flashDuration);
        if (_sr != null) _sr.color = _baseColor;
    }
}
