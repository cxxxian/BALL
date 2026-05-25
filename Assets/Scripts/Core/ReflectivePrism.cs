using UnityEngine;

/// <summary>
/// 反射棱镜：球碰撞时强制折射到固定方向，速度大小保持不变。
/// 如果挂了 LineRenderer，碰撞时绘制激光射线 0.2s 后淡出。
/// 需要此 GameObject 有 Collider2D（非 Trigger，物理碰撞）。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ReflectivePrism : MonoBehaviour
{
    [Header("Reflection")]
    [Tooltip("球碰到后强制射出的方向（世界空间，自动归一化）")]
    public Vector2 reflectDirection = Vector2.up;
    [Tooltip("速度继承系数（1 = 完全保留速度）")]
    public float speedFactor = 1.0f;

    [Header("Visual")]
    public Color prismColor   = new Color(1.4f, 0f, 2f, 1f);  // HDR 粉紫
    public float laserLength  = 6f;
    public float flashDuration = 0.12f;
    public float laserDuration = 0.22f;

    // ──────────────────────────────────────────────────────
    private SpriteRenderer        _sr;
    private MaterialPropertyBlock _mpb;
    private LineRenderer          _lr;
    private float                 _laserTimer;
    private bool                  _laserActive;

    // ──────────────────────────────────────────────────────
    private void Awake()
    {
        _sr  = GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        _lr  = GetComponent<LineRenderer>();

        // 初始化 LineRenderer
        _lr.positionCount = 2;
        _lr.startWidth    = 0.04f;
        _lr.endWidth      = 0.01f;
        _lr.enabled       = false;

        if (_lr.material == null || _lr.material.name == "Default-Line")
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            _lr.material = mat;
        }
    }

    private void Start()
    {
        SetBaseColor();
    }

    // ──────────────────────────────────────────────────────
    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;

        var rb = col.rigidbody;
        if (rb == null) return;

        float speed = rb.velocity.magnitude * speedFactor;
        rb.velocity = reflectDirection.normalized * Mathf.Max(speed, 5f);

        // 特效
        TriggerFlash();
        Vector2 hitPt = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        ShowLaser(hitPt);

        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(hitPt, prismColor, 1.2f);
    }

    // ── 视觉 ──────────────────────────────────────────────
    private void TriggerFlash()
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", prismColor * 3f);
        _sr.SetPropertyBlock(_mpb);
        Invoke(nameof(SetBaseColor), flashDuration);
    }

    private void SetBaseColor()
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", prismColor);
        _sr.SetPropertyBlock(_mpb);
    }

    private void ShowLaser(Vector2 origin)
    {
        _lr.enabled = true;
        _lr.SetPosition(0, origin);
        _lr.SetPosition(1, origin + reflectDirection.normalized * laserLength);
        _lr.startColor = prismColor;
        _lr.endColor   = new Color(prismColor.r, prismColor.g, prismColor.b, 0f);
        _laserTimer  = laserDuration;
        _laserActive = true;
    }

    private void Update()
    {
        if (!_laserActive) return;
        _laserTimer -= Time.deltaTime;
        float alpha = Mathf.Clamp01(_laserTimer / laserDuration);
        _lr.startColor = new Color(prismColor.r, prismColor.g, prismColor.b, alpha);
        _lr.endColor   = new Color(prismColor.r, prismColor.g, prismColor.b, 0f);
        if (_laserTimer <= 0f)
        {
            _lr.enabled  = false;
            _laserActive = false;
        }
    }
}
