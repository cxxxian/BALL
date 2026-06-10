using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("Buff Pool (assign all BuffDefinition assets here)")]
    public List<BuffDefinition> buffPool = new List<BuffDefinition>();

    private readonly Dictionary<BuffEffectType, int> _stacks = new Dictionary<BuffEffectType, int>();

    // ── 对外暴露的数值属性 ─────────────────────────────────────
    public int BallDamageBonus         { get; private set; } = 0;
    public int MaxHPBonus              { get; private set; } = 0;
    public int ComboThresholdReduction { get; private set; } = 0;
    public int HeartGuardCharges       { get; private set; } = 0;
    public int MaxHeartGuardCharges    { get; private set; } = 0;

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

    /// <summary>返回固定 3 槽：未满层 Buff 随机填入，不足部分为 null（空槽占位，后续可接新 Buff）。</summary>
    public BuffDefinition[] GetRandomSelection(int count = 3)
    {
        var result = new BuffDefinition[count];

        if (buffPool == null || buffPool.Count == 0)
            return result;

        var eligible = new List<BuffDefinition>();
        foreach (var b in buffPool)
        {
            if (b != null && GetStacks(b.effectType) < b.maxStacks)
                eligible.Add(b);
        }

        var bag = new List<BuffDefinition>(eligible);
        int pickCount = Mathf.Min(count, bag.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(0, bag.Count);
            result[i] = bag[idx];
            bag.RemoveAt(idx);
        }

        return result;
    }

    public void ApplyBuff(BuffDefinition def)
    {
        if (def == null) return;
        int current = GetStacks(def.effectType);
        if (current >= def.maxStacks) return;
        _stacks[def.effectType] = current + 1;
        RecalculateStats();

        if (def.effectType == BuffEffectType.HeartGuard)
            HeartGuardCharges = Mathf.Min(MaxHeartGuardCharges, HeartGuardCharges + 1);

        Debug.Log($"[BuffManager] Applied: {def.buffName}  stacks={_stacks[def.effectType]}/{def.maxStacks}");
    }

    private void RecalculateStats()
    {
        BallDamageBonus         = 0;
        MaxHPBonus              = 0;
        ComboThresholdReduction = 0;
        MaxHeartGuardCharges    = 0;

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
                case BuffEffectType.HeartGuard:
                    MaxHeartGuardCharges = stacks;
                    break;
                case BuffEffectType.DeployTeslaCoil:
                case BuffEffectType.DeployFrostTower:
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
        HeartGuardCharges = 0;
        RecalculateStats();
    }

    private int GetStacks(BuffEffectType type) =>
        _stacks.TryGetValue(type, out int v) ? v : 0;
}
