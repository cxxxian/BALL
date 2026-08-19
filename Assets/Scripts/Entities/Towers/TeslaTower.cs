using UnityEngine;

public class TeslaTower : MonoBehaviour
{
    public int level = 1;
    public float attackRadius = 5.0f;
    public float baseAttackInterval = 4.0f;
    public int baseDamage = 2;

    private float _timer = 0f;
    private int _arcSeed;

    private void Awake()
    {
        TeslaArcFX.EnsureInstance();

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreateTeslaSprite();
        sr.color = new Color(0f, 0.95f, 1f, 1f);
        var cyberShader = Shader.Find("Custom/CyberPulseSprite");
        sr.material = cyberShader != null ? new Material(cyberShader) : new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 3;
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            float interval = Mathf.Max(2.0f, baseAttackInterval - level * 0.5f);
            if (DebuffManager.Instance != null)
                interval *= DebuffManager.Instance.TowerAttackIntervalMultiplier;
            _timer = interval;
            AttackSingleTarget();
        }
    }

    private void AttackSingleTarget()
    {
        int damage = baseDamage + level * 2;
        float radius = attackRadius + (level - 1) * 0.25f;
        Vector2 towerPos = transform.position;

        EnemyBase target = FindBottomThreatTarget(towerPos, radius);
        bool hit = target != null;
        if (hit)
        {
            target.TakeHit(damage);
            int seed = _arcSeed++;
            TeslaArcFX.Instance?.SpawnArc(towerPos, target.transform.position, seed);
            ImpactFX.Instance?.SpawnHit(
                target.transform.position,
                NeonColors.Active.GetBase(NeonRole.TowerTesla),
                0.55f);
        }

        JuiceRouter.TowerFire(towerPos, NeonRole.TowerTesla, hit);
    }

    /// <summary>
    /// 攻击范围内、非 Boss：优先 Y 最低（最接近触底），其次距塔心更近。
    /// </summary>
    private static EnemyBase FindBottomThreatTarget(Vector2 towerPos, float radius)
    {
        float radiusSq = radius * radius;
        Collider2D[] cols = Physics2D.OverlapCircleAll(towerPos, radius);

        EnemyBase best = null;
        float bestY = float.MaxValue;
        float bestDistSq = float.MaxValue;

        foreach (var c in cols)
        {
            if (!c.CompareTag("Enemy")) continue;
            var enemy = c.GetComponent<EnemyBase>();
            if (enemy == null || enemy.IsDead || enemy is Boss) continue;

            Vector2 pos = enemy.transform.position;
            float distSq = (pos - towerPos).sqrMagnitude;
            if (distSq > radiusSq) continue;

            float y = pos.y;
            if (y < bestY - 0.001f || (Mathf.Abs(y - bestY) <= 0.001f && distSq < bestDistSq))
            {
                best = enemy;
                bestY = y;
                bestDistSq = distSq;
            }
        }

        return best;
    }

    private static Sprite CreateTeslaSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= half - 2f)
                {
                    if (dist > half - 6f)
                    {
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360f;
                        if (angle % 90f < 70f)
                            tex.SetPixel(x, y, Color.white);
                        else
                            tex.SetPixel(x, y, new Color(0f, 0.4f, 0.6f, 0.8f));
                    }
                    else if (dist < 8f)
                        tex.SetPixel(x, y, Color.white);
                    else if (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f)
                        tex.SetPixel(x, y, new Color(0.6f, 1f, 1f, 0.9f));
                    else
                        tex.SetPixel(x, y, new Color(0f, 0.1f, 0.2f, 0.6f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
