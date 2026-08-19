using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)]
public class GravityWell : MonoBehaviour
{
    public static GravityWell Instance { get; private set; }

    public Vector2 Center { get; private set; }
    public float   TimeLeft { get; private set; }
    public bool    IsActive => TimeLeft > 0f;

    private float _duration;
    private float _radius;
    private float _pullStrength;
    private float _rampTime;
    private float _dwellRampTime;
    private float _coreRatio;
    private int   _maxFullPull;
    private float _overflowMult;

    private LineRenderer _ringLine;
    private int _rebuildFrame = -1;
    private readonly HashSet<int> _fullPullIds = new HashSet<int>();
    private readonly Dictionary<int, float> _dwellTimes = new Dictionary<int, float>();
    private static readonly Collider2D[] _overlapBuf = new Collider2D[32];
    private static readonly List<(Minion minion, float ringDist)> _sortBuf = new List<(Minion, float)>(32);

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildVisual();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(DestroyWell);
            GameManager.Instance.onBallLost.AddListener(DestroyWell);
            GameManager.Instance.onGameOver.AddListener(DestroyWell);
            GameManager.Instance.onBuffSelection.AddListener(DestroyWell);
        }

        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.AddListener(OnWaveStart);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.RemoveListener(DestroyWell);
            GameManager.Instance.onBallLost.RemoveListener(DestroyWell);
            GameManager.Instance.onGameOver.RemoveListener(DestroyWell);
            GameManager.Instance.onBuffSelection.RemoveListener(DestroyWell);
        }

        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.RemoveListener(OnWaveStart);

        if (Instance == this) Instance = null;
    }

    private void OnWaveStart(int _) => DestroyWell();

    public static GravityWell EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(GravityWell));
        return go.AddComponent<GravityWell>();
    }

    public static void Spawn(Vector2 center)
    {
        EnsureInstance().ActivateAt(center);
    }

    public void ActivateAt(Vector2 center)
    {
        CacheConfig();
        _dwellTimes.Clear();

        Center   = center;
        TimeLeft = _duration;
        transform.position = center;

        ScaleRingVisual(_radius);
        SetRingAlpha(1f);

        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        SlowMoFX.Instance?.PulseFlash(new Color(0.55f, 0.25f, 1f), 0.3f, 0.1f);
    }

    public void DestroyWell()
    {
        TimeLeft = 0f;
        _fullPullIds.Clear();
        _dwellTimes.Clear();
        if (_ringLine != null) _ringLine.enabled = false;
    }

    public bool IsInside(Vector2 pos)
    {
        if (!IsActive) return false;
        return Vector2.Distance(pos, Center) <= _radius;
    }

    public bool TryGetPull(Minion minion, out Vector2 pullVel, out bool inCoreZone)
    {
        pullVel    = Vector2.zero;
        inCoreZone = false;
        if (!IsActive || minion == null || minion.IsDead) return false;

        Vector2 pos      = minion.transform.position;
        Vector2 toCenter = Center - pos;
        float   dist     = toCenter.magnitude;
        if (dist > _radius) return false;

        float distRatio = dist / _radius;
        if (distRatio <= _coreRatio)
        {
            inCoreZone = true;
            TrackDwell(minion);
            return true;
        }

        EnsurePullSetBuilt();
        TrackDwell(minion);

        Vector2 pullDir = toCenter / Mathf.Max(dist, 0.001f);

        // 外缘弱 → 近核心强（在 core 边界处达到峰值）
        float zoneT = (distRatio - _coreRatio) / Mathf.Max(1f - _coreRatio, 0.01f);
        float radialCurve = Mathf.Lerp(1f, 0.12f, zoneT * zoneT);

        float wellRamp  = GetWellRamp();
        float dwellRamp = GetDwellRamp(minion.GetInstanceID());
        bool  fullPull  = _fullPullIds.Contains(minion.GetInstanceID());
        float speedRef  = minion.moveSpeed > 0.01f ? minion.moveSpeed : 0.5f;
        float strength  = _pullStrength * radialCurve * wellRamp * dwellRamp * (speedRef / 0.5f)
                        * (fullPull ? 1f : _overflowMult);

        pullVel = pullDir * strength;
        return true;
    }

    public float GetDownSpeedScale(Vector2 pos)
    {
        if (!IsActive) return 1f;
        float distRatio = Vector2.Distance(pos, Center) / _radius;
        if (distRatio > 1f) return 1f;

        float baseScale = Config != null ? Config.gravityWellDownSpeedScale : 0.3f;
        if (distRatio <= _coreRatio) return baseScale * 0.15f;

        float t = (distRatio - _coreRatio) / Mathf.Max(1f - _coreRatio, 0.01f);
        return Mathf.Lerp(baseScale * 0.2f, baseScale, t);
    }

    public void StabilizeCoreVelocity(Minion minion, ref Vector2 vel)
    {
        Vector2 pos      = minion.transform.position;
        Vector2 toCenter = Center - pos;
        float   dist     = toCenter.magnitude;
        if (dist > _radius * _coreRatio + 0.05f) return;

        if (toCenter.sqrMagnitude > 0.0001f)
        {
            Vector2 radialIn = toCenter / dist;
            float radialVel  = Vector2.Dot(vel, radialIn);
            vel -= radialIn * radialVel * 0.92f;
        }

        if (vel.y < 0f) vel.y *= 0.12f;
        vel *= 0.9f;
    }

    private void TrackDwell(Minion minion)
    {
        int id = minion.GetInstanceID();
        _dwellTimes.TryGetValue(id, out float t);
        _dwellTimes[id] = t + Time.fixedDeltaTime;
    }

    private float GetWellRamp()
    {
        if (_rampTime <= 0.01f) return 1f;
        float elapsed = _duration - TimeLeft;
        return Mathf.SmoothStep(0f, 1f, elapsed / _rampTime);
    }

    private float GetDwellRamp(int minionId)
    {
        if (_dwellRampTime <= 0.01f) return 1f;
        _dwellTimes.TryGetValue(minionId, out float dwell);
        return Mathf.SmoothStep(0f, 1f, dwell / _dwellRampTime);
    }

    private void FixedUpdate()
    {
        if (!IsActive) return;
        EnsurePullSetBuilt();
    }

    private void Update()
    {
        if (!IsActive) return;

        if (ShouldEndEarly())
        {
            DestroyWell();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsWaveSimActive())
            TimeLeft -= Time.deltaTime;

        if (TimeLeft <= 0f)
        {
            DestroyWell();
            return;
        }

        float alpha = _duration > 0.01f ? Mathf.Clamp01(TimeLeft / _duration) : 0f;
        SetRingAlpha(alpha);
    }

    private void EnsurePullSetBuilt()
    {
        if (_rebuildFrame == Time.frameCount) return;
        _rebuildFrame = Time.frameCount;
        _fullPullIds.Clear();
        if (!IsActive) return;

        _sortBuf.Clear();
        int n = Physics2D.OverlapCircleNonAlloc(Center, _radius, _overlapBuf);
        for (int i = 0; i < n; i++)
        {
            var col = _overlapBuf[i];
            if (col == null || !col.CompareTag("Enemy")) continue;
            var minion = col.GetComponent<Minion>();
            if (minion == null || minion.IsDead) continue;

            float dist = Vector2.Distance(minion.transform.position, Center);
            _sortBuf.Add((minion, dist));
        }

        _sortBuf.Sort((a, b) => b.ringDist.CompareTo(a.ringDist));
        for (int i = 0; i < _sortBuf.Count && i < _maxFullPull; i++)
            _fullPullIds.Add(_sortBuf[i].minion.GetInstanceID());
    }

    private void CacheConfig()
    {
        _duration      = Config != null ? Config.gravityWellDuration : 2.5f;
        _radius        = Config != null ? Config.gravityWellRadius : 3.2f;
        _pullStrength  = Config != null ? Config.gravityWellPullStrength : 9f;
        _rampTime      = Config != null ? Config.gravityWellRampTime : 0.9f;
        _dwellRampTime = Config != null ? Config.gravityWellDwellRampTime : 0.55f;
        _coreRatio     = Config != null ? Config.gravityWellCoreRadiusRatio : 0.22f;
        _maxFullPull   = Config != null ? Config.gravityWellMaxFullPull : 8;
        _overflowMult  = Config != null ? Config.gravityWellOverflowPullMult : 0.3f;
    }

    private static bool ShouldEndEarly()
    {
        var gm = GameManager.Instance;
        if (gm == null) return true;
        var state = gm.State;
        return state != GameState.Playing && state != GameState.BallRespawning;
    }

    private void BuildVisual()
    {
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);

        _ringLine = ringGo.AddComponent<LineRenderer>();
        _ringLine.useWorldSpace = false;
        _ringLine.loop = true;
        const int sides = 48;
        _ringLine.positionCount = sides + 1;
        _ringLine.sortingOrder = 8;
        _ringLine.startWidth = 0.08f;
        _ringLine.endWidth = 0.08f;
        _ringLine.material = CyberVisualFactory.UnlitMaterial;
        _ringLine.startColor = new Color(0.65f, 0.35f, 1f, 0.85f);
        _ringLine.endColor = _ringLine.startColor;

        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            _ringLine.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        _ringLine.enabled = false;
    }

    private void ScaleRingVisual(float radius)
    {
        if (_ringLine == null) return;
        _ringLine.transform.localScale = Vector3.one * radius;
        _ringLine.enabled = true;
    }

    private void SetRingAlpha(float alpha)
    {
        if (_ringLine == null) return;
        var c = _ringLine.startColor;
        c.a = alpha * 0.85f;
        _ringLine.startColor = c;
        _ringLine.endColor = c;
    }
}
