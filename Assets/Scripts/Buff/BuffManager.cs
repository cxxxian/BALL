using System.Collections.Generic;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance { get; private set; }

    [Header("Buff Pool (assign all BuffDefinition assets here)")]
    public List<BuffDefinition> buffPool = new List<BuffDefinition>();

    // ── 当前叠加层数追踪 ──────────────────────────────────────────────────
    private readonly Dictionary<BuffEffectType, int> _stacks = new Dictionary<BuffEffectType, int>();

    // ── 对外暴露的数值属性 ────────────────────────────────────────────────
    public int   BallDamageBonus         { get; private set; } = 0;
    public float BallSizeMultiplier      { get; private set; } = 1f;
    public int   MaxHPBonus              { get; private set; } = 0;
    public int   ComboThresholdReduction { get; private set; } = 0;
    public float BumperForceMultiplier   { get; private set; } = 1f;
    public int   KillsPerHeal            { get; private set; } = 0;   // 0 = 未激活

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

    // ── 获取随机 Buff 卡（供 UI 面板调用）────────────────────────────────
    public BuffDefinition[] GetRandomSelection(int count = 3)
    {
        if (buffPool == null || buffPool.Count == 0) return new BuffDefinition[0];

        var pool = new List<BuffDefinition>(buffPool);
        // 移除已满层数的 Buff（已满则不展示，避免浪费选择）
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

    // ── 应用一个 Buff ─────────────────────────────────────────────────────
    public void ApplyBuff(BuffDefinition def)
    {
        if (def == null) return;
        int current = GetStacks(def.effectType);
        if (current >= def.maxStacks) return;
        _stacks[def.effectType] = current + 1;
        RecalculateStats();
        Debug.Log($"[BuffManager] Applied: {def.buffName}  stacks={_stacks[def.effectType]}/{def.maxStacks}");
    }

    // ── 重新计算所有数值 ──────────────────────────────────────────────────
    private void RecalculateStats()
    {
        BallDamageBonus         = 0;
        BallSizeMultiplier      = 1f;
        MaxHPBonus              = 0;
        ComboThresholdReduction = 0;
        BumperForceMultiplier   = 1f;
        KillsPerHeal            = 0;

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
                case BuffEffectType.BallSizeUp:
                    BallSizeMultiplier += (def.effectValue - 1f) * stacks;
                    break;
                case BuffEffectType.MaxHPUp:
                    MaxHPBonus += Mathf.RoundToInt(def.effectValue * stacks);
                    break;
                case BuffEffectType.ComboThresholdDown:
                    ComboThresholdReduction += Mathf.RoundToInt(def.effectValue * stacks);
                    break;
                case BuffEffectType.BumperForceUp:
                    BumperForceMultiplier += (def.effectValue - 1f) * stacks;
                    break;
                case BuffEffectType.HealOnKill:
                    KillsPerHeal = Mathf.RoundToInt(def.effectValue);
                    break;
            }
        }

        // 通知需要感知 Buff 变化的系统
        ApplyMaxHPChange();
        ApplyBallSize();
    }

    private void ApplyMaxHPChange()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetMaxHPBonus(MaxHPBonus);
    }

    private void ApplyBallSize()
    {
        if (BallController.Instance == null) return;
        BallController.Instance.SetSizeMultiplier(BallSizeMultiplier);
    }

    // ── 击杀回调（由 EnemyBase 调用）─────────────────────────────────────
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

    // ── 重置（每局开始）──────────────────────────────────────────────────
    private void ResetForNewGame()
    {
        _stacks.Clear();
        _killCounter = 0;
        RecalculateStats();
    }

    private int GetStacks(BuffEffectType type) =>
        _stacks.TryGetValue(type, out int v) ? v : 0;
}
