using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>当局模式：主菜单进关前设置，回主菜单时清空。</summary>
public enum RunMode
{
    None = 0,
    Endless = 1,
    Campaign = 2,
    Tutorial = 3
}

public static class RunSession
{
    /// <summary>Phase 0 占位：每波结束固定 +2（正式比例见 Phase 7 / D3）。</summary>
    public const int PlaceholderChipsPerWave = 2;

    /// <summary>单 BuildTag 累计上限（Electric / Frost / Combo 各封顶 3）。</summary>
    public const int MaxBuildTagCount = 3;

    /// <summary>匹配 Tag 的 Buff 相对抽卡权重倍率（倾向，非锁定）。</summary>
    public const float BuildTagWeightBonus = 1.25f;

    public static RunMode Mode { get; private set; } = RunMode.None;

    /// <summary>本 Run 内筹码（波次间保留；Run 结束清空）。</summary>
    public static int Chips { get; private set; }

    /// <summary>当前波次开始时 <see cref="GameManager.Score"/> 快照。</summary>
    public static int WaveScoreAtWaveStart { get; private set; }

    /// <summary>本 Run 构筑倾向；开局为空，仅由 Buff Apply 写入。</summary>
    private static readonly Dictionary<BuildTag, int> BuildTags = new Dictionary<BuildTag, int>();

    public static event Action<int> OnChipsChanged;

    public static bool IsTutorial => Mode == RunMode.Tutorial;
    public static bool IsEndless => Mode == RunMode.Endless;

    public static void BeginEndless()
    {
        Mode = RunMode.Endless;
    }

    public static void BeginCampaign()
    {
        Mode = RunMode.Campaign;
    }

    public static void BeginTutorial()
    {
        Mode = RunMode.Tutorial;

        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog == null) return;

        var ball = catalog.GetDefaultBall();
        if (ball != null)
            RunLoadout.TrySelectBall(ball.ballId, catalog);
        else
            RunLoadout.EnsureDefaults(catalog);
    }

    public static void Clear()
    {
        Mode = RunMode.None;
        ResetChipState();
        ClearBuildTags();
    }

    /// <summary>新局：筹码清零、BuildTag 清空、重置波次 Score 快照。</summary>
    public static void ResetForNewRun()
    {
        ResetChipState();
        ClearBuildTags();
    }

    public static int GetBuildTagCount(BuildTag tag) =>
        BuildTags.TryGetValue(tag, out int v) ? v : 0;

    /// <summary>Buff Apply 唯一写入入口。选球 / RunLoadout 禁止调用。</summary>
    public static void AddBuildTag(BuildTag tag, int amount = 1)
    {
        if (amount <= 0) return;
        int current = GetBuildTagCount(tag);
        int next = Mathf.Min(MaxBuildTagCount, current + amount);
        if (next == current) return;
        BuildTags[tag] = next;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[RunSession] BuildTag {tag} → {next}/{MaxBuildTagCount}");
#endif
    }

    /// <summary>若任一 buildTag 在本 Run 已有 count≥1，该 Buff 抽卡权重 ×1.25。</summary>
    public static float GetBuffDrawWeight(BuffDefinition def)
    {
        if (def == null || def.buildTags == null || def.buildTags.Length == 0)
            return 1f;

        for (int i = 0; i < def.buildTags.Length; i++)
        {
            if (GetBuildTagCount(def.buildTags[i]) >= 1)
                return BuildTagWeightBonus;
        }

        return 1f;
    }

    private static void ClearBuildTags()
    {
        BuildTags.Clear();
    }

    /// <summary>测试沙盒：清空构筑 Tag（不碰 Chips）。</summary>
    public static void ResetBuildTags() => ClearBuildTags();

    public static void SnapshotWaveScore(int scoreAtWaveStart)
    {
        WaveScoreAtWaveStart = Mathf.Max(0, scoreAtWaveStart);
    }

    /// <summary>波次结束、进入 Buff 选择前调用。</summary>
    public static int AwardChipsForCompletedWave(int scoreNow)
    {
        int delta = Mathf.Max(0, scoreNow - WaveScoreAtWaveStart);
        int earned = ChipConversion(delta);
        if (earned <= 0) return 0;

        Chips += earned;
        OnChipsChanged?.Invoke(Chips);

#if DEVELOPMENT_BUILD
        Debug.Log($"[RunSession] Wave chips +{earned} (deltaScore={delta}, total={Chips})");
#endif
        return earned;
    }

    public static bool TrySpendChips(int amount)
    {
        if (amount <= 0) return true;
        if (Chips < amount) return false;
        Chips -= amount;
        OnChipsChanged?.Invoke(Chips);
        return true;
    }

    /// <summary>占位换算；Phase 7 可改为 Score 比例 / 上限等。</summary>
    public static int ChipConversion(int deltaScore)
    {
        _ = deltaScore;
        return PlaceholderChipsPerWave;
    }

    private static void ResetChipState()
    {
        Chips = 0;
        WaveScoreAtWaveStart = 0;
        OnChipsChanged?.Invoke(Chips);
    }
}
