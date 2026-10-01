using UnityEngine;

/// <summary>正式波次与独立测试场共用的小兵生成路径。</summary>
public static class Phase6EnemyFactory
{
    public static Minion Spawn(MinionDefinition def, Vector3 position, int waveIndex = 0)
    {
        if (def == null) return null;

        var go = new GameObject($"Minion_{def.minionName}");
        go.transform.position = position;
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.mass = 80f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.42f;

        var minion = go.AddComponent<Minion>();
        minion.Initialize(def, waveIndex);

        var boss = WaveManager.Instance != null ? WaveManager.Instance.CurrentBoss : null;
        if (boss != null)
        {
            var bossCollider = boss.GetComponent<Collider2D>();
            if (bossCollider != null) Physics2D.IgnoreCollision(col, bossCollider);
        }

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.RegisterMinion(minion);
            minion.onDeath.AddListener(_ =>
            {
                if (WaveManager.Instance != null)
                    WaveManager.Instance.UnregisterMinion(minion);
            });
        }
        return minion;
    }
}
