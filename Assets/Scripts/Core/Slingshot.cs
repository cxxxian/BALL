using System.Collections;
using UnityEngine;

public class Slingshot : MonoBehaviour
{
    [Header("Settings")]
    public int scoreOnHit = 20;
    public float kickForce = 10f;
    public float flashDuration = 0.1f;

    private SpriteRenderer _sr;
    private Color _baseColor;

    // ── 碰撞冷却（防穿模抖动） ─────────────────────────────────────────
    private float _lastHitTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null) _baseColor = _sr.color;

        // 踢力由脚本负责；材质 1.2 弹力会与脚本冲量叠加热量。
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            var mat = new PhysicsMaterial2D("SlingshotScripted")
            {
                friction = 0f,
                bounciness = 0f
            };
            col.sharedMaterial = mat;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;

        // ── 碰撞冷却检测：防止穿模抖动 ─────────────────────────────────────
        float currentTime = Time.time;
        if (currentTime - _lastHitTime < COLLISION_COOLDOWN) return;
        _lastHitTime = currentTime;

        var rb = col.rigidbody;
        if (rb != null && col.contactCount > 0)
        {
            // OnCollisionEnter 时 rb.velocity 已是求解后速度；再 Reflect 等于二次反射，边缘会飞偏。
            // 用 relativeVelocity（撞击前相对速度）+ 指向球的外法线。
            ContactPoint2D contact = col.GetContact(0);
            Vector2 n = contact.normal; // 从 Ball → Slingshot
            Vector2 toBall = (Vector2)col.transform.position - (Vector2)transform.position;
            if (Vector2.Dot(n, toBall) < 0f) n = -n; // 确保朝向球（外法线）

            Vector2 incoming = col.relativeVelocity; // Ball 相对本物体
            if (incoming.sqrMagnitude < 0.0001f)
                incoming = rb.velocity;
            if (Vector2.Dot(incoming, n) > 0f)
                incoming = -incoming;

            Vector2 reflected = Vector2.Reflect(incoming, n);
            if (reflected.sqrMagnitude < 0.0001f)
                reflected = n;
            float speed = Mathf.Max(incoming.magnitude, kickForce);
            rb.velocity = reflected.normalized * speed;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ComboSystem.Instance?.RegisterAirtimeHit(hitPos);
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
