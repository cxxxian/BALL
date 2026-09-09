using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Boss 导弹弹刀测试台：黑底 + Boss 发射导弹 + 可移动测试球。
/// 场景：Assets/Scenes/Test/BossMissileParryTest.unity
/// </summary>
public class BossMissileParryTestHarness : MonoBehaviour
{
    private const string BossAssetPath = "Assets/ScriptableObjects/Enemies/Boss_W2.asset";
    private const string ConfigAssetPath = "Assets/ScriptableObjects/DefaultGameConfig.asset";
    private const string BloomProfilePath = "Assets/Settings/TronGlobalProfile.asset";
    private const string ExplodeSfxPath = "Assets/Audio/Music/SFX/explode.wav";
    private const string ReboundSfxPath = "Assets/Audio/Music/SFX/rebound.wav";

    [Header("Boss")]
    public BossDefinition bossDefinition;
    public Vector3 bossPosition = new Vector3(0f, 5.2f, 0f);
    public float bossMoveMinX = -3.2f;
    public float bossMoveMaxX = 3.2f;
    public float firstMissileDelay = 1.2f;
    public float missileCooldown = 4.5f;

    [Header("Volley Test (late-game)")]
    [Tooltip("齐射导弹数：1 单发 / 2~3 交错多发")]
    [Range(1, 3)] public int missilesPerVolley = 1;
    [Tooltip("齐射内相邻导弹间隔（秒）")]
    [Range(0.15f, 1.2f)] public float volleyStagger = 0.48f;
    [Tooltip("交错弧左右额外摆幅")]
    [Range(0f, 1.2f)] public float volleySideBoost = 0.55f;

    [Header("Ball")]
    public Vector3 ballStartPos = new Vector3(0f, -1.2f, 0f);
    public float ballMoveSpeed = 6f;
    public float ballClampX = 3.6f;
    public float ballClampYMin = -4.5f;
    public float ballClampYMax = 2.5f;
    [Tooltip("无按键时左右缓扫，方便空闲练习弹刀")]
    public bool autoDrift = true;
    public float autoDriftSpeed = 1.6f;

    [Header("Refs (runtime)")]
    public GameConfig config;

    private Boss _boss;
    private BossMissileAttack _missile;
    private BallController _ball;
    private float _driftDir = 1f;
    private int _parryHintFlash;

    private void Start()
    {
        Time.timeScale = 1f;
        SlowMoFX.Instance?.ForceRestore();
        EnsureBlackCameraWithBloom();
        EnsureRuntimeServices();
        ResolveBossDefinition();
        SpawnBall();
        SpawnBoss();
    }

