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
    public int maxLives = 5;

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
    // comboEnergyBase 已废弃：充能豆推迟至弹药型弹珠技能

    [Header("Combo Milestone — Bumper Pulse")]
    [Tooltip("首次触发脉冲的连击数（测试 5，正式 25）")]
    public int   comboRewardThreshold25 = 25;
    [Tooltip("之后每隔 N 连击再触发一次（5→10→15… 或 25→30→35…）")]
    public int   comboPulseInterval = 10;
    public int   bumperPulseMilestoneDamage = 1;
    public float bumperPulseRadius          = 3.2f;
    public float bumperPulseRingDuration    = 1.0f;

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

    [Header("Skill - Timestop Aura")]
    public float timestopDuration          = 4f;
    public float timestopMinionSpeedScale  = 0.35f;
    public float timestopBossSpeedScale    = 0.6f;

    [Header("Skill - Gravity Well")]
    public float gravityWellDuration         = 2.5f;
    public float gravityWellRadius           = 3.2f;
    public float gravityWellPullStrength     = 9f;
    [Tooltip("阱内向下速度保留比例（越低越易被横向吸过来）")]
    public float gravityWellDownSpeedScale   = 0.3f;
    [Tooltip("阱生成后吸力渐强时长（秒）")]
    public float gravityWellRampTime         = 0.9f;
    [Tooltip("敌人进入阱后吸力渐强时长（秒）")]
    public float gravityWellDwellRampTime    = 0.55f;
    [Tooltip("核心区半径占 R 的比例；入内后停吸并稳定，防来回弹")]
    public float gravityWellCoreRadiusRatio  = 0.22f;
    public float gravityWellRingRatio        = 0.6f;
    public float gravityWellSeparationMult   = 2.0f;
    public int   gravityWellMaxFullPull      = 8;
    public float gravityWellOverflowPullMult = 0.3f;
    public float gravityWellMinPlaceOffset   = 0.8f;
    public float gravityWellPlaceMarginX     = 0.5f;

    [Header("Skill - Bullet Time")]
    public float skillCooldown        = 12f;   // 基础冷却秒数
    public float skillComboCDReduce   = 0.4f;  // 每次 Combo 命中减少的 CD 秒数
    public float skillBottomZoneRatio = 0.22f; // 手机触控底部挡板区占屏高比例
    public float skillSlowMoScale     = 0.12f; // 时缓倍率（0.1 = 十分之一速度）

    [Header("Camera / World")]
    public float worldWidth = 9f;
    public float worldHeight = 16f;

    [Header("Danger Line")]
    [Tooltip("敌人脚底 Y ≤ 此线视为攻击成功并扣血（挡板上方，与护盾线对齐）")]
    public float minionAttackLineY = -4.0f;

    [Tooltip("弹珠 Y ≤ 此线视为掉出界外扣命（挡板下方）")]
    public float ballFallLineY = -8.85f;

    [Header("Slot Machine UI")]
    [Tooltip("为 true 时在拉霸界面显示「确认领取」按钮；默认仅拉杆领取")]
    public bool slotMachineShowConfirmButton = false;

    [Header("Neon Visuals")]
    [Tooltip("统一霓虹语法 Palette；改此资产即可全局换色")]
    public NeonPalette neonPalette;
}
