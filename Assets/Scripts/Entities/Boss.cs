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
    private Coroutine _p2PulseCoroutine;
    private LineRenderer _p2Line;
    private LineRenderer _p2LineOuter;
    private Transform _p2RingInnerT;
    private Transform _p2RingOuterT;
    private SpriteRenderer _p2Halo;
    private Vector3 _baseScale;
    private float _p2LineBaseWidth = 0.11f;
    private float _p2ShakeTimer;

    private static readonly Color P2RingInner = new Color(5.2f, 0.18f, 0.02f, 1f);
    private static readonly Color P2RingOuter = new Color(6.5f, 0.06f, 0.01f, 1f);
    private static readonly Color P2ThreatVignette = new Color(1f, 0.06f, 0.02f);
    private static Sprite _p2HaloSprite;
    private int _waveIndex;

    protected override void Awake()
    {
        base.Awake();
        checkBottomLine = false;
    }

    public void Initialize(BossDefinition def, float minX, float maxX, int waveIndex)
    {
        definition   = def;
        _minX        = minX;
        _maxX        = maxX;
        _waveIndex   = waveIndex;

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
        BaseColor    = _baseColor;
        MainSR       = _sr;
        _sr.color    = _baseColor;
        _sr.sortingOrder = 2;

        _baseScale   = transform.localScale;
        SetupP2Ring();

        var hb = GetComponent<BossHealthBar>();
        if (hb == null) hb = gameObject.AddComponent<BossHealthBar>();
        hb.Bind(this);

        _spawnCoroutine = StartCoroutine(SpawnCycle());
    }

    protected override void ApplyMovement()
    {
        if (_rb == null) return;
        float bossScale = TimestopAura.Instance != null ? TimestopAura.Instance.GetBossSpeedScale() : 1f;
        _rb.velocity = Vector2.right * _curMoveSpeed * _moveDir * bossScale;

        float x = transform.position.x;
        if (x >= _maxX) { _moveDir = -1f; transform.position = new Vector3(_maxX, transform.position.y, 0f); }
        else if (x <= _minX) { _moveDir =  1f; transform.position = new Vector3(_minX, transform.position.y, 0f); }
    }

    private void Update()
    {
        if (IsDead) return;
        if (GameManager.Instance == null) return;
        var s = GameManager.Instance.State;
        if (s == GameState.GameOver || s == GameState.BuffSelection || s == GameState.Idle) return;

        TryEnterPhase2();
    }

    public override void TakeHit(int damage = 1, bool isFromBall = false, Vector2? hitPos = null)
    {
        base.TakeHit(damage, isFromBall, hitPos);
        TryEnterPhase2();
    }

    private void TryEnterPhase2()
    {
        if (_inPhase2 || IsDead) return;
        if (CurrentHits < maxHits / 2) return;
        EnterPhase2();
    }

    private void EnterPhase2()
    {
        _inPhase2     = true;
        _curMoveSpeed = definition.moveSpeed * definition.phase2SpeedMult;
        _baseScale    = transform.localScale;
        _p2ShakeTimer = 1.2f;

        if (_p2Line != null)
        {
            _p2Line.enabled = true;
            if (_p2LineOuter != null) _p2LineOuter.enabled = true;
            if (_p2Halo != null) _p2Halo.enabled = true;
            if (_p2PulseCoroutine != null) StopCoroutine(_p2PulseCoroutine);
            _p2PulseCoroutine = StartCoroutine(P2RingPulseRoutine());
        }

        SlowMoFX.Instance?.PulseFlash(new Color(1f, 0.15f, 0.03f), 0.68f, 0.2f);
        SlowMoFX.Instance?.SetBossKillVignette(0.42f, P2ThreatVignette);
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
    }

    private void SetRingDiamond(LineRenderer line, float expand)
    {
        if (line == null || _sr == null || _sr.sprite == null) return;

        var b = _sr.sprite.bounds;
        float ex = b.extents.x * expand;
        float ey = b.extents.y * expand;
        Vector3 c = b.center;

        line.SetPosition(0, c + new Vector3(0f,  ey, 0f));
        line.SetPosition(1, c + new Vector3(ex,  0f, 0f));
        line.SetPosition(2, c + new Vector3(0f, -ey, 0f));
        line.SetPosition(3, c + new Vector3(-ex, 0f, 0f));
        line.SetPosition(4, c + new Vector3(0f,  ey, 0f));
    }

    private IEnumerator P2RingPulseRoutine()
    {
        const float innerSpeed = 11f;
        const float outerSpeed = 6.5f;
        while (_inPhase2 && !IsDead && _p2Line != null)
        {
            float t = Time.time;
            float innerPulse = 0.5f + 0.5f * Mathf.Sin(t * innerSpeed);
            float outerPulse = 0.5f + 0.5f * Mathf.Sin(t * outerSpeed + 1.2f);
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 10.5f)), 4f);

            float innerExpand = 1.14f + innerPulse * 0.12f + beat * 0.06f;
            float outerExpand = 1.28f + outerPulse * 0.16f + beat * 0.08f;

            float innerW = _p2LineBaseWidth * (0.95f + innerPulse * 0.65f + beat * 0.25f);
            _p2Line.startWidth = innerW;
            _p2Line.endWidth   = innerW;
            var innerCol = P2RingInner;
            innerCol.a = 0.55f + innerPulse * 0.45f;
            _p2Line.startColor = innerCol;
            _p2Line.endColor   = innerCol;
            SetRingDiamond(_p2Line, innerExpand);

            if (_p2LineOuter != null)
            {
                float outerW = _p2LineBaseWidth * (1.15f + outerPulse * 0.85f + beat * 0.2f);
                _p2LineOuter.startWidth = outerW;
                _p2LineOuter.endWidth   = outerW;
                var outerCol = P2RingOuter;
                outerCol.a = 0.3f + outerPulse * 0.5f;
                _p2LineOuter.startColor = outerCol;
                _p2LineOuter.endColor   = outerCol;
                SetRingDiamond(_p2LineOuter, outerExpand);
            }

            if (_p2RingInnerT != null)
                _p2RingInnerT.localRotation = Quaternion.Euler(0f, 0f, t * 58f);
            if (_p2RingOuterT != null)
                _p2RingOuterT.localRotation = Quaternion.Euler(0f, 0f, -t * 36f + 45f);

            if (_p2Halo != null)
            {
                float haloPulse = 0.5f + 0.5f * Mathf.Sin(t * 7.5f + 0.6f);
                _p2Halo.transform.localScale = Vector3.one * (1.3f + haloPulse * 0.28f + beat * 0.12f);
                var haloCol = _p2Halo.color;
                haloCol.a = 0.32f + haloPulse * 0.48f + beat * 0.2f;
                _p2Halo.color = haloCol;
            }

            if (_baseScale.sqrMagnitude > 0.001f)
                transform.localScale = _baseScale * (1f + beat * 0.06f);

            _p2ShakeTimer -= Time.deltaTime;
            if (_p2ShakeTimer <= 0f)
            {
                CameraShake.Instance?.Shake(CameraShake.Preset.Light);
                _p2ShakeTimer = Random.Range(1.9f, 2.7f);
            }

            SlowMoFX.Instance?.SetBossKillVignette(0.26f + beat * 0.24f, P2ThreatVignette);

            yield return null;
        }

        _p2PulseCoroutine = null;
    }

    private static LineRenderer CreateP2Line(Transform parent, int sortingOrder, float width)
    {
        var line = parent.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace        = false;
        line.loop                 = true;
        line.positionCount        = 5;
        line.numCornerVertices    = 0;
        line.numCapVertices       = 0;
        line.startWidth           = width;
        line.endWidth             = width;
        line.sortingOrder         = sortingOrder;
        line.sharedMaterial       = CyberVisualFactory.UnlitMaterial;
        line.enabled              = false;
        return line;
    }

    private void SetupP2Ring()
    {
        var effectsT = transform.Find("P2Effects");
        if (effectsT == null)
        {
            var effectsObj = new GameObject("P2Effects");
            effectsObj.transform.SetParent(transform, false);
            effectsT = effectsObj.transform;
        }

        SetupP2Halo(effectsT);

        var ringT = effectsT.Find("P2Ring");
        if (ringT == null)
        {
            var ringObj = new GameObject("P2Ring");
            ringObj.transform.SetParent(effectsT, false);
            ringT = ringObj.transform;
        }
        _p2RingInnerT = ringT;

        var legacySr = ringT.GetComponent<SpriteRenderer>();
        if (legacySr != null) legacySr.enabled = false;

        _p2Line = ringT.GetComponent<LineRenderer>();
        if (_p2Line == null) _p2Line = CreateP2Line(ringT, 8, _p2LineBaseWidth);

        var outerT = effectsT.Find("P2RingOuter");
        if (outerT == null)
        {
            var outerObj = new GameObject("P2RingOuter");
            outerObj.transform.SetParent(effectsT, false);
            outerT = outerObj.transform;
        }
        _p2RingOuterT = outerT;

        _p2LineOuter = outerT.GetComponent<LineRenderer>();
        if (_p2LineOuter == null) _p2LineOuter = CreateP2Line(outerT, 7, _p2LineBaseWidth * 1.4f);

        SetRingDiamond(_p2Line, 1.14f);
        SetRingDiamond(_p2LineOuter, 1.3f);
    }

    private void SetupP2Halo(Transform parent)
    {
        var haloT = parent.Find("P2Halo");
        if (haloT == null)
        {
            var haloObj = new GameObject("P2Halo");
            haloObj.transform.SetParent(parent, false);
            haloT = haloObj.transform;
        }

        _p2Halo = haloT.GetComponent<SpriteRenderer>();
        if (_p2Halo == null) _p2Halo = haloT.gameObject.AddComponent<SpriteRenderer>();
        _p2Halo.sprite = GetP2HaloSprite();
        _p2Halo.material = CyberVisualFactory.UnlitMaterial;
        _p2Halo.color = new Color(4.2f, 0.18f, 0.04f, 0.45f);
        _p2Halo.sortingOrder = 1;
        _p2Halo.enabled = false;
    }

    private static Sprite GetP2HaloSprite()
    {
        if (_p2HaloSprite != null) return _p2HaloSprite;

        const int sz = 64;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[sz * sz];
        float half = sz * 0.5f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = (x - half + 0.5f) / half;
                float dy = (y - half + 0.5f) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.SmoothStep(0.95f, 0.42f, d) * Mathf.SmoothStep(0.18f, 0.38f, d);
                px[y * sz + x] = new Color(1f, 0.2f, 0.05f, ring);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        _p2HaloSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz / 2.2f);
        return _p2HaloSprite;
    }

    private IEnumerator SpawnCycle()
    {
        bool openingBatch = true;
        while (!IsDead)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsWaveSimActive())
            {
                if (openingBatch)
                {
                    openingBatch = false;
                    var crt = ArcadeCRTController.Instance;
                    if (crt != null)
                    {
                        crt.TriggerWaveScan();
                        yield return crt.WaitForScanComplete();
                    }
                    if (!IsDead) SpawnBatch();
                }

                yield return new WaitForSeconds(GetEffectiveSpawnInterval());
                if (!IsDead) SpawnBatch();
            }
            else
            {
                yield return null;
            }
        }
    }

    private float GetScaledSpawnInterval()
    {
        float baseInterval = _inPhase2 ? definition.spawnIntervalP2 : definition.spawnInterval;
        return EndlessWaveScaling.GetSpawnInterval(baseInterval, _waveIndex);
    }

    private float GetEffectiveSpawnInterval()
    {
        float interval = GetScaledSpawnInterval();
        if (TimestopAura.Instance != null)
            interval *= TimestopAura.Instance.GetSpawnIntervalMultiplier();
        return interval;
    }

    private int GetScaledSpawnCount()
    {
        int baseCount = _inPhase2 ? definition.spawnCountP2 : definition.spawnCount;
        return EndlessWaveScaling.GetSpawnCount(baseCount, _waveIndex, _inPhase2);
    }

    private const float SpawnYBase      = 1.0f;
    private const float SpawnYStep      = 0.35f;
    private const float MinSpawnSpacing = 0.9f;

    private void SpawnBatch()
    {
        if (definition.spawnTypes == null || definition.spawnTypes.Length == 0) return;
        int count = GetScaledSpawnCount();
        float[] xs = ComputeSpawnXs(count);
        for (int i = 0; i < count; i++)
        {
            var def = EndlessWaveScaling.PickMinion(definition.spawnTypes, _waveIndex);
            if (def == null) continue;
            float y = transform.position.y - SpawnYBase - i * SpawnYStep;
            var spawnPos = new Vector3(xs[i], y, 0f);
            WaveManager.Instance?.SpawnMinion(def, spawnPos, _waveIndex);
        }
    }

    private float[] ComputeSpawnXs(int count)
    {
        if (count <= 0) return System.Array.Empty<float>();
        if (count == 1)
            return new[] { PickLeastCrowdedX(transform.position.y - SpawnYBase) };

        float span = _maxX - _minX;
        float step = Mathf.Max(MinSpawnSpacing, span / (count - 1));
        float startX = (_minX + _maxX) * 0.5f - step * (count - 1) * 0.5f;
        startX = Mathf.Clamp(startX, _minX, _maxX);

        var xs = new float[count];
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Clamp(startX + step * i, _minX, _maxX);
            xs[i] = x;
        }

        // 若跨度不够，仍保证最小间距并夹紧到范围
        for (int i = 1; i < count; i++)
        {
            if (xs[i] - xs[i - 1] < MinSpawnSpacing)
                xs[i] = Mathf.Min(xs[i - 1] + MinSpawnSpacing, _maxX);
        }
        for (int i = count - 2; i >= 0; i--)
        {
            if (xs[i + 1] - xs[i] < MinSpawnSpacing)
                xs[i] = Mathf.Max(xs[i + 1] - MinSpawnSpacing, _minX);
        }

        return xs;
    }

    private float PickLeastCrowdedX(float spawnY)
    {
        const int slots = 9;
        float bestX = Random.Range(_minX, _maxX);
        float bestScore = -1f;
        var wm = WaveManager.Instance;

        for (int i = 0; i < slots; i++)
        {
            float t = (i + 0.5f) / slots;
            float x = Mathf.Lerp(_minX, _maxX, t);
            float score = wm != null ? wm.GetMinionClearanceAt(x, spawnY) : 10f;
            if (score > bestScore)
            {
                bestScore = score;
                bestX = x;
            }
        }

        return bestX;
    }

    protected override void OnDie()
    {
        if (_rb != null) _rb.velocity = Vector2.zero;
        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
        if (_p2PulseCoroutine != null) StopCoroutine(_p2PulseCoroutine);
        if (_p2Line != null) _p2Line.enabled = false;
        if (_p2LineOuter != null) _p2LineOuter.enabled = false;
        if (_p2Halo != null) _p2Halo.enabled = false;
        if (_baseScale.sqrMagnitude > 0.001f) transform.localScale = _baseScale;
        SlowMoFX.Instance?.SetBossKillVignette(0f, Color.clear);
    }

    // GenerateP2RingSprite 保留供后续美术替换；当前 P2 使用 LineRenderer 外框

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
