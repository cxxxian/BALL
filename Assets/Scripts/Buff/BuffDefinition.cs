using UnityEngine;

public enum BuffRarity { Common, Rare, Epic }

public enum BuffEffectType
{
    BallDamageUp,       // 弹珠碰撞伤害 +N
    BallSizeUp,         // 弹珠半径 ×倍率
    MaxHPUp,            // 最大 HP +1
    ComboThresholdDown, // Combo 触发阈值 -N
    BumperForceUp,      // Bumper 弹力 ×倍率
    HealOnKill,         // 每击杀 N 只小兵回 1 HP
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
