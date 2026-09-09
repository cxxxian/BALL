using UnityEngine;

/// <summary>
/// 折射棱镜：把球踢向「偏好方向」，但带冷却 + 防竖直回弹。
/// 与 Bumper：Bumper 从圆心外推（随机）；棱镜是路线工具，只在冷却好时强制改向。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ReflectivePrism : MonoBehaviour
{
    [Header("Reflection")]
    [Tooltip("偏好射出方向（世界空间）")]
    public Vector2 reflectDirection = new Vector2(0.55f, 1f);
    [Tooltip("速度继承系数")]
    public float speedFactor = 1.0f;
    [Tooltip("触发后冷却：期间当普通墙，避免竖直来回弹")]
    public float cooldown = 0.55f;
    [Tooltip("入射已接近偏好方向时不强制改向（避免同向连踢）")]
    [Range(0f, 1f)]
    public float skipIfAlignedDot = 0.75f;
    [Tooltip("出射相对入射反向的最小夹角（度），防止 180° 竖直接力")]
    [Range(0f, 90f)]
    public float minExitVsEntryAngle = 35f;

    [Header("Reward")]
    public int scoreOnHit = 15;

    [Header("Visual")]
    public Color prismColor = new Color(1.4f, 0f, 2f, 1f);
    public Color cooldownColor = new Color(0.35f, 0.1f, 0.45f, 1f);
    public float laserLength = 6f;
    public float flashDuration = 0.12f;
    public float laserDuration = 0.22f;

    private SpriteRenderer _sr;
    private MaterialPropertyBlock _mpb;
    private LineRenderer _lr;
    private float _laserTimer;
    private bool _laserActive;
    private float _cooldownLeft;
    private Collider2D _col;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        _lr = GetComponent<LineRenderer>();
        _col = GetComponent<Collider2D>();

        _lr.positionCount = 2;
        _lr.startWidth = 0.04f;
        _lr.endWidth = 0.01f;
        _lr.enabled = false;

        if (_lr.material == null || _lr.material.name == "Default-Line")
            _lr.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void Start() => SetBaseColor(prismColor);

    private void Update()
    {
        if (_cooldownLeft > 0f)
        {
            _cooldownLeft -= Time.deltaTime;
            if (_cooldownLeft <= 0f)
                SetBaseColor(prismColor);
        }

        if (!_laserActive) return;
        _laserTimer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(_laserTimer / laserDuration);
        _lr.startColor = new Color(prismColor.r, prismColor.g, prismColor.b, alpha);
        _lr.endColor = new Color(prismColor.r, prismColor.g, prismColor.b, 0f);
        if (_laserTimer <= 0f)
        {
            _lr.enabled = false;
            _laserActive = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;
        if (_cooldownLeft > 0f) return;

        var rb = col.rigidbody;
        if (rb == null) return;

        Vector2 preferred = reflectDirection.sqrMagnitude > 0.0001f
            ? reflectDirection.normalized
            : Vector2.up;

        Vector2 incoming = rb.velocity;
        if (incoming.sqrMagnitude < 0.01f && col.contactCount > 0)
            incoming = -col.GetContact(0).normal;
        Vector2 inDir = incoming.sqrMagnitude > 0.0001f ? incoming.normalized : -preferred;

        // 已经朝偏好方向飞：不当成强制折射，避免连踢
        if (Vector2.Dot(inDir, preferred) >= skipIfAlignedDot)
            return;

        Vector2 exit = preferred;
        // 若出射几乎是入射反向 → 侧向掰开，打断竖直乒乓
        float reverseDot = Vector2.Dot(exit, -inDir);
        if (reverseDot > Mathf.Cos(minExitVsEntryAngle * Mathf.Deg2Rad))
        {
            float side = Mathf.Sign(Vector2.Dot(new Vector2(-inDir.y, inDir.x), preferred));
            if (Mathf.Abs(side) < 0.01f) side = 1f;
            exit = (Quaternion.Euler(0f, 0f, side * 55f) * preferred).normalized;
        }

        float speed = Mathf.Max(incoming.magnitude * speedFactor, 5f);
        rb.velocity = exit * speed;

        _cooldownLeft = cooldown;
        SetBaseColor(cooldownColor);

        Vector2 hitPt = col.contactCount > 0 ? col.GetContact(0).point : (Vector2)transform.position;
        TriggerFlash();
        ShowLaser(hitPt, exit);

        ComboSystem.Instance?.RegisterAirtimeHit(hitPt);
        GameManager.Instance?.AddScore(scoreOnHit);
        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(hitPt, prismColor, 1.2f);
    }

    private void TriggerFlash()
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", prismColor * 3f);
        _sr.SetPropertyBlock(_mpb);
        Invoke(nameof(RestoreColor), flashDuration);
    }

    private void RestoreColor()
    {
        SetBaseColor(_cooldownLeft > 0f ? cooldownColor : prismColor);
    }

    private void SetBaseColor(Color c)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _sr.SetPropertyBlock(_mpb);
    }

    private void ShowLaser(Vector2 origin, Vector2 dir)
    {
        _lr.enabled = true;
        _lr.SetPosition(0, origin);
        _lr.SetPosition(1, origin + dir.normalized * laserLength);
        _lr.startColor = prismColor;
        _lr.endColor = new Color(prismColor.r, prismColor.g, prismColor.b, 0f);
        _laserTimer = laserDuration;
        _laserActive = true;
    }
}
