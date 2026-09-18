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

    /// <summary>Boss 击杀清场中：停步且禁止冲线扣血。</summary>
    public bool FrozenForWaveClear { get; private set; }

    /// <summary>协议校准：仅斩杀连锁可击杀，普通球撞击不掉血。</summary>
    public bool TutorialExecuteOnlyHits { get; set; }

    /// <summary>协议校准弹刀靶：弹珠、技能、挡板武器都不造成伤害。</summary>
    public bool TutorialInvulnerable { get; set; }

    /// <summary>含分裂等小数积攒的受击进度，供血条显示（避免「闪了但条不动」）。</summary>
    public float DamageProgress => CurrentHits + _ballHitCredit;

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
        if (IsDead || FrozenForWaveClear) return;
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
        if (IsDead || FrozenForWaveClear) return;
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

    /// <summary>Boss 击杀瞬间冻结：停步 + 禁止冲线，等待冲线解体。</summary>
    public void FreezeForWaveClear()
    {
        if (IsDead) return;
        FrozenForWaveClear = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
    }

    /// <summary>Boss 击杀后清场：复用冲线底部解体，不用 Boss 同款特效，不计分不伤玩家。</summary>
    public void DissolveAsBreachClear()
    {
        if (IsDead) return;
        IsDead = true;
        FrozenForWaveClear = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        GetHealthBar()?.OnEnemyDeath();
        HideVisualForDissolve();
        ImpactFX.Instance?.SpawnBottomDissolveCompact(transform.position, GetDissolveColor(), 0.72f);
        WaveManager.Instance?.UnregisterMinion(this);
        Destroy(gameObject);
    }

    /// <summary>护盾吸收清场：青闪后解体，不触发普通击杀粒子。</summary>
    public void DissolveFromShieldAbsorb()
    {
        if (IsDead) return;
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;
        int killPts = ResolveKillScore(scoreOnKill);
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(killPts);
        ScorePopUI.Spawn(transform.position, killPts);
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
        if (IsDead || TutorialInvulnerable) return;
        CurrentHits = maxHits - 1;
        TakeHit();
    }

    private float _ballHitCredit;

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDead || TutorialInvulnerable) return;

        Vector2? hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2?)null;

        // 幻影球：固定走倍率积攒（默认 0.5×）
        var phantom = col.gameObject.GetComponent<SplitPhantomBall>();
        if (phantom != null)
        {
            float scale = SplitProtocol.Instance != null ? SplitProtocol.Instance.DamageScale : 0.5f;
            TakeBallHitScaled(scale, hitPos);
            return;
        }

        // 主球：始终满伤；分裂期间也不吃幻影倍率
        if (col.gameObject.CompareTag("Ball"))
        {
            BallController ball = col.gameObject.GetComponent<BallController>();
            if (ball != null && ball.IsExecuteChainActive) return;
            // 教学靶：未开斩杀链前不掉血，避免误杀卡关
            if (TutorialExecuteOnlyHits && ball != null) return;
            if (ball != null)
            {
                TakeHit(1, true, hitPos);
                return;
            }

            // 其它带 Ball 标签但无 BallController 的物体：按满伤
            if (TutorialExecuteOnlyHits) return;
            TakeHit(1, true, hitPos);
        }
    }

    /// <summary>按倍率累计碰撞伤（分裂幻影 0.5× 等），凑整后走 TakeHit。</summary>
    public void TakeBallHitScaled(float scale, Vector2? hitPos = null)
    {
        if (IsDead || TutorialInvulnerable) return;
        float dmg = 1f;
        if (BuffManager.Instance != null)
            dmg += BuffManager.Instance.BallDamageBonus;
        if (ProtocolFieldDirector.Instance != null)
            dmg += ProtocolFieldDirector.Instance.TempBallDamageBonus;
        dmg *= Mathf.Max(0f, scale);
        _ballHitCredit += dmg;
        int whole = Mathf.FloorToInt(_ballHitCredit + 1e-4f);
        if (whole <= 0)
        {
            // 未凑满 1 点：仍播受击反馈，血条按 DamageProgress 显示半格进度
            OnHit();
            EnemyJuice.OnHit(this, true, hitPos);
            return;
        }
        _ballHitCredit -= whole;
        // 力量加成已计入 credit，避免 TakeHit 再加一次
        ApplyBallHits(whole, hitPos);
    }

    /// <summary>已含力量加成的整点伤害（分裂积攒兑现）。</summary>
    private void ApplyBallHits(int damage, Vector2? hitPos)
    {
        if (IsDead || damage <= 0) return;
        CurrentHits += damage;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit * damage);
        OnHit();
        EnemyJuice.OnHit(this, true, hitPos);

        if (CurrentHits >= maxHits)
        {
            if (this is Boss)
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

    public virtual void TakeHit(int damage = 1, bool isFromBall = false, Vector2? hitPos = null)
    {
        if (IsDead || TutorialInvulnerable) return;
        if (isFromBall && BuffManager.Instance != null)
            damage += BuffManager.Instance.BallDamageBonus;
        if (isFromBall && ProtocolFieldDirector.Instance != null)
            damage += ProtocolFieldDirector.Instance.TempBallDamageBonus;
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

    /// <summary>挡板武器直接伤害：不吃弹珠 Buff，仍走受击/击杀流程。</summary>
    public void TakeFlipperWeaponHit(int damage, Vector2 hitPos)
    {
        if (IsDead || TutorialInvulnerable || damage <= 0) return;

        CurrentHits += damage;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit * damage);
        OnHit();
        EnemyJuice.OnHit(this, false, hitPos);

        if (CurrentHits >= maxHits)
        {
            if (this is Boss)
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

    /// <summary>球心震爆命中：伤害只加一次力量加成；击杀走脉冲散落 Juice。</summary>
    public void TakeHitFromCorePulse(int baseDamage, Vector2 hitPos)
    {
        if (IsDead || TutorialInvulnerable) return;

        int damage = Mathf.Max(1, baseDamage);
        if (BuffManager.Instance != null)
            damage += BuffManager.Instance.BallDamageBonus;

        CurrentHits += damage;
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(scoreOnHit * damage);
        OnHit();

        if (CurrentHits >= maxHits)
        {
            if (this is Boss)
            {
                EnemyJuice.OnHit(this, true, hitPos);
                VFXDirector.Instance?.TriggerBossKillEffect(transform.position);
                HideVisualForDissolve();
                ImpactFX.Instance?.SpawnBossDissolve(transform.position, BaseColor, 1.5f);
                Die(skipKillJuice: true);
                return;
            }

            Die(skipKillJuice: true);
            EnemyJuice.OnPulseKill(this, hitPos);
            return;
        }

        EnemyJuice.OnHit(this, true, hitPos);
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
        if (this is Boss)
            WaveManager.Instance?.NotifyBossKilledClearField();
        int killPts = ResolveKillScore(scoreOnKill);
        if (GameManager.Instance != null)
            GameManager.Instance.AddScore(killPts);
        ScorePopUI.Spawn(transform.position, killPts);
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
