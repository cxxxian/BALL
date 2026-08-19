using UnityEngine;

/// <summary>
/// Buff / 塔测试用下落敌人。外观对齐正式 Minion，不依赖 GameManager。
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
        scoreOnHit = 0;
        scoreOnKill = 0;
        damageToPlayer = 0;
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

        sr.material = CyberVisualFactory.UnlitMaterial;

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
