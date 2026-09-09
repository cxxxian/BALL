using UnityEngine;

public class BulletTimeAim : MonoBehaviour
{
    public static BulletTimeAim Instance { get; private set; }

    private Rigidbody2D _ballRb;
    private float       _savedSpeed;
    private Vector2     _aimDir = Vector2.up;
    private bool        _isAiming;
    private Camera      _cam;
    private float       _aimElapsedUnscaled;
    private float       _aimMaxSeconds = 4f;

    private GameConfig Config => GameManager.Instance?.config;

    /// <summary>决定窗剩余比例：1=刚进入，0=到期。</summary>
    public float AimRemainingRatio
    {
        get
        {
            if (!_isAiming || _aimMaxSeconds <= 0.01f) return 1f;
            return Mathf.Clamp01(1f - _aimElapsedUnscaled / _aimMaxSeconds);
        }
    }

    public bool IsInAimWarning
    {
        get
        {
            if (!_isAiming) return false;
            float warn = Config != null ? Mathf.Max(0f, Config.protocolAimWarnSeconds) : 1f;
            return (_aimMaxSeconds - _aimElapsedUnscaled) <= warn;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
    }

    private void Start()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.onProtocolRedirectActivated.AddListener(OnActivated);
            SkillManager.Instance.onFired.AddListener(OnFired);
            SkillManager.Instance.onAimingAborted.AddListener(OnAimingAborted);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLostInterrupt);
            GameManager.Instance.onGameOver.AddListener(OnInterrupt);
        }
    }

    private void OnBallLostInterrupt() => CancelAimingState();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.onProtocolRedirectActivated.RemoveListener(OnActivated);
            SkillManager.Instance.onFired.RemoveListener(OnFired);
            SkillManager.Instance.onAimingAborted.RemoveListener(OnAimingAborted);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBallLost.RemoveListener(OnBallLostInterrupt);
            GameManager.Instance.onGameOver.RemoveListener(OnInterrupt);
        }
    }

    private void Update()
    {
        if (!_isAiming) return;
        if (_ballRb == null)
        {
            CancelAimingState();
            return;
        }

        _aimElapsedUnscaled += Time.unscaledDeltaTime;
        UpdateAimDir();
        LaunchGuide.Instance?.UpdateDirection(_ballRb.position, _aimDir);
        LaunchGuide.Instance?.SetUrgency(IsInAimWarning);

        if (_aimElapsedUnscaled >= _aimMaxSeconds)
        {
            SkillManager.Instance?.Fire(_aimDir);
            return;
        }

        CheckFireInput();
    }

    private void OnActivated()
    {
        var ball = BallController.Instance;
        if (ball == null) return;
        _ballRb = ball.Rb;
        if (_ballRb == null) return;

        float spd = Config != null
            ? (Config.executeChainMaxSpeed > 0.1f ? Config.executeChainMaxSpeed : Config.ballMaxSpeed)
            : 26f;
        _savedSpeed = spd;

        _aimDir = _ballRb.velocity.sqrMagnitude > 0.01f
            ? _ballRb.velocity.normalized : Vector2.up;

        _aimMaxSeconds = Config != null
            ? Mathf.Max(0.25f, Config.protocolAimMaxSeconds)
            : 4f;
        _aimElapsedUnscaled = 0f;

        _isAiming = true;
        LaunchGuide.Instance?.Show(_ballRb.position, _aimDir);
        LaunchGuide.Instance?.SetUrgency(false);

        // 协议校准「静止瞄准」拍：禁止技能时缓，避免球掉出后卡在发球等待
        if (!TutorialTimeControl.SuppressSkillSlowMo)
        {
            float scale = Config?.skillSlowMoScale ?? 0.12f;
            SlowMoFX.Instance?.Activate(scale);
        }
    }

    private void OnFired(Vector2 direction)
    {
        _isAiming = false;
        _aimElapsedUnscaled = 0f;
        LaunchGuide.Instance?.SetUrgency(false);
        LaunchGuide.Instance?.Hide();
        SlowMoFX.Instance?.Deactivate();

        if (_ballRb == null) return;
        _ballRb.velocity = direction.sqrMagnitude > 0.001f
            ? direction * _savedSpeed
            : _ballRb.velocity.normalized * _savedSpeed;
    }

    private void OnAimingAborted()
    {
        _isAiming = false;
        _aimElapsedUnscaled = 0f;
        LaunchGuide.Instance?.SetUrgency(false);
        LaunchGuide.Instance?.Hide();
        SlowMoFX.Instance?.CancelSkillAim();
    }

    private void OnGameStart() => CancelAimingState();

    private void OnInterrupt() => CancelAimingState();

    private void CancelAimingState()
    {
        bool wasAiming = _isAiming || (SkillManager.Instance != null && SkillManager.Instance.IsAiming);
        _isAiming = false;
        _aimElapsedUnscaled = 0f;
        SkillManager.Instance?.CancelAiming();
        LaunchGuide.Instance?.SetUrgency(false);
        LaunchGuide.Instance?.Hide();

        if (wasAiming || Time.timeScale < 0.99f)
            SlowMoFX.Instance?.CancelSkillAim();
    }

    private void UpdateAimDir()
    {
        Vector2? screenPos = GetCursorScreenPos();
        if (screenPos == null) return;

        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screenPos.Value.x, screenPos.Value.y, -_cam.transform.position.z));
        Vector2 delta = (Vector2)world - _ballRb.position;
        if (delta.sqrMagnitude > 0.04f)
            _aimDir = delta.normalized;
    }

    private void CheckFireInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            SkillManager.Instance?.Fire(_aimDir);
        if (Input.GetKeyDown(KeyCode.Escape))
            SkillManager.Instance?.Fire(Vector2.zero);
#else
        float botZone = Config?.skillBottomZoneRatio ?? 0.22f;
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height <= botZone) continue;
            if (t.phase == TouchPhase.Ended)
            {
                SkillManager.Instance?.Fire(_aimDir);
                return;
            }
        }
#endif
    }

    private Vector2? GetCursorScreenPos()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.mousePosition;
#else
        float botZone = Config?.skillBottomZoneRatio ?? 0.22f;
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height > botZone)
                return t.position;
        }
        return null;
#endif
    }
}