    private void Update()
    {
        // 防止 GameManager.onGameStart 把测试球打回「等待发球」
        if (_ball != null && _ball.IsWaitingForLaunch)
            _ball.ForceTestInPlay(new Vector2(2.5f, 3.5f));

        if (Input.GetKeyDown(KeyCode.R))
        {
            ClearMissiles();
            SpawnBoss();
        }

        if (Input.GetKeyDown(KeyCode.B))
            SpawnBall();

        if (Input.GetKeyDown(KeyCode.Space))
            autoDrift = !autoDrift;

        // 1/2/3：设定齐射数并立即发射；V 循环切换齐射数；[ ] 调交错间隔
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            SetVolleyAndFire(1);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            SetVolleyAndFire(2);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            SetVolleyAndFire(3);

        if (Input.GetKeyDown(KeyCode.V))
        {
            missilesPerVolley = missilesPerVolley >= 3 ? 1 : missilesPerVolley + 1;
            ApplyVolleyScaling();
        }

        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            volleyStagger = Mathf.Max(0.15f, volleyStagger - 0.05f);
            ApplyVolleyScaling();
        }
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            volleyStagger = Mathf.Min(1.2f, volleyStagger + 0.05f);
            ApplyVolleyScaling();
        }

        if (Input.GetKeyDown(KeyCode.F))
            ForceFireMissile();

        if (Input.GetKeyDown(KeyCode.T))
        {
            Time.timeScale = 1f;
            SlowMoFX.Instance?.ForceRestore();
        }

        TickBallControl();
    }

    private void TickBallControl()
    {
        if (_ball == null) return;
        var rb = _ball.Rb;
        if (rb == null) return;

        Vector2 input = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;

        Vector3 pos = _ball.transform.position;
        if (input.sqrMagnitude > 0.01f)
        {
            pos += (Vector3)(input.normalized * ballMoveSpeed * Time.deltaTime);
            rb.velocity = input.normalized * Mathf.Max(config != null ? config.ballMinSpeed : 5f, 5f);
        }
        else if (autoDrift)
        {
            pos.x += _driftDir * autoDriftSpeed * Time.deltaTime;
            if (pos.x > ballClampX) { pos.x = ballClampX; _driftDir = -1f; }
            if (pos.x < -ballClampX) { pos.x = -ballClampX; _driftDir = 1f; }
            rb.velocity = new Vector2(_driftDir * autoDriftSpeed, 0.35f);
        }
        else
        {
            // 保持微速，避免死区过载 / Miss 搅角无速度
            if (rb.velocity.magnitude < 0.8f)
                rb.velocity = new Vector2(1.2f * _driftDir, 0.4f);
        }

        pos.x = Mathf.Clamp(pos.x, -ballClampX, ballClampX);
        pos.y = Mathf.Clamp(pos.y, ballClampYMin, ballClampYMax);
        pos.z = 0f;
        _ball.transform.position = pos;
    }

    private void SpawnBoss()
    {
        if (_boss != null)
            Destroy(_boss.gameObject);

        if (bossDefinition == null)
        {
            Debug.LogError("[BossMissileParryTest] bossDefinition missing.");
            return;
        }

        var go = new GameObject("Test_Boss");
        go.transform.position = bossPosition;
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        go.AddComponent<BoxCollider2D>();

        _missile = go.AddComponent<BossMissileAttack>();
        _missile.minWaveIndex = 0;
        _missile.firstDelay = firstMissileDelay;
        _missile.cooldownP1 = missileCooldown;
        _missile.cooldownP2 = Mathf.Max(2.5f, missileCooldown * 0.7f);
        _missile.telegraphDuration = 1.25f;
        _missile.flightDuration = 2.45f;
        _missile.shrinkDuration = 0.68f;
        _missile.approachRadius = 1.75f;
        _missile.bangOrbitRadius = 1.15f;
        ApplyVolleyScaling();

        _boss = go.AddComponent<Boss>();
        // waveIndex=0 + minWaveIndex=0 → 测试场景必开火
        _boss.Initialize(bossDefinition, bossMoveMinX, bossMoveMaxX, 0);

        // 测试用：抬高血量方便多轮弹刀
        _boss.maxHits = Mathf.Max(_boss.maxHits, 40);
    }

    private void ApplyVolleyScaling()
    {
        if (_missile == null) return;
        _missile.useWaveScaling = false;
        _missile.scalingOverride = new BossMissileScaling
        {
            flightDurationScale = 1f,
            telegraphDurationScale = 1f,
            missilesPerVolley = Mathf.Clamp(missilesPerVolley, 1, 3),
            volleyStagger = volleyStagger,
            volleySideBoost = volleySideBoost,
        };
    }

    private void SetVolleyAndFire(int count)
    {
        missilesPerVolley = Mathf.Clamp(count, 1, 3);
        ApplyVolleyScaling();
        ForceFireMissile();
    }

    private void SpawnBall()
    {
        if (_ball != null)
            Destroy(_ball.gameObject);

        if (config == null)
        {
            Debug.LogError("[BossMissileParryTest] GameConfig missing.");
            return;
        }

        var go = new GameObject("Test_Ball");
        go.transform.position = ballStartPos;
        go.tag = "Ball";

        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CircleCollider2D>();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        _ball = go.AddComponent<BallController>();
        _ball.config = config;
        _ball.ForceTestInPlay(new Vector2(2.5f, 3.5f));
    }

    public void ForceFireMissile()
    {
        if (_missile == null || _boss == null || _boss.IsDead) return;
        if (_ball != null && _ball.IsWaitingForLaunch)
            _ball.ForceTestInPlay(new Vector2(2.5f, 3.5f));

        // 测试台优先立即出弹，方便手感；自动循环仍走预警
        if (!_missile.TryFireImmediate())
        {
            if (!_missile.TryFireNow())
                Debug.Log("[BossMissileParryTest] Fire failed (missile active or ball not ready).");
        }
        _parryHintFlash = 45;
    }

    private void ClearMissiles()
    {
        foreach (var m in FindObjectsOfType<BezierMissile>())
        {
            if (m != null) Destroy(m.gameObject);
        }
    }

    private void EnsureRuntimeServices()
    {
        if (config == null)
        {
#if UNITY_EDITOR
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
#endif
        }

        var gm = GameManager.Instance;
        if (gm == null)
        {
            var gmGo = new GameObject("Test_GameManager");
            gm = gmGo.AddComponent<GameManager>();
        }
        gm.config = config;
        if (gm.State == GameState.Idle)
            gm.StartGame();

        if (InputManager.Instance == null)
        {
            var inputGo = new GameObject("Test_InputManager");
            inputGo.AddComponent<InputManager>();
        }

        if (ImpactFX.Instance == null)
        {
            var fxGo = new GameObject("Test_ImpactFX");
            fxGo.AddComponent<ImpactFX>();
        }

        if (ComboSystem.Instance == null)
        {
            var comboGo = new GameObject("Test_ComboSystem");
            comboGo.AddComponent<ComboSystem>();
        }

        if (AudioManager.Instance == null)
        {
            var audioGo = new GameObject("Test_AudioManager");
            var audio = audioGo.AddComponent<AudioManager>();
            audio.playBgmOnStart = false;
#if UNITY_EDITOR
            audio.explodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ExplodeSfxPath);
            audio.reboundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ReboundSfxPath);
