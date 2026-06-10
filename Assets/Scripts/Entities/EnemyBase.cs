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
    public bool checkBottomLine = true;

    private const float FallbackBottomLineY = -8.5f;

    public int  CurrentHits { get; protected set; } = 0;
    public bool IsDead      { get; protected set; } = false;

    public UnityEvent<EnemyBase> onDeath = new UnityEvent<EnemyBase>();

    protected Rigidbody2D     _rb;
    public SpriteRenderer MainSR { get; protected set; }
    public    Color          BaseColor { get; protected set; } = Color.white;

    protected virtual void Awake()
    {
        _rb    = GetComponent<Rigidbody2D>();
        MainSR = GetComponent<SpriteRenderer>();
    }

    protected virtual void LateUpdate()
    {
        if (IsDead) return;
        if (checkBottomLine)
        {
            bool shieldUp = BlockShield.Instance != null && BlockShield.Instance.IsActive;
            float checkY  = shieldUp ? BlockShield.Instance.shieldY : GetMinionBottomLineY();
            if (transform.position.y <= checkY)
                OnReachBottom();
        }
    }

    private static float GetMinionBottomLineY()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.minionBottomLineY : FallbackBottomLineY;
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
            FinishBottomExit(NeonColors.Active.GetBase(NeonRole.SkillShield), 0.85f);
            return;
        }

        bool tookDamage = true;
        if (BuffManager.Instance != null &&
            BuffManager.Instance.TryConsumeHeartGuard(out bool showShieldVfx))
        {
            if (showShieldVfx)
                HUDController.Instance?.PlayHeartGuardShieldVfx();
            tookDamage = false;
        }
        else
            GameManager.Instance?.TakeDamage(damageToPlayer);

        if (isBomber)
            WaveManager.Instance?.TriggerBomberEffect(bomberDisableDuration);

        FinishBottomExit(GetDissolveColor(), tookDamage ? 1f : 0.65f);
    }

    protected virtual Color GetDissolveColor()
    {
        var sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr.color : NeonColors.Active.GetBase(NeonRole.Danger);
    }

    private void FinishBottomExit(Color dissolveColor, float intensity)
    {
        HideVisualForDissolve();
        ImpactFX.Instance?.SpawnBottomDissolve(transform.position, dissolveColor, intensity);
        WaveManager.Instance?.UnregisterMinion(this);
        Destroy(gameObject);
    }

    private void HideVisualForDissolve()
    {
        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;
        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            canvas.gameObject.SetActive(false);
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
            // 斩杀链由 BallController 统一结算，避免双次 TakeHit
            if (ball != null && ball.IsExecuteChainActive) return;
            Vector2? hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2?)null;
            TakeHit(1, true, hitPos);
        }
    }

    public virtual void TakeHit(int damage = 1, bool isFromBall = false, Vector2? hitPos = null)
    {
        if (IsDead) return;
        if (isFromBall && BuffManager.Instance != null)
            damage += BuffManager.Instance.BallDamageBonus;
        CurrentHits += damage;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit * damage);
        OnHit();
        EnemyJuice.OnHit(this, isFromBall, hitPos);

        if (CurrentHits >= maxHits)
        {
            // Boss 击杀触发完整特效
            if (isFromBall && this is Boss)
            {
                VFXDirector.Instance?.TriggerBossKillEffect(transform.position);
            }
            Die();
        }
    }

    protected virtual void OnHit() { }

    protected virtual void Die()
    {
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnKill);
        EnemyJuice.OnKill(this, transform.position);
        onDeath.Invoke(this);
        OnDie();
        Destroy(gameObject);
    }

    protected virtual void OnDie() { }
}
