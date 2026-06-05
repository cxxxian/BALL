using UnityEngine;

[CreateAssetMenu(fileName = "MinionDef", menuName = "PinballGame/MinionDefinition")]
public class MinionDefinition : ScriptableObject
{
    [Header("Identity")]
    public string minionName = "Grunt";
    public Color  baseColor  = new Color(0.9f, 0.3f, 0.3f);
    public Sprite sprite;

    [Header("Stats")]
    public int   maxHP         = 1;
    public float moveSpeed     = 0.8f;
    public int   scoreOnHit    = 10;
    public int   scoreOnKill   = 50;

    [Header("Damage")]
    [Tooltip("触底时对玩家造成的伤害")]
    public int   damageToPlayer = 1;

    [Header("Health Bar")]
    [Tooltip("出生时是否默认显示血条。适合装甲兵/精英兵")]
    public bool  showHealthBarOnSpawn = false;
    [Tooltip("受击后血条至少保持可见的时间")]
    public float healthBarVisibleDuration = 1.2f;
    [Tooltip("血条相对小兵头顶的偏移")]
    public float healthBarYOffset = 0.85f;
    [Tooltip("血条宽度倍率")]
    public float healthBarWidthScale = 1f;

    [Header("Bomber")]
    [Tooltip("触底时禁用场上所有 Bumper")]
    public bool  isBomber               = false;
    public float bomberDisableDuration  = 5f;
}
