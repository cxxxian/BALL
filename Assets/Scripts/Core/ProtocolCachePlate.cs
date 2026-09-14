using UnityEngine;

/// <summary>
/// 协议缓存盘（Death Race 井盖思路）：
/// 第一次压过 → 开始转圈充能；转满 → 武装就绪（奖励尚未发放）；
/// 再次压过 → 领取奖励。也可切到「一压即武装 / 充满自动领」模式。
/// </summary>
public class ProtocolCachePlate : MonoBehaviour
{
    public enum ArmMode
    {
        /// <summary>压过开充能 → 转满武装 → 再压领取（默认，兼顾死亡飞车 + 转圈）。</summary>
        ChargeThenClaim = 0,
        /// <summary>第一次压过立刻武装，第二次领取（经典死亡飞车）。</summary>
        ArmThenClaim = 1,
        /// <summary>压过开充能，转满自动发奖（少一次决策）。</summary>
        ChargeAutoClaim = 2,
    }

    public enum PlateState
    {
        Idle = 0,
        Charging = 1,
        Ready = 2,
        Cooldown = 3,
    }

    [Header("Rules")]
    public ArmMode armMode = ArmMode.ChargeThenClaim;
    public ProtocolRewardType rewardType = ProtocolRewardType.ScoreBurst;
    [Tooltip("充能所需秒数（Charge 模式）")]
    public float chargeDuration = 6f;
    [Tooltip("充能期间再次压过额外进度（0-1）")]
    [Range(0f, 1f)] public float passChargeBoost = 0.18f;
    [Tooltip("武装后多久未领取则退回 Idle；0=永不超时")]
    public float readyTimeout = 18f;
    public float claimCooldown = 2.5f;
    public float reentryIgnore = 0.35f;

    [Header("Physics Feel")]
    [Tooltip("触发时是否给球一点弹开（避免粘在盘上）")]
    public bool nudgeBall = true;
    public float nudgeForce = 3.5f;

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
        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.AddListener(_ => { /* 跨波保留充能，更有「下一圈再领」感 */ });
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
                OnChargeComplete();
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

        _ignoreUntil = Time.time + reentryIgnore;
        HandleBallPass(ball);
    }

    private void HandleBallPass(BallController ball)
    {
        Nudge(ball);
        ComboSystem.Instance?.RegisterAirtimeHit(transform.position);
        AudioManager.Instance?.PlayBounce();

        switch (State)
        {
            case PlateState.Idle:
                BeginArmFromIdle();
                break;
            case PlateState.Charging:
                Charge01 = Mathf.Clamp01(Charge01 + passChargeBoost);
                ImpactFX.Instance?.SpawnHit(transform.position, chargingColor, 0.85f);
                if (Charge01 >= 1f)
                    OnChargeComplete();
                else
                    ApplyVisualState();
                break;
            case PlateState.Ready:
                Claim(ball);
                break;
            case PlateState.Cooldown:
                ImpactFX.Instance?.SpawnHit(transform.position, cooldownColor, 0.5f);
                break;
        }
    }

    private void BeginArmFromIdle()
    {
        switch (armMode)
        {
            case ArmMode.ArmThenClaim:
                EnterReady();
                break;
            case ArmMode.ChargeThenClaim:
            case ArmMode.ChargeAutoClaim:
                State = PlateState.Charging;
                Charge01 = Mathf.Max(Charge01, 0.05f);
                ApplyVisualState();
                ImpactFX.Instance?.SpawnHit(transform.position, chargingColor, 1.0f);
                break;
        }
    }

    private void OnChargeComplete()
    {
        Charge01 = 1f;
        if (armMode == ArmMode.ChargeAutoClaim)
        {
            Claim(BallController.Instance);
            return;
        }
        EnterReady();
    }

    private void EnterReady()
    {
        State = PlateState.Ready;
        Charge01 = 1f;
        _readyLeft = readyTimeout > 0f ? readyTimeout : 9999f;
        ApplyVisualState();
        ImpactFX.Instance?.SpawnHit(transform.position, readyColor, 1.25f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    private void Claim(BallController ball)
    {
        var director = ProtocolFieldDirector.EnsureExists();
        Vector2 pos = transform.position;
        director.GrantReward(rewardType, pos, this);

        // 过载时撞盘额外脉冲
        if (director.IsOverloading && rewardType != ProtocolRewardType.BumperPulse)
            BumperPulse.ReleaseAt(pos, readyColor);

        State = PlateState.Cooldown;
        Charge01 = 0f;
        _cooldownLeft = claimCooldown;
        ApplyVisualState();
    }

    public void ResetPlate()
    {
        State = PlateState.Idle;
        Charge01 = 0f;
        _readyLeft = 0f;
        _cooldownLeft = 0f;
        ApplyVisualState();
    }

    private void Nudge(BallController ball)
    {
        if (!nudgeBall || ball == null || ball.Rb == null) return;
        Vector2 away = ((Vector2)ball.transform.position - (Vector2)transform.position).normalized;
        if (away.sqrMagnitude < 0.01f)
            away = ball.Rb.velocity.sqrMagnitude > 0.01f ? ball.Rb.velocity.normalized : Vector2.up;
        ball.Rb.velocity += away * nudgeForce;
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
