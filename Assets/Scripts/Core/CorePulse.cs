using UnityEngine;

/// <summary>球心震爆：身份技 Core Pulse（不依赖已删除的 Pulse Buff 线）。</summary>
public class CorePulse : MonoBehaviour
{
    public static CorePulse Instance { get; private set; }

    private static readonly Collider2D[] OverlapBuf = new Collider2D[48];

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static CorePulse EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(CorePulse));
        return go.AddComponent<CorePulse>();
    }

    /// <summary>释放震爆。返回值保留接口兼容（恒为 0，CD 回退由身份技自身处理）。</summary>
    public float Activate()
    {
        var ball = BallController.Instance;
        if (ball == null) return 0f;

        Vector2 center = ball.transform.position;
        float radius = Config != null ? Config.corePulseRadius : 2.5f;
        float mult = Config != null ? Config.corePulseDamageMult : 2f;
        float knock = Config != null ? Config.corePulseKnockback : 2.5f;
        float waveDur = Config != null ? Config.corePulseWaveDuration : 0.4f;

        Color pulseColor = new Color(0.45f, 0.95f, 1f, 1f);

        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        SlowMoFX.Instance?.PulseFlash(pulseColor, 0.4f, 0.1f);
        ImpactFX.Instance?.SpawnCorePulseWave(center, radius, pulseColor, waveDur);

        int n = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuf);
        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;

            Vector2 hitPos = enemy.transform.position;
            int baseDmg = Mathf.Max(1, Mathf.RoundToInt(1f * mult));

            enemy.TakeHitFromCorePulse(baseDmg, hitPos);

            if (!(enemy is Boss))
                ApplyKnockback(enemy, center, enemy.IsDead ? knock * 0.35f : knock);
        }

        return 0f;
    }

    private static void ApplyKnockback(EnemyBase enemy, Vector2 center, float force)
    {
        if (force <= 0.01f || enemy == null) return;
        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb == null) return;
        Vector2 dir = ((Vector2)enemy.transform.position - center);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        else dir.Normalize();
        rb.AddForce(dir * force, ForceMode2D.Impulse);
    }
}
