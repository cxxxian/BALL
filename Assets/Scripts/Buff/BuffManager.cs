using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("Buff Pool (assign all BuffDefinition assets here)")]
    public List<BuffDefinition> buffPool = new List<BuffDefinition>();

    private readonly Dictionary<BuffEffectType, int> _stacks = new Dictionary<BuffEffectType, int>();
    private readonly Dictionary<BuffEffectType, float> _halfStackBonuses = new Dictionary<BuffEffectType, float>();

    private float _epicWeightPadding;
    private float _rareWeightPadding;

    // ── 对外暴露的数值属性 ─────────────────────────────────────
    public int BallDamageBonus         { get; private set; } = 0;
    public int MaxHPBonus              { get; private set; } = 0;
    public int ComboThresholdReduction { get; private set; } = 0;
    public int HeartGuardCharges       { get; private set; } = 0;
    public int MaxHeartGuardCharges    { get; private set; } = 0;
    /// <summary>击杀得分额外倍率增量（0.10 = +10%）。最终分 = base × (1 + ScoreOnKillBonus)。</summary>
    public float ScoreOnKillBonus      { get; private set; } = 0f;
    /// <summary>生命汲取层数：每累计 KillsRequired 只小兵，回复 stacks 点生命。</summary>
    public int HealOnKillStacks        { get; private set; }
    /// <summary>生命汲取所需击杀数（取自 BuffDefinition.effectValue）。</summary>
    public int HealOnKillKillsRequired { get; private set; } = 5;
    /// <summary>当前汲取进度（已击杀数，未达阈值）。沙盒 HUD 用。</summary>
    public int HealOnKillProgress => _healOnKillProgress;
    /// <summary>Electric Common：电荷残留层数（燃料效率）。</summary>
    public int ElectricChargeStacks    { get; private set; }
    /// <summary>Electric Rare：打火器层数（球上点火）。</summary>
    public int ElectricIgniterStacks   { get; private set; }
    /// <summary>Frost Common：霜痕层数（叠痕效率）。</summary>
    public int FrostMarkStacks         { get; private set; }
    /// <summary>Frost Rare：霜爆层数（满痕触发冰爆）。</summary>
    public int FrostBurstStacks        { get; private set; }
    /// <summary>Combo Common：连击动能层数。</summary>
    public int ComboMomentumStacks     { get; private set; }
    /// <summary>Combo Rare：过载奖励层数。</summary>
    public int ComboOverloadStacks     { get; private set; }

    public float EpicWeightPadding => _epicWeightPadding;

    private int _healOnKillProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable() => BindGameStart();

    private void Start() => BindGameStart();

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(ResetForNewGame);
    }

    private void BindGameStart()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.onGameStart.RemoveListener(ResetForNewGame);
        GameManager.Instance.onGameStart.AddListener(ResetForNewGame);
    }

    public int GetStacks(BuffEffectType type) =>
        _stacks.TryGetValue(type, out int v) ? v : 0;

    public BuffDefinition DrawRandomFromPool(BuffRarity rarity, int waveIndex)
    {
        if (buffPool == null || buffPool.Count == 0) return null;

        var eligible = new List<BuffDefinition>();
        var weights = new List<float>();
        float total = 0f;

        foreach (var b in buffPool)
        {
            if (b == null) continue;
            if (b.rarity != rarity) continue;
            if (GetStacks(b.effectType) >= b.maxStacks) continue;
            if (waveIndex < b.minWave) continue;
            if (b.effectType == BuffEffectType.ComboThresholdDown) continue; // Legacy 下池
            if (!IsTowerModEligible(b)) continue;

            float w = RunSession.GetBuffDrawWeight(b);
            eligible.Add(b);
            weights.Add(w);
            total += w;
        }

        if (eligible.Count == 0 || total <= 0f) return null;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < eligible.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
                return eligible[i];
        }

        return eligible[eligible.Count - 1];
    }

    public void ApplyBuff(BuffDefinition def, int extraStacks = 0)
    {
        if (def == null) return;
        int current = GetStacks(def.effectType);
        int toAdd = 1 + extraStacks;
        int room = def.maxStacks - current;
        if (room <= 0) return;

        toAdd = Mathf.Min(toAdd, room);
        _stacks[def.effectType] = current + toAdd;
        RecalculateStats();
        WriteBuildTags(def);

        if (def.effectType == BuffEffectType.HeartGuard)
            HeartGuardCharges = Mathf.Min(MaxHeartGuardCharges, HeartGuardCharges + toAdd);

        Debug.Log($"[BuffManager] Applied: {def.buffName}  stacks={_stacks[def.effectType]}/{def.maxStacks}");
    }

    private static void WriteBuildTags(BuffDefinition def)
    {
        // Universal 永不写 Tag；仅 Electric / Frost / Combo 构筑卡写入。
        if (def == null || def.category == BuffCategory.Universal) return;
        if (def.buildTags == null || def.buildTags.Length == 0) return;
        for (int i = 0; i < def.buildTags.Length; i++)
            RunSession.AddBuildTag(def.buildTags[i], 1);
    }

    /// <summary>小兵击杀回调（Boss 不计）。用于 Universal 生命汲取。</summary>
    public void NotifyMinionKilled()
    {
        if (HealOnKillStacks <= 0 || HealOnKillKillsRequired <= 0) return;

        _healOnKillProgress++;
        if (_healOnKillProgress < HealOnKillKillsRequired) return;

        _healOnKillProgress = 0;
        GameManager.Instance?.Heal(HealOnKillStacks);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[BuffManager] HealOnKill +{HealOnKillStacks} HP (every {HealOnKillKillsRequired} kills)");
#endif
    }

    /// <summary>电荷叠层：每命中加几层燃料（高叠略快）。</summary>
    public int GetChargePerHit() => ElectricChargeStacks >= 3 ? 2 : 1;

    // ── 打火器（Rare · 局部）────────────────────────────────
    /// <summary>打火器最大跳数：Lv1=2，之后最多 3（邻域小群）。</summary>
    public int GetIgniterMaxHops() =>
        Mathf.Clamp(1 + Mathf.Max(1, ElectricIgniterStacks), 2, 3);

    public float GetIgniterRadius() =>
        2.0f + 0.25f * Mathf.Max(0, ElectricIgniterStacks - 1);

    public float GetBallSparkCooldown() =>
        Mathf.Max(1.0f, 1.5f - 0.15f * Mathf.Max(0, ElectricIgniterStacks - 1));

    public int GetIgniterFuelThreshold() => 2;

    // ── 霜痕 / 霜爆（Frost · D-F1：2～3 层触发，短冻 ≤1s）──
    /// <summary>每命中叠几层霜痕（高叠略快）。</summary>
    public int GetFrostMarksPerHit() => FrostMarkStacks >= 3 ? 2 : 1;

    /// <summary>霜爆触发所需霜痕：Lv1=3，Lv2+=2。</summary>
    public int GetFrostBurstThreshold() =>
        FrostBurstStacks >= 2 ? 2 : 3;

    public float GetFrostBurstRadius() =>
        2.4f + 0.3f * Mathf.Max(0, FrostBurstStacks - 1);

    /// <summary>冰爆伤害（弱于主球叠伤；Boss 可受伤但短冻只对 Minion）。</summary>
    public int GetFrostBurstDamage() =>
        Mathf.Clamp(1 + Mathf.Max(0, FrostBurstStacks - 1), 1, 3);

    /// <summary>短冻时长，硬顶 1s。</summary>
    public float GetFrostBurstFreezeDuration() =>
        Mathf.Min(1f, 0.55f + 0.15f * Mathf.Max(0, FrostBurstStacks - 1));

    // ── Combo 动能 / 过载（D-C1：每 N 命中额外 +1；不绑 Pulse）──
    /// <summary>每多少次有效命中额外 +1 Combo。Lv1=4，Lv2=3，Lv3=2。</summary>
    public int GetMomentumHitsPerBonus() =>
        Mathf.Max(2, 5 - Mathf.Max(1, ComboMomentumStacks));

    /// <summary>过载阶段间隔（每 N Combo 触发一次）。</summary>
    public int GetOverloadInterval() => 5;

    /// <summary>过载首次触发门槛。</summary>
    public int GetOverloadFirstThreshold() => 5;

    /// <summary>过载时额外分数（随层数）。</summary>
    public int GetOverloadScoreBonus() =>
        30 + 20 * Mathf.Max(1, ComboOverloadStacks);

    /// <summary>过载后接下来几次球命中 +1 伤。</summary>
    public int GetOverloadTempHitCharges() =>
        1 + Mathf.Max(1, ComboOverloadStacks);

    // ── 特斯拉连锁（Rare 建筑 · 明显强于打火器，随塔等级涨）──
    /// <summary>L1 半屏感；L3 接近大半张台面。全图留给 Epic 导电网络。</summary>
    public static int GetTeslaMaxHops(int towerLevel) =>
        Mathf.Clamp(3 + Mathf.Max(1, towerLevel), 4, 7);

    public static float GetTeslaChainRadius(int towerLevel) =>
        towerLevel switch
        {
            <= 1 => 5.0f,
            2 => 8.0f,
            _ => 12.0f
        };

    public static int GetTeslaFuelThreshold(int towerLevel) =>
        towerLevel >= 3 ? 1 : 2;

    public void ApplyPurpleScrap(BuffDefinition def)
    {
        if (def == null) return;

        int current = GetStacks(def.effectType);
        if (current > 0 && current < def.maxStacks)
        {
            ApplyBuff(def);
            return;
        }

        if (current >= def.maxStacks)
        {
            AddRareWeightPadding(0.03f);
            return;
        }

        if (IsStatEffect(def.effectType))
        {
            ApplyHalfStack(def);
            return;
        }

        AddRareWeightPadding(0.03f);
    }

    public void ApplyHalfStack(BuffDefinition def)
    {
        if (def == null || !IsStatEffect(def.effectType)) return;

        float half = def.effectValue * 0.5f;
        if (!_halfStackBonuses.ContainsKey(def.effectType))
            _halfStackBonuses[def.effectType] = 0f;
        _halfStackBonuses[def.effectType] += half;
        RecalculateStats();
        Debug.Log($"[BuffManager] Half-stack: {def.buffName} (+{half})");
    }

    public void AddEpicWeightPadding(float amount, float cap)
    {
        _epicWeightPadding = Mathf.Min(cap, _epicWeightPadding + amount);
    }

    public void AddRareWeightPadding(float amount)
    {
        _rareWeightPadding += amount;
    }

    public float ConsumeRareWeightPadding()
    {
        float v = _rareWeightPadding;
        _rareWeightPadding = 0f;
        return v;
    }

    public static bool IsStatEffect(BuffEffectType type) =>
        type == BuffEffectType.BallDamageUp ||
        type == BuffEffectType.MaxHPUp ||
        type == BuffEffectType.ComboThresholdDown ||
        type == BuffEffectType.ScoreOnKillUp;

    /// <summary>应用赏金猎人等击杀分倍率；命中分不受影响。</summary>
    public int ApplyKillScoreBonus(int baseKillScore)
    {
        if (baseKillScore <= 0 || ScoreOnKillBonus <= 0f) return baseKillScore;
        return Mathf.Max(0, Mathf.RoundToInt(baseKillScore * (1f + ScoreOnKillBonus)));
    }

    public static bool IsTowerBuildEffect(BuffEffectType type) =>
        type == BuffEffectType.DeployTeslaCoil ||
        type == BuffEffectType.DeployFrostTower;

    private static bool IsTowerModEligible(BuffDefinition def) => true;

    private void RecalculateStats()
    {
        BallDamageBonus         = 0;
        MaxHPBonus              = 0;
        ComboThresholdReduction = 0;
        MaxHeartGuardCharges    = 0;
        ScoreOnKillBonus        = 0f;
        HealOnKillStacks        = 0;
        HealOnKillKillsRequired = 5;
        ElectricChargeStacks    = 0;
        ElectricIgniterStacks   = 0;
        FrostMarkStacks         = 0;
        FrostBurstStacks        = 0;
        ComboMomentumStacks     = 0;
        ComboOverloadStacks     = 0;

        foreach (var def in buffPool)
        {
            if (def == null) continue;
            int stacks = GetStacks(def.effectType);
            if (stacks <= 0) continue;
            switch (def.effectType)
            {
                case BuffEffectType.BallDamageUp:
                    BallDamageBonus += Mathf.RoundToInt(def.effectValue * stacks);
                    break;
                case BuffEffectType.MaxHPUp:
                    MaxHPBonus += Mathf.RoundToInt(def.effectValue * stacks);
                    break;
                case BuffEffectType.ComboThresholdDown:
                    // Legacy：连击大师已下池；若仍残留层数不计 Pulse 门槛（4b-4 解耦）
                    break;
                case BuffEffectType.ScoreOnKillUp:
                    ScoreOnKillBonus += def.effectValue * stacks;
                    break;
                case BuffEffectType.HealOnKill:
                    HealOnKillStacks = stacks;
                    HealOnKillKillsRequired = Mathf.Max(1, Mathf.RoundToInt(def.effectValue));
                    break;
                case BuffEffectType.ElectricCharge:
                    ElectricChargeStacks = stacks;
                    break;
                case BuffEffectType.ElectricIgniter:
                    ElectricIgniterStacks = stacks;
                    break;
                case BuffEffectType.FrostMark:
                    FrostMarkStacks = stacks;
                    break;
                case BuffEffectType.FrostBurst:
                    FrostBurstStacks = stacks;
                    break;
                case BuffEffectType.ComboMomentum:
                    ComboMomentumStacks = stacks;
                    break;
                case BuffEffectType.ComboOverload:
                    ComboOverloadStacks = stacks;
                    break;
                case BuffEffectType.HeartGuard:
                    MaxHeartGuardCharges = stacks;
                    break;
                case BuffEffectType.DeployTeslaCoil:
                case BuffEffectType.DeployFrostTower:
                    break;
            }
        }

        foreach (var kv in _halfStackBonuses)
        {
            switch (kv.Key)
            {
                case BuffEffectType.BallDamageUp:
                    BallDamageBonus += Mathf.RoundToInt(kv.Value);
                    break;
                case BuffEffectType.MaxHPUp:
                    MaxHPBonus += Mathf.RoundToInt(kv.Value);
                    break;
                case BuffEffectType.ComboThresholdDown:
                    ComboThresholdReduction += Mathf.RoundToInt(kv.Value);
                    break;
                case BuffEffectType.ScoreOnKillUp:
                    ScoreOnKillBonus += kv.Value;
                    break;
            }
        }

        HeartGuardCharges = Mathf.Min(HeartGuardCharges, MaxHeartGuardCharges);

        // Phase 3 护栏：永久弹珠伤害加成上限 3；赏金击杀上限 +30%
        BallDamageBonus = Mathf.Min(3, BallDamageBonus);
        ScoreOnKillBonus = Mathf.Min(0.30f, ScoreOnKillBonus);

        ApplyMaxHPChange();
        EnsureTowerManagerExists();
    }

    private void ApplyMaxHPChange()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetMaxHPBonus(MaxHPBonus);
    }

    private void EnsureTowerManagerExists()
    {
        if (TowerManager.Instance != null) return;
        var go = new GameObject("TowerManager_Auto");
        go.AddComponent<TowerManager>();
    }

    /// <summary>
    /// 小兵触底、BlockShield 未吸收时尝试消耗护心。返回 true 表示本次伤害被抵消。
    /// 仅免伤，不回血（Balance 04）。
    /// </summary>
    public bool TryConsumeHeartGuard(out bool showShieldVfx)
    {
        showShieldVfx = false;
        if (HeartGuardCharges <= 0) return false;

        HeartGuardCharges--;
        showShieldVfx = true;
        return true;
    }

    /// <summary>新局：清空全部 Buff 层与半层垫刀（供 GameManager / 事件调用）。</summary>
    public void ResetForNewGame()
    {
        _stacks.Clear();
        _halfStackBonuses.Clear();
        _epicWeightPadding = 0f;
        _rareWeightPadding = 0f;
        HeartGuardCharges = 0;
        _healOnKillProgress = 0;
        RecalculateStats();
    }

    /// <summary>测试沙盒：清空全部 Buff 层与半层垫刀，并重置 BuildTag。</summary>
    public void ClearAllBuffsForTest()
    {
        _stacks.Clear();
        _halfStackBonuses.Clear();
        _epicWeightPadding = 0f;
        _rareWeightPadding = 0f;
        HeartGuardCharges = 0;
        _healOnKillProgress = 0;
        RunSession.ResetBuildTags();
        RecalculateStats();
    }

    /// <summary>测试沙盒：指定 effect 减 1 层；归零后重建 BuildTag。</summary>
    public void RemoveOneStackForTest(BuffEffectType type)
    {
        if (!_stacks.TryGetValue(type, out int v) || v <= 0) return;
        v--;
        if (v <= 0) _stacks.Remove(type);
        else _stacks[type] = v;
        RebuildBuildTagsFromStacks();
        RecalculateStats();
    }

    private void RebuildBuildTagsFromStacks()
    {
        RunSession.ResetBuildTags();
        if (buffPool == null) return;
        foreach (var def in buffPool)
        {
            if (def == null) continue;
            int stacks = GetStacks(def.effectType);
            for (int i = 0; i < stacks; i++)
                WriteBuildTags(def);
        }
    }
}

