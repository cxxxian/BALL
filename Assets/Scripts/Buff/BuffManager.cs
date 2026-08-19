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
    /// <summary>击杀得分额外倍率增量（0.15 = +15%）。最终分 = base × (1 + ScoreOnKillBonus)。</summary>
    public float ScoreOnKillBonus      { get; private set; } = 0f;

    public float EpicWeightPadding => _epicWeightPadding;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ResetForNewGame);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(ResetForNewGame);
    }

    public int GetStacks(BuffEffectType type) =>
        _stacks.TryGetValue(type, out int v) ? v : 0;

    public BuffDefinition DrawRandomFromPool(BuffRarity rarity, int waveIndex)
    {
        if (buffPool == null || buffPool.Count == 0) return null;

        var eligible = new List<BuffDefinition>();
        foreach (var b in buffPool)
        {
            if (b == null) continue;
            if (b.rarity != rarity) continue;
            if (GetStacks(b.effectType) >= b.maxStacks) continue;
            if (waveIndex < b.minWave) continue;
            if (!IsTowerModEligible(b)) continue;
            eligible.Add(b);
        }

        if (eligible.Count == 0) return null;
        return eligible[Random.Range(0, eligible.Count)];
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

        if (def.effectType == BuffEffectType.HeartGuard)
            HeartGuardCharges = Mathf.Min(MaxHeartGuardCharges, HeartGuardCharges + toAdd);

        Debug.Log($"[BuffManager] Applied: {def.buffName}  stacks={_stacks[def.effectType]}/{def.maxStacks}");
    }

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
                    ComboThresholdReduction += Mathf.RoundToInt(def.effectValue * stacks);
                    break;
                case BuffEffectType.ScoreOnKillUp:
                    ScoreOnKillBonus += def.effectValue * stacks;
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
    /// </summary>
    public bool TryConsumeHeartGuard(out bool showShieldVfx)
    {
        showShieldVfx = false;
        if (HeartGuardCharges <= 0) return false;

        HeartGuardCharges--;
        var gm = GameManager.Instance;
        if (gm == null) return true;

        if (gm.Lives < gm.MaxLives)
            gm.Heal(1);
        else
            showShieldVfx = true;

        return true;
    }

    private void ResetForNewGame()
    {
        _stacks.Clear();
        _halfStackBonuses.Clear();
        _epicWeightPadding = 0f;
        _rareWeightPadding = 0f;
        HeartGuardCharges = 0;
        RecalculateStats();
    }
}
