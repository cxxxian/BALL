using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 滞空连击：球在场期间命中敌人/机关累加 Combo；任意挡板接触严格清零；超时亦清零。
/// </summary>
public class ComboSystem : MonoBehaviour
{
    public static ComboSystem Instance { get; private set; }

    public const int MinComboThreshold       = 3;
    public const int BaseShakeThreshold      = 5;
    public const int BaseHeavyShakeThreshold = 10;

    public int CurrentCombo { get; private set; }
    public Vector2 LastHitWorldPosition { get; private set; }

    public UnityEvent<int> onComboChanged = new UnityEvent<int>();
    public UnityEvent<int> onComboMilestone = new UnityEvent<int>();

    private float _lastHitTime = -99f;
    private float _frenzyKillExtensionUsed;

    private GameConfig Config => GameManager.Instance != null ? GameManager.Instance.config : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
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

    private float EffectiveComboTimeout
    {
        get
        {
            float timeout = Config != null ? Config.comboTimeout : 3f;
            if (DebuffManager.Instance != null)
                timeout += DebuffManager.Instance.ComboTimeoutModifier;

            var bm = BuffManager.Instance;
            if (bm != null && CurrentCombo >= bm.GetFrenzyHighComboThreshold())
                timeout += bm.GetFrenzyTimeoutBonus();

            return Mathf.Max(0.5f, timeout);
        }
    }

    private void Update()
    {
        if (Config == null || CurrentCombo == 0) return;
        if (Time.time - _lastHitTime > EffectiveComboTimeout)
            ResetCombo();
    }

    /// <summary>兼容旧调用；等同于 RegisterAirtimeHit。</summary>
    public void RegisterHit() => RegisterAirtimeHit();

    /// <summary>滞空段有效命中（敌人 / Bumper / 弹弓 / 加速齿轮等）。</summary>
    public void RegisterAirtimeHit(Vector2? worldPos = null)
    {
        if (Config == null) return;

        var ball = BallController.Instance;
        if (ball == null || ball.IsWaitingForLaunch) return;

        LastHitWorldPosition = worldPos ?? (Vector2)ball.transform.position;

        if (Time.time - _lastHitTime > EffectiveComboTimeout)
        {
            CurrentCombo = 0;
            _frenzyKillExtensionUsed = 0f;
            ComboCombat.OnComboReset();
        }

        CurrentCombo++;
        int combo = CurrentCombo;
        ComboCombat.ApplyMomentumAndOverload(ref combo);
        CurrentCombo = combo;
        _lastHitTime = Time.time;
        onComboChanged.Invoke(CurrentCombo);

        int shakeThreshold = GetShakeThreshold(BaseShakeThreshold);
        int heavyThreshold = GetShakeThreshold(BaseHeavyShakeThreshold);

        if (CurrentCombo == shakeThreshold)
        {
            CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
            onComboMilestone.Invoke(CurrentCombo);
        }
        else if (CurrentCombo == heavyThreshold)
        {
            CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
            onComboMilestone.Invoke(CurrentCombo);
        }
        else if (CurrentCombo > heavyThreshold && (CurrentCombo - heavyThreshold) % 5 == 0)
        {
            CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);
            onComboMilestone.Invoke(CurrentCombo);
        }
    }

    /// <summary>给 Frenzy 击杀延长窗口；每轮 Combo 的总额有硬上限。</summary>
    public void ExtendFrenzyComboWindow(float amount, float maxTotal)
    {
        float available = Mathf.Max(0f, maxTotal - _frenzyKillExtensionUsed);
        float applied = Mathf.Min(Mathf.Max(0f, amount), available);
        if (applied <= 0f) return;
        _frenzyKillExtensionUsed += applied;
        _lastHitTime += applied;
    }

    /// <summary>震屏门槛：固定 5/10，不受 Buff 影响。</summary>
    public static int GetShakeThreshold(int baseThreshold) =>
        Mathf.Max(MinComboThreshold, baseThreshold);

    /// <summary>
    /// 显示/通用门槛。4b-4 后不再被连击大师降低；Pulse 门槛见 <see cref="GetPulseEffectiveThreshold"/>。
    /// </summary>
    public static int GetEffectiveThreshold(int baseThreshold) =>
        Mathf.Max(MinComboThreshold, baseThreshold);

    /// <summary>Pulse 里程碑门槛（固定；不再吃 Buff 减门槛）。</summary>
    public static int GetPulseEffectiveThreshold(int baseThreshold) =>
        Mathf.Max(MinComboThreshold, baseThreshold);

    /// <summary>挡板严格断连：任意挡板接触即清零；未用 CD 券作废。</summary>
    public void BreakOnFlipper()
    {
        ResetCombo();
    }

    public void ResetCombo()
    {
        if (CurrentCombo == 0) return;
        ForceResetCombo();
    }

    public void ForceResetCombo()
    {
        CurrentCombo = 0;
        _lastHitTime = -99f;
        _frenzyKillExtensionUsed = 0f;
        ComboCombat.OnComboReset();
        onComboChanged.Invoke(0);
    }

    private void OnGameStart()
    {
        CurrentCombo = 0;
        _lastHitTime = -99f;
        _frenzyKillExtensionUsed = 0f;
        ComboCombat.DevReset();
        onComboChanged.Invoke(0);
    }

    private void OnBallLost()
    {
        ForceResetCombo();
    }
}
