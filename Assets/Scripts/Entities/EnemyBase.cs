using UnityEngine;
using UnityEngine.Events;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Stats")]
    public int   maxHits           = 2;
    public float moveSpeed         = 0.5f;
    public int   scoreOnHit        = 10;
    public int   scoreOnKill       = 50;
    public int   damageToPlayer    = 1;
    public bool  isBomber          = false;
    public float bomberDisableDuration = 5f;

    [Header("Bottom Detection")]
    [Tooltip("禁用底线检测（Boss 设为 false）")]
    public bool  checkBottomLine   = true;

    public static float BottomLineY = -7.5f;

    public int  CurrentHits { get; protected set; } = 0;
    public bool IsDead      { get; protected set; } = false;

    public UnityEvent<EnemyBase> onDeath = new UnityEvent<EnemyBase>();

    protected Rigidbody2D _rb;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    protected virtual void Update()
    {
        if (IsDead) return;
        if (checkBottomLine)
        {
            // 护盾激活时：在护盾线处截击；否则在普通底线触发
            bool shieldUp = BlockShield.Instance != null && BlockShield.Instance.IsActive;
            float checkY  = shieldUp ? BlockShield.Instance.shieldY : BottomLineY;
            if (transform.position.y <= checkY)
                OnReachBottom();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (IsDead) return;
        if (GameManager.Instance == null) return;
        var state = GameManager.Instance.State;
        if (state == GameState.GameOver || state == GameState.BuffSelection || state == GameState.Idle) return;
        ApplyMovement();
    }

    protected virtual void ApplyMovement()
    {
        if (_rb != null)
            _rb.velocity = Vector2.down * moveSpeed * WaveManager.MinionSpeedMultiplier;
        else
            transform.Translate(Vector3.down * moveSpeed * WaveManager.MinionSpeedMultiplier * Time.deltaTime);
    }

    protected virtual void OnReachBottom()
    {
        if (IsDead) return;
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;

        // 护盾激活时拦截伤害：护盾吸收 → 触发清场效果
        if (BlockShield.Instance != null && BlockShield.Instance.IsActive)
        {
            BlockShield.Instance.TriggerAbsorb();
            WaveManager.Instance?.UnregisterMinion(this);
            Destroy(gameObject);
            return;
        }

        GameManager.Instance?.TakeDamage(damageToPlayer);
        if (isBomber)
            WaveManager.Instance?.TriggerBomberEffect(bomberDisableDuration);
        WaveManager.Instance?.UnregisterMinion(this);
        Destroy(gameObject);
    }

    // ── 护盾清场：强制击杀（给分，触发死亡流程）───────────────────────────
    public void ForceKill()
    {
        if (IsDead) return;
        CurrentHits = maxHits - 1;
        TakeHit();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDead) return;
        if (col.gameObject.CompareTag("Ball"))
        {
            BallController ball = col.gameObject.GetComponent<BallController>();
            if (ball != null && ball.IsInvincible) return;
            TakeHit();
        }
    }

    public virtual void TakeHit()
    {
        if (IsDead) return;
        CurrentHits++;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit);
        OnHit();
        if (CurrentHits >= maxHits)
            Die();
    }

    protected virtual void OnHit() { }

    protected virtual void Die()
    {
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnKill);
        onDeath.Invoke(this);
        OnDie();
        Destroy(gameObject);
    }

    protected virtual void OnDie() { }
}
