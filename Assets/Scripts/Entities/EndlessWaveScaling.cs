using UnityEngine;

/// <summary>
/// 无尽模式波次缩放。W1 为教学波不参与小兵倍率；W2+ 以 Boss_W2 为模板加压。
/// Boss HP/移速/派兵间隔：W1–W3 查表，W4+ 公式（方案 B，运行时覆盖资产基准）。
/// </summary>
public static class EndlessWaveScaling
{
    public const int TutorialWaveIndex = 0;

    public static bool IsTutorialWave(int waveIndex) => waveIndex == TutorialWaveIndex;

    /// <summary>派兵权重/数量/间隔缩放用。W1→0，W2→1，W3→2 …</summary>
    public static int GetScaledWave(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 0;
        return waveIndex;
    }

    /// <summary>HP/移速倍率与 W4+ Boss 公式用。W1→0，W2→2，W3→3 …</summary>
    public static int GetDifficultyWave(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 0;
        return waveIndex + 1;
    }

    public static float GetSpawnInterval(float baseInterval, int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return baseInterval;
        int sw = GetScaledWave(waveIndex);
        return baseInterval * Mathf.Max(0.55f, 1f - sw * 0.05f);
    }

    public static int GetSpawnCount(int baseCount, int waveIndex, bool phase2)
    {
        if (IsTutorialWave(waveIndex)) return baseCount;
        int sw = GetScaledWave(waveIndex);
        if (phase2)
            return baseCount + Mathf.Min(1, sw / 5);
        return baseCount + Mathf.Min(2, sw / 4);
    }

    public static float GetMinionHpMultiplier(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 1f;
        int dw = GetDifficultyWave(waveIndex);
        return Mathf.Min(1.6f, 1f + 0.08f * Mathf.Max(0, dw - 1));
    }

    public static float GetMinionSpeedMultiplier(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 1f;
        int dw = GetDifficultyWave(waveIndex);
        return Mathf.Min(1.35f, 1f + 0.04f * Mathf.Max(0, dw - 1));
    }

    /// <summary>W1–W3 查表；W4+ round(18 + 8×dw)。</summary>
    public static int GetBossMaxHP(int waveIndex)
    {
        return waveIndex switch
        {
            0 => 10,
            1 => 16,
            2 => 24,
            _ => Mathf.RoundToInt(18 + 8 * GetDifficultyWave(waveIndex))
        };
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
