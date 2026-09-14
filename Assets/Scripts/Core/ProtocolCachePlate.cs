using UnityEngine;

/// <summary>
/// 协议缓存盘（井盖）：纯 Trigger，不改球物理。
/// 第一次压过 → 进入 CD；CD 结束 → 就绪；再次压过 → 扩散波。
/// </summary>
public class ProtocolCachePlate : MonoBehaviour
{
    public enum PlateState
    {
        Idle = 0,
        Charging = 1,
        Ready = 2,
        Cooldown = 3,
    }

    [Header("Rules")]
    [Tooltip("第一次压过后，需等待的充能秒数")]
    public float chargeDuration = 6f;
    [Tooltip("武装后多久未领取则退回 Idle；0=永不超时")]
    public float readyTimeout = 18f;
    [Tooltip("领取奖励后的冷却")]
    public float claimCooldown = 2.5f;
    [Tooltip("同一次穿过的重复触发间隔")]
    public float reentryIgnore = 0.35f;

    [Header("Visual")]
    public float radius = 0.85f;
    public Color idleColor = new Color(0.25f, 0.55f, 0.9f, 0.55f);
    public Color chargingColor = new Color(0.15f, 1.6f, 1.9f, 0.9f);
    public Color readyColor = new Color(2.2f, 1.5f, 0.2f, 1f);
    public Color cooldownColor = new Color(0.3f, 0.3f, 0.35f, 0.35f);
    public float idleSpinSpeed = 35f;
    public float chargeSpinSpeed = 140f;
    public float readySpinSpeed = 220f;

    public PlateState State { get; private set; } = PlateState.Idle;
    public float Charge01 { get; private set; }
    public Color ReadyColor => readyColor;

    private float _readyLeft;
    private float _cooldownLeft;
    private float _ignoreUntil;
    private bool _ballInside;
    /// <summary>就绪时球仍在盘上，须先离开再压一次才能领取。</summary>
    private bool _requireExitBeforeClaim;
    private LineRenderer _track;
    private LineRenderer _fill;
    private SpriteRenderer _core;
    private MaterialPropertyBlock _mpb;

