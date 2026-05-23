using UnityEngine;

[CreateAssetMenu(fileName = "BossDef", menuName = "PinballGame/BossDefinition")]
public class BossDefinition : ScriptableObject
{
    [Header("Identity")]
    public string bossName = "Boss";
    public Color  baseColor = new Color(0.6f, 0.1f, 0.9f);
    public Sprite sprite;

    [Header("Stats")]
    public int   maxHP       = 20;
    public int   scoreOnHit  = 30;
    public int   scoreOnKill = 500;

    [Header("Movement")]
    public float moveSpeed   = 1.5f;
    public float moveRangeX  = 3.0f;   // 中心左右各延伸的距离

    [Header("Phase 2 (HP <= 50%)")]
    [Tooltip("阶段二移速倍率")]
    public float phase2SpeedMult = 1.3f;

    [Header("Minion Spawning - Phase 1")]
    public float spawnInterval  = 4f;
    public int   spawnCount     = 1;

    [Header("Minion Spawning - Phase 2")]
    public float spawnIntervalP2 = 2.5f;
    public int   spawnCountP2    = 2;

    [Header("Minion Pool")]
    [Tooltip("Boss 可以派遣的小兵类型，随机选取")]
    public MinionDefinition[] spawnTypes;
}
