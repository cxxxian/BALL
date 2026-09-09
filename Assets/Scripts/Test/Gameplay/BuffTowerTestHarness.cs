using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 塔 / Buff 临时测试台：纯黑背景，刷基础小兵外观 + 一座特斯拉塔。
/// </summary>
public class BuffTowerTestHarness : MonoBehaviour
{
    private const string GruntAssetPath = "Assets/ScriptableObjects/Enemies/Minion_Grunt.asset";
    private const string BloomProfilePath = "Assets/Settings/TronGlobalProfile.asset";

    [Header("Tower")]
    public int towerLevel = 1;
    public Vector3 towerPosition = new Vector3(0f, -2.5f, 0f);

    [Header("Spawn")]
    public MinionDefinition enemyDefinition;
    public bool autoSpawn = true;
    public float spawnInterval = 1.2f;
    public float spawnY = 6.5f;
    public float spawnXRange = 3.5f;
    [Tooltip("测试用 HP，覆盖 Definition（方便观察电塔输出）")]
    public int enemyHp = 12;
    public float enemySpeed = 0.7f;
    public int maxAlive = 20;

    [Header("Debug Draw")]
    public bool drawAttackRadius = true;
    public bool drawAttackLine = true;

    private GameObject _towerGo;
    private TeslaTower _tesla;
    private float _spawnTimer;
    private int _alive;

    private void Start()
    {
        Time.timeScale = 1f;
        EnsureBlackCameraWithBloom();
        ResolveEnemyDefinition();
        RebuildTower();
        _spawnTimer = 0.25f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTowerLevel(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTowerLevel(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTowerLevel(3);
        if (Input.GetKeyDown(KeyCode.R)) RebuildTower();
        if (Input.GetKeyDown(KeyCode.C)) ClearEnemies();
        if (Input.GetKeyDown(KeyCode.Space)) autoSpawn = !autoSpawn;

        if (!autoSpawn) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;
        _spawnTimer = spawnInterval;
        if (_alive < maxAlive)
            SpawnEnemy();
    }

    public void SetTowerLevel(int level)
    {
        towerLevel = Mathf.Clamp(level, 1, 5);
        ApplyTowerLevel();
    }

    public void RebuildTower()
    {
        if (_towerGo != null)
            Destroy(_towerGo);

        _tesla = null;
        _towerGo = new GameObject("Test_TeslaTower");
        _towerGo.transform.position = towerPosition;
        _tesla = _towerGo.AddComponent<TeslaTower>();
        _tesla.level = Mathf.Max(1, towerLevel);
        TowerLevelDisplay.Attach(_towerGo, _tesla.level);
    }

    private void ApplyTowerLevel()
    {
        if (_tesla == null) return;
        _tesla.level = towerLevel;
        var display = _towerGo != null ? _towerGo.GetComponent<TowerLevelDisplay>() : null;
        display?.SetLevel(towerLevel);
    }

    private void SpawnEnemy()
    {
        if (enemyDefinition == null)
        {
            Debug.LogWarning("[BuffTowerTest] enemyDefinition is null, skip spawn.");
            return;
        }

        float x = Random.Range(-spawnXRange, spawnXRange);
        var go = new GameObject($"Test_{enemyDefinition.minionName}");
        go.transform.position = new Vector3(x, spawnY, 0f);
        go.tag = "Enemy";

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.mass = 1f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.42f;

        var enemy = go.AddComponent<TestFallingEnemy>();
        float speed = enemySpeed > 0.01f ? enemySpeed : enemyDefinition.moveSpeed;
        enemy.Configure(enemyDefinition, enemyHp, speed);

        _alive++;
        enemy.onDeath.AddListener(_ => _alive = Mathf.Max(0, _alive - 1));
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
        if (enemyDefinition == null)
            Debug.LogError($"[BuffTowerTest] Failed to load {GruntAssetPath}");
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
                Debug.LogWarning($"[BuffTowerTest] Missing bloom profile at {BloomProfilePath}");
            }
        }
    }

    private void OnGUI()
    {
        var prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUILayout.BeginArea(new Rect(12, 12, 300, 96));
        GUILayout.Label($"Tesla Lv{towerLevel}  |  Alive {_alive}/{maxAlive}");
        GUILayout.Label($"Spawn {(autoSpawn ? "ON" : "OFF")}   1/2/3  Space  R  C");
        GUILayout.EndArea();
        GUI.color = prev;
    }

    private void OnDrawGizmos()
    {
        if (drawAttackLine)
        {
            float y = MinionLineRules.GetAttackLineY();
            float half = MinionLineRules.GetAttackHalfWidth();
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
            Gizmos.DrawLine(new Vector3(-half, y, 0f), new Vector3(half, y, 0f));
        }

        if (!drawAttackRadius) return;

        float radius = 5f;
        if (_tesla != null)
            radius = _tesla.attackRadius + (_tesla.level - 1) * 0.25f;

        Gizmos.color = new Color(0f, 0.9f, 1f, 0.3f);
        Gizmos.DrawWireSphere(towerPosition, radius);
    }
}
