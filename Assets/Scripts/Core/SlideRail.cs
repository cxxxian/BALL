using System.Collections;
using UnityEngine;

/// <summary>
/// 滑轨：球碰到入口后沿轨道「骑行」到出口再弹出。
/// 全程可见位移（非 Portal 瞬移），适合做上坡/侧扫观光路线。
/// </summary>
public class SlideRail : MonoBehaviour
{
    [Header("Path")]
    public Transform entry;
    public Transform exit;
    [Tooltip("可选中间控制点，缺省则直线")]
    public Transform control;

    [Header("Ride")]
    public float rideDuration = 0.55f;
    public float exitSpeed = 12f;
    public float minEntrySpeed = 4f;
    public float cooldown = 2f;
    public bool oneWay = true;
    public int scoreOnRide = 55;

    [Header("Visual")]
    public Color railColor = new Color(1.6f, 0.3f, 1.8f, 0.9f);

    private bool _busy;
    private float _cooldownLeft;
    private LineRenderer _lr;

    public bool CanAccept => !_busy && _cooldownLeft <= 0f;

    private void Awake()
    {
        if (entry == null) entry = transform.Find("Entry");
        if (exit == null) exit = transform.Find("Exit");
        if (control == null) control = transform.Find("Control");
        EnsureRailVisual();
        EnsureEntryZone();
        RebuildRail();
    }

    private void Update()
    {
        if (_cooldownLeft > 0f)
            _cooldownLeft -= Time.deltaTime;
    }

    internal void HandleEntry(Collider2D other)
    {
        if (!CanAccept) return;
        if (!other.CompareTag("Ball")) return;
        var ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsWaitingForLaunch) return;
        if (ball.Rb == null || ball.Rb.velocity.sqrMagnitude < minEntrySpeed * minEntrySpeed)
            return;
        StartCoroutine(RideRoutine(ball));
    }

    private IEnumerator RideRoutine(BallController ball)
    {
        _busy = true;
        var rb = ball.Rb;
        var col = ball.GetComponent<Collider2D>();
        Vector3 savedScale = ball.transform.localScale;

        Vector2 p0 = entry.position;
        Vector2 p2 = exit.position;
        Vector2 p1 = control != null
            ? (Vector2)control.position
            : (p0 + p2) * 0.5f + Vector2.Perpendicular((p2 - p0).normalized) * 1.2f;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;

        ComboSystem.Instance?.RegisterAirtimeHit(p0);
        GameManager.Instance?.AddScore(scoreOnRide);
        JuiceRouter.Play(JuiceRouter.Tier.Tap, p0, railColor);
        AudioManager.Instance?.PlayBounce();

        float t = 0f;
        Vector2 last = p0;
        while (t < rideDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / rideDuration);
            Vector2 pos = QuadBezier(p0, p1, p2, u);
            ball.transform.position = pos;
            ball.transform.localScale = Vector3.Lerp(savedScale, savedScale * 0.75f, Mathf.Sin(u * Mathf.PI) * 0.4f);
            last = pos;
            yield return null;
        }

        Vector2 tangent = (QuadBezier(p0, p1, p2, 0.99f) - QuadBezier(p0, p1, p2, 0.9f)).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = (p2 - p0).normalized;

        ball.transform.position = p2;
        ball.transform.localScale = savedScale;
        if (col != null) col.enabled = true;
        rb.isKinematic = false;
        rb.velocity = tangent * exitSpeed;

        JuiceRouter.Play(JuiceRouter.Tier.Hit, p2, railColor);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);

        _cooldownLeft = cooldown;
        _busy = false;
    }

    private static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private void EnsureEntryZone()
    {
        if (entry == null) return;
        var zone = entry.GetComponent<SlideRailEntryZone>();
        if (zone == null) zone = entry.gameObject.AddComponent<SlideRailEntryZone>();
        zone.owner = this;
    }

    private void EnsureRailVisual()
    {
        _lr = GetComponent<LineRenderer>();
        if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace = true;
        _lr.startWidth = 0.08f;
        _lr.endWidth = 0.05f;
        _lr.material = new Material(Shader.Find("Sprites/Default"));
        _lr.sortingOrder = 3;
    }

    private void RebuildRail()
    {
        if (_lr == null || entry == null || exit == null) return;
        Vector2 p0 = entry.position;
        Vector2 p2 = exit.position;
        Vector2 p1 = control != null
            ? (Vector2)control.position
            : (p0 + p2) * 0.5f + Vector2.Perpendicular((p2 - p0).normalized) * 1.2f;

        const int n = 24;
        _lr.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float u = i / (n - 1f);
            Vector2 p = QuadBezier(p0, p1, p2, u);
            _lr.SetPosition(i, p);
        }
        _lr.startColor = railColor;
        _lr.endColor = new Color(railColor.r, railColor.g, railColor.b, 0.35f);
    }
}

public class SlideRailEntryZone : MonoBehaviour
{
    [HideInInspector] public SlideRail owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.HandleEntry(other);
    }
}
