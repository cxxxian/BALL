using System.Collections;
using UnityEngine;

public class Slingshot : MonoBehaviour
{
    [Header("Settings")]
    public int scoreOnHit = 20;
    public float kickForce = 10f;

    [Header("Hit Juice")]
    [Tooltip("霓虹 HitFlash 峰值（SpriteNeonHDR._HitFlash）")]
    public float flashPeak = 1f;
    public float flashDuration = 0.14f;
    [Range(0.6f, 1f)]
    public float squashScale = 0.86f;
    [Range(1f, 1.35f)]
    public float popScale = 1.1f;
    public float squashIn = 0.035f;
    public float popOut = 0.055f;
    public float settle = 0.09f;

    private SpriteRenderer _sr;
    private Transform _visual;
    private Vector3 _visualBaseScale = Vector3.one;
    private Color _baseColor = Color.white;
    private MaterialPropertyBlock _mpb;
    private Coroutine _juiceRoutine;
    private float _flashValue;
    private float _lastHitTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;

    private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");

    private void Awake()
    {
        ResolveVisualRenderer();
        _mpb = new MaterialPropertyBlock();
        if (_sr != null) _baseColor = _sr.color;
        ApplyHitFlash(0f);

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

    private void Start()
    {
        // Prototype 可能在 Awake/OnEnable 里重建 Visual，再解析一次
        ResolveVisualRenderer();
        CacheVisualBase();
    }

    private void ResolveVisualRenderer()
    {
        // 根节点常有禁用的占位 SpriteRenderer；真正贴图在 Visual 子物体上
        var visualT = transform.Find("Visual");
        if (visualT != null)
            _sr = visualT.GetComponent<SpriteRenderer>();

        if (_sr == null)
        {
            var srs = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                if (srs[i] != null && srs[i].enabled && srs[i].sprite != null)
                {
                    _sr = srs[i];
                    break;
                }
            }
        }

        if (_sr == null)
            _sr = GetComponentInChildren<SpriteRenderer>(true);

        _visual = _sr != null ? _sr.transform : transform;
    }

    private void CacheVisualBase()
    {
        if (_visual == null) return;
        _visualBaseScale = _visual.localScale;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Ball")) return;

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

        if (_juiceRoutine != null)
        {
            _flashValue = Mathf.Max(_flashValue, flashPeak);
            ApplyHitFlash(_flashValue);
        }
        else
        {
            CacheVisualBase();
            _juiceRoutine = StartCoroutine(HitJuice());
        }
    }

    private IEnumerator HitJuice()
    {
        _flashValue = flashPeak;
        ApplyHitFlash(_flashValue);

        yield return AnimateScale(_visualBaseScale * squashScale, squashIn, holdFlash: true);
        yield return AnimateScale(_visualBaseScale * popScale, popOut, holdFlash: true);
        yield return AnimateScale(_visualBaseScale, settle, holdFlash: false);

        float t = 0f;
        float startFlash = _flashValue;
        float trail = Mathf.Max(0.04f, flashDuration * 0.45f);
        while (t < trail)
        {
            t += Time.deltaTime;
            _flashValue = Mathf.Lerp(startFlash, 0f, t / trail);
            ApplyHitFlash(_flashValue);
            yield return null;
        }

        _flashValue = 0f;
        ApplyHitFlash(0f);
        if (_visual != null) _visual.localScale = _visualBaseScale;
        if (_sr != null) _sr.color = _baseColor;
        _juiceRoutine = null;
    }

    private IEnumerator AnimateScale(Vector3 target, float duration, bool holdFlash)
    {
        if (_visual == null || duration <= 0.0001f)
        {
            if (_visual != null) _visual.localScale = target;
            yield break;
        }

        Vector3 from = _visual.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            u = u * u * (3f - 2f * u);
            _visual.localScale = Vector3.LerpUnclamped(from, target, u);

            if (holdFlash)
                _flashValue = Mathf.Max(_flashValue, flashPeak * 0.85f);
            else
                _flashValue = Mathf.Max(0f, _flashValue - Time.deltaTime / Mathf.Max(flashDuration, 0.01f));
            ApplyHitFlash(_flashValue);
            yield return null;
        }
        _visual.localScale = target;
    }

    private void ApplyHitFlash(float value)
    {
        if (_sr == null) return;
        _sr.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HitFlashID, Mathf.Clamp01(value));
        _sr.SetPropertyBlock(_mpb);
    }
}
