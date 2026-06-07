using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("Buff Pool (assign all BuffDefinition assets here)")]
    public List<BuffDefinition> buffPool = new List<BuffDefinition>();

    private readonly Dictionary<BuffEffectType, int> _stacks = new Dictionary<BuffEffectType, int>();

    // ── 对外暴露的数值属性 ─────────────────────────────────────
    public int   BallDamageBonus         { get; private set; } = 0;
    public int   MaxHPBonus              { get; private set; } = 0;
    public int   ComboThresholdReduction { get; private set; } = 0;
    public int   KillsPerHeal            { get; private set; } = 0;
    public int   ElectricShellLevel      { get; private set; } = 0;

    private int _killCounter = 0;

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

    public BuffDefinition[] GetRandomSelection(int count = 3)
    {
        if (buffPool == null || buffPool.Count == 0) return new BuffDefinition[0];

        var pool = new List<BuffDefinition>(buffPool);
        pool.RemoveAll(b => b != null && GetStacks(b.effectType) >= b.maxStacks);

        var result = new List<BuffDefinition>();
        int safeCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < safeCount; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result.ToArray();
    }

    public void ApplyBuff(BuffDefinition def)
    {
        if (def == null) return;
        int current = GetStacks(def.effectType);
        if (current >= def.maxStacks) return;
        _stacks[def.effectType] = current + 1;
        RecalculateStats();
        Debug.Log($"[BuffManager] Applied: {def.buffName}  stacks={_stacks[def.effectType]}/{def.maxStacks}");
    }

    private void RecalculateStats()
    {
        BallDamageBonus         = 0;
        MaxHPBonus              = 0;
        ComboThresholdReduction = 0;
        KillsPerHeal            = 0;
        ElectricShellLevel      = 0;

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
                case BuffEffectType.HealOnKill:
                    KillsPerHeal = Mathf.RoundToInt(def.effectValue);
                    break;
                case BuffEffectType.DeployTeslaCoil:
                case BuffEffectType.DeployFrostTower:
                    break;
                case BuffEffectType.ElectricShell:
                    ElectricShellLevel += stacks;
                    break;
            }
        }

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

    public void OnEnemyKilled()
    {
        if (KillsPerHeal <= 0) return;
        _killCounter++;
        if (_killCounter >= KillsPerHeal)
        {
            _killCounter = 0;
            GameManager.Instance?.Heal(1);
        }
    }

    private void ResetForNewGame()
    {
        _stacks.Clear();
        _killCounter = 0;
        RecalculateStats();
    }

    private int GetStacks(BuffEffectType type) =>
        _stacks.TryGetValue(type, out int v) ? v : 0;
}
