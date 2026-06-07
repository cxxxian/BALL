using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 滞空连击：球在场期间命中敌人/机关累加 Combo；任意挡板接触严格清零；超时亦清零。
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    public int CurrentCombo { get; private set; }

    public UnityEvent<int> onComboChanged = new UnityEvent<int>();

    private float _lastHitTime = -99f;

    private GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
            GameManager.Instance.onBallLost.AddListener(OnBallLost);
        }
    }

    private void Update()
    {
        if (Config == null || CurrentCombo == 0) return;
        if (Time.time - _lastHitTime > Config.comboTimeout)
            ResetCombo();
    }

    /// <summary>兼容旧调用；等同于 RegisterAirtimeHit。</summary>
    public void RegisterHit() => RegisterAirtimeHit();

    /// <summary>滞空段有效命中（敌人 / Bumper / 弹弓 / 加速齿轮等）。</summary>
    public void RegisterAirtimeHit()
    {
        if (Config == null) return;

        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch) return;

        if (Time.time - _lastHitTime > Config.comboTimeout)
            CurrentCombo = 0;

        CurrentCombo++;
        _lastHitTime = Time.time;
        onComboChanged.Invoke(CurrentCombo);

        if      (CurrentCombo == 5)                          CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        else if (CurrentCombo >= 10 && CurrentCombo % 5 == 0) CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
    }

    /// <summary>挡板严格断连：任意挡板接触即清零。</summary>
    public void BreakOnFlipper() => ResetCombo();

    public void ResetCombo()
    {
        if (CurrentCombo == 0) return;
        ForceResetCombo();
    }

    public void ForceResetCombo()
    {
        CurrentCombo = 0;
        _lastHitTime = -99f;
        onComboChanged.Invoke(0);
    }

    private void OnGameStart()
    {
        CurrentCombo = 0;
        _lastHitTime = -99f;
        onComboChanged.Invoke(0);
    }

    private void OnBallLost()
    {
        ForceResetCombo();
    }
}
