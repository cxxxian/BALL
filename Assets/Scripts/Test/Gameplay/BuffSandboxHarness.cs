using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Buff 构筑沙盒：正式弹珠 + 台面外框 + OnGUI 加减 Buff。
/// 显示 Score / HP / Combo，敌人有得分；可开触底扣血测护心。场景：BuffSandbox.unity
/// </summary>
public class BuffSandboxHarness : MonoBehaviour
{
    private const string GruntAssetPath = "Assets/ScriptableObjects/Enemies/Minion_Grunt.asset";
    private const string BuffFolder = "Assets/ScriptableObjects/Buffs";
    private const string ConfigAssetPath = "Assets/ScriptableObjects/DefaultGameConfig.asset";
    private const string BallDefPath = "Assets/ScriptableObjects/ThunderBall.asset";
    private const string WallSpritePath = "Assets/Art/Table/wall_segment.png";
    private const string WallMatPath = "Assets/Materials/TronWall.mat";

    [Header("Catalog")]
    [Tooltip("留空则 Editor 下自动扫描 Buffs 文件夹（自动排除 Legacy）")]
    public List<BuffDefinition> buffCatalog = new List<BuffDefinition>();

    [Header("Spawn")]
    public MinionDefinition enemyDefinition;
    public bool autoSpawn = true;
    public float spawnInterval = 1.4f;
    public float spawnY = 5.8f;
    public float spawnXRange = 3.2f;
    public int enemyHp = 8;
    public float enemySpeed = 0.45f;
    public int maxAlive = 12;

    [Header("Ball (SampleScene feel)")]
    public GameConfig config;
    public BallDefinition ballDefinition;
    public Vector3 ballSpawnPos = new Vector3(0f, -6.8f, 0f);
    public float ballLaunchSpeed = 12f;

    [Header("Arena")]
    public bool buildPlayfieldFrame = true;
    public float bottomWallY = -8.6f;

    [Header("Sandbox Rules")]
    [Tooltip("触底扣 1 血（测护心符 / 最大生命）。默认关。")]
    public bool bottomDamagesPlayer;

    private BuffManager _buffManager;
    private TowerManager _towerManager;
    private BallController _ball;
    private float _spawnTimer;
    private int _alive;
    private Vector2 _scroll;
    private string _filter = "";
    private bool _showOwnedOnly;
    private int _sessionKills;
    private string _lastEvent = "";
    private float _lastEventUntil;

    private void Awake()
    {
        Time.timeScale = 1f;
        EnsureBlackCamera();
        EnsureRuntimeServices();
        EnsurePlayfield();
        EnsureManagers();
        LoadCatalog();
        SyncBuffPool();
        ResolveEnemyDefinition();
        SpawnOrResetBall();
        _spawnTimer = 0.4f;
    }

    private void Update()
    {
        // GameManager.onGameStart 可能把球打回等待发球
        if (_ball != null && _ball.IsWaitingForLaunch)
            _ball.ForceTestInPlay(Vector2.up * Mathf.Max(6f, ballLaunchSpeed * 0.5f));

        if (Input.GetKeyDown(KeyCode.C)) ClearEnemies();
        if (Input.GetKeyDown(KeyCode.B)) SpawnOrResetBall();
        if (Input.GetKeyDown(KeyCode.Space)) autoSpawn = !autoSpawn;
        if (Input.GetKeyDown(KeyCode.X)) ClearAllBuffsAndTowers();
        if (Input.GetKeyDown(KeyCode.H))
        {
            bottomDamagesPlayer = !bottomDamagesPlayer;
            SyncBottomDamageFlag();
            PushEvent(bottomDamagesPlayer ? "Bottom damage ON" : "Bottom damage OFF");
        }
        if (Input.GetKeyDown(KeyCode.R)) ResetRunStats();

        HandleBallLaunchInput();

        if (!autoSpawn) return;
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;
        _spawnTimer = spawnInterval;
        if (_alive < maxAlive)
            SpawnEnemy();
    }

