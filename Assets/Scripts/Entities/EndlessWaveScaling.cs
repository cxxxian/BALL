using UnityEngine;

/// <summary>
/// 无尽模式波次缩放（Phase 4）。
/// 小兵 HP +6%/难度波 SoftCap 1.6（Armored 可达 1.8）；速度 +3% Cap 1.3。
/// Boss HP：W0～7 查表，W8+ 每波 +4，HardCap 60。验收看战时长。
/// Threat Budget：Phase 4 仅注释概念；完整编队驱动 spawnCount 见 Phase 6（D2）。
/// </summary>
public static class EndlessWaveScaling
{
    public const int TutorialWaveIndex = 0;

    // Threat Budget（概念注释；Phase 6 再驱动 spawn）
    // 建议单波 Threat 点数 ≈ 8 + 2×scaledWave；Splitter/Charger 等按 Threat 成本入编。
    public const float ThreatBase = 8f;
    public const float ThreatPerScaledWave = 2f;

    public static bool IsTutorialWave(int waveIndex) => waveIndex == TutorialWaveIndex;

    /// <summary>派兵权重/数量/间隔缩放用。W1→0，W2→1，W3→2 …</summary>
    public static int GetScaledWave(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 0;
        return waveIndex;
    }

    /// <summary>HP/移速倍率与难度曲线用。W1→0，W2→2，W3→3 …</summary>
    public static int GetDifficultyWave(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 0;
        return waveIndex + 1;
    }

    /// <summary>概念 Threat 预算（Phase 4 不驱动 spawn；供 Telemetry / Phase 6）。</summary>
    public static float GetThreatBudget(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return ThreatBase;
        return ThreatBase + ThreatPerScaledWave * GetScaledWave(waveIndex);
    }

    public static float GetSpawnInterval(float baseInterval, int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return baseInterval;
        int sw = GetScaledWave(waveIndex);
        return baseInterval * Mathf.Max(0.55f, 1f - sw * 0.05f);
    }

    public static int GetSpawnCount(int baseCount, int waveIndex, bool phase2)
    {
        // Phase 4：仍按旧曲线；Phase 6 改为 Threat Budget 驱动（D2）。
        if (IsTutorialWave(waveIndex)) return baseCount;
        int sw = GetScaledWave(waveIndex);
        if (phase2)
            return baseCount + Mathf.Min(1, sw / 5);
        return baseCount + Mathf.Min(2, sw / 4);
    }

    /// <summary>小兵 HP：每难度波 +6%，Soft Cap 1.6；Armored Soft Cap 1.8。</summary>
    public static float GetMinionHpMultiplier(int waveIndex, bool armored = false)
    {
        if (IsTutorialWave(waveIndex)) return 1f;
        int dw = GetDifficultyWave(waveIndex);
        float raw = 1f + 0.06f * Mathf.Max(0, dw - 1);
        float cap = armored ? 1.8f : 1.6f;
        return Mathf.Min(cap, raw);
    }

    /// <summary>小兵速度：每难度波 +3%，Cap 1.3×。</summary>
    public static float GetMinionSpeedMultiplier(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 1f;
        int dw = GetDifficultyWave(waveIndex);
        return Mathf.Min(1.3f, 1f + 0.03f * Mathf.Max(0, dw - 1));
    }

    /// <summary>Boss HP：W0～7 → 10,16,24,28,32,36,40,44；W8+ 每波 +4，HardCap 60。</summary>
    public static int GetBossMaxHP(int waveIndex)
    {
        int[] table = { 10, 16, 24, 28, 32, 36, 40, 44 };
        if (waveIndex < table.Length)
            return table[waveIndex];

        int hp = 44 + 4 * (waveIndex - 7);
        return Mathf.Min(60, hp);
    }

    /// <summary>W1–W3 查表；W4+ min(1.8, 1 + 0.08×dw)。</summary>
    public static float GetBossMoveSpeed(int waveIndex)
    {
        if (waveIndex <= 2)
        {
            return waveIndex switch
            {
                0 => 0.85f,
                1 => 1.05f,
                2 => 1.15f,
                _ => 1.05f
            };
        }
        int dw = GetDifficultyWave(waveIndex);
        return Mathf.Min(1.8f, 1.0f + 0.08f * dw);
    }

    /// <summary>W1–W3 固定间隔；W4+ 沿用资产基准再经 GetSpawnInterval 缩放。</summary>
    public static float GetBossSpawnInterval(int waveIndex, float assetInterval, bool phase2)
    {
        return waveIndex switch
        {
            0 => phase2 ? 5.0f : 5.5f,
            1 => phase2 ? 3.0f : 4.5f,
            2 => phase2 ? 2.4f : 3.8f,
            _ => assetInterval
        };
    }

    /// <summary>W1 固定 1/1；W2+ 沿用资产再经 GetSpawnCount 缩放。</summary>
    public static int GetBossSpawnCount(int waveIndex, int assetCount, bool phase2)
    {
        if (waveIndex == 0) return 1;
        return assetCount;
    }

    public static MinionDefinition PickMinion(MinionDefinition[] spawnTypes, int waveIndex)
    {
        if (spawnTypes == null || spawnTypes.Length == 0) return null;

        if (IsTutorialWave(waveIndex))
            return spawnTypes[Random.Range(0, spawnTypes.Length)];

        ClassifySpawnTypes(spawnTypes, out var grunt, out var armored, out var bomber);
        GetWeights(GetScaledWave(waveIndex), out float wGrunt, out float wArmored, out float wBomber);

        if (bomber == null) wBomber = 0f;
        if (armored == null) { wGrunt += wArmored; wArmored = 0f; }
        if (grunt == null && armored != null) { wGrunt = wArmored; wArmored = 0f; }

        float total = wGrunt + wArmored + wBomber;
        if (total <= 0f)
            return spawnTypes[Random.Range(0, spawnTypes.Length)];

        float roll = Random.Range(0f, total);
        if (roll < wGrunt && grunt != null) return grunt;
        roll -= wGrunt;
        if (roll < wArmored && armored != null) return armored;
        return bomber != null ? bomber : spawnTypes[Random.Range(0, spawnTypes.Length)];
    }

    private static void GetWeights(int scaledWave, out float grunt, out float armored, out float bomber)
    {
        if (scaledWave <= 1)
        {
            grunt = 70f; armored = 30f; bomber = 0f;
        }
        else if (scaledWave <= 3)
        {
            grunt = 55f; armored = 35f; bomber = 10f;
        }
        else if (scaledWave <= 6)
        {
            grunt = 45f; armored = 40f; bomber = 15f;
        }
        else
        {
            grunt = 35f; armored = 45f; bomber = 20f;
        }
    }

    private static void ClassifySpawnTypes(
        MinionDefinition[] types,
        out MinionDefinition grunt,
        out MinionDefinition armored,
        out MinionDefinition bomber)
    {
        grunt = armored = bomber = null;
        foreach (var t in types)
        {
            if (t == null) continue;
            if (t.isBomber) bomber = t;
            else if (t.maxHP >= 3) armored = t;
            else grunt = t;
        }
    }
}
