using System.Collections;
using UnityEngine;

/// <summary>
/// 能量炮台发射的自导弹：持续转向最近的存活敌人并飞过去，命中后调用 TakeHit()。
/// 由 EnergyCannon 负责实例化和初始化。
/// </summary>
public class HomingBullet : MonoBehaviour
{
    [HideInInspector] public float speed    = 12f;
    [HideInInspector] public float turnRate = 220f;   // 度/秒
    [HideInInspector] public float lifetime = 4f;
    [HideInInspector] public Color bulletColor = new Color(1f, 0.6f, 0f, 1f);

    private EnemyBase  _target;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        // 初始朝随机扩散方向（偏下方），更快飞向场内敌人
        float angle = Random.Range(-50f, 50f); // -50~+50 度，相对正下方扩散
        Vector2 initDir = Quaternion.Euler(0, 0, angle) * Vector2.down;
        _rb.velocity = initDir * speed;
        AcquireTarget();
        StartCoroutine(LifetimeRoutine());
    }

    private void FixedUpdate()
    {
        if (_target == null || _target.IsDead)
            AcquireTarget();

        if (_target == null)
        {
            // 没有目标：继续直行
            return;
        }

        // 转向目标
        Vector2 toTarget = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
        Vector2 currentDir = _rb.velocity.normalized;
        Vector2 newDir = Vector2.MoveTowards(currentDir, toTarget, turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime);
        _rb.velocity = newDir.normalized * speed;

        // 旋转 Sprite 朝向飞行方向
        float angle = Mathf.Atan2(_rb.velocity.y, _rb.velocity.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void AcquireTarget()
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        EnemyBase best = null;
        float minDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d < minDist) { minDist = d; best = e; }
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
