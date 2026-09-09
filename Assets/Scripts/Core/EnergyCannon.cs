using System.Collections;
using UnityEngine;

/// <summary>
/// 进洞 → 管中穿行 → 炮口玩家瞄准开炮（单向炮塔）。
/// 与 Portal：Portal 双向瞬移保速；炮塔单向、管内延时、到口后选手动方向再发射。
/// </summary>
public class EnergyCannon : MonoBehaviour
{
    public static bool IsPlayerAiming { get; private set; }

    [Header("Ports")]
    public Transform intake;
    public Transform muzzle;

    [Header("Launch")]
    [Tooltip("瞄准默认方向（锥体中心）")]
    public Vector2 defaultAim = Vector2.up;
    [Tooltip("相对 defaultAim 的半角限制（度）；180 = 任意方向")]
    [Range(30f, 180f)]
    public float aimConeHalfAngle = 80f;
    public float exitSpeed = 13f;
    public float transitDuration = 0.28f;
    public float minEntrySpeed = 5f;
    [Tooltip("瞄准最长等待；超时按当前瞄准自动开炮")]
    public float maxAimSeconds = 3.5f;
    [Tooltip("瞄准时的时间缩放（0=不减速）")]
    [Range(0f, 1f)]
    public float aimTimeScale = 0.22f;

    [Header("Cadence")]
    public float cooldown = 4f;
    public int scoreOnLaunch = 50;

    [Header("Visual")]
    public Color cannonColor = new Color(1.2f, 0.6f, 0f, 1f);
    public Color tubeColor = new Color(2f, 0.75f, 0.1f, 0.55f);
    public Color readyColor = new Color(1.2f, 0.6f, 0f, 1f);
    public Color cooldownColor = new Color(0.35f, 0.22f, 0.05f, 1f);
    public float pulsePeriod = 0.55f;

    // Legacy serialized field from older fixed-exit builds
    [HideInInspector] public Vector2 exitDirection = Vector2.up;

    private bool _onCooldown;
    private bool _busy;
    private float _cooldownTimer;
    private float _pulseTimer;
    private bool _pulsing;
    private LineRenderer _tube;
    private SpriteRenderer _muzzleSr;
    private MaterialPropertyBlock _mpb;
    private Camera _cam;
    private Vector2 _aimDir = Vector2.up;
    private bool _fireRequested;
    private bool _aimingActive;

    public bool CanAcceptBall => !_onCooldown && !_busy;