#endif
        }
        else
        {
#if UNITY_EDITOR
            if (AudioManager.Instance.explodeClip == null)
                AudioManager.Instance.explodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ExplodeSfxPath);
            if (AudioManager.Instance.reboundClip == null)
                AudioManager.Instance.reboundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ReboundSfxPath);
#endif
        }

        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraShake>() == null)
            cam.gameObject.AddComponent<CameraShake>();
    }

    private void ResolveBossDefinition()
    {
        if (bossDefinition != null) return;
#if UNITY_EDITOR
        bossDefinition = AssetDatabase.LoadAssetAtPath<BossDefinition>(BossAssetPath);
#endif
        if (bossDefinition == null)
            Debug.LogError($"[BossMissileParryTest] Failed to load {BossAssetPath}");
    }

    private static void EnsureBlackCameraWithBloom()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 0f, -10f);
        }

        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.allowHDR = true;

        var camData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null)
            camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        if (Object.FindObjectOfType<Volume>() == null)
        {
            VolumeProfile profile = null;
#if UNITY_EDITOR
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
#endif
            if (profile != null)
            {
                var volGo = new GameObject("Test_BloomVolume");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 1f;
                vol.weight = 1f;
                vol.sharedProfile = profile;
            }
            else
            {
                Debug.LogWarning($"[BossMissileParryTest] Missing bloom profile at {BloomProfilePath}");
            }
        }
    }

    private void OnGUI()
    {
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUILayout.BeginArea(new Rect(12, 12, 460, 190));
        int hp = 0;
        int max = 0;
        if (_boss != null && !_boss.IsDead)
        {
            hp = Mathf.Max(0, _boss.maxHits - _boss.CurrentHits);
            max = _boss.maxHits;
        }
        int live = FindObjectsOfType<BezierMissile>().Length;
        GUILayout.Label($"Boss Missile Parry Test  |  HP {hp}/{max}  |  Live missiles {live}");
        GUILayout.Label($"WASD 移球  |  左键/F 弹刀  |  Space 漂移 {(autoDrift ? "ON" : "OFF")}");
        GUILayout.Label($"1~3 齐射并发射  |  V 循环发数  |  [ ] 交错间隔 {volleyStagger:0.00}s");
        GUILayout.Label($"当前齐射: {missilesPerVolley} 发  |  侧摆加成 {volleySideBoost:0.00}  |  R Boss  |  B 球  |  T 恢复时缓");
        GUILayout.Label($"timeScale {Time.timeScale:0.00}");
        if (_parryHintFlash > 0)
        {
            _parryHintFlash--;
            GUI.color = new Color(1f, 0.7f, 0.2f, 1f);
            GUILayout.Label($">> Volley x{missilesPerVolley} inbound");
        }
        GUILayout.EndArea();
        GUI.color = prev;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.15f, 0.35f);
        Gizmos.DrawWireCube(bossPosition, new Vector3(1.6f, 1.6f, 0f));
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(ballStartPos, 0.25f);
    }
}
