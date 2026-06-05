using UnityEngine;

public enum BuffRarity { Common, Rare, Epic }

public enum BuffEffectType
{
    BallDamageUp,       // 弹珠基础伤害 +N
    MaxHPUp,            // 最大 HP +N
    ComboThresholdDown, // Combo 触发阈值 -N
    HealOnKill,         // 每击杀 N 只怪物回复 1 HP
    DeployTeslaCoil,    // 建造：特斯拉电圈（周期性闪电）
    DeployFrostTower,   // 建造：冰霜塔（周期性全屏/范围减速冻结）
    ElectricShell,      // 元素：感电外壳（物理击退与感电）
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