    private void EnsureRuntimeServices()
    {
#if UNITY_EDITOR
        if (config == null)
            config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
        if (ballDefinition == null)
            ballDefinition = AssetDatabase.LoadAssetAtPath<BallDefinition>(BallDefPath);
#endif
        if (config == null)
            Debug.LogError("[BuffSandbox] Missing GameConfig — ball physics will break.");

        var gm = GameManager.Instance;
        if (gm == null)
        {
            var gmGo = new GameObject("Sandbox_GameManager");
            gm = gmGo.AddComponent<GameManager>();
        }
        gm.config = config;
        if (gm.State == GameState.Idle)
            gm.StartGame();

        if (InputManager.Instance == null)
            new GameObject("Sandbox_InputManager").AddComponent<InputManager>();

        if (ImpactFX.Instance == null)
            new GameObject("Sandbox_ImpactFX").AddComponent<ImpactFX>();

        if (ComboSystem.Instance == null)
            new GameObject("Sandbox_ComboSystem").AddComponent<ComboSystem>();

        if (AudioManager.Instance == null)
        {
            var audio = new GameObject("Sandbox_AudioManager").AddComponent<AudioManager>();
            audio.playBgmOnStart = false;
        }

        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraShake>() == null)
            cam.gameObject.AddComponent<CameraShake>();
    }

    private void EnsurePlayfield()
    {
        if (!buildPlayfieldFrame) return;

        PhysicsMaterial2D wallPhys = CreateBounceMat("SandboxWallMat");

        var top = FindObjectOfType<PlayfieldTopArc>();
        if (top == null)
        {
            var go = new GameObject("PlayfieldTopArc");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<EdgeCollider2D>();
            go.AddComponent<PlayfieldWallPulse>();
            top = go.AddComponent<PlayfieldTopArc>();
        }

        top.physicsMaterial = wallPhys;
        top.hideSideWallSprites = true;
        top.wallBottomY = -10f;
        top.shoulderY = 7.1f;
        top.apexY = 9.25f;
        top.wallCenterX = 4.5f;
#if UNITY_EDITOR
        if (top.wallMaterial == null)
            top.wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
        if (top.wallSegmentSprite == null)
            top.wallSegmentSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePath);
#endif
        top.Rebuild();

        var shoulder = FindObjectOfType<PlayfieldSideShoulder>();
        if (shoulder == null)
        {
            var go = new GameObject("PlayfieldSideShoulder");
            shoulder = go.AddComponent<PlayfieldSideShoulder>();
        }
        shoulder.physicsMaterial = wallPhys;
        shoulder.wallCenterX = 4.5f;
        shoulder.wallBottomY = -10f;
        shoulder.wallTopY = 7.1f;
#if UNITY_EDITOR
        if (shoulder.artMaterial == null)
            shoulder.artMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