    private void Awake()
    {
        if (intake == null) intake = transform.Find("Intake");
        if (muzzle == null) muzzle = transform.Find("Muzzle");
        if (defaultAim.sqrMagnitude < 0.0001f && exitDirection.sqrMagnitude > 0.0001f)
            defaultAim = exitDirection;
        _mpb = new MaterialPropertyBlock();
        _cam = Camera.main;
        EnsureTube();
        EnsureIntakeZone();
        CacheMuzzleVisual();
        AlignMuzzleVisual(_aimDir.sqrMagnitude > 0.0001f ? _aimDir : defaultAim);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBallLost.AddListener(AbortAimIfNeeded);
            GameManager.Instance.onGameOver.AddListener(AbortAimIfNeeded);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBallLost.RemoveListener(AbortAimIfNeeded);
            GameManager.Instance.onGameOver.RemoveListener(AbortAimIfNeeded);
        }
        if (_aimingActive)
            EndAimVisuals(cancelSlowMo: true);
    }

    private void Start()
    {
        RefreshTube();
        SetMuzzleColor(readyColor);
        _pulsing = true;
        _aimDir = defaultAim.normalized;
    }

    private void Update()
    {
        if (_onCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _onCooldown = false;
                _pulsing = true;
                StartCoroutine(ReadyPulse());
            }
        }

        if (_pulsing && !_onCooldown && !_busy)
        {
            _pulseTimer += Time.deltaTime;
            float t = (Mathf.Sin(_pulseTimer * Mathf.PI * 2f / pulsePeriod) + 1f) * 0.5f;
            SetMuzzleColor(Color.Lerp(readyColor * 0.55f, readyColor, t));
        }

        if (!_aimingActive) return;

        UpdateAimFromCursor();
        AlignMuzzleVisual(_aimDir);
        Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        LaunchGuide.Instance?.UpdateDirection(origin, _aimDir);

        if (WasFirePressed())
            _fireRequested = true;
    }

    internal void HandleIntake(Collider2D other)
    {
        if (!CanAcceptBall) return;
        if (!other.CompareTag("Ball")) return;

        var ball = other.GetComponent<BallController>();
        if (ball == null || ball.IsWaitingForLaunch) return;

        var rb = ball.Rb;
        if (rb == null) return;
        if (rb.velocity.sqrMagnitude < minEntrySpeed * minEntrySpeed) return;

        StartCoroutine(LaunchRoutine(ball));
    }

    private IEnumerator LaunchRoutine(BallController ball)
    {
        _busy = true;
        _onCooldown = true;
        _cooldownTimer = cooldown;
        _pulsing = false;
        _fireRequested = false;

        var rb = ball.Rb;
        var col = ball.GetComponent<Collider2D>();
        Vector3 savedScale = ball.transform.localScale;
        Vector2 intakePos = intake != null ? (Vector2)intake.position : (Vector2)transform.position;
        Vector2 muzzlePos = muzzle != null ? (Vector2)muzzle.position : intakePos + defaultAim.normalized * 4f;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;

        SetMuzzleColor(cannonColor * 1.4f);
        JuiceRouter.Play(JuiceRouter.Tier.Tap, intakePos, cannonColor, 0.9f);
        AudioManager.Instance?.PlayBounce();

        float shrinkT = 0.1f;
        float shrinkElapsed = 0f;
        while (shrinkElapsed < shrinkT)
        {
            shrinkElapsed += Time.deltaTime;
            float u = shrinkElapsed / shrinkT;
            ball.transform.position = Vector2.Lerp(ball.transform.position, intakePos, u * 0.65f);
            ball.transform.localScale = Vector3.Lerp(savedScale, savedScale * 0.25f, u);
            yield return null;
        }

        float transitElapsed = 0f;
        while (transitElapsed < transitDuration)
        {
            transitElapsed += Time.deltaTime;
            float u = transitElapsed / transitDuration;
            u = u * u * (3f - 2f * u);
            ball.transform.position = Vector2.Lerp(intakePos, muzzlePos, u);
            yield return null;
        }

        ball.transform.position = muzzlePos;
        ball.transform.localScale = savedScale;

        // ── 炮口瞄准：玩家自由选方向 ────────────────────────
        _aimDir = ClampAim(defaultAim.normalized);
        BeginAim(muzzlePos);

        float aimElapsed = 0f;
        while (!_fireRequested && aimElapsed < maxAimSeconds)
        {
            if (ball == null || GameManager.Instance == null
                || GameManager.Instance.State == GameState.GameOver)
            {
                EndAimVisuals(cancelSlowMo: true);
                if (col != null) col.enabled = true;
                if (rb != null) rb.isKinematic = false;
                _busy = false;
                yield break;
            }

            aimElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Vector2 launchDir = ClampAim(_aimDir);
        EndAimVisuals(cancelSlowMo: true);

        if (col != null) col.enabled = true;
        rb.isKinematic = false;
        rb.velocity = launchDir * exitSpeed;

        ComboSystem.Instance?.RegisterAirtimeHit(muzzlePos);
        GameManager.Instance?.AddScore(scoreOnLaunch);
        JuiceRouter.Play(JuiceRouter.Tier.Hit, muzzlePos, cannonColor);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
        if (ImpactFX.Instance != null)
            ImpactFX.Instance.SpawnHit(muzzlePos, cannonColor, 1.25f);

        PulseTube();
        SetMuzzleColor(cooldownColor);
        _busy = false;
    }

    private void BeginAim(Vector2 origin)
    {
        _aimingActive = true;
        IsPlayerAiming = true;
        LaunchGuide.Instance?.Show(origin, _aimDir);
        AlignMuzzleVisual(_aimDir);
        if (aimTimeScale > 0.01f && aimTimeScale < 0.99f)
            SlowMoFX.Instance?.Activate(aimTimeScale);
    }

    private void EndAimVisuals(bool cancelSlowMo)
    {
        _aimingActive = false;
        IsPlayerAiming = false;
        _fireRequested = false;
        LaunchGuide.Instance?.Hide();
        if (cancelSlowMo)
            SlowMoFX.Instance?.Deactivate();
    }

    private void AbortAimIfNeeded()
    {
        if (!_aimingActive) return;
        _fireRequested = true; // 打断协程等待
        EndAimVisuals(cancelSlowMo: true);
    }

    private void UpdateAimFromCursor()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector2? screen = GetCursorScreenPos();
        if (screen == null) return;

        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screen.Value.x, screen.Value.y, -_cam.transform.position.z));
        Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        Vector2 delta = (Vector2)world - origin;
        if (delta.sqrMagnitude > 0.04f)
            _aimDir = ClampAim(delta.normalized);
    }

    private Vector2 ClampAim(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = defaultAim;
        dir.Normalize();
        if (aimConeHalfAngle >= 179f) return dir;

        Vector2 center = defaultAim.sqrMagnitude > 0.0001f ? defaultAim.normalized : Vector2.up;
        float ang = Vector2.SignedAngle(center, dir);
        float clamped = Mathf.Clamp(ang, -aimConeHalfAngle, aimConeHalfAngle);
        return (Quaternion.Euler(0f, 0f, clamped) * center).normalized;
    }

    private static bool WasFirePressed()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.GetKeyDown(KeyCode.Space)) return true;
        return false;