    private const int RingSegments = 48;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        EnsureVisuals();
        EnsureTrigger();
        ApplyVisualState();
    }

    private void Start()
    {
        ProtocolFieldDirector.EnsureExists();
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ResetPlate);
    }

    private void Update()
    {
        float spin = idleSpinSpeed;
        if (State == PlateState.Charging)
        {
            spin = chargeSpinSpeed;
            if (chargeDuration > 0.01f)
                Charge01 = Mathf.Clamp01(Charge01 + Time.deltaTime / chargeDuration);
            if (Charge01 >= 1f)
                EnterReady();
        }
        else if (State == PlateState.Ready)
        {
            spin = readySpinSpeed;
            if (readyTimeout > 0f)
            {
                _readyLeft -= Time.deltaTime;
                if (_readyLeft <= 0f)
                    ResetPlate();
            }
        }
        else if (State == PlateState.Cooldown)
        {
            spin = idleSpinSpeed * 0.4f;
            _cooldownLeft -= Time.deltaTime;
            if (_cooldownLeft <= 0f)
            {
                State = PlateState.Idle;
                Charge01 = 0f;
                ApplyVisualState();
            }
        }

        transform.Rotate(Vector3.forward, spin * Time.deltaTime);
        UpdateFillArc();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < _ignoreUntil) return;
        if (!other.CompareTag("Ball")) return;
        var ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsWaitingForLaunch) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsWaveSimActive()) return;

        _ballInside = true;
        _ignoreUntil = Time.time + reentryIgnore;
        HandleBallPass(ball);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Ball")) return;
        _ballInside = false;
        if (State == PlateState.Charging || State == PlateState.Ready)
            _requireExitBeforeClaim = false;
    }

    private void HandleBallPass(BallController ball)
    {
        switch (State)
        {
            case PlateState.Idle:
                BeginCharge();
                break;
            case PlateState.Charging:
                // CD 期间忽略再次压过，必须等转满
                break;
            case PlateState.Ready:
                if (!_requireExitBeforeClaim)
                    Claim();
                break;
            case PlateState.Cooldown:
                ImpactFX.Instance?.SpawnHit(transform.position, cooldownColor, 0.5f);
                break;
        }
    }

    private void BeginCharge()
    {
        State = PlateState.Charging;
        Charge01 = 0f;
        _requireExitBeforeClaim = false;
        ApplyVisualState();
        ImpactFX.Instance?.SpawnHit(transform.position, chargingColor, 1.0f);
    }

    private void EnterReady()
    {
        State = PlateState.Ready;
        Charge01 = 1f;
        _readyLeft = readyTimeout > 0f ? readyTimeout : 9999f;
        _requireExitBeforeClaim = _ballInside;
        ApplyVisualState();
        ImpactFX.Instance?.SpawnHit(transform.position, readyColor, 1.25f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    private void Claim()
    {
        Vector2 pos = transform.position;
        BumperPulse.ReleaseAt(pos, readyColor);

        State = PlateState.Cooldown;
        Charge01 = 0f;
        _cooldownLeft = claimCooldown;
        _requireExitBeforeClaim = false;
        ApplyVisualState();

        ImpactFX.Instance?.SpawnHit(pos, readyColor, 1.4f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        ProtocolFieldDirector.Instance?.onRewardGranted.Invoke(ProtocolRewardType.BumperPulse, pos);
    }

    public void ResetPlate()
    {
        State = PlateState.Idle;
        Charge01 = 0f;
        _readyLeft = 0f;
        _cooldownLeft = 0f;
        _requireExitBeforeClaim = false;
        ApplyVisualState();
    }

    private void EnsureTrigger()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col == null) col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = radius;
    }

    private void EnsureVisuals()
    {
        if (_track == null)
        {
            var trackGo = transform.Find("TrackRing");
            if (trackGo == null)
            {
                trackGo = new GameObject("TrackRing").transform;
                trackGo.SetParent(transform, false);
            }
            _track = trackGo.GetComponent<LineRenderer>();
            if (_track == null) _track = trackGo.gameObject.AddComponent<LineRenderer>();
            ConfigureRing(_track, 0.06f, idleColor, RingSegments + 1, loop: true);
            WriteFullCircle(_track, radius);
        }

        if (_fill == null)
        {
            var fillGo = transform.Find("FillArc");
            if (fillGo == null)
            {
                fillGo = new GameObject("FillArc").transform;
                fillGo.SetParent(transform, false);
            }
            _fill = fillGo.GetComponent<LineRenderer>();
            if (_fill == null) _fill = fillGo.gameObject.AddComponent<LineRenderer>();
            ConfigureRing(_fill, 0.11f, chargingColor, RingSegments + 1, loop: false);
        }

        if (_core == null)
        {
            var coreT = transform.Find("Core");
            if (coreT == null)
            {
                var coreGo = new GameObject("Core");
                coreGo.transform.SetParent(transform, false);
                coreT = coreGo.transform;
            }
            _core = coreT.GetComponent<SpriteRenderer>();
            if (_core == null) _core = coreT.gameObject.AddComponent<SpriteRenderer>();
            _core.sprite = CyberVisualFactory.CreatePortalSprite(idleColor);
            _core.material = CyberVisualFactory.UnlitMaterial;
            _core.sortingOrder = 6;
            coreT.localScale = Vector3.one * 0.55f;
        }
    }

    private void ConfigureRing(LineRenderer lr, float width, Color c, int count, bool loop)
    {
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.widthMultiplier = width;
        lr.positionCount = count;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.sortingOrder = 5;
        if (CyberVisualFactory.UnlitMaterial != null)
            lr.material = CyberVisualFactory.UnlitMaterial;
        lr.startColor = c;
        lr.endColor = c;
    }

    private void WriteFullCircle(LineRenderer lr, float r)
    {
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / (n - 1);
            float ang = t * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f));
        }
    }

    private void UpdateFillArc()
    {
        if (_fill == null) return;
        float shown = State == PlateState.Ready ? 1f : Charge01;
        if (State == PlateState.Idle || State == PlateState.Cooldown)
            shown = 0f;

        int pts = Mathf.Max(2, Mathf.CeilToInt(shown * RingSegments) + 1);
        _fill.positionCount = pts;
        float r = radius * 0.92f;
        for (int i = 0; i < pts; i++)
        {
            float t = pts == 1 ? 0f : (float)i / (pts - 1);
            float ang = t * shown * Mathf.PI * 2f + Mathf.PI * 0.5f;
            _fill.SetPosition(i, new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f));
        }
        Color c = State == PlateState.Ready ? readyColor : chargingColor;
        _fill.startColor = c;
        _fill.endColor = c;
        _fill.enabled = shown > 0.001f;
    }

    private void ApplyVisualState()
    {
        Color c = idleColor;
        switch (State)
        {
            case PlateState.Charging: c = chargingColor; break;
            case PlateState.Ready: c = readyColor; break;
            case PlateState.Cooldown: c = cooldownColor; break;
        }

        if (_track != null)
        {
            _track.startColor = Color.Lerp(idleColor, c, 0.65f);
            _track.endColor = _track.startColor;
        }

        if (_core != null)
        {
            _core.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", c);
            _core.SetPropertyBlock(_mpb);
            _core.color = c;
        }

        UpdateFillArc();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.2f, radius);
        chargeDuration = Mathf.Max(0.5f, chargeDuration);
        claimCooldown = Mathf.Max(0.2f, claimCooldown);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = readyColor;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
