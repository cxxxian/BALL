using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    // ── 全局小兵速度倍率（掉球时临时提升）────────────────────────────────
    public static float MinionSpeedMultiplier { get; private set; } = 1f;

    [Header("Boss Definitions (按 Wave 顺序，超出后循环末尾)")]
    public BossDefinition[] bossDefinitions;

    [Header("Wave Settings")]
    [Tooltip("Boss 出场后的首次出手延迟（秒）")]
    public float firstSpawnDelay  = 2f;
    [Tooltip("每波结束到下一波开始的等待时间")]
    public float postWaveDelay    = 3f;
    [Tooltip("自动跳过 Buff 选择的超时时间（无 Buff UI 时生效）")]
    public float buffSkipTimeout  = 1.5f;

    [Header("Ball Drop Penalty")]
    [Tooltip("掉球后小兵加速倍率")]
    public float speedBoostMult   = 1.5f;
    [Tooltip("小兵加速持续秒数")]
    public float speedBoostDuration = 3f;

    [Header("Spawn Area (世界坐标 X 范围)")]
    public float spawnMinX = -1.5f;
    public float spawnMaxX =  1.5f;

    [Header("Boss Y Position")]
    public float bossSpawnY = 6.5f;

    private readonly List<EnemyBase> _activeMinions = new List<EnemyBase>();
    private Boss    _currentBoss;
    private int     _currentWave = 0;
    private Coroutine _speedBoostCoroutine;
    private Coroutine _bomberCoroutine;
    private float     _bumperDisabledUntil = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        MinionSpeedMultiplier = 1f;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
        }
    }

    private void OnDestroy()
    {
        MinionSpeedMultiplier = 1f;
    }

    // ── 游戏开始 ──────────────────────────────────────────────────────────
    private void OnGameStart()
    {
        StopAllCoroutines();
        ClearAll();
        _currentWave = 0;
        MinionSpeedMultiplier = 1f;
        StartCoroutine(WaveLoop());
    }

    // ── 掉球惩罚：小兵加速 ───────────────────────────────────────────────
    private void OnBallLost()
    {
        if (_speedBoostCoroutine != null) StopCoroutine(_speedBoostCoroutine);
        _speedBoostCoroutine = StartCoroutine(SpeedBoostRoutine());
    }

    private IEnumerator SpeedBoostRoutine()
    {
        MinionSpeedMultiplier = speedBoostMult;
        yield return new WaitForSeconds(speedBoostDuration);
        MinionSpeedMultiplier = 1f;
    }

    // ── 主循环 ────────────────────────────────────────────────────────────
    private IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(1f);
        while (true)
        {
            if (GameManager.Instance == null) yield break;
            if (!GameManager.Instance.IsPlaying()) { yield return null; continue; }

            yield return StartCoroutine(RunWave(_currentWave));

            if (GameManager.Instance == null || GameManager.Instance.State == GameState.GameOver)
                yield break;

            // 波次完成 → 触发 Buff 选择
            GameManager.Instance.CompleteWave();

            // 等待 Buff UI 处理，超时则自动跳过
            float elapsed = 0f;
            while (GameManager.Instance.State == GameState.BuffSelection)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= buffSkipTimeout)
                    GameManager.Instance.OnBuffSelectionDone();
                yield return null;
            }

            yield return new WaitForSeconds(postWaveDelay);
            _currentWave++;
        }
    }

    private IEnumerator RunWave(int waveIndex)
    {
        _currentBoss = SpawnBoss(waveIndex);

        yield return new WaitForSeconds(firstSpawnDelay);

        // 等待 Boss 死亡或游戏结束
        yield return new WaitUntil(() =>
            _currentBoss == null ||
            _currentBoss.IsDead ||
            GameManager.Instance == null ||
            GameManager.Instance.State == GameState.GameOver);

        // 清理残余小兵；若 Boss 因 GameOver 退出（未被球击杀），显式销毁
        ClearMinions();
        if (_currentBoss != null)
        {
            Destroy(_currentBoss.gameObject);
            _currentBoss = null;
        }
    }

    // ── Boss 生成 ─────────────────────────────────────────────────────────
    private Boss SpawnBoss(int waveIndex)
    {
        BossDefinition def = GetBossDefinition(waveIndex);
        if (def == null)
        {
            Debug.LogWarning("[WaveManager] No BossDefinition assigned.");
            return null;
        }

        float cx      = (spawnMinX + spawnMaxX) * 0.5f;
        float halfRange = def.moveRangeX * 0.5f;
        float minX    = cx - halfRange;
        float maxX    = cx + halfRange;

        var go = new GameObject($"Boss_W{waveIndex + 1}");
        go.transform.position = new Vector3(cx, bossSpawnY, 0f);
        go.tag = "Enemy";

        var rb              = go.AddComponent<Rigidbody2D>();
        rb.gravityScale     = 0f;
        rb.mass             = 500f;
        rb.freezeRotation   = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation    = RigidbodyInterpolation2D.Interpolate;

        var col             = go.AddComponent<BoxCollider2D>();
        col.size            = new Vector2(0.85f, 0.85f);

        var boss = go.AddComponent<Boss>();
        boss.Initialize(def, minX, maxX);
        boss.onDeath.AddListener(_ => _currentBoss = null);

        return boss;
    }

    private BossDefinition GetBossDefinition(int waveIndex)
    {
        if (bossDefinitions == null || bossDefinitions.Length == 0) return null;
        return bossDefinitions[Mathf.Min(waveIndex, bossDefinitions.Length - 1)];
    }

    // ── 小兵生成（由 Boss 调用）──────────────────────────────────────────
    public void SpawnMinion(MinionDefinition def, Vector3 position)
    {
        if (def == null) return;
        if (GameManager.Instance == null || GameManager.Instance.State == GameState.GameOver) return;

        var go = new GameObject($"Minion_{def.minionName}");
        go.transform.position = position;
        go.tag = "Enemy";

        var rb              = go.AddComponent<Rigidbody2D>();
        rb.gravityScale     = 0f;
        rb.mass             = 80f;
        rb.freezeRotation   = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation    = RigidbodyInterpolation2D.Interpolate;

        var col             = go.AddComponent<CircleCollider2D>();
        col.radius          = 0.27f;

        var minion = go.AddComponent<Minion>();
        minion.Initialize(def);
        RegisterMinion(minion);
        minion.onDeath.AddListener(_ => UnregisterMinion(minion));
    }

    // ── 小兵注册管理 ─────────────────────────────────────────────────────
    public void RegisterMinion(EnemyBase e)
    {
        if (!_activeMinions.Contains(e)) _activeMinions.Add(e);
    }

    public void UnregisterMinion(EnemyBase e)
    {
        _activeMinions.Remove(e);
    }

    // ── 爆弹兵效果：禁用所有 Bumper ──────────────────────────────────────
    public void TriggerBomberEffect(float duration)
    {
        _bumperDisabledUntil = Time.time + duration;
        if (_bomberCoroutine == null)
            _bomberCoroutine = StartCoroutine(BomberRoutine());
    }

    private IEnumerator BomberRoutine()
    {
        var bumpers = FindObjectsOfType<Bumper>();
        foreach (var b in bumpers) b.SetDisabled(true);
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);

        // 用时间戳循环等待，允许被其他爆弹兵追加延长持续时间
        while (Time.time < _bumperDisabledUntil)
        {
            yield return null;
        }

        // 最终恢复
        foreach (var b in bumpers) if (b != null) b.SetDisabled(false);
        _bomberCoroutine = null;
    }

    // ── 清理 ─────────────────────────────────────────────────────────────
    private void ClearMinions()
    {
        _activeMinions.RemoveAll(e => e == null);
        foreach (var m in new List<EnemyBase>(_activeMinions))
            if (m != null) Destroy(m.gameObject);
        _activeMinions.Clear();
    }

    private void ClearAll()
    {
        ClearMinions();
        if (_currentBoss != null) { Destroy(_currentBoss.gameObject); _currentBoss = null; }

        // 兜底：销毁场景中所有残留的 Enemy 标签对象（防止引用丢失时泄漏）
        foreach (var go in GameObject.FindGameObjectsWithTag("Enemy"))
            if (go != null) Destroy(go);
    }
}
