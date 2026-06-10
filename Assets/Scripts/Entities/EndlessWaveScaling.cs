using UnityEngine;

/// <summary>
/// 无尽模式波次缩放（方案 B 骨架）。W1 为教学波不参与缩放；W2+ 以 Boss_W2 为模板加压。
/// 兵种权重为方案 C 预埋；数值膨胀接口预留，默认 1。
/// </summary>
public static class EndlessWaveScaling
{
    public const int TutorialWaveIndex = 0;

    public static bool IsTutorialWave(int waveIndex) => waveIndex == TutorialWaveIndex;

    /// <summary>W2→1, W3→2 … W1 返回 0 且不参与缩放计算。</summary>
    public static int GetScaledWave(int waveIndex)
    {
        if (IsTutorialWave(waveIndex)) return 0;
        return waveIndex;
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

    /// <summary>Phase 2 预留：小兵 HP 倍率。</summary>
    public static float GetMinionHpMultiplier(int waveIndex) => 1f;

    /// <summary>Phase 2 预留：小兵移速倍率。</summary>
    public static float GetMinionSpeedMultiplier(int waveIndex) => 1f;

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
