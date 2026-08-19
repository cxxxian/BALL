using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class BallController : MonoBehaviour
{
    public static BallController Instance { get; private set; }

    [Header("References")]
    public GameConfig config;
    public BallDefinition ballDefinition;

    public bool IsInvincible       { get; private set; } = false;
    public bool IsExecuteChainActive => _executeChainActive;
    public bool IsWaitingForLaunch { get; private set; } = false;

    /// <summary>
    /// 底线掉球判定：发球等待 / 重生无敌 忽略；Slash 斩杀链期间仍算掉球。
    /// </summary>
    public bool CanLoseLifeFromBottom()
    {
        if (IsWaitingForLaunch) return false;
        if (IsInvincible && !_executeChainActive) return false;
        return _launched;
    }
    public float SpeedMultiplier   { get; set; } = 1f;  // 动态速度倍率限制（加速齿轮等机制使用）
    public Rigidbody2D Rb => _rb;

    private float EffectiveMaxSpeed
    {
        get
        {
            float mult = SpeedMultiplier;
            if (DebuffManager.Instance != null)
                mult *= DebuffManager.Instance.BallMaxSpeedMultiplier;
            return config.ballMaxSpeed * mult;
        }
    }

    // ── 斩杀连锁技能状态 ──────────────────────────────────────────────
    private static readonly Color DefaultTrailStart = Color.white;
    private static readonly Color DefaultTrailEnd   = new Color(1f, 1f, 1f, 0f);

    // ── 默认拖尾动态（M2 过载轨视觉预埋）──────────────────────────────
    private const int   ComboTrailTintStart = 10;
    private const int   ComboTrailTintFull  = 15;
    private const float ComboTrailTintMax   = 0.38f;
    private static readonly Color ComboTrailMagentaHdr = new Color(1.75f, 0.3f, 1.65f, 1f);
    private const float TrailWidthSpeedMin = 0.55f;
    private const float TrailWidthSpeedMax = 1.45f;
    private const float DefaultTrailEndRatio = 0.1f;

    private bool  _executeChainActive = false;
    private int   _chainsRemaining    = 0;
    private float _originalTrailWidth;
    private bool  _trailColorOverridden;

    // ── 运动死区检测（引力过载） ─────────────────────────────────────────
    private float _horizontalDeadZoneTimer = 0f;
    private float _verticalDeadZoneTimer   = 0f;
    private bool  _gravityOverloadActive   = false;
    private const float AXIS_DEAD_THRESHOLD = 0.15f; // 副轴速度低于此视为死区
    private const float DEADZONE_DURATION    = 1.5f;   // 触发引力过载的时长

    private enum MotionDeadZone { None, Horizontal, Vertical }

    private Rigidbody2D      _rb;
    private CircleCollider2D _col;
    private SpriteRenderer   _sr;
    private TrailRenderer    _trail;
    private Vector2          _spawnPosition;
    private bool             _launched = false;

    // 引导线摆动状态
    private float _guideAngle    = 90f;
    private float _guideSwingDir = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _rb    = GetComponent<Rigidbody2D>();
        _col   = GetComponent<CircleCollider2D>();
        _sr    = GetComponent<SpriteRenderer>();
        _trail = GetComponent<TrailRenderer>();
        _spawnPosition = transform.position;

        if (_trail != null)
        {
            _originalTrailWidth = _trail.startWidth;
            ApplyDefaultTrail();
        }
    }

    /// <summary>战前配置注入弹珠类型（RunBootstrap 调用）。</summary>
    public void ApplyBallDefinition(BallDefinition def)
    {
        if (def == null) return;
        ballDefinition = def;
        if (_sr != null)
            _sr.color = def.glowColor;
        _trailColorOverridden = false;
        ApplyDefaultTrail();
    }

    /// <summary>统一默认拖尾：纯白 → 透明；高 Combo 略带品红 HDR。避免场景 colorGradient 与代码 startColor 不同步。</summary>
    private void ApplyDefaultTrail()
    {
        if (_trail == null) return;
        ApplyComboTrailTint();
    }

    private void ApplyComboTrailTint()
    {
        if (_trail == null) return;

        int combo = ComboSystem.Instance != null ? ComboSystem.Instance.CurrentCombo : 0;
        float tint = combo <= ComboTrailTintStart
            ? 0f
            : Mathf.SmoothStep(0f, 1f, (combo - ComboTrailTintStart) / (float)(ComboTrailTintFull - ComboTrailTintStart));
        float blend = tint * ComboTrailTintMax;

        _trail.startColor = Color.Lerp(DefaultTrailStart, ComboTrailMagentaHdr, blend);
        _trail.endColor   = Color.Lerp(DefaultTrailEnd,
            new Color(ComboTrailMagentaHdr.r, ComboTrailMagentaHdr.g, ComboTrailMagentaHdr.b, 0f), blend);
    }

    private void UpdateTrailFromSpeed(float speed)
    {
        if (_trail == null || !_trail.enabled) return;

        float minS = config.ballMinSpeed * SpeedMultiplier;
        float maxS = EffectiveMaxSpeed;
        float t    = Mathf.InverseLerp(minS, maxS, speed);
        float widthMul = Mathf.Lerp(TrailWidthSpeedMin, TrailWidthSpeedMax, t);

        float baseStart = _originalTrailWidth;
        float baseEnd   = _originalTrailWidth * DefaultTrailEndRatio;

        _trail.startWidth = baseStart * widthMul;
        _trail.endWidth   = baseEnd   * widthMul;

        if (!_trailColorOverridden && !_executeChainActive && !_gravityOverloadActive)
            ApplyComboTrailTint();
    }

    private void ApplyExecuteTrailColors()
    {
        if (_trail == null) return;
        var executeColor = NeonColors.Active.GetBase(NeonRole.SkillExecute);
        _trail.startColor = executeColor;
        _trail.endColor   = new Color(executeColor.r, executeColor.g, executeColor.b, 0.05f);
    }

    private void TruncateTrailOnImpact()
    {
        if (_trail != null && _trail.enabled)
            _trail.Clear();
    }

    // ── 激活斩杀连锁状态 ──────────────────────────────────────────────────
    public void StartExecuteChain(int maxChains)
    {
        _executeChainActive = true;
        _chainsRemaining    = maxChains;
        IsInvincible        = true;
        _launched           = true;

        ResetBallSize();
        ApplyExecuteTrailColors();
        if (_trail != null) _trail.Clear();

        // 所有 Bumper 进入穿透模式（碰撞体关闭 + 视觉暗化），弹珠自由穿场锁敌
        foreach (var b in FindObjectsOfType<Bumper>())
            b.SetPassthrough(true);

        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
    }

    public void StopExecuteChain()
    {
        _executeChainActive = false;
        _chainsRemaining    = 0;
        IsInvincible        = false;

        ResetBallSize();
        if (_trail != null)
            ApplyDefaultTrail();

        // 恢复全体 Bumper
        foreach (var b in FindObjectsOfType<Bumper>())
            b.SetPassthrough(false);
    }

    private void Start()
    {
        SetupPhysics();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
            GameManager.Instance.onGameOver.AddListener(OnGameOver);
        }
    }

    private void SetupPhysics()
    {
        _rb.gravityScale = 0f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        PhysicsMaterial2D mat = new PhysicsMaterial2D("BallMat");
        mat.bounciness = config.ballBounciness;
        mat.friction   = config.ballFriction;
        _col.sharedMaterial = mat;
        _col.radius = config.ballRadius;

        RestoreBallVisual();
    }

    private static Material _defaultBallMaterial;

    /// <summary>恢复 TronUnlit + HDR 球色（场景默认外观）。</summary>
    private void RestoreBallVisual()
    {
        if (_sr == null) return;

        if (_sr.sprite == null)
            _sr.sprite = GruntEnemy.CreateCircleSprite(64, Color.white);

        ResetBallSize();
        _sr.color = NeonColors.Active.GetBase(NeonRole.Ball);

        if (_defaultBallMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
                _defaultBallMaterial = new Material(shader);
        }
        if (_defaultBallMaterial != null)
            _sr.sharedMaterial = _defaultBallMaterial;
    }

    private void ResetBallSize()
    {
        transform.localScale = Vector3.one;
        if (_col != null && config != null)
            _col.radius = config.ballRadius;
    }

    private void Update()
    {
        if (!IsWaitingForLaunch) return;

        // 引导线摆动
        float guideDt = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
        _guideAngle += _guideSwingDir * config.guideSwingSpeed * guideDt;
        if (_guideAngle >= config.guideMaxAngle) { _guideAngle = config.guideMaxAngle; _guideSwingDir = -1f; }
        if (_guideAngle <= config.guideMinAngle) { _guideAngle = config.guideMinAngle; _guideSwingDir =  1f; }

        Vector2 guideDir = new Vector2(
            Mathf.Cos(_guideAngle * Mathf.Deg2Rad),
            Mathf.Sin(_guideAngle * Mathf.Deg2Rad));
        LaunchGuide.Instance?.UpdateDirection(transform.position, guideDir);

        // 检测确认发射输入
        bool fire = (InputManager.Instance != null && InputManager.Instance.LaunchPressed);
        if (fire) ExecuteLaunch(guideDir);
    }

    private void OnGameStart()
    {
        StopAllCoroutines();
        _respawnCoroutine = null;
        StopSlashAndSlowMo();
        RestoreBallVisual();
        _trailColorOverridden = false;
        ApplyDefaultTrail();
        RestoreComponents();
        transform.position = _spawnPosition;
        BeginWaitForLaunch();
    }

    private void OnGameOver()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
        StopAllCoroutines();
        StopSlashAndSlowMo();
        if (_executeChainActive) StopExecuteChain();
        IsWaitingForLaunch = false;
        LaunchGuide.Instance?.Hide();
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _launched = false;
        HideComponents();
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
    }

    private Coroutine _respawnCoroutine;

    private void OnBallLost()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        StopSlashAndSlowMo();
        StopAllCoroutines();

        if (_executeChainActive) StopExecuteChain();

        RestoreBallVisual();
        // 重置加速齿轮残留状态，防止黄色 trail 持续到下一条命
        SpeedMultiplier = 1f;
        _trailColorOverridden = false;
        ApplyDefaultTrail();

        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _launched = false;
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    /// <summary>掉球/中断时强制退出 Slash 瞄准与慢动作，避免 timeScale 卡住重生流程。</summary>
    private static void StopSlashAndSlowMo()
    {
        SkillManager.Instance?.CancelAiming();
        SlowMoFX.Instance?.ForceRestore();
        LaunchGuide.Instance?.Hide();
    }

    private IEnumerator RespawnRoutine()
    {
        HideComponents();
        yield return new WaitForSecondsRealtime(config.respawnDelay);

        if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
        {
            _respawnCoroutine = null;
            yield break;
        }

        RestoreComponents();
        RestoreBallVisual();
        transform.position = _spawnPosition;
        IsInvincible = true;
        BeginWaitForLaunch();

        while (IsWaitingForLaunch)
            yield return new WaitForSecondsRealtime(0.02f);

        yield return new WaitForSecondsRealtime(config.respawnInvincibleDuration);
        IsInvincible = false;
        if (GameManager.Instance != null)
            GameManager.Instance.OnBallRespawned();
        _respawnCoroutine = null;
    }

    private void BeginWaitForLaunch()
    {
        IsWaitingForLaunch = true;
        _guideAngle    = 90f;
        _guideSwingDir = 1f;
        Vector2 initDir = new Vector2(
            Mathf.Cos(_guideAngle * Mathf.Deg2Rad),
            Mathf.Sin(_guideAngle * Mathf.Deg2Rad));
        LaunchGuide.Instance?.Show(transform.position, initDir);
    }

    private void ExecuteLaunch(Vector2 dir)
    {
        IsWaitingForLaunch = false;
        LaunchGuide.Instance?.Hide();
        _rb.velocity = dir.normalized * config.ballLaunchSpeed;
        _launched    = true;
        ComboSystem.Instance?.ForceResetCombo();
    }

    private void HideComponents()
    {
        _col.enabled = false;
        if (_sr    != null) _sr.enabled = false;
        if (_trail != null) { _trail.Clear(); _trail.enabled = false; }
    }

    private void RestoreComponents()
    {
        _col.enabled = true;
        if (_sr    != null) _sr.enabled = true;
        if (_trail != null) _trail.enabled = true;
    }

    private void FixedUpdate()
    {
        CheckBottomFall();

        if (!_launched) return;

        float speed = _rb.velocity.magnitude;
        float minS  = config.ballMinSpeed * SpeedMultiplier;
        float maxS  = EffectiveMaxSpeed;

        if (speed < minS && speed > 0.1f)
            _rb.velocity = _rb.velocity.normalized * minS;
        else if (speed > maxS)
            _rb.velocity = _rb.velocity.normalized * maxS;

        UpdateTrailFromSpeed(speed);

        // ── 运动死区检测（引力过载） ───────────────────────────────────────
        if (_executeChainActive || _gravityOverloadActive) return;

        Vector2 vel = _rb.velocity;
        float absVx = Mathf.Abs(vel.x);
        float absVy = Mathf.Abs(vel.y);

        MotionDeadZone deadZone = MotionDeadZone.None;
        if (absVx > absVy && absVy < AXIS_DEAD_THRESHOLD)
            deadZone = MotionDeadZone.Horizontal;
        else if (absVy > absVx && absVx < AXIS_DEAD_THRESHOLD)
            deadZone = MotionDeadZone.Vertical;

        if (deadZone == MotionDeadZone.Horizontal)
        {
            _verticalDeadZoneTimer = 0f;
            _horizontalDeadZoneTimer += Time.fixedDeltaTime;
            if (_horizontalDeadZoneTimer >= DEADZONE_DURATION)
                StartCoroutine(GravityOverloadRoutine(deadZone));
        }
        else if (deadZone == MotionDeadZone.Vertical)
        {
            _horizontalDeadZoneTimer = 0f;
            _verticalDeadZoneTimer += Time.fixedDeltaTime;
            if (_verticalDeadZoneTimer >= DEADZONE_DURATION)
                StartCoroutine(GravityOverloadRoutine(deadZone));
        }
        else
        {
            _horizontalDeadZoneTimer = 0f;
            _verticalDeadZoneTimer   = 0f;
        }
    }

    public void SetSizeMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 0.5f, 3f);
        transform.localScale = Vector3.one * multiplier;
        if (_col != null && config != null)
            _col.radius = config.ballRadius * multiplier;
    }

    public void SetOverrideTrailColor(Color startColor, Color endColor)
    {
        _trailColorOverridden = true;
        if (_trail != null)
        {
            _trail.startColor = startColor;
            _trail.endColor = endColor;
        }
    }

    public void ResetTrailColor()
    {
        _trailColorOverridden = false;
        ApplyDefaultTrail();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!_launched) return;

        TruncateTrailOnImpact();

        // ── 斩杀连锁逻辑 ──────────────────────────────────────────────────
        if (_executeChainActive)
        {
            EnemyBase enemy = col.gameObject.GetComponentInParent<EnemyBase>();

            if (enemy != null && !enemy.IsDead)
            {
                Vector2 chainHitPos = col.contacts.Length > 0
                    ? col.contacts[0].point
                    : (Vector2)enemy.transform.position;
                ComboSystem.Instance?.RegisterAirtimeHit(chainHitPos);

                bool isBoss = enemy is Boss;
                if (!isBoss)
                {
                    // 小兵：瞬杀，消耗连锁次数，尝试追踪下一个小兵目标
                    int hitsNeeded = enemy.maxHits - enemy.CurrentHits;
                    for (int i = 0; i < hitsNeeded; i++) enemy.TakeHit();

                    _chainsRemaining--;
                    if (_chainsRemaining > 0)
                    {
                        Transform target = GetClosestEnemyTarget(); // 只返回小兵
                        if (target != null)
                        {
                            StartCoroutine(RedirectToTargetRoutine(target));
                            return;
                        }
                    }
                }
                else
                {
                    // Boss：1 次普通伤害，立即终止连锁（Boss 不是有效的链式目标）
                    enemy.TakeHit();
                }

                StopExecuteChain();
                return;
            }
            // 撞到墙壁/Bumper（已穿透）：不消耗连锁次数，正常物理反弹
        }

        if (col.gameObject.GetComponentInParent<FlipperController>() != null)
            ComboSystem.Instance?.BreakOnFlipper();

        // Bumper/Slingshot 已有自己的 ImpactFX 和音效调用，跳过避免重复
        if (col.gameObject.GetComponent<Bumper>()    != null) return;
        if (col.gameObject.GetComponent<Slingshot>() != null) return;

        // 敌人受击 Juice 由 EnemyBase.TakeHit → EnemyJuice 统一处理，避免双发粒子/Combo/音效
        if (col.gameObject.GetComponentInParent<EnemyBase>() != null) return;

        AudioManager.Instance?.PlayBounce();

        if (ImpactFX.Instance == null) return;

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;
        var sr = col.gameObject.GetComponentInChildren<SpriteRenderer>();
        Color hitColor = sr != null ? sr.color : NeonColors.Active.GetBase(NeonRole.SkillShield);

        if (JuiceRouter.IsWallCollider(col.collider))
        {
            Vector2 normal = col.contacts.Length > 0 ? col.contacts[0].normal : Vector2.up;
            JuiceRouter.WallHit(hitPos, normal, col.relativeVelocity.magnitude, hitColor);
            return;
        }

        float velMag = col.relativeVelocity.magnitude;
        float intensity = Mathf.Clamp01(velMag / 12f) * 0.85f + 0.15f;
        ImpactFX.Instance.SpawnHit(hitPos, hitColor, intensity);
    }

    // ── 斩杀连锁：扫描并获取最近的有效敌方目标 ─────────────────────────────────
    private Transform GetClosestEnemyTarget()
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        Transform closest = null;
        float minDist     = float.MaxValue;
        Vector3 pos       = transform.position;

        foreach (var e in enemies)
        {
            if (e == null || e.IsDead || e is Boss) continue; // Boss 不是有效的连锁目标
            float dist = (e.transform.position - pos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closest = e.transform;
            }
        }
        return closest;
    }

    // ── 斩杀连锁：在碰撞微小的反弹后，下一物理帧强行重定向，破空冲刺 ─────────────
    private IEnumerator RedirectToTargetRoutine(Transform target)
    {
        yield return new WaitForFixedUpdate();

        if (target == null || !_executeChainActive) yield break;

        Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        if (_rb != null)
        {
            _rb.velocity = dir * config.ballMaxSpeed * 1.25f; // 超高速破空重弹
        }

        // 斩击爆发时的豪华声光反馈
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
        if (ImpactFX.Instance != null)
        {
            ImpactFX.Instance.SpawnHit(transform.position, NeonColors.Active.GetBase(NeonRole.SkillExecute), 1.2f);
        }
    }

    // ── 引力过载：打破水平/垂直死区 ─────────────────────────────────────
    private IEnumerator GravityOverloadRoutine(MotionDeadZone deadZone)
    {
        _gravityOverloadActive = true;
        _horizontalDeadZoneTimer = 0f;
        _verticalDeadZoneTimer   = 0f;

        // 视觉特效：蓝色电光拖尾
        Color originalStart = _trail.startColor;
        Color originalEnd = _trail.endColor;
        Color electricBlue = new Color(0f, 0.8f, 1f, 1f);
        
        if (_trail != null)
        {
            _trail.startColor = electricBlue;
            _trail.endColor = new Color(electricBlue.r, electricBlue.g, electricBlue.b, 0.05f);
        }

        float forceMagnitude = config.ballMaxSpeed * 1.5f;
        if (deadZone == MotionDeadZone.Horizontal)
        {
            // 水平往复 → 强向下推力
            _rb.velocity = new Vector2(_rb.velocity.x * 0.3f, -forceMagnitude);
        }
        else
        {
            // 垂直往复 → 强横向推力（偏向场地中心）
            float hx = GetHorizontalBreakSign();
            _rb.velocity = new Vector2(hx * forceMagnitude, _rb.velocity.y * 0.3f);
        }

        // 音效和震屏
        AudioManager.Instance?.PlayBounce();
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        
        // 粒子特效
        if (ImpactFX.Instance != null)
        {
            ImpactFX.Instance.SpawnHit(transform.position, electricBlue, 1.0f);
        }

        // 持续0.5秒后恢复
        yield return new WaitForSeconds(0.5f);

        if (_trail != null && !_executeChainActive)
        {
            _trail.startColor = originalStart;
            _trail.endColor = originalEnd;
        }

        _gravityOverloadActive = false;
    }

    private float GetHorizontalBreakSign()
    {
        float x = transform.position.x;
        if (Mathf.Abs(x) > 0.05f) return x > 0f ? -1f : 1f;
        return Random.value > 0.5f ? 1f : -1f;
    }

    private void CheckBottomFall()
    {
        if (!CanLoseLifeFromBottom()) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        float limitY = config != null ? MinionLineRules.GetBallFallLineY() : -8.85f;
        if (transform.position.y > limitY) return;

        GameManager.Instance.BallFellDown();
    }
}
