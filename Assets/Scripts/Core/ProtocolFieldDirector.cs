using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 台面协议场：管理缓存盘奖励结算、协议锁、短时过载。
/// 挂在场景系统物体上（可与 GameManager 同级）；运行时若缺失会由缓存盘自动补建。
/// </summary>
public class ProtocolFieldDirector : MonoBehaviour
{
    public static ProtocolFieldDirector Instance { get; private set; }

    [Header("Lock / Overload")]
    [SerializeField] private int locksToOverload = 3;
    [SerializeField] private float overloadDuration = 10f;
    [SerializeField] private float overloadBumperForceMult = 1.45f;

    [Header("Default Reward Tuning")]
    [SerializeField] private int scoreBurstAmount = 250;
    [SerializeField] private float skillCooldownRefund = 4f;
    [SerializeField] private int comboBoostHits = 3;
    [SerializeField] private float damageBuffDuration = 8f;
    [SerializeField] private int damageBuffBonus = 1;

    public int CurrentLocks { get; private set; }
    public bool IsOverloading { get; private set; }
    public float OverloadBumperForceMult => IsOverloading ? overloadBumperForceMult : 1f;
    public int TempBallDamageBonus { get; private set; }

    public UnityEvent<int> onLocksChanged = new UnityEvent<int>();
    public UnityEvent onOverloadStarted = new UnityEvent();
    public UnityEvent onOverloadEnded = new UnityEvent();
    public UnityEvent<ProtocolRewardType, Vector2> onRewardGranted = new UnityEvent<ProtocolRewardType, Vector2>();

    private float _overloadLeft;
    private float _damageBuffLeft;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(ResetForNewRun);
            GameManager.Instance.onBuffSelection.AddListener(OnWaveGate);
        }
        if (WaveManager.Instance != null)
            WaveManager.Instance.onWaveStart.AddListener(_ => { /* 锁跨波保留，仅过载不跨波拖尾 */ });
    }

    private void Update()
    {
        if (_overloadLeft > 0f)
        {
            _overloadLeft -= Time.deltaTime;
            if (_overloadLeft <= 0f)
                EndOverload();
        }

        if (_damageBuffLeft > 0f)
        {
            _damageBuffLeft -= Time.deltaTime;
            if (_damageBuffLeft <= 0f)
                TempBallDamageBonus = 0;
        }
    }

    public void ResetForNewRun()
    {
        CurrentLocks = 0;
        TempBallDamageBonus = 0;
        _damageBuffLeft = 0f;
        if (IsOverloading) EndOverload();
        onLocksChanged.Invoke(CurrentLocks);
    }

    private void OnWaveGate()
    {
        // 波间选 Buff 时结束过载，避免 UI 期间继续加速 Bumper
        if (IsOverloading) EndOverload();
    }

    public static ProtocolFieldDirector EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ProtocolFieldDirector");
        return go.AddComponent<ProtocolFieldDirector>();
    }

    public void GrantReward(ProtocolRewardType type, Vector2 worldPos, ProtocolCachePlate source = null)
    {
        switch (type)
        {
            case ProtocolRewardType.ScoreBurst:
                GameManager.Instance?.AddScore(scoreBurstAmount);
                break;
            case ProtocolRewardType.SkillCooldown:
                SkillManager.Instance?.ReduceAllCooldowns(skillCooldownRefund);
                break;
            case ProtocolRewardType.BumperPulse:
            {
                Color fx = source != null ? source.ReadyColor : NeonColors.Active.GetBase(NeonRole.Bumper);
                BumperPulse.ReleaseAt(worldPos, fx);
                break;
            }
            case ProtocolRewardType.ComboBoost:
                if (ComboSystem.Instance != null)
                {
                    for (int i = 0; i < comboBoostHits; i++)
                        ComboSystem.Instance.RegisterAirtimeHit(worldPos);
                }
                break;
            case ProtocolRewardType.Heal:
                GameManager.Instance?.Heal(1);
                break;
            case ProtocolRewardType.ProtocolLock:
                AddLock(1);
                break;
            case ProtocolRewardType.DamageBuff:
                TempBallDamageBonus = damageBuffBonus;
                _damageBuffLeft = damageBuffDuration;
                break;
        }

        ImpactFX.Instance?.SpawnHit(worldPos, source != null ? source.ReadyColor : Color.yellow, 1.4f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
        onRewardGranted.Invoke(type, worldPos);
    }

    public void AddLock(int amount = 1)
    {
        if (amount <= 0) return;
        CurrentLocks = Mathf.Min(locksToOverload, CurrentLocks + amount);
        onLocksChanged.Invoke(CurrentLocks);
        if (CurrentLocks >= locksToOverload && !IsOverloading)
            StartOverload();
    }

    private void StartOverload()
    {
        CurrentLocks = 0;
        onLocksChanged.Invoke(CurrentLocks);
        IsOverloading = true;
        _overloadLeft = overloadDuration;
        onOverloadStarted.Invoke();
        CameraShake.Instance?.Shake(CameraShake.Preset.Heavy);

        // 过载开场脉冲，给一次立即爽感
        if (BallController.Instance != null)
        {
            Color fx = NeonColors.Active != null
                ? NeonColors.Active.GetBase(NeonRole.Bumper)
                : new Color(1.2f, 0.8f, 0.2f, 1f);
            BumperPulse.ReleaseAt(BallController.Instance.transform.position, fx);
        }
    }

    private void EndOverload()
    {
        IsOverloading = false;
        _overloadLeft = 0f;
        onOverloadEnded.Invoke();
    }
}
