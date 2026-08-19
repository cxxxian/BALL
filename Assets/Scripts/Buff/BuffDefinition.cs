using UnityEngine;

public enum BuffRarity { Common, Rare, Epic }

public enum BuffEffectType
{
    BallDamageUp,       // 弹珠基础伤害 +N
    MaxHPUp,            // 最大 HP +N
    ComboThresholdDown, // Combo 奖励阈值 -N（下限 3）
    HealOnKill,         // [已移除出池] 每击杀 N 只怪物回复 1 HP
    DeployTeslaCoil,    // 建造：特斯拉电圈（范围内单体，触底优先）
    DeployFrostTower,   // 建造：冰霜塔（周期性全屏/范围减速冻结）
    ElectricShell,      // [已移除出池] 元素：感电外壳
    HeartGuard,         // 护心符：触底免伤，最多储存 2 层
    ScoreOnKillUp,      // 击杀得分 +effectValue / 层（如 0.15 = +15%）
}

[CreateAssetMenu(fileName = "Buff_New", menuName = "Ball/BuffDefinition")]
public class BuffDefinition : ScriptableObject
{
    [Header("Display")]
    public string buffName;
    [TextArea(2, 4)]
    public string description;
    public BuffRarity rarity;

    [Header("Effect")]
    public BuffEffectType effectType;
    public float effectValue = 1f;
    public int maxStacks = 3;

    [Header("Pool")]
    [Tooltip("满足该 Wave 后才进入拉霸池")]
    public int minWave = 1;

    public string GetBriefDescription()
    {
        if (!string.IsNullOrWhiteSpace(description))
            return description.Trim();

        return effectType switch
        {
            BuffEffectType.BallDamageUp       => "弹珠碰撞伤害 +1 / 层",
            BuffEffectType.MaxHPUp              => "最大生命 +1 / 层",
            BuffEffectType.ComboThresholdDown   => "Combo 震屏阈值 -2 / 层",
            BuffEffectType.DeployTeslaCoil      => "部署或升级特斯拉塔（单体电击）",
            BuffEffectType.DeployFrostTower     => "部署或升级冰霜塔",
            BuffEffectType.HeartGuard           => "获得护心层，触底免伤",
            BuffEffectType.HealOnKill           => "击杀回复生命",
            BuffEffectType.ElectricShell        => "弹珠附带感电",
            BuffEffectType.ScoreOnKillUp        => "击杀得分 +15% / 层",
            _ => string.Empty
        };
    }
}
