using System.Collections;
using UnityEngine;

/// <summary>
/// 环轨：球进入后沿圆环公转数圈并提速，再沿切线甩出。
/// 乐趣：可见的回路加速（非瞬移），和 Portal 的「配对跳」完全不同。
/// </summary>
public class OrbitRing : MonoBehaviour
{
    [Header("Orbit")]
    public float radius = 1.35f;
    [Tooltip("公转圈数（可为小数）")]
    public float revolutions = 1.25f;
    public float baseAngularSpeed = 420f; // 度/秒
    public float exitSpeedBonus = 1.2f;   // 相对入射速度倍率
    public float minExitSpeed = 11f;
    public float maxExitSpeed = 18f;
    public float cooldown = 2.5f;
    public float minEntrySpeed = 4f;
    public int scoreOnOrbit = 60;

    [Header("Visual")]
    public Color ringColor = new Color(0.2f, 2.2f, 1.4f, 0.85f);
    public Color busyColor = new Color(0.1f, 0.8f, 0.6f, 0.35f);

    private bool _busy;
    private float _cooldownLeft;
    private LineRenderer _ring;
    private MaterialPropertyBlock _mpb;
    private SpriteRenderer _coreSr;

    public bool CanAccept => !_busy && _cooldownLeft <= 0f;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        EnsureVisuals();
        EnsureTrigger();
    }

    private void Update()
    {
        if (_cooldownLeft > 0f)
            _cooldownLeft -= Time.deltaTime;
        transform.Rotate(Vector3.forward, 40f * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanAccept) return;
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsWaitingForLaunch) return;
        if (ball.Rb == null || ball.Rb.velocity.sqrMagnitude < minEntrySpeed * minEntrySpeed)
            return;

        StartCoroutine(OrbitRoutine(ball));
    }

    private IEnumerator OrbitRoutine(BallController ball)
    {
        _busy = true;
        SetRingColor(busyColor);

        var rb = ball.Rb;
        var col = ball.GetComponent<Collider2D>();
        float entrySpeed = rb.velocity.magnitude;
        Vector2 center = transform.position;

        // 从当前角度开始公转
        Vector2 offset = (Vector2)ball.transform.position - center;
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        // 公转方向：沿入射切向偏好
        float sign = Mathf.Sign(Vector2.Dot(
            new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)),
            rb.velocity));
        if (Mathf.Abs(sign) < 0.01f) sign = 1f;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;

        ComboSystem.Instance?.RegisterAirtimeHit(ball.transform.position);
        GameManager.Instance?.AddScore(scoreOnOrbit);
        JuiceRouter.Play(JuiceRouter.Tier.Skill, center, ringColor);
        AudioManager.Instance?.PlayBounce();

        float totalDeg = 360f * revolutions;
        float traveled = 0f;
        float angSpeed = baseAngularSpeed;

        while (traveled < totalDeg)
        {
            if (ball == null) yield break;
            float step = angSpeed * Time.deltaTime;
            traveled += step;
            angle += sign * step;
            // 越转越快
            angSpeed = Mathf.Lerp(baseAngularSpeed, baseAngularSpeed * 1.6f, traveled / totalDeg);

            float rad = angle * Mathf.Deg2Rad;
            ball.transform.position = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            yield return null;
        }

        float radExit = angle * Mathf.Deg2Rad;
        Vector2 tangent = new Vector2(-Mathf.Sin(radExit), Mathf.Cos(radExit)) * sign;
        float exitSpeed = Mathf.Clamp(entrySpeed * exitSpeedBonus, minExitSpeed, maxExitSpeed);

        if (col != null) col.enabled = true;
        rb.isKinematic = false;
        rb.velocity = tangent * exitSpeed;

        JuiceRouter.Play(JuiceRouter.Tier.Hit, ball.transform.position, ringColor);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);

        _cooldownLeft = cooldown;
        _busy = false;
        SetRingColor(ringColor);
    }

    private void EnsureTrigger()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius * 0.85f;
    }

    private void EnsureVisuals()
    {
        _ring = GetComponent<LineRenderer>();
        if (_ring == null) _ring = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace = false;
        _ring.loop = true;
        _ring.startWidth = 0.07f;
        _ring.endWidth = 0.07f;
        _ring.material = new Material(Shader.Find("Sprites/Default"));
        _ring.sortingOrder = 4;
        const int segs = 48;
        _ring.positionCount = segs;
        for (int i = 0; i < segs; i++)
        {
            float a = (i / (float)segs) * Mathf.PI * 2f;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
        SetRingColor(ringColor);

        var core = transform.Find("Core");
        if (core == null)
        {
            var go = new GameObject("Core");
            go.transform.SetParent(transform, false);
            core = go.transform;
            _coreSr = go.AddComponent<SpriteRenderer>();
        }
        else _coreSr = core.GetComponent<SpriteRenderer>();

        if (_coreSr != null)
        {
            _coreSr.sprite = CyberVisualFactory.CreateBoostGearSprite(ringColor);
            _coreSr.color = ringColor;
            _coreSr.material = CyberVisualFactory.UnlitMaterial;
            _coreSr.sortingOrder = 5;
            core.localScale = Vector3.one * 0.55f;
        }
    }

    private void SetRingColor(Color c)
    {
        if (_ring == null) return;
        _ring.startColor = c;
        _ring.endColor = new Color(c.r, c.g, c.b, c.a * 0.5f);
        if (_coreSr != null) _coreSr.color = c;
    }
}