#endif
        shoulder.Rebuild();

        // 底边反弹墙：正式局靠挡板/底线；沙盒无挡板，补一条，避免球掉出
        if (GameObject.Find("SandboxBottomWall") == null)
        {
            var bottom = new GameObject("SandboxBottomWall");
            bottom.transform.position = new Vector3(0f, bottomWallY, 0f);
            var edge = bottom.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(-4.1f, 0f),
                new Vector2(4.1f, 0f)
            };
            edge.edgeRadius = 0.08f;
            edge.sharedMaterial = wallPhys;
        }
    }

    private void EnsureManagers()
    {
        _buffManager = BuffManager.Instance;
        if (_buffManager == null)
            _buffManager = new GameObject("BuffManager_Sandbox").AddComponent<BuffManager>();

        _towerManager = TowerManager.Instance;
        if (_towerManager == null)
            _towerManager = new GameObject("TowerManager_Sandbox").AddComponent<TowerManager>();
    }

    private void LoadCatalog()
    {
        if (buffCatalog != null && buffCatalog.Count > 0) return;
#if UNITY_EDITOR
        buffCatalog = new List<BuffDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:BuffDefinition", new[] { BuffFolder });
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var def = AssetDatabase.LoadAssetAtPath<BuffDefinition>(path);
            if (def == null) continue;
            // Legacy：连击大师已下池，沙盒也不展示
            if (def.effectType == BuffEffectType.ComboThresholdDown) continue;
            if (path.IndexOf("/Legacy/", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            buffCatalog.Add(def);
        }
        buffCatalog.Sort((a, b) =>
        {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            c = a.rarity.CompareTo(b.rarity);
            return c != 0 ? c : string.CompareOrdinal(a.buffName, b.buffName);
        });
#endif
    }

    private void SyncBuffPool()
    {
        if (_buffManager == null) return;
        _buffManager.buffPool = new List<BuffDefinition>();
        foreach (var def in buffCatalog)
        {
            if (def == null) continue;
            _buffManager.buffPool.Add(def);
        }
    }

    private void ApplyOne(BuffDefinition def)
    {
        if (def == null || _buffManager == null) return;

        if (!_buffManager.buffPool.Contains(def))
            _buffManager.buffPool.Add(def);

        _buffManager.ApplyBuff(def);

        if (BuffManager.IsTowerBuildEffect(def.effectType))
            _towerManager?.DevAutoPlaceOrUpgrade(def.effectType);
    }

    private void RemoveOne(BuffDefinition def)
    {
        if (def == null || _buffManager == null) return;
        _buffManager.RemoveOneStackForTest(def.effectType);
    }

    private void ClearAllBuffsAndTowers()
    {
        _buffManager?.ClearAllBuffsForTest();
        _towerManager?.DevClearAllTowers();
    }

    private void SpawnOrResetBall()
    {
        if (_ball != null)
            Destroy(_ball.gameObject);

        if (config == null)
        {
            Debug.LogError("[BuffSandbox] GameConfig missing — cannot spawn BallController.");
            return;
        }

        var go = new GameObject("Ball");
        go.tag = "Ball";
        go.transform.position = ballSpawnPos;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        go.AddComponent<CircleCollider2D>();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 20;

        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0.02f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.sortingOrder = 19;
        trail.enabled = false;

        _ball = go.AddComponent<BallController>();
        _ball.config = config;
        if (ballDefinition != null)
            _ball.ApplyBallDefinition(ballDefinition);

        // 下一帧再 Force：等 BallController.Start → SetupPhysics
        StartCoroutine(ForceLaunchNextFrame(Vector2.up * Mathf.Max(6f, ballLaunchSpeed * 0.55f)));
    }

    private System.Collections.IEnumerator ForceLaunchNextFrame(Vector2 vel)
    {
        yield return null;
        if (_ball != null)
            _ball.ForceTestInPlay(vel);
    }

    private void HandleBallLaunchInput()
    {
        if (_ball == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            SpawnOrResetBall();
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (Camera.main == null) return;

        Vector2 mouse = Input.mousePosition;
        if (mouse.x < 420f) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(mouse);
        world.z = 0f;
        Vector2 from = _ball.transform.position;
        Vector2 dir = ((Vector2)world - from);
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;

        float speed = ballLaunchSpeed;
        if (config != null)
            speed = Mathf.Clamp(speed, config.ballMinSpeed, config.ballMaxSpeed);

        _ball.ForceTestInPlay(dir.normalized * speed);
    }

    private void SpawnEnemy()
    {
        if (enemyDefinition == null) return;

        float x = Random.Range(-spawnXRange, spawnXRange);
        var go = new GameObject($"Sandbox_{enemyDefinition.minionName}");
        go.transform.position = new Vector3(x, spawnY, 0f);
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.mass = 1f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        go.AddComponent<CircleCollider2D>().radius = 0.42f;

        var enemy = go.AddComponent<TestFallingEnemy>();
        float speed = enemySpeed > 0.01f ? enemySpeed : enemyDefinition.moveSpeed;
        enemy.Configure(enemyDefinition, enemyHp, speed);
        enemy.damageToPlayer = bottomDamagesPlayer ? 1 : 0;

        _alive++;
        enemy.onDeath.AddListener(_ =>
        {
            _alive = Mathf.Max(0, _alive - 1);
            _sessionKills++;
        });
    }

    private void ResetRunStats()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            int score = gm.Score;
            if (score > 0)
                gm.AddScore(-score);
            int need = gm.MaxLives - gm.Lives;
            if (need > 0)
                gm.Heal(need);
        }
        ComboSystem.Instance?.ForceResetCombo();
        ComboCombat.DevReset();
        _sessionKills = 0;
        PushEvent("Reset score/HP/combo (buffs kept)");
    }

    private void PushEvent(string msg)
    {
        _lastEvent = msg;
        _lastEventUntil = Time.unscaledTime + 2.5f;
    }

    private void SyncBottomDamageFlag()
    {
        int dmg = bottomDamagesPlayer ? 1 : 0;
        foreach (var e in FindObjectsOfType<TestFallingEnemy>())
        {
            if (e != null)
                e.damageToPlayer = dmg;
        }
    }

    private void ClearEnemies()
    {
        foreach (var e in FindObjectsOfType<TestFallingEnemy>())
        {
            if (e != null)
                Destroy(e.gameObject);
        }
        _alive = 0;
    }

    private void ResolveEnemyDefinition()
    {
        if (enemyDefinition != null) return;
#if UNITY_EDITOR
        enemyDefinition = AssetDatabase.LoadAssetAtPath<MinionDefinition>(GruntAssetPath);
#endif
    }

    private static void EnsureBlackCamera()
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
        cam.orthographicSize = 9f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    private static PhysicsMaterial2D CreateBounceMat(string name)
    {
        return new PhysicsMaterial2D(name)
        {
            friction = 0f,
            bounciness = 1f
        };
    }

    private void OnGUI()
    {
        DrawPlayfieldHud();

        const float panelW = 420f;
        GUILayout.BeginArea(new Rect(8, 8, panelW, Screen.height - 16), GUI.skin.box);

        GUILayout.Label("<b>Buff Sandbox</b>", RichLabel());
        GUILayout.Label($"Alive {_alive}/{maxAlive}  |  Kills {_sessionKills}  |  Spawn {(autoSpawn ? "ON" : "OFF")}");
        GUILayout.Label("LMB aim  |  RMB/B ball  |  Space spawn  |  C clear  |  X buffs");
        GUILayout.Label("H 触底扣血  |  R 重置分/血/Combo（保留 Buff）");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn", GUILayout.Height(26)))
            SpawnEnemy();
        if (GUILayout.Button(autoSpawn ? "Auto:ON" : "Auto:OFF", GUILayout.Height(26)))
            autoSpawn = !autoSpawn;
        if (GUILayout.Button("Clear Enemies", GUILayout.Height(26)))
            ClearEnemies();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Ball", GUILayout.Height(26)))
            SpawnOrResetBall();
        if (GUILayout.Button("Clear Buffs", GUILayout.Height(26)))
            ClearAllBuffsAndTowers();
        if (GUILayout.Button("Reset Stats", GUILayout.Height(26)))
            ResetRunStats();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        bool nextBottom = GUILayout.Toggle(bottomDamagesPlayer, "触底扣血(测护心)", GUILayout.Height(26));
        if (nextBottom != bottomDamagesPlayer)
        {
            bottomDamagesPlayer = nextBottom;
            SyncBottomDamageFlag();
        }
        if (GUILayout.Button("-1 HP", GUILayout.Height(26)))
        {
            GameManager.Instance?.TakeDamage(1);
            PushEvent("Manual -1 HP");
        }
        if (GUILayout.Button("+1 HP", GUILayout.Height(26)))
        {
            GameManager.Instance?.Heal(1);
            PushEvent("Manual +1 HP");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Charge ALL", GUILayout.Height(26)))
            ElectricCombat.DevChargeAll(3);
        if (GUILayout.Button("Mark ALL", GUILayout.Height(26)))
            FrostCombat.DevMarkAll(3);
        if (GUILayout.Button("Combo +5", GUILayout.Height(26)))
        {
            for (int i = 0; i < 5; i++)
                ComboSystem.Instance?.RegisterAirtimeHit(_ball != null
                    ? (Vector2)_ball.transform.position
                    : Vector2.zero);
            PushEvent($"Combo → {ComboSystem.Instance?.CurrentCombo ?? 0}");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        DrawRunHudBlock();
        GUILayout.Space(2);
        DrawBuildStats();
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter", GUILayout.Width(40));
        _filter = GUILayout.TextField(_filter ?? "", GUILayout.Width(180));
        _showOwnedOnly = GUILayout.Toggle(_showOwnedOnly, "Owned", GUILayout.Width(70));
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        DrawBuffList();
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    /// <summary>台面右上角大字 HUD，打球时也能看见。</summary>
    private void DrawPlayfieldHud()
    {
        var gm = GameManager.Instance;
        int score = gm != null ? gm.Score : 0;
        int lives = gm != null ? gm.Lives : 0;
        int maxLives = gm != null ? gm.MaxLives : 0;
        int combo = ComboSystem.Instance != null ? ComboSystem.Instance.CurrentCombo : 0;
        int dmg = _buffManager != null ? 1 + _buffManager.BallDamageBonus : 1;
        dmg += ComboCombat.TempHitChargesRemaining > 0 ? 1 : 0;

        float x = Screen.width - 280f;
        GUILayout.BeginArea(new Rect(x, 12f, 268f, 160f), GUI.skin.box);
        GUILayout.Label($"<size=22><b>SCORE  {score}</b></size>", RichLabel());
        GUILayout.Label($"<size=18><b>HP  {lives}/{maxLives}</b></size>", RichLabel());
        GUILayout.Label($"<size=18><b>COMBO  {combo}</b></size>", RichLabel());
        GUILayout.Label($"<size=16>BALL DMG  {dmg}</size>", RichLabel());
        if (Time.unscaledTime < _lastEventUntil && !string.IsNullOrEmpty(_lastEvent))
            GUILayout.Label($"<color=#FFD080>{_lastEvent}</color>", RichLabel());
        GUILayout.EndArea();
    }

    private void DrawRunHudBlock()
    {
        var gm = GameManager.Instance;
        if (gm == null || _buffManager == null) return;

        GUILayout.Label("<b>── Run ──</b>", RichLabel());
        GUILayout.Label(
            $"Score {gm.Score}  |  HP {gm.Lives}/{gm.MaxLives}  |  " +
            $"Combo {ComboSystem.Instance?.CurrentCombo ?? 0}  |  " +
            $"BallDmg {1 + _buffManager.BallDamageBonus}" +
            (ComboCombat.TempHitChargesRemaining > 0
                ? $" (+temp×{ComboCombat.TempHitChargesRemaining})"
                : ""));

        if (_buffManager.HealOnKillStacks > 0)
            GUILayout.Label(
                $"HealOnKill  {_buffManager.HealOnKillProgress}/{_buffManager.HealOnKillKillsRequired}  " +
                $"(+{_buffManager.HealOnKillStacks} HP)");
        if (_buffManager.MaxHeartGuardCharges > 0)
            GUILayout.Label(
                $"HeartGuard  {_buffManager.HeartGuardCharges}/{_buffManager.MaxHeartGuardCharges}");
        if (_buffManager.ScoreOnKillBonus > 0f)
            GUILayout.Label($"ScoreOnKill  +{_buffManager.ScoreOnKillBonus:P0}");
        if (_buffManager.MaxHPBonus > 0)
            GUILayout.Label($"MaxHPBonus  +{_buffManager.MaxHPBonus}");

        GUILayout.Label(
            $"Bottom dmg: {(bottomDamagesPlayer ? "<color=#FF6060>ON</color>" : "OFF")}  " +
            $"Tags E={RunSession.GetBuildTagCount(BuildTag.Electric)} " +
            $"F={RunSession.GetBuildTagCount(BuildTag.Frost)} " +
            $"C={RunSession.GetBuildTagCount(BuildTag.Combo)}",
            RichLabel());
    }

    private void DrawBuildStats()
    {
        if (_buffManager == null) return;

        GUILayout.Label("<b>── Builds ──</b>", RichLabel());
        GUILayout.Label(
            $"Electric  Charge Lv{_buffManager.ElectricChargeStacks} +{_buffManager.GetChargePerHit()}/hit  |  " +
            $"Igniter Lv{_buffManager.ElectricIgniterStacks}  CD{ElectricCombat.BallSparkCdRemaining:0.0}s");
        GUILayout.Label(
            $"Frost  Mark Lv{_buffManager.FrostMarkStacks}  |  " +
            $"Burst Lv{_buffManager.FrostBurstStacks} need{_buffManager.GetFrostBurstThreshold()}");
        GUILayout.Label(
            $"Combo  Mom Lv{_buffManager.ComboMomentumStacks} " +
            $"({ComboCombat.MomentumHitCounter}/{_buffManager.GetMomentumHitsPerBonus()})  |  " +
            $"Overload Lv{_buffManager.ComboOverloadStacks}");

        int fueled = 0, fuelSum = 0, marked = 0, markSum = 0;
        foreach (var e in FindObjectsOfType<EnemyBase>())
        {
            if (e == null || e.IsDead) continue;
            if (e.TryGetComponent(out EnemyElectricState st) && st.ChargeStacks > 0)
            {
                fueled++;
                fuelSum += st.ChargeStacks;
            }
            if (e.TryGetComponent(out EnemyFrostState frost) && frost.MarkStacks > 0)
            {
                marked++;
                markSum += frost.MarkStacks;
            }
        }
        GUILayout.Label($"Field  fuel:{fueled}/{fuelSum}  frost:{marked}/{markSum}");

        if (Time.unscaledTime - ElectricCombat.LastZapUnscaledTime < 2.5f &&
            !string.IsNullOrEmpty(ElectricCombat.LastZapReason))
            GUILayout.Label($"<color=#40F0FF>CHAIN [{ElectricCombat.LastZapReason}]</color>", RichLabel());
        if (Time.unscaledTime - FrostCombat.LastBurstUnscaledTime < 2.5f &&
            !string.IsNullOrEmpty(FrostCombat.LastBurstReason))
            GUILayout.Label($"<color=#90E8FF>BURST [{FrostCombat.LastBurstReason}]</color>", RichLabel());
        if (Time.unscaledTime - ComboCombat.LastOverloadUnscaledTime < 2.5f &&
            !string.IsNullOrEmpty(ComboCombat.LastOverloadReason))
            GUILayout.Label($"<color=#FFA040>OVERLOAD [{ComboCombat.LastOverloadReason}]</color>", RichLabel());

        if (_ball != null && _ball.Rb != null)
            GUILayout.Label($"Ball spd {_ball.Rb.velocity.magnitude:0.0}");
    }

    private void DrawBuffList()
    {
        if (buffCatalog == null) return;
        string filter = (_filter ?? "").Trim().ToLowerInvariant();

        BuffCategory? lastCat = null;
        foreach (var def in buffCatalog)
        {
            if (def == null) continue;

            int stacks = _buffManager != null ? _buffManager.GetStacks(def.effectType) : 0;
            if (_showOwnedOnly && stacks <= 0) continue;

            string name = string.IsNullOrEmpty(def.buffName) ? def.name : def.buffName;
            if (filter.Length > 0 &&
                name.ToLowerInvariant().IndexOf(filter) < 0 &&
                def.effectType.ToString().ToLowerInvariant().IndexOf(filter) < 0 &&
                def.category.ToString().ToLowerInvariant().IndexOf(filter) < 0)
                continue;

            if (lastCat == null || lastCat.Value != def.category)
            {
                lastCat = def.category;
                GUILayout.Space(6);
                GUILayout.Label($"── {def.category} ──", RichLabel());
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{RarityShort(def.rarity)} {name}  x{stacks}/{def.maxStacks}", GUILayout.Width(240));
            GUI.enabled = stacks < def.maxStacks;
            if (GUILayout.Button("+", GUILayout.Width(28)))
                ApplyOne(def);
            GUI.enabled = stacks > 0;
            if (GUILayout.Button("-", GUILayout.Width(28)))
                RemoveOne(def);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private static string RarityShort(BuffRarity r) =>
        r == BuffRarity.Common ? "C" : r == BuffRarity.Rare ? "R" : "E";

    private static GUIStyle RichLabel()
    {
        var s = new GUIStyle(GUI.skin.label) { richText = true };
        return s;
    }
}
