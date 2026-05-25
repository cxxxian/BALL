using System.Collections;
using UnityEngine;

public class Boss : EnemyBase
{
    public BossDefinition definition;

    private float _moveDir     = 1f;
    private float _minX, _maxX;
    private bool  _inPhase2    = false;
    private float _curMoveSpeed;
    private SpriteRenderer _sr;
    private Color _baseColor;
    private Coroutine _spawnCoroutine;

    protected override void Awake()
    {
        base.Awake();
        checkBottomLine = false;
    }

    public void Initialize(BossDefinition def, float minX, float maxX)
    {
        definition   = def;
        _minX        = minX;
        _maxX        = maxX;

        maxHits      = def.maxHP;
        scoreOnHit   = def.scoreOnHit;
        scoreOnKill  = def.scoreOnKill;
        moveSpeed    = def.moveSpeed;
        _curMoveSpeed = def.moveSpeed;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        // 强制使用 Unlit 材质，保证亮黄色 100% 亮度输出，不受 scene 2D 光照变暗影响
        _sr.material = new Material(Shader.Find("Sprites/Default"));

        if (def.sprite != null)
        {
            _sr.sprite = def.sprite;
            float spriteWidth = def.sprite.rect.width / def.sprite.pixelsPerUnit;
            if (spriteWidth > 0f)
            {
                float targetScale = 1.6f / spriteWidth;
                transform.localScale = new Vector3(targetScale, targetScale, 1f);

                // 配合 localScale 缩放动态调整 Collider 大小，确保在世界坐标下的实际碰撞包边始终为 1.5f * 1.5f
                var boxCol = GetComponent<BoxCollider2D>();
                if (boxCol != null)
                {
                    float localColSize = (1.5f / 1.6f) * spriteWidth;
                    boxCol.size = new Vector2(localColSize, localColSize);
                }
            }
        }
        else
        {
            _sr.sprite = GenerateBossSprite(64, def.baseColor);
            transform.localScale = Vector3.one;

            var boxCol = GetComponent<BoxCollider2D>();
            if (boxCol != null)
            {
                boxCol.size = new Vector2(1.5f, 1.5f); // 降级回退标准大小
            }
        }

        _baseColor   = def.baseColor;
        _sr.color    = _baseColor;
        _sr.sortingOrder = 2;

        _spawnCoroutine = StartCoroutine(SpawnCycle());
    }

    protected override void ApplyMovement()
    {
        if (_rb == null) return;
        _rb.velocity = Vector2.right * _curMoveSpeed * _moveDir;

        float x = transform.position.x;
        if (x >= _maxX) { _moveDir = -1f; transform.position = new Vector3(_maxX, transform.position.y, 0f); }
        else if (x <= _minX) { _moveDir =  1f; transform.position = new Vector3(_minX, transform.position.y, 0f); }
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead) return;
        if (GameManager.Instance == null) return;
        var s = GameManager.Instance.State;
        if (s == GameState.GameOver || s == GameState.BuffSelection || s == GameState.Idle) return;

        if (!_inPhase2 && CurrentHits >= maxHits / 2)
            EnterPhase2();
    }

    private void EnterPhase2()
    {
        _inPhase2     = true;
        _curMoveSpeed = definition.moveSpeed * definition.phase2SpeedMult;
        if (_sr != null) _sr.color = new Color(1.0f, 0.5f, 0.0f, 1f); // 亮橘黄（过载黄色）
        _baseColor = _sr.color;
    }

    private IEnumerator SpawnCycle()
    {
        while (!IsDead)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPlaying())
            {
                float interval = _inPhase2 ? definition.spawnIntervalP2 : definition.spawnInterval;
                yield return new WaitForSeconds(interval);
                if (!IsDead) SpawnBatch();
            }
            else
            {
                yield return null;
            }
        }
    }

    private void SpawnBatch()
    {
        if (definition.spawnTypes == null || definition.spawnTypes.Length == 0) return;
        int count = _inPhase2 ? definition.spawnCountP2 : definition.spawnCount;
        for (int i = 0; i < count; i++)
        {
            var def = definition.spawnTypes[Random.Range(0, definition.spawnTypes.Length)];
            if (def == null) continue;
            var spawnPos = new Vector3(GetSpawnX(i, count), transform.position.y - 1.0f, 0f);
            WaveManager.Instance?.SpawnMinion(def, spawnPos);
        }
    }

    private float GetSpawnX(int index, int total)
    {
        if (total == 1)
            return Random.Range(_minX, _maxX);

        // 多个时均匀分布在整个移动范围内
        float t = (float)index / (total - 1);
        // 加小抖动避免整齐排列
        float jitter = Random.Range(-0.3f, 0.3f);
        return Mathf.Clamp(Mathf.Lerp(_minX, _maxX, t) + jitter, _minX, _maxX);
    }

    protected override void OnDie()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
    }

    private static Sprite GenerateBossSprite(int size, Color color)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        float r    = half - 2f;
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float dx = Mathf.Abs((i % size) - half + 0.5f);
            float dy = Mathf.Abs((i / size) - half + 0.5f);
            // 菱形形状
            bool inside = (dx + dy) <= r;
            pixels[i]   = inside ? color : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        float ppu = size / 0.9f;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
    }
}
