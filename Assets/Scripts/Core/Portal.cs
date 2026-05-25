using System.Collections;
using UnityEngine;

/// <summary>
/// 双向传送门：成对使用（A 引用 B，B 可以不引用 A）。
/// 球进入后：缩小动画 → 瞬移到伙伴出口 → 放大弹出，保持速度大小与方向。
/// 0.5s 冷却防止无限循环。
/// 需要此 GameObject 有 Collider2D（设为 IsTrigger=true）。
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Pairing")]
    [Tooltip("出口传送门（双向时 A.partner=B，B.partner=A；单向时 B.partner 留空）")]
    public Portal partnerPortal;

    [Header("Settings")]
    public float cooldown         = 0.5f;
    public float animDuration     = 0.1f;   // 缩放动画持续秒数（单侧）

    [Header("Visual")]
    public Color portalColor = new Color(0f, 0.6f, 2f, 1f); // HDR 科技蓝
    public float idleRotationSpeed = -60f;                   // 缓慢逆时针旋转

    // ──────────────────────────────────────────────────────
    private bool                  _onCooldown;
    private SpriteRenderer        _sr;
    private MaterialPropertyBlock _mpb;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        SetColor(portalColor);
    }

    private void Update()
    {
        transform.Rotate(Vector3.forward, idleRotationSpeed * Time.deltaTime);
    }

    // ──────────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_onCooldown) return;
        if (partnerPortal == null) return;
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null) return;

        StartCoroutine(TeleportRoutine(ball));
    }

    private IEnumerator TeleportRoutine(BallController ball)
    {
        // ── 锁定两端冷却，防止反复触发 ────────────────────
        _onCooldown = true;
        partnerPortal._onCooldown = true;

        Rigidbody2D rb      = ball.Rb;
        Vector2 savedVel    = rb.velocity;
        Vector3 savedScale  = ball.transform.localScale;

        // 停速，锁定球位置
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        // ── 进门：缩小 ────────────────────────────────────
        yield return StartCoroutine(ScaleAnim(ball.transform, savedScale, Vector3.zero, animDuration));

        // 传送特效
        AudioManager.Instance?.PlayBounce();
        if (ImpactFX.Instance != null)
        {
            ImpactFX.Instance.SpawnHit(transform.position,           portalColor,                  1.2f);
            ImpactFX.Instance.SpawnHit(partnerPortal.transform.position, partnerPortal.portalColor, 1.2f);
        }

        // 瞬移到出口
        ball.transform.position = partnerPortal.transform.position;

        // ── 出门：放大 ────────────────────────────────────
        yield return StartCoroutine(ScaleAnim(ball.transform, Vector3.zero, savedScale, animDuration));

        // 还原物理
        rb.isKinematic = false;
        rb.velocity    = savedVel;

        // ── 等冷却结束再解锁 ──────────────────────────────
        yield return new WaitForSeconds(cooldown);
        _onCooldown = false;
        if (partnerPortal != null) partnerPortal._onCooldown = false;
    }

    private IEnumerator ScaleAnim(Transform t, Vector3 from, Vector3 to, float dur)
    {
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(from, to, elapsed / dur);
            yield return null;
        }
        t.localScale = to;
    }

    private void SetColor(Color c)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _sr.SetPropertyBlock(_mpb);
    }
}
