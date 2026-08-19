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
            float checkY = MinionLineRules.GetAttackLineY();
            if (GetFootY() <= checkY)
                OnReachBottom();
        }
    }

    private float GetFootY()
    {
        if (MainSR != null && MainSR.sprite != null)
            return MainSR.bounds.min.y;
        return transform.position.y;
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

    /// <summary>护盾吸收清场：青闪后解体，不触发普通击杀粒子。</summary>
    public void DissolveFromShieldAbsorb()
    {
        if (IsDead) return;
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(ResolveKillScore(scoreOnKill));
        GetHealthBar()?.OnEnemyDeath();
        WaveManager.Instance?.UnregisterMinion(this);
        HideVisualForDissolve();
        Color cyan = NeonColors.Active.GetBase(NeonRole.SkillShield);
        ImpactFX.Instance?.SpawnBottomDissolve(transform.position, cyan, 0.85f);
        Destroy(gameObject);
    }

    protected void HideVisualForDissolve()
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
            bool bossBallKill = isFromBall && this is Boss;
            if (bossBallKill)
            {
                VFXDirector.Instance?.TriggerBossKillEffect(transform.position);
                HideVisualForDissolve();
                ImpactFX.Instance?.SpawnBossDissolve(transform.position, BaseColor, 1.5f);
                Die(skipKillJuice: true);
                return;
            }
            Die();
        }
    }

    protected virtual void OnHit() { }

    private static int ResolveKillScore(int baseKillScore)
    {
        if (BuffManager.Instance == null) return baseKillScore;
        return BuffManager.Instance.ApplyKillScoreBonus(baseKillScore);
    }

    protected virtual void Die(bool skipKillJuice = false)
    {
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(ResolveKillScore(scoreOnKill));
        if (!skipKillJuice)
            EnemyJuice.OnKill(this, transform.position);
        else
            GetHealthBar()?.OnEnemyDeath();
        onDeath.Invoke(this);
        OnDie();
        Destroy(gameObject);
    }

    private IEnemyHealthBar GetHealthBar()
    {
        var minionBar = GetComponent<MinionHealthBar>();
        if (minionBar != null) return minionBar;
        return GetComponent<BossHealthBar>();
    }

    protected virtual void OnDie() { }
}
