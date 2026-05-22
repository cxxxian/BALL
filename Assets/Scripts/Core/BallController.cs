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

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (!_launched || ImpactFX.Instance == null) return;
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
}