#else
        foreach (Touch t in Input.touches)
        {
            if (t.phase == TouchPhase.Ended && t.position.y / Screen.height > 0.22f)
                return true;
        }
        return false;
#endif
    }

    private static Vector2? GetCursorScreenPos()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.mousePosition;
#else
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height > 0.22f)
                return t.position;
        }
        return null;
#endif
    }

    private IEnumerator ReadyPulse()
    {
        SetMuzzleColor(cannonColor * 1.35f);
        yield return new WaitForSeconds(0.12f);
        SetMuzzleColor(readyColor);
    }

    private void EnsureIntakeZone()
    {
        if (intake == null) return;
        var zone = intake.GetComponent<CannonIntakeZone>();
        if (zone == null) zone = intake.gameObject.AddComponent<CannonIntakeZone>();
        zone.owner = this;
    }

    private void EnsureTube()
    {
        _tube = GetComponent<LineRenderer>();
        if (_tube != null) return;

        _tube = gameObject.AddComponent<LineRenderer>();
        _tube.positionCount = 2;
        _tube.startWidth = 0.08f;
        _tube.endWidth = 0.05f;
        _tube.material = new Material(Shader.Find("Sprites/Default"));
        _tube.sortingOrder = 3;
        _tube.useWorldSpace = true;
    }

    private void RefreshTube()
    {
        if (_tube == null || intake == null || muzzle == null) return;
        _tube.SetPosition(0, intake.position);
        _tube.SetPosition(1, muzzle.position);
        _tube.startColor = tubeColor;
        _tube.endColor = new Color(tubeColor.r, tubeColor.g, tubeColor.b, tubeColor.a * 0.35f);
    }

    private void PulseTube()
    {
        StartCoroutine(TubePulseRoutine());
    }

    private IEnumerator TubePulseRoutine()
    {
        if (_tube == null) yield break;
        Color bright = new Color(cannonColor.r, cannonColor.g, cannonColor.b, 0.95f);
        _tube.startColor = bright;
        _tube.endColor = new Color(bright.r, bright.g, bright.b, 0.2f);
        yield return new WaitForSeconds(0.18f);
        RefreshTube();
    }

    private void CacheMuzzleVisual()
    {
        if (muzzle == null) return;
        _muzzleSr = muzzle.GetComponentInChildren<SpriteRenderer>();
    }

    private void AlignMuzzleVisual(Vector2 dir)
    {
        if (muzzle == null || dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        muzzle.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void SetMuzzleColor(Color c)
    {
        if (_muzzleSr == null) return;
        _muzzleSr.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", c);
        _muzzleSr.SetPropertyBlock(_mpb);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AlignMuzzleVisual(defaultAim.sqrMagnitude > 0.0001f ? defaultAim : Vector2.up);
        RefreshTube();
    }
#endif
}

/// <summary>仅挂在 Intake 子物体上，接收进洞触发。</summary>
public class CannonIntakeZone : MonoBehaviour
{
    [HideInInspector] public EnergyCannon owner;

    private void OnTriggerEnter2D(Collider2D other)
    {
        owner?.HandleIntake(other);
    }
}
