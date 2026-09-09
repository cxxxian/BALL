using System.Collections;
using UnityEngine;

/// <summary>
/// 能量炮台自导弹：沿炮台传入的初始方向飞行，持续转向目标敌人。
/// 由 EnergyCannon 负责实例化和 Init。
/// </summary>
public class HomingBullet : MonoBehaviour
{
    [HideInInspector] public float speed = 14f;
    [HideInInspector] public float turnRate = 280f;
    [HideInInspector] public float lifetime = 4f;
    [HideInInspector] public Color bulletColor = new Color(1f, 0.6f, 0f, 1f);

    private EnemyBase _target;
    private Rigidbody2D _rb;
    private Vector2 _fireDir = Vector2.up;
    private bool _prioritizeBoss;
    private bool _initialized;

    private void Awake()
    {
        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    /// <summary>由 EnergyCannon 在生成后立即调用。</summary>
    public void Init(Vector2 fireDirection, bool prioritizeBoss)
    {
        _fireDir = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.up;
        _prioritizeBoss = prioritizeBoss;
        _initialized = true;

        // 轻微扇形散布，避免三颗完全重叠
        float spread = Random.Range(-8f, 8f);
        Vector2 dir = Quaternion.Euler(0f, 0f, spread) * _fireDir;
        _rb.velocity = dir * speed;

        AcquireTarget();
        StartCoroutine(LifetimeRoutine());
    }

    private void Start()
    {
        // 兼容旧场景里未 Init 的子弹：沿正下方扩散
        if (_initialized) return;
        float angle = Random.Range(-50f, 50f);
        Vector2 initDir = Quaternion.Euler(0, 0, angle) * Vector2.down;
        _rb.velocity = initDir * speed;
        AcquireTarget();
        StartCoroutine(LifetimeRoutine());
    }

    private void FixedUpdate()
    {
        if (_target == null || _target.IsDead)
            AcquireTarget();

        if (_target == null) return;

        Vector2 toTarget = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
        Vector2 currentDir = _rb.velocity.sqrMagnitude > 0.0001f ? _rb.velocity.normalized : _fireDir;
        Vector2 newDir = Vector2.MoveTowards(currentDir, toTarget, turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime);
        _rb.velocity = newDir.normalized * speed;

        float angle = Mathf.Atan2(_rb.velocity.y, _rb.velocity.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void AcquireTarget()
    {
        if (_prioritizeBoss)
        {
            var boss = FindObjectOfType<Boss>();
            if (boss != null && !boss.IsDead)
            {
                _target = boss;
                return;
            }
        }

        var enemies = FindObjectsOfType<EnemyBase>();
        EnemyBase best = null;
        float bestScore = float.MinValue;

        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;

            Vector2 toEnemy = (Vector2)e.transform.position - (Vector2)transform.position;
            float dist = toEnemy.sqrMagnitude;
            if (dist < 0.0001f) continue;

            Vector2 dir = toEnemy.normalized;
            float forward = Vector2.Dot(dir, _fireDir); // 1 = 正前方
            float score = forward * 2f - dist * 0.02f;  // 优先前方、其次近
            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        _target = best;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeHit();
                if (ImpactFX.Instance != null)
                    ImpactFX.Instance.SpawnHit(transform.position, bulletColor, 1.1f);
            }
            Destroy(gameObject);
        }
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        Destroy(gameObject);
    }
}
