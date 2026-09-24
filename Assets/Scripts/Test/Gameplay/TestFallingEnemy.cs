using UnityEngine;

/// <summary>
/// Buff / 塔测试用下落敌人。外观对齐正式 Minion，不依赖 WaveManager。
/// 触底：默认不扣血；Harness 可将 damageToPlayer&gt;0 打开以测护心/生命。
/// </summary>
public class TestFallingEnemy : EnemyBase
{
    public void Configure(MinionDefinition def, int hp, float speed)
    {
        if (def == null)
        {
            Debug.LogError("[TestFallingEnemy] MinionDefinition missing.");
            return;
        }

        maxHits = Mathf.Max(1, hp);
        moveSpeed = Mathf.Max(0.05f, speed);
        scoreOnHit = Mathf.Max(1, def.scoreOnHit);
        scoreOnKill = Mathf.Max(1, def.scoreOnKill > 0 ? def.scoreOnKill : def.scoreOnHit * 5);
        damageToPlayer = 0; // Harness 可再打开
        isBomber = def.isBomber;
        checkBottomLine = true;

        ApplyMinionVisual(def);
    }

    protected override void FixedUpdate()
    {
        if (IsDead) return;
        ApplyMovement();
    }

    protected override void ApplyMovement()
    {
        if (_rb != null)
            _rb.velocity = Vector2.down * moveSpeed;
        else
            transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
    }

    protected override void OnReachBottom()
    {
        if (IsDead) return;
        IsDead = true;
        if (_rb != null) _rb.velocity = Vector2.zero;

        // 可选：测护心符 / 最大生命（Harness 设 damageToPlayer>0）
        if (damageToPlayer > 0 && GameManager.Instance != null)
        {
            if (BuffManager.Instance != null &&
                BuffManager.Instance.TryConsumeHeartGuard(out bool showShieldVfx))
            {
                if (showShieldVfx)
                    Debug.Log("[BuffSandbox] HeartGuard blocked bottom damage");
            }
            else
            {
                // 沙盒保底 1 命，避免进 GameOver 打断测试
                var gm = GameManager.Instance;
                if (gm.Lives <= 1)
                    Debug.Log("[BuffSandbox] Bottom hit at 1 HP (sandbox keeps you alive)");
                else
                    gm.TakeDamage(damageToPlayer);
            }
        }

        GetComponent<MinionHealthBar>()?.OnEnemyDeath();
        onDeath.Invoke(this);
        Destroy(gameObject);
    }

    private void ApplyMinionVisual(MinionDefinition def)
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = gameObject.AddComponent<SpriteRenderer>();

        sr.material = EnemyBuildStackVisual.SharedMaterial != null
            ? EnemyBuildStackVisual.SharedMaterial
            : CyberVisualFactory.UnlitMaterial;

        if (def.sprite != null)
        {
            sr.sprite = def.sprite;
            float spriteWidth = def.sprite.rect.width / def.sprite.pixelsPerUnit;
            if (spriteWidth > 0f)
            {
                float targetScale = 0.9f / spriteWidth;
                transform.localScale = new Vector3(targetScale, targetScale, 1f);

                var circleCol = GetComponent<CircleCollider2D>();
                if (circleCol != null)
                    circleCol.radius = (0.42f / 0.9f) * spriteWidth;
            }
        }
        else
        {
            sr.sprite = CyberVisualFactory.CreateMinionSprite(def.baseColor, def.isBomber);
            transform.localScale = Vector3.one;
            var circleCol = GetComponent<CircleCollider2D>();
            if (circleCol != null)
                circleCol.radius = 0.42f;
        }

        Color baseColor = NeonColors.ApplyMinionBase(def.baseColor);
        BaseColor = baseColor;
        MainSR = sr;
        sr.color = baseColor;
        sr.sortingOrder = 2;

        EnemyBuildStackVisual.EnsureOn(this);
        if (TryGetComponent(out EnemyElectricState elec))
            elec.ClearCharge();
        if (TryGetComponent(out EnemyFrostState frost))
            frost.ClearMarks();
        if (TryGetComponent(out EnemyBuildStackVisual buildVis))
            buildVis.ForceRefresh();

        var healthBar = GetComponent<MinionHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<MinionHealthBar>();
        float barW = def.healthBarWidthScale > 0.01f
            ? def.healthBarWidthScale
            : (maxHits > 1 ? 0.88f : 0.58f);
        healthBar.Configure(true, def.healthBarVisibleDuration, 0f, barW);

        if (GetComponent<MinionFallPreview>() == null)
            gameObject.AddComponent<MinionFallPreview>();
    }
}
