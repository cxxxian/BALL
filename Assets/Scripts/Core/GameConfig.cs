using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "PinballGame/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Ball Physics")]
    public float ballLaunchSpeed = 8f;
    public float ballMinSpeed = 5f;
    public float ballMaxSpeed = 20f;
    public float ballBounciness = 1f;
    public float ballFriction = 0f;

    [Header("Ball Settings")]
    public float ballRadius = 0.15f;
    public float respawnInvincibleDuration = 0.5f;
    public float respawnDelay = 0.3f;
    public Vector2 launchAngleRange = new Vector2(60f, 80f);

    [Header("Life System")]
    public int initialLives = 3;
    public int maxLives = 6;

    [Header("Flipper Settings")]
    public float flipperRestAngle = -30f;
    public float flipperActivatedAngle = 15f;
    public float flipperActivateDuration = 0.055f;   // 上弹快
    public float flipperReturnDuration = 0.18f;      // 落回慢
    public float flipperBodyLength = 1.9f;           // 用于计算尖端速度
    public float flipperBoostFactor = 1.15f;         // 挡板命中球时的速度加成系数

    [Header("Score")]
    public int scorePerGruntHit = 10;
    public int scorePerGruntKill = 50;

    [Header("Combo")]
    public float comboTimeout = 2f;                  // 超时重置连击
    public int comboDisplayThreshold = 3;            // 达到此数显示 Combo UI
    public float comboEnergyBase = 0.06f;            // 每次命中的基础蓝条增量

    [Header("Camera Shake")]
    public float shakeTraumaLight = 0.32f;           // Bumper
    public float shakeTraumaMedium = 0.52f;          // Slingshot
    public float shakeTraumaHeavy = 0.82f;           // 掉球
    public float shakeDecaySpeed = 7f;
    public float shakeMaxOffset = 0.22f;

    [Header("Launch Guide")]
    public float launchGuideLength = 6f;
    public int launchGuideDots = 20;
    public float guideSwingSpeed = 65f;              // 引导线来回摆动速度(度/秒)
    public float guideMinAngle = 45f;                // 最小发射角(从水平线起)
    public float guideMaxAngle = 135f;               // 最大发射角

    [Header("Skill - Bullet Time")]
    public float skillCooldown        = 12f;   // 基础冷却秒数
    public float skillComboCDReduce   = 0.4f;  // 每次 Combo 命中减少的 CD 秒数
    public float skillBottomZoneRatio = 0.22f; // 手机触控底部挡板区占屏高比例
    public float skillSlowMoScale     = 0.12f; // 时缓倍率（0.1 = 十分之一速度）

    [Header("Camera / World")]
    public float worldWidth = 9f;
    public float worldHeight = 16f;
}
