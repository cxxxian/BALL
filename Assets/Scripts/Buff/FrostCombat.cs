using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人霜痕状态。由 FrostCombat 读写。
/// 短冻钉死移速；叠层视觉由 EnemyBuildStackVisual / Shader 负责。
/// </summary>
public class EnemyFrostState : MonoBehaviour
{
    public const int MarkCap = 3;

    public int MarkStacks { get; private set; }

    private float _freezeRemain;
    private float _savedMoveSpeed = -1f;
    private bool _holdingSpeed;

    public bool IsFrozen => _freezeRemain > 0f;

    public void AddMarks(int amount = 1, int cap = MarkCap)
    {
        if (amount <= 0) return;
        MarkStacks = Mathf.Min(cap, MarkStacks + amount);
    }

    public bool TryConsumeAllMarks(out int consumed)
    {
        consumed = MarkStacks;
        if (MarkStacks <= 0) return false;
        MarkStacks = 0;
        return true;
    }

    public void ClearMarks() => MarkStacks = 0;

    /// <summary>沙盒：直接灌满霜痕。</summary>
    public void DevFillMarks(int amount = MarkCap) =>
        MarkStacks = Mathf.Clamp(amount, 0, MarkCap);

    /// <summary>短冻：钉死 moveSpeed；可刷新时长。Boss 免疫。</summary>
    public void ApplyShortFreeze(float duration)
    {
        if (duration <= 0f) return;
        var enemy = GetComponent<EnemyBase>();
        if (enemy == null || enemy.IsDead || enemy is Boss) return;
        if (enemy.moveSpeed <= 0f && !_holdingSpeed) return;

        if (!_holdingSpeed)
        {
            _savedMoveSpeed = enemy.moveSpeed;
            _holdingSpeed = true;
            enemy.moveSpeed = 0f;
            if (enemy.TryGetComponent(out Rigidbody2D rb))
                rb.velocity = Vector2.zero;
        }

        _freezeRemain = Mathf.Max(_freezeRemain, duration);
    }

    private void Update()
    {
        if (_freezeRemain <= 0f) return;
        _freezeRemain -= Time.deltaTime;
        if (_freezeRemain > 0f)
        {
            if (_holdingSpeed && TryGetComponent(out Rigidbody2D rb))
                rb.velocity = Vector2.zero;
            return;
        }

        _freezeRemain = 0f;
        if (!_holdingSpeed) return;

        var enemy = GetComponent<EnemyBase>();
        if (enemy != null && !enemy.IsDead && _savedMoveSpeed > 0f)
            enemy.moveSpeed = _savedMoveSpeed;

        _holdingSpeed = false;
        _savedMoveSpeed = -1f;
    }
}

/// <summary>
/// Frost 4b-3：霜痕 → 累积 → 霜爆；冰霜塔叠痕（非纯减速）。
/// 短冻 ≤1s；不大幅减速破坏高速手感。Permafrost Epic 最多额外扩散 2 次。
/// </summary>
public static class FrostCombat
{
    private static readonly Collider2D[] OverlapBuf = new Collider2D[64];

    public static string LastBurstReason { get; private set; } = "";
    public static float LastBurstUnscaledTime { get; private set; }
    public static int LastBurstHits { get; private set; }

    public static void OnBallHitEnemy(EnemyBase enemy, Vector2? hitPos = null)
    {
        if (enemy == null || enemy.IsDead) return;
        var bm = BuffManager.Instance;
        if (bm == null) return;

        Vector2 pos = hitPos ?? (Vector2)enemy.transform.position;

        if (bm.FrostMarkStacks > 0)
            ApplyMarks(enemy, bm.GetFrostMarksPerHit(), tryBurst: true, burstOrigin: pos, reason: "Ball");
        else if (bm.FrostBurstStacks > 0)
            TryBurstIfReady(enemy, pos, "Ball");
    }

    /// <summary>冰霜塔：给范围内敌人叠霜痕；有霜爆时每跳最多触发一次冰爆。</summary>
    public static void OnTowerPulse(Vector2 center, float radius, int marksPerTarget, int towerLevel)
    {
        var bm = BuffManager.Instance;
        if (bm == null) return;
        if (marksPerTarget <= 0) return;

        radius = Mathf.Max(0.5f, radius);
        int n = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuf);
        bool burstFired = false;
        string reason = $"FrostTowerL{Mathf.Max(1, towerLevel)}";

        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;
            // 塔只给 Minion 叠痕（Boss 免疫场上叠霜，Balance 对齐旧冻塔）
            if (enemy is Boss) continue;

