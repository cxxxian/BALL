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

    [Header("Bomber")]
    [Tooltip("触底时禁用场上所有 Bumper")]
    public bool  isBomber               = false;
    public float bomberDisableDuration  = 5f;
}