/// <summary>
/// 敌人电荷燃料。由 ElectricCombat 读写。
/// 视觉由 EnemyBuildStackVisual / Shader 负责（不再全身 tint）。
/// </summary>
public class EnemyElectricState : MonoBehaviour
{
    public const int ChargeCap = 3;

    public int ChargeStacks { get; private set; }

    public void AddCharge(int amount = 1, int cap = ChargeCap)
    {
        if (amount <= 0) return;
        ChargeStacks = Mathf.Min(cap, ChargeStacks + amount);
    }

    public bool TryConsumeCharge(int amount = 1)
    {
        if (ChargeStacks < amount) return false;
        ChargeStacks -= amount;
        return true;
    }

    public void ClearCharge() => ChargeStacks = 0;

    /// <summary>沙盒：直接灌满燃料。</summary>
    public void DevFillCharge(int amount = ChargeCap) =>
        ChargeStacks = Mathf.Clamp(amount, 0, ChargeCap);
}

/// <summary>
/// Electric v0.4.1：电荷=燃料；打火器/Tesla=点火；连锁耗燃料。
/// </summary>
public static class ElectricCombat
{
    public const int ZapDamageCap = 1;

    private static readonly Collider2D[] OverlapBuf = new Collider2D[64];
    private static float _nextBallSparkUnscaled;