            bool tryBurst = !burstFired && bm.FrostBurstStacks > 0;
            if (ApplyMarks(
                    enemy, marksPerTarget,
                    tryBurst: tryBurst,
                    burstOrigin: enemy.transform.position,
                    reason: reason))
                burstFired = true;
        }
    }

    /// <summary>沙盒：给场上全部敌人灌霜痕。</summary>
    public static void DevMarkAll(int stacks = EnemyFrostState.MarkCap)
    {
        foreach (var enemy in Object.FindObjectsOfType<EnemyBase>())
        {
            if (enemy == null || enemy.IsDead) continue;
            GetOrAddState(enemy).DevFillMarks(stacks);
            if (enemy.TryGetComponent(out EnemyBuildStackVisual vis))
                vis.ForceRefresh();
        }
    }

    private static bool ApplyMarks(
        EnemyBase enemy, int amount, bool tryBurst, Vector2 burstOrigin, string reason)
    {
        if (enemy == null || enemy.IsDead || amount <= 0) return false;
        var st = GetOrAddState(enemy);
        st.AddMarks(amount);
        if (tryBurst)
            return TryBurstIfReady(enemy, burstOrigin, reason);
        return false;
    }

    private static bool TryBurstIfReady(EnemyBase seed, Vector2 origin, string reason)
    {
        var bm = BuffManager.Instance;
        if (bm == null || bm.FrostBurstStacks <= 0) return false;
        if (seed == null || seed.IsDead) return false;

        var st = GetOrAddState(seed);
        int need = bm.GetFrostBurstThreshold();
        if (st.MarkStacks < need) return false;
        if (!st.TryConsumeAllMarks(out _)) return false;

        return DetonateBurst(origin, reason, bm);
    }

    private static bool DetonateBurst(Vector2 center, string reason, BuffManager bm)
    {
        float radius = bm.GetFrostBurstRadius();
        int damage = bm.GetFrostBurstDamage();
        float freeze = bm.GetFrostBurstFreezeDuration();
        int maxExtraBursts = bm.GetPermafrostExtraBursts();

        var centers = new Queue<Vector2>();
        var hitOnce = new HashSet<EnemyBase>();
        var chainSeeds = new HashSet<EnemyBase>();
        centers.Enqueue(center);
        int totalHits = 0;
        int burstCount = 0;

        while (centers.Count > 0 && burstCount <= maxExtraBursts)
        {
            Vector2 burstCenter = centers.Dequeue();
            EnemyBase chainTarget = null;
            if (burstCount < maxExtraBursts)
                chainTarget = FindPermafrostTarget(burstCenter, radius, chainSeeds);

            if (chainTarget != null)
            {
                chainSeeds.Add(chainTarget);
                if (chainTarget.TryGetComponent(out EnemyFrostState targetState))
                    targetState.ClearMarks();
                centers.Enqueue(chainTarget.transform.position);
            }

            int n = Physics2D.OverlapCircleNonAlloc(burstCenter, radius, OverlapBuf);
            int localHits = 0;
            for (int i = 0; i < n; i++)
            {
                var col = OverlapBuf[i];
                if (col == null || !col.CompareTag("Enemy")) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) continue;

                if (enemy.TryGetComponent(out EnemyFrostState state))
                    state.ClearMarks();
                if (!hitOnce.Add(enemy)) continue;

                enemy.TakeHit(damage, isFromBall: false, burstCenter);
                totalHits++;
                localHits++;

                if (!(enemy is Boss))
                    GetOrAddState(enemy).ApplyShortFreeze(freeze);
            }

            if (localHits > 0)
                SpawnBurstFx(burstCenter, radius);
            burstCount++;
        }

        if (totalHits <= 0) return false;

        LastBurstHits = totalHits;
        LastBurstReason = $"{reason} x{totalHits} (chain {Mathf.Max(0, burstCount - 1)})";
        LastBurstUnscaledTime = Time.unscaledTime;

        ImpactFX.Instance?.SpawnHit(center, new Color(0.6f, 0.92f, 1f), 1.1f);
        if (totalHits >= 2)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        return true;
    }

    /// <summary>只沿未命中的带霜痕目标扩散；半径有限且每段最多触发一次。</summary>
    private static EnemyBase FindPermafrostTarget(
        Vector2 center, float burstRadius, HashSet<EnemyBase> alreadyChained)
    {
        float searchRadius = burstRadius * 2.5f;
        int n = Physics2D.OverlapCircleNonAlloc(center, searchRadius, OverlapBuf);
        EnemyBase best = null;
        float bestDistance = searchRadius * searchRadius;
        float burstRadiusSq = burstRadius * burstRadius;

        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead || alreadyChained.Contains(enemy)) continue;
            if (!enemy.TryGetComponent(out EnemyFrostState state) || state.MarkStacks <= 0)
                continue;

            float distance = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (distance <= burstRadiusSq || distance >= bestDistance) continue;
            bestDistance = distance;
            best = enemy;
        }

        return best;
    }

    private static void SpawnBurstFx(Vector2 center, float radius)
    {
        var go = new GameObject("FrostBurstFx");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBurstSprite();
        sr.color = new Color(0.65f, 0.92f, 1f, 0.75f);
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = 5;
        go.AddComponent<FrostBurstFx>().Play(radius);
    }

    private static Sprite CreateBurstSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float half = size * 0.5f;
        float r = half - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r)
                {
                    float alpha = 1f - dist / r;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.55f));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }

    private static EnemyFrostState GetOrAddState(EnemyBase enemy)
    {
        if (!enemy.TryGetComponent(out EnemyFrostState state))
            state = enemy.gameObject.AddComponent<EnemyFrostState>();
        EnemyBuildStackVisual.EnsureOn(enemy);
        return state;
    }
}

/// <summary>一次性冰爆环扩散（运行时生成，自销毁）。</summary>
public class FrostBurstFx : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _duration = 0.4f;
    private float _elapsed;
    private Vector3 _targetScale = Vector3.one;

    public void Play(float radius)
    {
        _sr = GetComponent<SpriteRenderer>();
        _targetScale = new Vector3(radius * 2f, radius * 2f, 1f);
        _elapsed = 0f;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        float ease = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.Lerp(Vector3.zero, _targetScale, ease);
        if (_sr != null)
        {
            var c = _sr.color;
            c.a = Mathf.Lerp(0.75f, 0f, t);
            _sr.color = c;
        }
        if (t >= 1f)
            Destroy(gameObject);
    }
}
