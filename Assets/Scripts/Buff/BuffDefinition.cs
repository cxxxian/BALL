using UnityEngine;

public enum BuffRarity { Common, Rare, Epic }

/// <summary>构筑倾向标签。开局为空；仅由带 buildTags 的 Buff Apply 写入。仅三构筑。</summary>
public enum BuildTag
{
    Electric = 0,
    Frost = 1,
    Combo = 2
}

/// <summary>Buff 构筑分类（v0.4：Universal + 三构筑）。</summary>
public enum BuffCategory
{
    Universal = 0,
    Electric = 1,
    Frost = 2,
    Combo = 3
}

/// <summary>重复获得同一 Buff 时的处理。</summary>
public enum DuplicateBehavior
{
    Stack = 0,
    Upgrade = 1,
    Transform = 2
}

public enum BuffEffectType
{
    BallDamageUp,       // 0 弹珠基础伤害 +N
    MaxHPUp,            // 1 最大 HP +N
    ComboThresholdDown, // 2 Combo 门槛 -N（4b-4 迁出 Pulse 绑定）
    HealOnKill,         // 3 每击杀 N 只怪物回复 1 HP
    DeployTeslaCoil,    // 4 Electric Rare：特斯拉
    DeployFrostTower,   // 5 Frost Rare：冰霜塔
    ElectricIgniter,    // 6 Electric Rare：打火器（球上点火）
    HeartGuard,         // 7 护心符
    ScoreOnKillUp,      // 8 击杀得分倍率
    ElectricCharge,     // 9 Electric Common：电荷残留（燃料）
    FrostMark,          // 10 Frost Common：霜痕
    FrostBurst,         // 11 Frost Rare：霜爆
    ComboMomentum,      // 12 Combo Common：连击动能
    ComboOverload,      // 13 Combo Rare：过载奖励
}

[CreateAssetMenu(fileName = "Buff_New", menuName = "Ball/BuffDefinition")]
public class BuffDefinition : ScriptableObject
{
    [Header("Display")]
    public string buffName;
    [TextArea(2, 4)]
    public string description;
    public BuffRarity rarity;

    [Header("Build")]
    public BuffCategory category = BuffCategory.Universal;
    public BuildTag[] buildTags;
    public DuplicateBehavior duplicateBehavior = DuplicateBehavior.Stack;
    [Tooltip("该 Buff 是否部署建筑（Tesla / FrostTower 等）")]
    public bool createsBuilding;

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
            BuffEffectType.MaxHPUp            => "最大生命 +1 / 层",
            BuffEffectType.ComboThresholdDown => "（Legacy）Pulse 门槛 -1 — 已下池",
            BuffEffectType.DeployTeslaCoil    => "部署特斯拉：命中带电群时点燃连锁",
            BuffEffectType.DeployFrostTower   => "部署冰霜塔：周期叠霜痕（非纯减速）",
            BuffEffectType.HeartGuard         => "护心层：触底免伤（不回血）",
            BuffEffectType.HealOnKill         => "击杀回复生命",
            BuffEffectType.ElectricIgniter    => "弹珠命中可点燃电荷连锁（有火花 CD）",
            BuffEffectType.ScoreOnKillUp      => "击杀得分 +10% / 层",
            BuffEffectType.ElectricCharge     => "命中给敌人叠电荷燃料",
            BuffEffectType.FrostMark          => "命中给敌人叠霜痕（不大幅减速）",
            BuffEffectType.FrostBurst         => "霜痕满层时触发冰爆（短冻/范围伤）",
            BuffEffectType.ComboMomentum      => "每 N 次命中额外 +1 Combo",
            BuffEffectType.ComboOverload      => "达 Combo 阶段获得分数/临伤（不触发 Pulse）",
            _ => string.Empty
        };
    }
}