    public static string LastZapReason { get; private set; } = "";
    public static float LastZapUnscaledTime { get; private set; }
    public static int LastChainHops { get; private set; }

    public static float BallSparkCdRemaining =>
        Mathf.Max(0f, _nextBallSparkUnscaled - Time.unscaledTime);

    public static void OnBallHitEnemy(EnemyBase enemy, Vector2? hitPos = null)
    {
        if (enemy == null || enemy.IsDead) return;
        var bm = BuffManager.Instance;
        if (bm == null) return;

        Vector2 pos = hitPos ?? (Vector2)enemy.transform.position;

        // 1) 积燃料（仅有电荷残留时）
        if (bm.ElectricChargeStacks > 0)
            GetOrAddState(enemy).AddCharge(bm.GetChargePerHit());

        // 2) 打火器：局部小连锁（Rare）
        if (bm.ElectricIgniterStacks > 0)
        {
            TrySparkChain(
                enemy, pos,
                useBallCooldown: true,
                reason: "Igniter",
                radius: bm.GetIgniterRadius(),
                maxHops: bm.GetIgniterMaxHops(),
                fuelNeed: bm.GetIgniterFuelThreshold());
        }
    }

    public static void OnTeslaHit(EnemyBase enemy, int towerLevel = 1)
    {
        if (enemy == null || enemy.IsDead) return;
        // 塔自带攻击间隔；连锁规格随塔等级，明显强于打火器
        int lv = Mathf.Max(1, towerLevel);
        TrySparkChain(
            enemy, enemy.transform.position,
            useBallCooldown: false,
            reason: $"TeslaL{lv}",
            radius: BuffManager.GetTeslaChainRadius(lv),
            maxHops: BuffManager.GetTeslaMaxHops(lv),
            fuelNeed: BuffManager.GetTeslaFuelThreshold(lv));
    }

