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
    public bool IsWaitingForLaunch { get; private set; } = false;
    public Rigidbody2D Rb => _rb;

    // ── 斩杀连锁技能状态 ──────────────────────────────────────────────
    private bool  _executeChainActive = false;
    private int   _chainsRemaining    = 0;
    private Color _originalTrailColor;
    private float _originalTrailWidth;

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
            _originalTrailColor = _trail.startColor;
            _originalTrailWidth = _trail.startWidth;
        }
    }

    // ── 激活斩杀连锁状态 ──────────────────────────────────────────────────
    public void StartExecuteChain(int maxChains)
    {
        _executeChainActive = true;
        _chainsRemaining    = maxChains;
        IsInvincible        = true;
        _launched           = true;

        if (_trail != null)
        {
            _trail.startWidth = _originalTrailWidth * 2.2f;
            _trail.endWidth   = _originalTrailWidth * 0.4f;
            _trail.startColor = new Color(1f, 0f, 0.47f, 1f);
            _trail.endColor   = new Color(1f, 0f, 0.47f, 0.05f);
        }

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

        if (_trail != null)
        {
            _trail.startWidth = _originalTrailWidth;
            _trail.endWidth   = _originalTrailWidth * 0.1f;
            _trail.startColor = _originalTrailColor;
            _trail.endColor   = new Color(_originalTrailColor.r, _originalTrailColor.g, _originalTrailColor.b, 0f);
        }

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

        if (_sr != null && _sr.sprite == null)
            _sr.sprite = GruntEnemy.CreateCircleSprite(64, Color.white);
        // Tron: 球始终使用 HDR 白，配合 2D Point Light 产生白色光晕
        if (_sr != null) _sr.color = new Color(3.5f, 3.5f, 3.8f, 1f);
    }

    private void Update()
    {
        if (!IsWaitingForLaunch) return;

        // 引导线摆动
        _guideAngle += _guideSwingDir * config.guideSwingSpeed * Time.deltaTime;
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
        RestoreComponents();
        transform.position = _spawnPosition;
        BeginWaitForLaunch();
    }

    private void OnGameOver()
    {
        StopAllCoroutines();
        if (_executeChainActive) StopExecuteChain(); // 清除斩杀状态、恢复 trail 和 Bumper
        IsWaitingForLaunch = false;
        LaunchGuide.Instance?.Hide();
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _launched = false;
        HideComponents();
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
    }

    private void OnBallLost()
    {
        if (_executeChainActive) StopExecuteChain(); // 死亡中断斩杀链，trail 立即恢复正常
        _rb.velocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _launched = false;
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        HideComponents();
        yield return new WaitForSeconds(config.respawnDelay);

        if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            yield break;

        RestoreComponents();
        transform.position = _spawnPosition;
        IsInvincible = true;
        BeginWaitForLaunch();

        // 等待玩家发射 + 无敌时间
        yield return new WaitUntil(() => !IsWaitingForLaunch);
        yield return new WaitForSeconds(config.respawnInvincibleDuration);
        IsInvincible = false;
        if (GameManager.Instance != null)
            GameManager.Instance.OnBallRespawned();
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
        if (!_launched) return;
        float speed = _rb.velocity.magnitude;
        if (speed < config.ballMinSpeed && speed > 0.1f)
            _rb.velocity = _rb.velocity.normalized * config.ballMinSpeed;
        else if (speed > config.ballMaxSpeed)
            _rb.velocity = _rb.velocity.normalized * config.ballMaxSpeed;
    }

    public void SetSizeMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 0.5f, 3f);
        transform.localScale = Vector3.one * multiplier;
        if (_col != null) _col.radius = 0.275f * multiplier;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!_launched || ImpactFX.Instance == null) return;

        // ── 斩杀连锁逻辑 ──────────────────────────────────────────────────
        if (_executeChainActive)
        {
            EnemyBase enemy = col.gameObject.GetComponentInParent<EnemyBase>();

            if (enemy != null && !enemy.IsDead)
            {
                // Boss 只打 1 次普通伤害，小兵才触发瞬杀（打满剩余 HP）
                bool isBoss = enemy is Boss;
                if (!isBoss)
                {
                    int hitsNeeded = enemy.maxHits - enemy.CurrentHits;
                    for (int i = 0; i < hitsNeeded; i++) enemy.TakeHit();
                }
                else
                {
                    enemy.TakeHit();
                }

                // ★ 只有打中敌人才消耗连锁次数；打墙/打 Bumper（已穿透）不计
                _chainsRemaining--;
                if (_chainsRemaining > 0)
                {
                    Transform target = GetClosestEnemyTarget();
                    if (target != null)
                    {
                        StartCoroutine(RedirectToTargetRoutine(target));
                        return;
                    }
                }
                StopExecuteChain();
                return;
            }
            // 撞到墙壁：什么都不做，正常物理反弹，保留剩余连锁次数
        }

        // Bumper/Slingshot 已有自己的 ImpactFX 调用，跳过避免重复
        if (col.gameObject.GetComponent<Bumper>()    != null) return;
        if (col.gameObject.GetComponent<Slingshot>() != null) return;

        Vector2 hitPos = col.contacts.Length > 0 ? col.contacts[0].point : (Vector2)transform.position;

        // 取碰撞物体的颜色（墙壁=蓝，挡板=橙，默认=青）
        var sr = col.gameObject.GetComponentInChildren<SpriteRenderer>();
        Color hitColor = sr != null ? sr.color : new Color(0f, 0.85f, 1.0f);

        // 强度根据碰撞速度调整（慢速碰撞产生更少粒子）
        float velMag    = col.relativeVelocity.magnitude;
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
            if (e == null || e.IsDead) continue;
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
            ImpactFX.Instance.SpawnHit(transform.position, new Color(1f, 0f, 0.47f, 1f), 1.2f);
        }
    }
}
