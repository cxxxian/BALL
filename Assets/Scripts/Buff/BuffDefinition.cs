using UnityEngine;

public enum BuffRarity { Common, Rare, Epic }

public enum BuffEffectType
{
    BallDamageUp,       // 弹珠基础伤害 +N
    MaxHPUp,            // 最大 HP +N
    ComboThresholdDown, // Combo 奖励阈值 -N（下限 3）
    HealOnKill,         // [已移除出池] 每击杀 N 只怪物回复 1 HP
    DeployTeslaCoil,    // 建造：特斯拉电圈（周期性闪电）
    DeployFrostTower,   // 建造：冰霜塔（周期性全屏/范围减速冻结）
    ElectricShell,      // [已移除出池] 元素：感电外壳
    HeartGuard,         // 护心符：触底免伤，最多储存 2 层
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
}