    /// <summary>沙盒：给场上全部敌人灌燃料。</summary>
    public static void DevChargeAll(int stacks = EnemyElectricState.ChargeCap)
    {
        foreach (var enemy in Object.FindObjectsOfType<EnemyBase>())
        {
            if (enemy == null || enemy.IsDead) continue;
            GetOrAddState(enemy).DevFillCharge(stacks);
            if (enemy.TryGetComponent(out EnemyBuildStackVisual vis))
                vis.ForceRefresh();
        }
    }

    public static bool TrySparkChain(
        EnemyBase seed,
        Vector2 seedHitPos,
        bool useBallCooldown,
        string reason,
        float radius,
        int maxHops,
        int fuelNeed)
    {
        if (seed == null || seed.IsDead) return false;
        var bm = BuffManager.Instance;

        radius = Mathf.Max(0.5f, radius);
        maxHops = Mathf.Max(1, maxHops);
        fuelNeed = Mathf.Max(1, fuelNeed);

        if (useBallCooldown)
        {
            if (Time.unscaledTime < _nextBallSparkUnscaled) return false;
        }

        int neighborhoodFuel = SumFuelInRadius(seed.transform.position, radius);
        if (neighborhoodFuel < fuelNeed) return false;

        EnemyBase current = seed;
        var seedState = GetOrAddState(seed);
        if (seedState.ChargeStacks <= 0)
        {
            current = FindChargedNear(seed.transform.position, radius, null);
            if (current == null) return false;
        }

        if (useBallCooldown && bm != null)
            _nextBallSparkUnscaled = Time.unscaledTime + bm.GetBallSparkCooldown();

        var visited = new HashSet<EnemyBase>();
        int hops = 0;
        Vector2 prevPos = seedHitPos;

        while (current != null && hops < maxHops)
        {
            if (current.IsDead || visited.Contains(current)) break;
            var st = GetOrAddState(current);
            if (!st.TryConsumeCharge(1)) break;

            visited.Add(current);
            Vector2 pos = current.transform.position;
            if (hops > 0)
            {
                TeslaArcFX.EnsureInstance();
                TeslaArcFX.Instance?.SpawnArc(prevPos, pos, Random.Range(0, 9999));
            }

            Zap(current, ZapDamageCap, pos, reason);
            hops++;
            prevPos = pos;

            if (current.IsDead) break;
            current = FindChargedNear(pos, radius, visited);
        }

        if (hops <= 0) return false;

        LastChainHops = hops;
        LastZapReason = $"{reason} x{hops}";
        LastZapUnscaledTime = Time.unscaledTime;
        if (hops >= 2)
            CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        return true;
    }

    private static void Zap(EnemyBase enemy, int damage, Vector2 hitPos, string reason)
    {
        if (enemy == null || enemy.IsDead || damage <= 0) return;
        damage = Mathf.Min(ZapDamageCap, damage);
        enemy.TakeHit(damage, isFromBall: false, hitPos);
        ImpactFX.Instance?.SpawnHit(hitPos, new Color(0.2f, 0.95f, 1f), 0.7f);
    }

    private static int SumFuelInRadius(Vector2 center, float radius)
    {
        int n = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuf);
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;
            if (enemy.TryGetComponent(out EnemyElectricState st))
                sum += st.ChargeStacks;
        }
        return sum;
    }

    private static EnemyBase FindChargedNear(Vector2 center, float radius, HashSet<EnemyBase> exclude)
    {
        int n = Physics2D.OverlapCircleNonAlloc(center, radius, OverlapBuf);
        EnemyBase best = null;
        float bestDistSq = radius * radius;
        for (int i = 0; i < n; i++)
        {
            var col = OverlapBuf[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            var enemy = col.GetComponentInParent<EnemyBase>();
            if (enemy == null || enemy.IsDead) continue;
            if (exclude != null && exclude.Contains(enemy)) continue;
            if (!enemy.TryGetComponent(out EnemyElectricState st) || st.ChargeStacks <= 0)
                continue;

            float d = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (d <= bestDistSq)
            {
                bestDistSq = d;
                best = enemy;
            }
        }
        return best;
    }

    private static EnemyElectricState GetOrAddState(EnemyBase enemy)
    {
        if (!enemy.TryGetComponent(out EnemyElectricState state))
            state = enemy.gameObject.AddComponent<EnemyElectricState>();
        EnemyBuildStackVisual.EnsureOn(enemy);
        return state;
    }
}
